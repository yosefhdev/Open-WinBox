//! Bandeja (área de notificación). Una `Shell_TrayWnd` propia, oculta y por encima en Z de la de
//! Explorer: `Shell_NotifyIcon` la encuentra con `FindWindow` y le manda los iconos. Todo mensaje se
//! reenvía a Explorer, que sigue siendo el dueño real: si la barra muere, su bandeja está al día.
//! Formatos y reglas medidos en la Fase 0, prueba 1 (docs/plans/barra-de-tareas-rust.md).

use std::cell::RefCell;
use std::ffi::c_void;

use windows::Win32::Foundation::*;
use windows::Win32::System::Com::CoTaskMemFree;
use windows::Win32::System::DataExchange::COPYDATASTRUCT;
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::System::Registry::*;
use windows::Win32::System::Threading::{CreateEventW, WaitForSingleObject};
use windows::Win32::UI::Input::KeyboardAndMouse::*;
use windows::Win32::UI::Shell::*;
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::core::*;

use crate::{log, shell, tasks};

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
const NIN_SELECT: u32 = 0x400;
const TIMER_SYNC: usize = 1;
const SETTINGS_KEY: PCWSTR = w!(r"Control Panel\NotifyIconSettings");
/// Iconos de Explorer que la barra ya dibuja en su grupo de sistema (`status.rs`): volumen y energía.
const EXPLORER_DRAWN_UIDS: [u32; 2] = [100, 1225];

/// Layout de shell32 dentro del `WM_COPYDATA`: handles truncados a 32 bits (32 y 64 bits comparten formato).
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

pub struct Icon {
    /// Identificador propio y estable mientras el icono exista (lo usa la barra para hit-test).
    pub id: u32,
    owner: HWND,
    uid: u32,
    guid: Option<GUID>,
    callback: u32,
    version: u32,
    /// Copia propia (`CopyIcon`): la app puede destruir el suyo cuando quiera.
    pub icon: HICON,
    pub tip: String,
    hidden: bool,
    pid: u32,
    exe: String,
    /// Visible en la barra (como "Otros iconos de la bandeja" de Windows); si no, va al desbordamiento.
    pub promoted: bool,
    /// Subclave de `NotifyIconSettings` que Windows le asignó; ahí se guarda si es visible.
    setting: Option<String>,
}

#[derive(Default)]
struct Tray {
    hwnd: HWND,
    explorer: Option<HWND>,
    explorer_pid: u32,
    icons: Vec<Icon>,
    next_id: u32,
    settings: Option<(HKEY, HANDLE)>,
    last_click: Option<(u32, u32)>,
}

thread_local! {
    static TRAY: RefCell<Tray> = RefCell::default();
}

/// Crea la bandeja y pide a las apps que vuelvan a registrar sus iconos.
pub fn start() {
    unsafe {
        let Ok(module) = GetModuleHandleW(None) else { return };
        let instance: HINSTANCE = module.into();
        let wc = WNDCLASSEXW {
            cbSize: size_of::<WNDCLASSEXW>() as u32,
            lpfnWndProc: Some(tray_proc),
            hInstance: instance,
            lpszClassName: shell::PRIMARY_CLASS,
            ..Default::default()
        };
        RegisterClassExW(&wc);
        let hwnd = match CreateWindowExW(
            WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE,
            shell::PRIMARY_CLASS,
            w!("OpenWinBox Tray"),
            WS_POPUP,
            0,
            0,
            0,
            0,
            None,
            None,
            Some(instance),
            None,
        ) {
            Ok(h) => h,
            Err(e) => {
                log(&format!("bandeja: no se pudo crear la ventana: {e}"));
                return;
            }
        };
        // Apps elevadas también llaman a Shell_NotifyIcon: sin esto UIPI bloquea su WM_COPYDATA.
        let _ = ChangeWindowMessageFilterEx(hwnd, WM_COPYDATA, MSGFLT_ALLOW, None);
        let settings = open_settings();
        TRAY.with_borrow_mut(|t| {
            t.hwnd = hwnd;
            t.settings = settings;
        });
        sync();
        SetTimer(Some(hwnd), TIMER_SYNC, 500, None);
        let found = FindWindowW(shell::PRIMARY_CLASS, None).ok();
        log(&format!("bandeja lista; FindWindow la devuelve a ella: {}", found == Some(hwnd)));
        broadcast_taskbar_created();
    }
}

