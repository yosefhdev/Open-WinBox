//! Fase 0, prueba 3b: liberar el área de trabajo que reserva la barra nativa oculta.
//!
//! `workarea_spike spi`      → SPI_SETWORKAREA después de registrar nuestra AppBar.
//! `workarea_spike autohide` → poner la barra de Explorer en ocultación automática (ABM_SETSTATE).
//! En ambos: oculta las barras nativas, registra una AppBar propia de 48 px abajo (monitor
//! principal), registra el área de trabajo en cada paso y al final restaura todo.
//! El vigilante (`--watchdog <pid> <estado_original>`) restaura barras, estado y área si muere.

use std::cell::{Cell, RefCell};
use std::mem::size_of;
use std::os::windows::process::CommandExt;
use std::path::PathBuf;
use std::process::Command;

use windows::Win32::Foundation::*;
use windows::Win32::Graphics::Gdi::*;
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::System::Threading::*;
use windows::Win32::UI::Accessibility::*;
use windows::Win32::UI::Shell::*;
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::core::*;

const BAR: i32 = 48;
const TIMER_STEP: usize = 1;

thread_local! {
    static MODE: RefCell<String> = RefCell::default();
    static STEP: Cell<u32> = const { Cell::new(0) };
    static RESHOWS: Cell<u32> = const { Cell::new(0) };
    static ORIGINAL_STATE: Cell<usize> = const { Cell::new(0) };
    static ORIGINAL_WA: Cell<RECT> = Cell::new(RECT::default());
    static HOLD_SECONDS: Cell<u32> = const { Cell::new(0) };
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

fn set_taskbars_visible(visible: bool) {
    for h in taskbars() {
        unsafe {
            let _ = ShowWindow(h, if visible { SW_SHOW } else { SW_HIDE });
        }
    }
}

/// `region` = modo nuevo: la barra queda "visible" para Explorer pero con región vacía,
/// así no pinta nada ni recibe clics aunque Explorer la vuelva a mostrar.
fn set_empty_region(on: bool) {
    for h in taskbars() {
        unsafe {
            if on {
                let rgn = CreateRectRgn(0, 0, 0, 0);
                SetWindowRgn(h, Some(rgn), true); // el sistema se queda con la región
            } else {
                SetWindowRgn(h, None, true);
            }
        }
    }
}

fn regions_empty() -> String {
    taskbars()
        .iter()
        .map(|&h| unsafe {
            let rgn = CreateRectRgn(0, 0, 0, 0);
            let kind = GetWindowRgn(h, rgn);
            let _ = DeleteObject(rgn.into());
            match kind {
                NULLREGION => "vacía",
                RGN_ERROR => "sin región",
                _ => "con forma",
            }
        })
        .collect::<Vec<_>>()
        .join(",")
}

/// Estado original guardado en disco ANTES de tocar nada: si barra y vigilante mueren a la
/// vez, el siguiente arranque lo restaura.
fn state_file() -> PathBuf {
    std::env::temp_dir().join("workarea_spike_state.txt")
}

fn restore_everything(state: usize) {
    set_taskbar_state(state);
    set_empty_region(false);
    set_taskbars_visible(true);
}

fn work_area() -> RECT {
    let mut r = RECT::default();
    unsafe {
        let _ = SystemParametersInfoW(SPI_GETWORKAREA, 0, Some(&mut r as *mut _ as *mut _), SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS(0));
    }
    r
}

fn set_work_area(r: RECT) {
    let mut r = r;
    unsafe {
        let _ = SystemParametersInfoW(SPI_SETWORKAREA, 0, Some(&mut r as *mut _ as *mut _), SPIF_SENDCHANGE);
    }
}

fn primary_monitor() -> RECT {
    unsafe {
        let mon = MonitorFromPoint(POINT { x: 0, y: 0 }, MONITOR_DEFAULTTOPRIMARY);
        let mut info = MONITORINFO { cbSize: size_of::<MONITORINFO>() as u32, ..Default::default() };
        let _ = GetMonitorInfoW(mon, &mut info);
        info.rcMonitor
    }
}

fn appbar(msg: u32, data: &mut APPBARDATA) -> usize {
    unsafe { SHAppBarMessage(msg, data) }
}

fn taskbar_state() -> usize {
    let mut d = APPBARDATA { cbSize: size_of::<APPBARDATA>() as u32, ..Default::default() };
    appbar(ABM_GETSTATE, &mut d)
}

fn set_taskbar_state(state: usize) {
    let mut d = APPBARDATA {
        cbSize: size_of::<APPBARDATA>() as u32,
        hWnd: taskbars().first().copied().unwrap_or_default(),
        lParam: LPARAM(state as isize),
        ..Default::default()
    };
    appbar(ABM_SETSTATE, &mut d);
}

fn log(what: &str) {
    let wa = work_area();
    println!(
        "[{what}] área de trabajo: {}x{} (bottom={})  estado barra={}  regiones={}",
        wa.right - wa.left,
        wa.bottom - wa.top,
        wa.bottom,
        taskbar_state(),
        regions_empty()
    );
}

fn main() -> Result<()> {
    let args: Vec<String> = std::env::args().collect();
    if args.get(1).map(String::as_str) == Some("--watchdog") {
        return watchdog(&args);
    }
    let mode = args.get(1).cloned().unwrap_or_else(|| "spi".into());
    MODE.with_borrow_mut(|m| *m = mode.clone());
    HOLD_SECONDS.with(|c| c.set(args.get(2).and_then(|a| a.parse().ok()).unwrap_or(0)));
    run(&mode)
}

fn watchdog(args: &[String]) -> Result<()> {
    let pid: u32 = args.get(2).and_then(|p| p.parse().ok()).unwrap_or(0);
    let state: usize = args.get(3).and_then(|p| p.parse().ok()).unwrap_or(0);
    unsafe {
        let process = OpenProcess(PROCESS_SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION, false, pid)?;
        WaitForSingleObject(process, INFINITE);
        let mut code = 0;
        let _ = GetExitCodeProcess(process, &mut code);
        if code != 0 {
            // Muerte sucia: devolver ajuste del usuario y barras; Explorer recalcula el área al reaparecer.
            restore_everything(state);
            let _ = std::fs::remove_file(state_file());
            log("vigilante restauró");
        }
    }
    Ok(())
}

fn run(mode: &str) -> Result<()> {
    unsafe {
        if let Some(prev) = std::fs::read_to_string(state_file()).ok().and_then(|t| t.trim().parse().ok()) {
            restore_everything(prev);
            log("restaurado estado de una ejecución anterior que murió");
            let _ = std::fs::remove_file(state_file());
        }
        ORIGINAL_STATE.with(|c| c.set(taskbar_state()));
        ORIGINAL_WA.with(|c| c.set(work_area()));
        std::fs::write(state_file(), taskbar_state().to_string()).expect("guardar estado");

        // Desacoplado del árbol/job: que no muera con nosotros (incidente del 2026-11-25).
        const DETACHED_PROCESS: u32 = 0x8;
        const CREATE_BREAKAWAY_FROM_JOB: u32 = 0x0100_0000;
        let args = ["--watchdog".to_string(), std::process::id().to_string(), taskbar_state().to_string()];
        let exe = std::env::current_exe().unwrap();
        let detached = Command::new(&exe).args(&args).creation_flags(DETACHED_PROCESS | CREATE_BREAKAWAY_FROM_JOB).spawn();
        println!("vigilante fuera del job: {}", detached.is_ok());
        if detached.is_err() {
            Command::new(&exe).args(&args).creation_flags(DETACHED_PROCESS).spawn().expect("vigilante");
        }

        let instance: HINSTANCE = GetModuleHandleW(None)?.into();
        let wc = WNDCLASSEXW {
            cbSize: size_of::<WNDCLASSEXW>() as u32,
            lpfnWndProc: Some(wnd_proc),
            hInstance: instance,
            lpszClassName: w!("WorkAreaSpike"),
            hbrBackground: HBRUSH(GetStockObject(DKGRAY_BRUSH).0),
            ..Default::default()
        };
        RegisterClassExW(&wc);
        let mon = primary_monitor();
        let hwnd = CreateWindowExW(
            WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE,
            w!("WorkAreaSpike"),
            None,
            WS_POPUP | WS_VISIBLE,
            mon.left,
            mon.bottom - BAR,
            mon.right - mon.left,
            BAR,
            None,
            None,
            Some(instance),
            None,
        )?;
        let hook = SetWinEventHook(EVENT_OBJECT_SHOW, EVENT_OBJECT_SHOW, None, Some(on_show), 0, 0, WINEVENT_OUTOFCONTEXT);

        println!("== modo {mode} ==");
        log("0 inicio");
        SetTimer(Some(hwnd), TIMER_STEP, 2500, None);
        on_step(hwnd);

        let mut msg = MSG::default();
        while GetMessageW(&mut msg, None, 0, 0).as_bool() {
            let _ = TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        let _ = UnhookWinEvent(hook);
        let _ = std::fs::remove_file(state_file());
        println!("reacciones a Explorer mostrando su barra: {}", RESHOWS.with(|c| c.get()));
    }
    Ok(())
}

fn register_appbar(hwnd: HWND, msg: u32) {
    let mon = primary_monitor();
    let mut d = APPBARDATA {
        cbSize: size_of::<APPBARDATA>() as u32,
        hWnd: hwnd,
        uEdge: ABE_BOTTOM,
        rc: RECT { left: mon.left, top: mon.bottom - BAR, right: mon.right, bottom: mon.bottom },
        ..Default::default()
    };
    if msg == ABM_NEW {
        appbar(ABM_NEW, &mut d);
    }
    appbar(ABM_QUERYPOS, &mut d);
    d.rc.top = d.rc.bottom - BAR;
    appbar(ABM_SETPOS, &mut d);
}

fn on_step(hwnd: HWND) {
    let mode = MODE.with_borrow(|m| m.clone());
    let step = STEP.with(|c| {
        c.set(c.get() + 1);
        c.get()
    });
    let mon = primary_monitor();
    match step {
        1 => {
            if mode == "region" {
                set_empty_region(true);
                log("1 barras nativas con región vacía (siguen 'visibles')");
            } else {
                set_taskbars_visible(false);
                log("1 barras nativas ocultas");
            }
        }
        2 => {
            if mode == "autohide" || mode == "region" {
                set_taskbar_state(ABS_AUTOHIDE as usize);
                log("2 Explorer en ocultación automática");
            } else {
                log("2 (spi: nada aún)");
            }
        }
        3 => {
            register_appbar(hwnd, ABM_NEW);
            log("3 AppBar propia registrada");
        }
        4 => {
            if mode == "spi" {
                set_work_area(RECT { bottom: mon.bottom - BAR, ..mon });
                log("4 SPI_SETWORKAREA = monitor - nuestra barra");
            } else {
                log("4 (autohide: nada)");
            }
        }
        5 => log("5 tras 2,5 s"),
        6 => {
            register_appbar(hwnd, ABM_SETPOS);
            log("6 tras volver a posicionar nuestra AppBar");
        }
        7 => {
            unsafe {
                let _ = SendMessageTimeoutW(HWND_BROADCAST, WM_SETTINGCHANGE, WPARAM(SPI_SETWORKAREA.0 as usize), LPARAM(0), SMTO_ABORTIFHUNG, 1000, None);
            }
            log("7 tras WM_SETTINGCHANGE");
        }
        8 => {
            log("8 tras 2,5 s más");
            let hold = HOLD_SECONDS.with(|c| c.get());
            if hold > 0 {
                // Mantener el estado final para observarlo a ojo antes de restaurar.
                println!("   manteniendo {hold} s; re-ocultadas hasta ahora: {}", RESHOWS.with(|c| c.get()));
                unsafe {
                    SetTimer(Some(hwnd), TIMER_STEP, hold * 1000, None);
                }
            }
        }
        _ => {
            let mut d = APPBARDATA { cbSize: size_of::<APPBARDATA>() as u32, hWnd: hwnd, ..Default::default() };
            appbar(ABM_REMOVE, &mut d);
            restore_everything(ORIGINAL_STATE.with(|c| c.get()));
            log("9 restaurado (AppBar quitada, estado original, barras visibles)");
            if work_area() != ORIGINAL_WA.with(|c| c.get()) {
                set_work_area(ORIGINAL_WA.with(|c| c.get()));
                log("9b área original forzada");
            }
            unsafe {
                let _ = DestroyWindow(hwnd);
            }
        }
    }
}

unsafe extern "system" fn on_show(_: HWINEVENTHOOK, _: u32, hwnd: HWND, id_object: i32, _: i32, _: u32, _: u32) {
    if id_object == OBJID_WINDOW.0 && STEP.with(|c| c.get()) < 9 && taskbars().contains(&hwnd) {
        if MODE.with_borrow(|m| m == "region") {
            // No ocultar: solo comprobar que la región sigue vacía (Explorer podría quitarla).
            println!("   Explorer la mostró; regiones={}", regions_empty());
            set_empty_region(true);
            RESHOWS.with(|c| c.set(c.get() + 1));
            return;
        }
        unsafe {
            let _ = ShowWindow(hwnd, SW_HIDE);
        }
        RESHOWS.with(|c| c.set(c.get() + 1));
        let mut pt = POINT::default();
        unsafe {
            let _ = GetCursorPos(&mut pt);
        }
        println!("   re-ocultada (ratón en y={})", pt.y);
    }
}

unsafe extern "system" fn wnd_proc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    unsafe {
        match msg {
            WM_TIMER if wparam.0 == TIMER_STEP => {
                on_step(hwnd);
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
