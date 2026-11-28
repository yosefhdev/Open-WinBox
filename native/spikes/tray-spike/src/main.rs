//! Fase 0, prueba 1: recibir los iconos de bandeja con Explorer en marcha.
//!
//! Crea una ventana `Shell_TrayWnd` propia, la mantiene por encima de la de Explorer para que
//! `Shell_NotifyIcon` (que usa FindWindow) la encuentre, y reenvía TODO a Explorer para que su
//! bandeja siga intacta. Dibuja los iconos recibidos en una tira; clic izquierdo/derecho se
//! reenvía a la app dueña. Al cerrar, difunde `TaskbarCreated` para que las apps se re-registren
//! con Explorer.

use std::cell::RefCell;
use std::ffi::c_void;
use std::mem::size_of;

use windows::Win32::Foundation::*;
use windows::Win32::Graphics::Gdi::*;
use windows::Win32::System::DataExchange::COPYDATASTRUCT;
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::UI::HiDpi::*;
use windows::Win32::UI::Input::KeyboardAndMouse::*;
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::core::*;

const SHELLTRAY_MAGIC_ADD_ETC: usize = 1; // COPYDATASTRUCT.dwData de Shell_NotifyIcon
const NIM_ADD: u32 = 0;
const NIM_MODIFY: u32 = 1;
const NIM_DELETE: u32 = 2;
const NIM_SETVERSION: u32 = 4;
const NIF_MESSAGE: u32 = 0x1;
const NIF_ICON: u32 = 0x2;
const NIF_TIP: u32 = 0x4;
const NIF_STATE: u32 = 0x8;
const NIF_GUID: u32 = 0x20;
const NIS_HIDDEN: u32 = 0x1;
const TIMER_TOPMOST: usize = 1;

/// Tamaño nativo de icono de bandeja (el mismo que usa Explorer) y celda alrededor.
/// Dibujarlo a otro tamaño lo estira y se ve pixelado.
fn icon_metrics() -> (i32, i32) {
    let icon = unsafe { GetSystemMetricsForDpi(SM_CXSMICON, GetDpiForSystem()) };
    (icon, icon * 2)
}

/// Layout que usa shell32 dentro del WM_COPYDATA: handles truncados a 32 bits para que
/// procesos de 32 y 64 bits compartan formato.
#[repr(C)]
#[derive(Clone, Copy)]
struct NotifyIconData32 {
    cb_size: u32,
    hwnd: u32,
    uid: u32,
    flags: u32,
    callback_message: u32,
    hicon: u32,
    tip: [u16; 128],
    state: u32,
    state_mask: u32,
    info: [u16; 256],
    version_or_timeout: u32,
    info_title: [u16; 64],
    info_flags: u32,
    guid: GUID,
    balloon_icon: u32,
}

#[repr(C)]
#[derive(Clone, Copy)]
struct ShellTrayData {
    signature: u32,
    message: u32,
    nid: NotifyIconData32,
}

struct TrayIcon {
    owner: HWND,
    uid: u32,
    guid: Option<GUID>,
    callback: u32,
    version: u32,
    icon: HICON,
    tip: String,
    hidden: bool,
}

#[derive(Default)]
struct State {
    explorer_tray: HWND,
    explorer_pid: u32,
    icons: Vec<TrayIcon>,
    received: u32,
}

thread_local! {
    static STATE: RefCell<State> = RefCell::default();
}