pub fn stop() {
    let (hwnd, settings) = TRAY.with_borrow_mut(|t| (std::mem::take(&mut t.hwnd), t.settings.take()));
    unsafe {
        if !hwnd.is_invalid() {
            let _ = DestroyWindow(hwnd);
        }
        if let Ok(module) = GetModuleHandleW(None) {
            let _ = UnregisterClassW(shell::PRIMARY_CLASS, Some(module.into()));
        }
        if let Some((key, event)) = settings {
            let _ = RegCloseKey(key);
            let _ = CloseHandle(event);
        }
    }
    TRAY.with_borrow_mut(|t| {
        for i in t.icons.drain(..) {
            destroy_icon(i.icon);
        }
    });
}

/// Iconos visibles (sin `NIS_HIDDEN`), en orden de llegada.
pub fn visible() -> Vec<(u32, HICON, bool)> {
    TRAY.with_borrow(|t| {
        t.icons
            .iter()
            .filter(|i| !i.hidden && !i.icon.is_invalid() && !(i.pid == t.explorer_pid && EXPLORER_DRAWN_UIDS.contains(&i.uid)))
            .map(|i| (i.id, i.icon, i.promoted))
            .collect()
    })
}

fn broadcast_taskbar_created() {
    unsafe {
        let msg = RegisterWindowMessageW(w!("TaskbarCreated"));
        let _ = PostMessageW(Some(HWND_BROADCAST), msg, WPARAM(0), LPARAM(0));
    }
}

unsafe extern "system" fn tray_proc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    unsafe {
        match msg {
            WM_COPYDATA => on_copy_data(wparam, lparam),
            WM_TIMER if wparam.0 == TIMER_SYNC => {
                sync();
                LRESULT(0)
            }
            // Siempre oculta: si alguien la muestra creyendo que es la barra nativa, sería un hueco.
            WM_WINDOWPOSCHANGING => {
                let pos = &mut *(lparam.0 as *mut WINDOWPOS);
                pos.flags &= !SWP_SHOWWINDOW;
                LRESULT(0)
            }
            _ => DefWindowProcW(hwnd, msg, wparam, lparam),
        }
    }
}

/// Mantiene la ventana encima en Z, con el rectángulo y las propiedades de la de Explorer (las apps
/// leen `TaskbandHWND` para el progreso y `GetWindowRect` para colocar avisos), y limpia iconos huérfanos.
fn sync() {
    let Some(hwnd) = TRAY.with_borrow(|t| (!t.hwnd.is_invalid()).then_some(t.hwnd)) else { return };
    let explorer = shell::explorer_tray();
    let changed = TRAY.with_borrow_mut(|t| {
        let changed = t.explorer.is_some() && explorer.is_some() && t.explorer != explorer;
        t.explorer = explorer;
        if let Some(e) = explorer {
            unsafe { GetWindowThreadProcessId(e, Some(&mut t.explorer_pid)) };
        }
        changed
    });
    unsafe {
        if let Some(e) = explorer {
            let _ = EnumPropsExW(e, Some(copy_prop), LPARAM(hwnd.0 as isize));
        }
        let r = explorer.and_then(shell::window_rect).unwrap_or_default();
        let _ = SetWindowPos(hwnd, Some(HWND_TOPMOST), r.left, r.top, r.right - r.left, r.bottom - r.top, SWP_NOACTIVATE | SWP_NOREDRAW);
    }
    // Explorer reiniciado: sus TaskbarCreated pudieron llegarle antes de recuperar el primer puesto.
    if changed {
        log("bandeja: Explorer reiniciado, pidiendo los iconos otra vez");
        broadcast_taskbar_created();
    }
    let mut visual = TRAY.with_borrow_mut(|t| {
        let before = t.icons.len();
        t.icons.retain(|i| {
            let alive = unsafe { IsWindow(Some(i.owner)) }.as_bool() || i.owner.is_invalid();
            if !alive {
                destroy_icon(i.icon);
            }
            alive
        });
        t.icons.len() != before
    });
    if settings_changed() {
        visual |= refresh_promoted();
    }
    if visual {
        crate::bar::tray_changed();
    }
}

