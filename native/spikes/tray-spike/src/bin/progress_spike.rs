//! Fase 0, prueba 2: ¿a qué ventana manda `ITaskbarList3` el progreso y el overlay?
//!
//! Imita la jerarquía de la barra de Explorer (Shell_TrayWnd > ReBarWindow32 > MSTaskSwWClass >
//! MSTaskListWClass, + TrayNotifyWnd), se pone por encima para que FindWindow la encuentre y
//! registra todo mensaje no rutinario que reciba cada ventana. WM_COPYDATA se reenvía a Explorer
//! para no romper la bandeja. Sale sola tras N segundos (argumento, por defecto 60).

use std::cell::RefCell;
use std::mem::size_of;

use windows::Win32::Foundation::*;
use windows::Win32::System::DataExchange::{COPYDATASTRUCT, GetClipboardFormatNameW, GlobalGetAtomNameW};
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::core::*;

const TIMER_TOPMOST: usize = 1;
const TIMER_QUIT: usize = 2;

thread_local! {
    static EXPLORER_TRAY: RefCell<HWND> = RefCell::new(HWND::default());
    static TOP: RefCell<HWND> = RefCell::new(HWND::default());
}

fn main() -> Result<()> {
    let seconds: u32 = std::env::args().nth(1).and_then(|a| a.parse().ok()).unwrap_or(60);
    unsafe {
        let explorer = FindWindowW(w!("Shell_TrayWnd"), None)?;
        EXPLORER_TRAY.with_borrow_mut(|h| *h = explorer);
        let answer = SendMessageW(explorer, WM_USER + 239, None, None);
        let as_hwnd = HWND(answer.0 as *mut _);
        println!(
            "Explorer responde a WM_USER+239: 0x{:X} (¿ventana? {} clase {:?})",
            answer.0,
            IsWindow(Some(as_hwnd)).as_bool(),
            class_name(as_hwnd)
        );
        let instance: HINSTANCE = GetModuleHandleW(None)?.into();

        for class in [w!("Shell_TrayWnd"), w!("ReBarWindow32"), w!("MSTaskSwWClass"), w!("MSTaskListWClass"), w!("TrayNotifyWnd")] {
            let wc = WNDCLASSEXW {
                cbSize: size_of::<WNDCLASSEXW>() as u32,
                lpfnWndProc: Some(wnd_proc),
                hInstance: instance,
                lpszClassName: class,
                ..Default::default()
            };
            if RegisterClassExW(&wc) == 0 {
                return Err(Error::from_thread());
            }
        }

        let top = CreateWindowExW(
            WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
            w!("Shell_TrayWnd"),
            None,
            WS_POPUP,
            0,
            0,
            1,
            1,
            None,
            None,
            Some(instance),
            None,
        )?;
        TOP.with_borrow_mut(|h| *h = top);
        let child = |parent: HWND, class: PCWSTR| {
            CreateWindowExW(WINDOW_EX_STYLE(0), class, None, WS_CHILD, 0, 0, 1, 1, Some(parent), None, Some(instance), None)
        };
        let rebar = child(top, w!("ReBarWindow32"))?;
        let tasksw = child(rebar, w!("MSTaskSwWClass"))?;
        let _list = child(tasksw, w!("MSTaskListWClass"))?;
        let _notify = child(top, w!("TrayNotifyWnd"))?;
        // Explorer publica aquí la ventana de la banda de tareas; ITaskbarList la lee en HrInit.
        SetPropW(top, w!("TaskbandHWND"), Some(HANDLE(tasksw.0)))?;

        for hwnd in [top, rebar, tasksw] {
            // Mensajes de cualquier proceso, también elevados.
            let _ = ChangeWindowMessageFilterEx(hwnd, WM_COPYDATA, MSGFLT_ALLOW, None);
        }

        let _ = SetWindowPos(top, Some(HWND_TOPMOST), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        SetTimer(Some(top), TIMER_TOPMOST, 500, None);
        SetTimer(Some(top), TIMER_QUIT, seconds * 1000, None);
        println!(
            "Listo. FindWindow devuelve la nuestra: {}. Escuchando {seconds} s.",
            FindWindowW(w!("Shell_TrayWnd"), None)? == top
        );

        // Las apps que ya inicializaron ITaskbarList guardaron la TaskbandHWND de Explorer.
        // Avisar a cada ventana como si su botón acabara de crearse, a ver si se reinician.
        let created = RegisterWindowMessageW(w!("TaskbarButtonCreated"));
        let _ = EnumWindows(Some(announce), LPARAM(created as isize));
        println!("TaskbarButtonCreated enviado a las ventanas visibles.");

        let mut msg = MSG::default();
        while GetMessageW(&mut msg, None, 0, 0).as_bool() {
            if msg.hwnd.is_invalid() && msg.message >= WM_USER {
                println!("[thread] msg=0x{:X} w=0x{:X} l=0x{:X}", msg.message, msg.wParam.0, msg.lParam.0);
            }
            let _ = TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }

        for class in [w!("Shell_TrayWnd"), w!("ReBarWindow32"), w!("MSTaskSwWClass"), w!("MSTaskListWClass"), w!("TrayNotifyWnd")] {
            let _ = UnregisterClassW(class, Some(instance));
        }
        let tc = RegisterWindowMessageW(w!("TaskbarCreated"));
        let _ = PostMessageW(Some(HWND_BROADCAST), tc, WPARAM(0), LPARAM(0));
        println!("Salida limpia.");
    }
    Ok(())
}

unsafe extern "system" fn announce(hwnd: HWND, msg: LPARAM) -> BOOL {
    unsafe {
        if IsWindowVisible(hwnd).as_bool() && GetWindow(hwnd, GW_OWNER).is_err() {
            let _ = PostMessageW(Some(hwnd), msg.0 as u32, WPARAM(0), LPARAM(0));
        }
    }
    TRUE
}

fn class_name(hwnd: HWND) -> String {
    let mut buf = [0u16; 64];
    let n = unsafe { GetClassNameW(hwnd, &mut buf) };
    String::from_utf16_lossy(&buf[..n.max(0) as usize])
}

fn message_name(msg: u32) -> String {
    if msg >= 0xC000 {
        // Los mensajes registrados comparten tabla de átomos con los formatos de portapapeles.
        let mut buf = [0u16; 128];
        let n = unsafe { GetClipboardFormatNameW(msg, &mut buf) };
        if n > 0 {
            return format!("\"{}\"", String::from_utf16_lossy(&buf[..n as usize]));
        }
    }
    if msg >= WM_USER && msg < WM_APP {
        return format!("WM_USER+{}", msg - WM_USER);
    }
    format!("0x{msg:X}")
}

fn is_routine(msg: u32) -> bool {
    matches!(
        msg,
        WM_CREATE | WM_NCCREATE | WM_NCCALCSIZE | WM_SIZE | WM_MOVE | WM_SHOWWINDOW | WM_WINDOWPOSCHANGING
            | WM_WINDOWPOSCHANGED | WM_NCDESTROY | WM_DESTROY | WM_TIMER | WM_GETMINMAXINFO | WM_NCPAINT
            | WM_PAINT | WM_ERASEBKGND | WM_GETOBJECT | WM_PARENTNOTIFY | WM_SETTINGCHANGE | WM_THEMECHANGED
            | WM_DWMNCRENDERINGCHANGED | WM_GETICON | WM_DISPLAYCHANGE | WM_DEVICECHANGE | WM_ACTIVATEAPP
            | WM_FONTCHANGE | WM_NCACTIVATE | WM_ACTIVATE | WM_SETFOCUS | WM_KILLFOCUS | WM_IME_SETCONTEXT
            | WM_IME_NOTIFY | WM_DWMCOLORIZATIONCOLORCHANGED | WM_CLIPBOARDUPDATE | WM_POWERBROADCAST
            | WM_DPICHANGED | WM_GETTEXT | WM_GETTEXTLENGTH
    )
}

unsafe extern "system" fn wnd_proc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    unsafe {
        let top = TOP.with_borrow(|h| *h);
        if hwnd == top {
            match msg {
                WM_TIMER if wparam.0 == TIMER_TOPMOST => {
                    let _ = SetWindowPos(hwnd, Some(HWND_TOPMOST), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    return LRESULT(0);
                }
                WM_TIMER if wparam.0 == TIMER_QUIT => {
                    let _ = DestroyWindow(hwnd);
                    return LRESULT(0);
                }
                WM_DESTROY => {
                    PostQuitMessage(0);
                    return LRESULT(0);
                }
                _ => {}
            }
        }
        if hwnd == top && msg == WM_USER + 239 {
            // Explorer contesta 5: imitarlo para ver qué hace ITaskbarList después.
            println!("[Shell_TrayWnd] WM_USER+239 -> respondo 5");
            return LRESULT(5);
        }
        if msg == WM_COPYDATA {
            let cds = &*(lparam.0 as *const COPYDATASTRUCT);
            if cds.dwData != 1 {
                println!("[{}] WM_COPYDATA dwData={} cb={}", class_name(hwnd), cds.dwData, cds.cbData);
            }
            let mut fwd = *cds;
            let explorer = EXPLORER_TRAY.with_borrow(|h| *h);
            return SendMessageW(explorer, WM_COPYDATA, Some(wparam), Some(LPARAM(&mut fwd as *mut _ as isize)));
        }
        if (msg == WM_USER + 80 || msg == WM_USER + 85) && lparam.0 != 0 {
            let mut buf = [0u16; 260];
            let n = GlobalGetAtomNameW(lparam.0 as u16, &mut buf);
            println!("    átomo 0x{:X} = {:?}", lparam.0, String::from_utf16_lossy(&buf[..n as usize]));
        }
        if !is_routine(msg) {
            let pid = {
                let mut pid = 0;
                // wParam suele ser el HWND de la ventana de la app en estos mensajes.
                GetWindowThreadProcessId(HWND(wparam.0 as *mut _), Some(&mut pid));
                pid
            };
            println!(
                "[{}] {} w=0x{:X} (pid de w: {pid}) l=0x{:X}",
                class_name(hwnd),
                message_name(msg),
                wparam.0,
                lparam.0
            );
        }
        DefWindowProcW(hwnd, msg, wparam, lparam)
    }
}