fn main() -> Result<()> {
    unsafe {
        let _ = SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        let explorer_tray = FindWindowW(w!("Shell_TrayWnd"), None)?;
        let explorer_pid = owner_pid(explorer_tray);
        STATE.with_borrow_mut(|s| {
            s.explorer_tray = explorer_tray;
            s.explorer_pid = explorer_pid;
        });
        println!("Explorer Shell_TrayWnd = {:?}", explorer_tray.0);

        let instance: HINSTANCE = GetModuleHandleW(None)?.into();
        let class = WNDCLASSEXW {
            cbSize: size_of::<WNDCLASSEXW>() as u32,
            lpfnWndProc: Some(wnd_proc),
            hInstance: instance,
            lpszClassName: w!("Shell_TrayWnd"),
            hCursor: LoadCursorW(None, IDC_ARROW)?,
            hbrBackground: HBRUSH(GetStockObject(DKGRAY_BRUSH).0),
            ..Default::default()
        };
        if RegisterClassExW(&class) == 0 {
            return Err(Error::from_thread());
        }

        let hwnd = CreateWindowExW(
            WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
            w!("Shell_TrayWnd"),
            w!("tray-spike"),
            WS_POPUP | WS_VISIBLE | WS_BORDER,
            0,
            0,
            32,
            32,
            None,
            None,
            Some(instance),
            None,
        )?;

        // Apps elevadas también llaman a Shell_NotifyIcon: sin esto UIPI bloquea su WM_COPYDATA.
        let taskbar_created = RegisterWindowMessageW(w!("TaskbarCreated"));
        ChangeWindowMessageFilterEx(hwnd, WM_COPYDATA, MSGFLT_ALLOW, None)?;
        ChangeWindowMessageFilterEx(hwnd, taskbar_created, MSGFLT_ALLOW, None)?;

        layout(hwnd);
        SetTimer(Some(hwnd), TIMER_TOPMOST, 500, None);

        let found = FindWindowW(w!("Shell_TrayWnd"), None)?;
        println!("FindWindow(Shell_TrayWnd) devuelve la nuestra: {}", found == hwnd);

        // Pide a todas las apps que vuelvan a registrar sus iconos (llegarán a nuestra ventana).
        let _ = PostMessageW(Some(HWND_BROADCAST), taskbar_created, WPARAM(0), LPARAM(0));
        println!("TaskbarCreated difundido; esperando iconos...");

        let mut msg = MSG::default();
        while GetMessageW(&mut msg, None, 0, 0).as_bool() {
            let _ = TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }

        // Devolver los iconos a Explorer: nuestra clase ya no existe, FindWindow vuelve a la suya.
        let _ = UnregisterClassW(w!("Shell_TrayWnd"), Some(instance));
        let _ = PostMessageW(Some(HWND_BROADCAST), taskbar_created, WPARAM(0), LPARAM(0));
        println!("Salida limpia; TaskbarCreated difundido para Explorer.");
    }
    Ok(())
}

unsafe extern "system" fn wnd_proc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    unsafe {
        match msg {
            WM_COPYDATA => on_copy_data(hwnd, wparam, lparam),
            WM_TIMER if wparam.0 == TIMER_TOPMOST => {
                let _ = SetWindowPos(hwnd, Some(HWND_TOPMOST), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                LRESULT(0)
            }
            WM_LBUTTONUP | WM_RBUTTONUP => {
                let x = (lparam.0 & 0xFFFF) as i16 as i32;
                click(hwnd, (x / icon_metrics().1) as usize, msg == WM_RBUTTONUP);
                LRESULT(0)
            }
            WM_PAINT => {
                paint(hwnd);
                LRESULT(0)
            }
            WM_KEYDOWN if wparam.0 == VK_ESCAPE_CODE => {
                let _ = DestroyWindow(hwnd);
                LRESULT(0)
            }
            WM_DESTROY => {
                PostQuitMessage(0);
                LRESULT(0)
            }
            _ => DefWindowProcW(hwnd, msg, wparam, lparam),
        }
    }
}

const VK_ESCAPE_CODE: usize = 0x1B;

unsafe fn on_copy_data(hwnd: HWND, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    unsafe {
        let cds = &*(lparam.0 as *const COPYDATASTRUCT);
        let explorer = STATE.with_borrow(|s| s.explorer_tray);

        // Explorer sigue siendo el dueño real de todo (appbars, GETRECT, iconos): reenviar y
        // devolver su respuesta para que las apps no noten nada.
        let mut forwarded = *cds;
        let result = SendMessageW(
            explorer,
            WM_COPYDATA,
            Some(wparam),
            Some(LPARAM(&mut forwarded as *mut COPYDATASTRUCT as isize)),
        );

        let header = 8;
        if cds.dwData == SHELLTRAY_MAGIC_ADD_ETC && cds.cbData as usize > header {
            let mut data: ShellTrayData = std::mem::zeroed();
            let len = (cds.cbData as usize).min(size_of::<ShellTrayData>());
            std::ptr::copy_nonoverlapping(cds.lpData as *const u8, &mut data as *mut _ as *mut u8, len);
            apply(data);
            let _ = InvalidateRect(Some(hwnd), None, true);
            layout(hwnd);
        }
        result
    }
}