unsafe extern "system" fn copy_prop(_: HWND, name: PCWSTR, data: HANDLE, target: usize) -> BOOL {
    unsafe {
        // Átomos y punteros de Explorer (OLE, MSAA) no sirven en otra ventana.
        if (name.0 as usize) >> 16 != 0 {
            let s = name.to_string().unwrap_or_default();
            if !(s.starts_with("Ole") || s.starts_with("MSAA") || s == "SysSetRedraw") {
                let _ = SetPropW(HWND(target as *mut c_void), name, Some(data));
            }
        }
    }
    TRUE
}

unsafe fn on_copy_data(wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    unsafe {
        let cds = &*(lparam.0 as *const COPYDATASTRUCT);
        let explorer = TRAY.with_borrow(|t| t.explorer);
        // Explorer es el dueño real (appbars, GETRECT, iconos): reenviar y devolver su respuesta.
        let mut forwarded = *cds;
        let mut result = (cds.dwData == 1) as usize;
        if let Some(e) = explorer {
            let mut out = 0usize;
            let ok = SendMessageTimeoutW(
                e,
                WM_COPYDATA,
                wparam,
                LPARAM(&mut forwarded as *mut COPYDATASTRUCT as isize),
                SMTO_ABORTIFHUNG | SMTO_BLOCK,
                3000,
                Some(&mut out),
            );
            if ok.0 != 0 {
                result = out;
            }
        }
        if cds.dwData == 1 && cds.cbData as usize > 8 && !cds.lpData.is_null() {
            let mut data: ShellTrayData = std::mem::zeroed();
            let len = (cds.cbData as usize).min(size_of::<ShellTrayData>());
            std::ptr::copy_nonoverlapping(cds.lpData as *const u8, &mut data as *mut _ as *mut u8, len);
            if apply(data) {
                crate::bar::tray_changed();
            }
        }
        LRESULT(result as isize)
    }
}

