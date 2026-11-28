//! Fase 0, prueba 3: ocultar la barra nativa y garantizar que vuelve.
//!
//! Modo barra (`hide_spike [segundos]`): lanza su vigilante, oculta Shell_TrayWnd y todas las
//! Shell_SecondaryTrayWnd, las vuelve a ocultar si Explorer las muestra (WinEventHook) o si
//! Explorer se reinicia (TaskbarCreated), y al terminar las restaura.
//! Modo vigilante (`hide_spike --watchdog <pid>`): espera a que muera la barra y restaura la
//! barra nativa pase lo que pase.

use std::cell::Cell;
use std::mem::size_of;
use std::process::Command;
use std::time::{SystemTime, UNIX_EPOCH};

use windows::Win32::Foundation::*;
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::System::Threading::*;
use windows::Win32::UI::Accessibility::*;
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::core::*;

const TIMER_QUIT: usize = 1;

thread_local! {
    static TASKBAR_CREATED: Cell<u32> = const { Cell::new(0) };
    static RESHOWS: Cell<u32> = const { Cell::new(0) };
}

fn now_ms() -> u128 {
    SystemTime::now().duration_since(UNIX_EPOCH).unwrap().as_millis()
}

fn taskbars() -> Vec<HWND> {
    let mut out = Vec::new();
    unsafe {
        if let Ok(main) = FindWindowW(w!("Shell_TrayWnd"), None) {
            out.push(main);
        }
        let mut prev = None;
        while let Ok(h) = FindWindowExW(None, prev, w!("Shell_SecondaryTrayWnd"), None) {
            out.push(h);
            prev = Some(h);
        }
    }
    out
}

fn set_taskbars_visible(visible: bool) -> usize {
    let bars = taskbars();
    for &h in &bars {
        unsafe {
            let _ = ShowWindow(h, if visible { SW_SHOW } else { SW_HIDE });
        }
    }
    bars.len()
}

fn visible_count() -> usize {
    taskbars().iter().filter(|&&h| unsafe { IsWindowVisible(h).as_bool() }).count()
}

fn work_area() -> RECT {
    let mut r = RECT::default();
    unsafe {
        let _ = SystemParametersInfoW(SPI_GETWORKAREA, 0, Some(&mut r as *mut _ as *mut _), SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS(0));
    }
    r
}

fn main() -> Result<()> {
    let args: Vec<String> = std::env::args().collect();
    if args.get(1).map(String::as_str) == Some("--watchdog") {
        return watchdog(args.get(2).and_then(|p| p.parse().ok()).unwrap_or(0));
    }
    let seconds: u32 = args.get(1).and_then(|a| a.parse().ok()).unwrap_or(30);
    bar(seconds)
}

fn watchdog(pid: u32) -> Result<()> {
    unsafe {
        let process = OpenProcess(PROCESS_SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION, false, pid)?;
        println!("[vigilante] vigilando pid {pid}");
        WaitForSingleObject(process, INFINITE);
        let died = now_ms();
        let mut code = 0u32;
        let _ = GetExitCodeProcess(process, &mut code);
        let _ = CloseHandle(process);
        // Siempre restaurar: si la barra salió limpia ya lo hizo y esto no cambia nada.
        let n = set_taskbars_visible(true);
        println!(
            "[vigilante] barra terminó (código {code}) en t={died}; {n} barras mostradas en {} ms; visibles ahora: {}; área de trabajo {:?}",
            now_ms() - died,
            visible_count(),
            work_area()
        );
    }
    Ok(())
}

fn bar(seconds: u32) -> Result<()> {
    unsafe {
        let me = std::process::id();
        Command::new(std::env::current_exe().unwrap())
            .args(["--watchdog", &me.to_string()])
            .spawn()
            .expect("lanzar vigilante");

        let instance: HINSTANCE = GetModuleHandleW(None)?.into();
        let wc = WNDCLASSEXW {
            cbSize: size_of::<WNDCLASSEXW>() as u32,
            lpfnWndProc: Some(wnd_proc),
            hInstance: instance,
            lpszClassName: w!("HideSpike"),
            ..Default::default()
        };
        RegisterClassExW(&wc);
        // Ventana de nivel superior oculta: TaskbarCreated llega por HWND_BROADCAST, que no
        // alcanza a las ventanas solo-mensaje.
        let hwnd = CreateWindowExW(WS_EX_TOOLWINDOW, w!("HideSpike"), None, WS_POPUP, 0, 0, 0, 0, None, None, Some(instance), None)?;
        let tc = RegisterWindowMessageW(w!("TaskbarCreated"));
        TASKBAR_CREATED.with(|c| c.set(tc));
        let _ = ChangeWindowMessageFilterEx(hwnd, tc, MSGFLT_ALLOW, None);

        let hook = SetWinEventHook(EVENT_OBJECT_SHOW, EVENT_OBJECT_SHOW, None, Some(on_show), 0, 0, WINEVENT_OUTOFCONTEXT);

        println!("[barra] pid {me}; área de trabajo antes: {:?}", work_area());
        let n = set_taskbars_visible(false);
        println!("[barra] t={} ocultadas {n} barras; visibles: {}; área de trabajo después: {:?}", now_ms(), visible_count(), work_area());

        SetTimer(Some(hwnd), TIMER_QUIT, seconds * 1000, None);
        let mut msg = MSG::default();
        while GetMessageW(&mut msg, None, 0, 0).as_bool() {
            let _ = TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }

        let _ = UnhookWinEvent(hook);
        set_taskbars_visible(true);
        println!(
            "[barra] salida limpia; re-ocultadas {} veces; visibles: {}; área de trabajo: {:?}",
            RESHOWS.with(|c| c.get()),
            visible_count(),
            work_area()
        );
    }
    Ok(())
}

unsafe extern "system" fn on_show(_: HWINEVENTHOOK, _event: u32, hwnd: HWND, id_object: i32, _: i32, _: u32, _: u32) {
    if id_object != OBJID_WINDOW.0 || !taskbars().contains(&hwnd) {
        return;
    }
    unsafe {
        let _ = ShowWindow(hwnd, SW_HIDE);
    }
    RESHOWS.with(|c| c.set(c.get() + 1));
    println!("[barra] t={} Explorer volvió a mostrar 0x{:X}: re-ocultada", now_ms(), hwnd.0 as usize);
}

unsafe extern "system" fn wnd_proc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    unsafe {
        if msg == TASKBAR_CREATED.with(|c| c.get()) {
            let n = set_taskbars_visible(false);
            println!("[barra] t={} TaskbarCreated (Explorer reiniciado): ocultadas {n} barras de nuevo", now_ms());
            return LRESULT(0);
        }
        match msg {
            WM_TIMER if wparam.0 == TIMER_QUIT => {
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