fn apply(data: ShellTrayData) {
    let nid = data.nid;
    let owner = HWND(nid.hwnd as usize as *mut c_void);
    let guid = (nid.flags & NIF_GUID != 0).then_some(nid.guid);
    STATE.with_borrow_mut(|s| {
        s.received += 1;
        let pos = s.icons.iter().position(|i| {
            (guid.is_some() && i.guid == guid) || (!owner.is_invalid() && i.owner == owner && i.uid == nid.uid)
        });
        match data.message {
            NIM_ADD | NIM_MODIFY => {
                let idx = pos.unwrap_or_else(|| {
                    s.icons.push(TrayIcon {
                        owner,
                        uid: nid.uid,
                        guid,
                        callback: 0,
                        version: 0,
                        icon: HICON::default(),
                        tip: String::new(),
                        hidden: false,
                    });
                    s.icons.len() - 1
                });
                let icon = &mut s.icons[idx];
                if guid.is_some() {
                    icon.guid = guid;
                }
                if !owner.is_invalid() {
                    icon.owner = owner;
                }
                if nid.flags & NIF_MESSAGE != 0 {
                    icon.callback = nid.callback_message;
                }
                if nid.flags & NIF_ICON != 0 {
                    // El HICON es de la app y puede destruirlo en cualquier momento: copia propia.
                    let src = HICON(nid.hicon as usize as *mut c_void);
                    if let Ok(copy) = unsafe { CopyIcon(src) } {
                        if !icon.icon.is_invalid() {
                            let _ = unsafe { DestroyIcon(icon.icon) };
                        }
                        icon.icon = copy;
                    }
                }
                if nid.flags & NIF_TIP != 0 {
                    let end = nid.tip.iter().position(|&c| c == 0).unwrap_or(nid.tip.len());
                    icon.tip = String::from_utf16_lossy(&nid.tip[..end]);
                }
                if nid.flags & NIF_STATE != 0 && nid.state_mask & NIS_HIDDEN != 0 {
                    icon.hidden = nid.state & NIS_HIDDEN != 0;
                }
                let verb = if data.message == NIM_ADD { "ADD" } else { "MOD" };
                println!(
                    "[{verb}] pid={} uid={} hidden={} tip={:?}",
                    owner_pid(owner),
                    icon.uid,
                    icon.hidden,
                    icon.tip
                );
            }
            NIM_DELETE => {
                if let Some(i) = pos {
                    let icon = s.icons.remove(i);
                    println!("[DEL] uid={} tip={:?}", icon.uid, icon.tip);
                    if !icon.icon.is_invalid() {
                        let _ = unsafe { DestroyIcon(icon.icon) };
                    }
                }
            }
            NIM_SETVERSION => {
                if let Some(i) = pos {
                    s.icons[i].version = nid.version_or_timeout;
                }
                println!(
                    "[VER] v{} pid={} uid={} guid={} encontrado={}",
                    nid.version_or_timeout,
                    owner_pid(owner),
                    nid.uid,
                    guid.is_some(),
                    pos.is_some()
                );
            }
            _ => {}
        }
        println!("  -> {} iconos, {} mensajes recibidos", s.icons.len(), s.received);
    });
}

fn owner_pid(owner: HWND) -> u32 {
    let mut pid = 0;
    unsafe { GetWindowThreadProcessId(owner, Some(&mut pid)) };
    pid
}

fn visible_icons(s: &State) -> Vec<usize> {
    (0..s.icons.len()).filter(|&i| !s.icons[i].hidden).collect()
}

fn layout(hwnd: HWND) {
    let (_, cell) = icon_metrics();
    let count = STATE.with_borrow(|s| visible_icons(s).len()).max(1) as i32;
    unsafe {
        let mut work = RECT::default();
        let _ = SystemParametersInfoW(SPI_GETWORKAREA, 0, Some(&mut work as *mut _ as *mut c_void), SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS(0));
        let width = count * cell;
        let _ = SetWindowPos(
            hwnd,
            Some(HWND_TOPMOST),
            work.right - width - 8,
            work.bottom - cell - 8,
            width,
            cell,
            SWP_NOACTIVATE,
        );
    }
}