/// Aplica un ADD/MODIFY/DELETE/SETVERSION. Verdadero si cambia algo visible.
fn apply(data: ShellTrayData) -> bool {
    let nid = data.nid;
    let owner = HWND(nid.hwnd as usize as *mut c_void);
    // Iconos con GUID llegan con hWnd 0 en algunos MODIFY: identificarlos por GUID primero.
    let guid = (nid.flags & NIF_GUID != 0).then_some(nid.guid);
    let found = TRAY.with_borrow(|t| {
        t.icons.iter().position(|i| (guid.is_some() && i.guid == guid) || (!owner.is_invalid() && i.owner == owner && i.uid == nid.uid))
    });
    match data.message {
        NIM_ADD | NIM_MODIFY => {
            let new = found.is_none();
            if new && data.message == NIM_MODIFY && owner.is_invalid() {
                return false;
            }
            // Fuera del préstamo: abre el proceso y lee el registro.
            let exe = if new { tasks::process_path(owner).unwrap_or_default() } else { String::new() };
            let entries = if new { settings_entries() } else { Vec::new() };
            let pid = window_pid(owner);
            TRAY.with_borrow_mut(|t| {
                // Un icono es de quien lo registró: otro proceso no puede cambiarle la ventana ni el
                // mensaje de callback (los clics se le reenvían a esa ventana).
                if let Some(i) = found {
                    if pid != 0 && pid != t.icons[i].pid {
                        return false;
                    }
                }
                let idx = match found {
                    Some(i) => i,
                    None => {
                        t.next_id += 1;
                        let entry = match_entry(&entries, &exe, nid.uid, guid);
                        let (promoted, setting) = (entry.is_some_and(|e| e.promoted), entry.map(|e| e.key.clone()));
                        t.icons.push(Icon {
                            id: t.next_id,
                            owner,
                            uid: nid.uid,
                            guid,
                            callback: 0,
                            version: 0,
                            icon: HICON::default(),
                            tip: String::new(),
                            hidden: false,
                            pid,
                            exe,
                            promoted,
                            setting,
                        });
                        t.icons.len() - 1
                    }
                };
                let icon = &mut t.icons[idx];
                let mut visual = new;
                if guid.is_some() {
                    icon.guid = guid;
                }
                if !owner.is_invalid() {
                    icon.owner = owner;
                }
                // Sin ventana no se sabe quién lo manda; y solo mensajes privados (nada de WM_CLOSE…).
                if nid.flags & NIF_MESSAGE != 0 && !owner.is_invalid() && nid.callback_message >= WM_USER {
                    icon.callback = nid.callback_message;
                }
                if nid.flags & NIF_ICON != 0 {
                    let src = HICON(nid.hicon as usize as *mut c_void);
                    if let Ok(copy) = unsafe { CopyIcon(src) } {
                        destroy_icon(icon.icon);
                        icon.icon = copy;
                        visual = true;
                    }
                }
                if nid.flags & NIF_TIP != 0 {
                    let end = nid.tip.iter().position(|&c| c == 0).unwrap_or(nid.tip.len());
                    icon.tip = String::from_utf16_lossy(&nid.tip[..end]);
                }
                if nid.flags & NIF_STATE != 0 && nid.state_mask & NIS_HIDDEN != 0 {
                    let hidden = nid.state & NIS_HIDDEN != 0;
                    visual |= hidden != icon.hidden;
                    icon.hidden = hidden;
                }
                if new {
                    log(&format!("bandeja + {:?} uid={} pid={} visible={} {}", icon.tip, icon.uid, icon.pid, icon.promoted, icon.exe));
                }
                visual
            })
        }
        NIM_DELETE => TRAY.with_borrow_mut(|t| match found {
            Some(i) if owner.is_invalid() || window_pid(owner) == t.icons[i].pid => {
                let icon = t.icons.remove(i);
                destroy_icon(icon.icon);
                true
            }
            _ => false,
        }),
        NIM_SETVERSION => {
            TRAY.with_borrow_mut(|t| {
                // Los iconos con GUID mandan SETVERSION sin ventana: entonces vale el GUID.
                if let Some(i) = found.filter(|&i| owner.is_invalid() || window_pid(owner) == t.icons[i].pid) {
                    t.icons[i].version = nid.version_or_timeout;
                }
            });
            false
        }
        _ => false,
    }
}

/// Proceso dueño de la ventana; 0 si no existe.
fn window_pid(h: HWND) -> u32 {
    let mut pid = 0;
    if !h.is_invalid() {
        unsafe { GetWindowThreadProcessId(h, Some(&mut pid)) };
    }
    pid
}

fn destroy_icon(icon: HICON) {
    if !icon.is_invalid() {
        unsafe {
            let _ = DestroyIcon(icon);
        }
    }
}

/// Reenvía un clic a la app como lo haría Explorer, según la versión de notificación del icono.
/// `double`: segundo clic dentro del tiempo de doble clic (algunas apps solo abren con doble clic).
pub fn click(id: u32, right: bool) {
    let now = unsafe { GetMessageTime() } as u32;
    let double = !right
        && TRAY.with_borrow_mut(|t| {
            let d = t.last_click.is_some_and(|(last, at)| last == id && now.wrapping_sub(at) <= unsafe { GetDoubleClickTime() });
            t.last_click = if d { None } else { Some((id, now)) };
            d
        });
    let Some((owner, uid, callback, version, pid, explorer_pid)) = TRAY.with_borrow(|t| {
        t.icons.iter().find(|i| i.id == id).map(|i| (i.owner, i.uid, i.callback, i.version, i.pid, t.explorer_pid))
    }) else {
        return;
    };
    // Iconos de sistema de Explorer (micrófono en uso, volumen…): sin callback o ignoran clics
    // ajenos; Explorer los abre en ajustes rápidos.
    // shortcut: regla empírica (explorer + sin callback o v<4); "Quitar hardware" (v4) sí acepta el reenvío.
    if pid == explorer_pid && (callback == 0 || version < 4) {
        crate::bar::send_keys(&[VK_LWIN, VK_A]);
        return;
    }
    // La ventana pudo cerrarse y su HWND reutilizarse por otro proceso: no reenviarle nada.
    if callback < WM_USER || window_pid(owner) != pid {
        return;
    }
    unsafe {
        // La app necesita permiso para traer su menú o ventana al frente.
        let _ = AllowSetForegroundWindow(pid);
        let mut pt = POINT::default();
        let _ = GetCursorPos(&mut pt);
        let send = |m: u32, v4: bool| {
            let (w, l) = if v4 {
                (((pt.y as u16 as usize) << 16) | pt.x as u16 as usize, ((uid as u16 as isize) << 16) | m as u16 as isize)
            } else {
                (uid as usize, m as isize)
            };
            let _ = PostMessageW(Some(owner), callback, WPARAM(w), LPARAM(l));
        };
        let v4 = version >= 4;
        if double {
            send(WM_LBUTTONDBLCLK, v4);
            send(WM_LBUTTONUP, v4);
            return;
        }
        let (down, up) = if right { (WM_RBUTTONDOWN, WM_RBUTTONUP) } else { (WM_LBUTTONDOWN, WM_LBUTTONUP) };
        send(down, v4);
        send(up, v4);
        // v0 sin NIM_SETVERSION tras TaskbarCreated (Qt, Stream Deck) solo reacciona al formato v4
        // de NIN_SELECT / WM_CONTEXTMENU; una app v0 real ignora ese mensaje desconocido.
        if v4 || version == 0 {
            send(if right { WM_CONTEXTMENU } else { NIN_SELECT }, true);
        }
    }
}

// --- "Otros iconos de la bandeja" de Windows: HKCU\Control Panel\NotifyIconSettings\<n> ---

fn open_settings() -> Option<(HKEY, HANDLE)> {
    unsafe {
        let mut key = HKEY::default();
        if RegOpenKeyExW(HKEY_CURRENT_USER, SETTINGS_KEY, Some(0), KEY_READ | KEY_NOTIFY, &mut key).is_err() {
            return None;
        }
        let event = CreateEventW(None, false, false, None).ok()?;
        let _ = RegNotifyChangeKeyValue(key, true, REG_NOTIFY_CHANGE_NAME | REG_NOTIFY_CHANGE_LAST_SET, Some(event), true);
        Some((key, event))
    }
}

/// El usuario cambió qué iconos se ven (Configuración > Barra de tareas). Rearma el aviso.
fn settings_changed() -> bool {
    let Some((key, event)) = TRAY.with_borrow(|t| t.settings) else { return false };
    unsafe {
        if WaitForSingleObject(event, 0) != WAIT_OBJECT_0 {
            return false;
        }
        let _ = RegNotifyChangeKeyValue(key, true, REG_NOTIFY_CHANGE_NAME | REG_NOTIFY_CHANGE_LAST_SET, Some(event), true);
    }
    true
}