fn paint(hwnd: HWND) {
    unsafe {
        let mut ps = PAINTSTRUCT::default();
        let hdc = BeginPaint(hwnd, &mut ps);
        let (icon, cell) = icon_metrics();
        STATE.with_borrow(|s| {
            for (slot, &i) in visible_icons(s).iter().enumerate() {
                let off = (cell - icon) / 2;
                let _ = DrawIconEx(hdc, slot as i32 * cell + off, off, s.icons[i].icon, icon, icon, 0, None, DI_NORMAL);
            }
        });
        let _ = EndPaint(hwnd, &ps);
    }
}

/// Reenvía el clic a la app como lo haría Explorer, según la versión de notificación del icono.
fn click(hwnd: HWND, slot: usize, right: bool) {
    let target = STATE.with_borrow(|s| {
        visible_icons(s).get(slot).map(|&i| {
            let icon = &s.icons[i];
            (icon.owner, icon.uid, icon.callback, icon.version, icon.tip.clone(), s.explorer_pid)
        })
    });
    let Some((owner, uid, callback, version, tip, explorer_pid)) = target else { return };
    let pid = owner_pid(owner);
    let side = if right { "der" } else { "izq" };

    // Iconos del sistema que Explorer registra (volumen, micrófono, batería): su ventana de
    // callback ignora clics ajenos o no tiene callback. Explorer los abre en ajustes rápidos.
    // shortcut: regla empírica (explorer + sin callback o v<4); "Quitar hardware" (v4) sí acepta el reenvío.
    if pid == explorer_pid && (callback == 0 || version < 4) {
        open_quick_settings();
        println!("[CLICK] {side} {tip:?} -> sistema: ajustes rápidos (Win+A)");
        return;
    }
    if callback == 0 {
        println!("[CLICK] {side} {tip:?} no tiene mensaje de callback");
        return;
    }
    unsafe {
        // La app necesita permiso para traer su menú/ventana al frente.
        let fg = AllowSetForegroundWindow(pid);
        let mut pt = POINT::default();
        let _ = GetCursorPos(&mut pt);
        let (down, up) = if right { (WM_RBUTTONDOWN, WM_RBUTTONUP) } else { (WM_LBUTTONDOWN, WM_LBUTTONUP) };
        let mut errors = Vec::new();
        let mut send_as = |m: u32, v4: bool| {
            let (w, l) = if v4 {
                let w = ((pt.y as u16 as usize) << 16) | pt.x as u16 as usize;
                let l = ((uid as u16 as isize) << 16) | m as u16 as isize;
                (w, l)
            } else {
                (uid as usize, m as isize)
            };
            if let Err(e) = PostMessageW(Some(owner), callback, WPARAM(w), LPARAM(l)) {
                errors.push(format!("0x{:X}: {}", m, e.message()));
            }
        };
        let select = if right { WM_CONTEXTMENU } else { 0x400 }; // NIN_SELECT
        send_as(down, version >= 4);
        send_as(up, version >= 4);
        if version >= 4 {
            send_as(select, true);
        } else if version == 0 {
            // Apps como Stream Deck (Qt, callback WM_APP+101) pusieron v4 con Explorer al
            // arrancar y no lo repiten tras TaskbarCreated: solo reaccionan a NIN_SELECT /
            // WM_CONTEXTMENU en formato v4. Una app v0 real ignora ese mensaje desconocido.
            send_as(select, true);
        }
        let _ = hwnd;
        println!(
            "[CLICK] {side} {tip:?} pid={pid} v{version} cb=0x{callback:X} allowFg={} errores={errors:?}",
            fg.is_ok()
        );
    }
}

fn open_quick_settings() {
    let key = |vk: VIRTUAL_KEY, up: bool| INPUT {
        r#type: INPUT_KEYBOARD,
        Anonymous: INPUT_0 {
            ki: KEYBDINPUT { wVk: vk, dwFlags: if up { KEYEVENTF_KEYUP } else { KEYBD_EVENT_FLAGS(0) }, ..Default::default() },
        },
    };
    let inputs = [key(VK_LWIN, false), key(VK_A, false), key(VK_A, true), key(VK_LWIN, true)];
    unsafe { SendInput(&inputs, size_of::<INPUT>() as i32) };
}