fn refresh_promoted() -> bool {
    let entries = settings_entries();
    TRAY.with_borrow_mut(|t| {
        let mut changed = false;
        for i in &mut t.icons {
            let entry = match_entry(&entries, &i.exe, i.uid, i.guid);
            let p = entry.is_some_and(|e| e.promoted);
            changed |= p != i.promoted;
            i.promoted = p;
            if let Some(e) = entry {
                i.setting = Some(e.key.clone());
            }
        }
        changed
    })
}

fn match_entry<'a>(entries: &'a [Entry], exe: &str, uid: u32, guid: Option<GUID>) -> Option<&'a Entry> {
    if exe.is_empty() {
        return None;
    }
    // Entre las entradas del mismo exe, la de su UID (o GUID); si no, cualquiera de ese exe.
    let mut fallback = None;
    for entry in entries {
        if !entry.exe.eq_ignore_ascii_case(exe) {
            continue;
        }
        let same = match guid {
            Some(g) => entry.guid == Some(g),
            None => entry.uid == Some(uid),
        };
        if same {
            return Some(entry);
        }
        fallback.get_or_insert(entry);
    }
    fallback
}

/// Arrastre entre la barra y el desbordamiento: lo guarda donde Windows (`IsPromoted`), así la
/// barra nativa y Configuración > Barra de tareas ven lo mismo.
pub fn set_promoted(id: u32, promoted: bool) {
    let Some(setting) = TRAY.with_borrow_mut(|t| {
        let icon = t.icons.iter_mut().find(|i| i.id == id)?;
        icon.promoted = promoted;
        Some(icon.setting.clone())
    }) else {
        return;
    };
    match setting {
        Some(key) => {
            let path: Vec<u16> = format!(r"Control Panel\NotifyIconSettings\{key}").encode_utf16().chain([0]).collect();
            let value = promoted as u32;
            let err = unsafe {
                RegSetKeyValueW(HKEY_CURRENT_USER, PCWSTR(path.as_ptr()), w!("IsPromoted"), REG_DWORD.0, Some(&value as *const u32 as *const c_void), 4)
            };
            if err.is_err() {
                log(&format!("bandeja: no se pudo guardar IsPromoted en {key}: {err:?}"));
            }
        }
        // shortcut: sin entrada de Windows solo dura la sesión; Explorer crea la entrada al registrar el icono, así que es raro.
        None => log("bandeja: icono sin entrada en NotifyIconSettings; el cambio no se guarda"),
    }
    crate::bar::tray_changed();
}

thread_local! {
    /// Copia del icono usada como cursor de arrastre: la app puede borrar el suyo a mitad de arrastre.
    static DRAG_CURSOR: RefCell<Option<(u32, HICON)>> = const { RefCell::new(None) };
}

/// Pone como cursor una copia propia del icono `id` (creada una vez por arrastre).
pub fn set_drag_cursor(id: u32) {
    DRAG_CURSOR.with_borrow_mut(|c| {
        if c.is_none_or(|(cid, _)| cid != id) {
            end_drag_cursor_inner(c);
            let src = TRAY.with_borrow(|t| t.icons.iter().find(|i| i.id == id).map(|i| i.icon));
            if let Some(copy) = src.filter(|h| !h.is_invalid()).and_then(|h| unsafe { CopyIcon(h) }.ok()) {
                *c = Some((id, copy));
            }
        }
        if let Some((_, icon)) = *c {
            unsafe { SetCursor(Some(HCURSOR(icon.0))) };
        }
    });
}

/// Fin del arrastre: vuelve a la flecha y libera la copia.
pub fn end_drag_cursor() {
    DRAG_CURSOR.with_borrow_mut(end_drag_cursor_inner);
}

fn end_drag_cursor_inner(c: &mut Option<(u32, HICON)>) {
    if let Some((_, icon)) = c.take() {
        unsafe {
            if let Ok(arrow) = LoadCursorW(None, IDC_ARROW) {
                SetCursor(Some(arrow));
            }
            let _ = DestroyIcon(icon);
        }
    }
}

struct Entry {
    key: String,
    exe: String,
    uid: Option<u32>,
    guid: Option<GUID>,
    promoted: bool,
}

fn settings_entries() -> Vec<Entry> {
    let mut out = Vec::new();
    let Some(key) = TRAY.with_borrow(|t| t.settings.map(|s| s.0)) else { return out };
    let folders = known_folders();
    unsafe {
        for index in 0.. {
            let mut name = [0u16; 64];
            let mut len = name.len() as u32;
            if RegEnumKeyExW(key, index, Some(PWSTR(name.as_mut_ptr())), &mut len, None, None, None, None).is_err() {
                break;
            }
            let sub = PCWSTR(name.as_ptr());
            let Some(raw) = reg_string(key, sub, w!("ExecutablePath")) else { continue };
            // "{FOLDERID}\resto": la carpeta conocida va como GUID.
            let exe = folders.iter().find_map(|(g, path)| raw.get(..38).filter(|p| p.eq_ignore_ascii_case(g)).map(|_| format!("{path}{}", &raw[38..]))).unwrap_or(raw);
            let guid = reg_bytes::<GUID>(key, sub, w!("IconGuid"));
            let subkey = String::from_utf16_lossy(&name[..len as usize]);
            out.push(Entry { key: subkey, exe, uid: reg_dword(key, sub, w!("UID")), guid, promoted: reg_dword(key, sub, w!("IsPromoted")) == Some(1) });
        }
    }
    out
}

fn known_folders() -> Vec<(&'static str, String)> {
    let ids = [
        ("{6D809377-6AF0-444B-8957-A3773F02200E}", &FOLDERID_ProgramFilesX64),
        ("{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}", &FOLDERID_ProgramFilesX86),
        ("{F38BF404-1D43-42F2-9305-67DE0B28FC23}", &FOLDERID_Windows),
        ("{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}", &FOLDERID_System),
    ];
    ids.iter()
        .filter_map(|(s, id)| unsafe {
            let p = SHGetKnownFolderPath(*id, KNOWN_FOLDER_FLAG(0), None).ok()?;
            let path = p.to_string().ok();
            CoTaskMemFree(Some(p.0 as *const c_void));
            path.map(|path| (*s, path))
        })
        .collect()
}

fn reg_string(key: HKEY, sub: PCWSTR, value: PCWSTR) -> Option<String> {
    let mut buf = [0u16; 1024];
    let mut size = (buf.len() * 2) as u32;
    unsafe { RegGetValueW(key, sub, value, RRF_RT_REG_SZ, None, Some(buf.as_mut_ptr() as *mut c_void), Some(&mut size)) }.ok().ok()?;
    let n = (size as usize / 2).saturating_sub(1);
    Some(String::from_utf16_lossy(&buf[..n]))
}

fn reg_dword(key: HKEY, sub: PCWSTR, value: PCWSTR) -> Option<u32> {
    let mut v = 0u32;
    let mut size = 4u32;
    unsafe { RegGetValueW(key, sub, value, RRF_RT_REG_DWORD, None, Some(&mut v as *mut u32 as *mut c_void), Some(&mut size)) }.ok().ok()?;
    Some(v)
}

fn reg_bytes<T: Copy>(key: HKEY, sub: PCWSTR, value: PCWSTR) -> Option<T> {
    let mut v = std::mem::MaybeUninit::<T>::zeroed();
    let mut size = size_of::<T>() as u32;
    unsafe { RegGetValueW(key, sub, value, RRF_RT_REG_BINARY, None, Some(v.as_mut_ptr() as *mut c_void), Some(&mut size)) }.ok().ok()?;
    (size as usize == size_of::<T>()).then(|| unsafe { v.assume_init() })
}
