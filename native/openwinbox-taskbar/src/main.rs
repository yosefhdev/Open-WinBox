#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

//! openwinbox-taskbar: barra de tareas propia, complemento de OpenWinBox.
//!
//! Modos:
//!   (sin argumentos)          barra
//!   --exit-after <s>          barra que se cierra sola (pruebas)
//!   --watchdog <pid>          vigilante: restaura la barra nativa si <pid> muere sin hacerlo

mod bar;
mod config;
mod ipc;
mod pins;
mod render;
mod shell;
mod tasks;

use std::io::Write;
use std::sync::atomic::{AtomicIsize, Ordering};

use windows::Win32::Foundation::*;
use windows::Win32::System::Com::*;
use windows::Win32::System::Console::SetConsoleCtrlHandler;
use windows::Win32::System::Threading::CreateMutexW;
use windows::Win32::UI::HiDpi::*;
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::core::*;

static MAIN_WINDOW: AtomicIsize = AtomicIsize::new(0);

pub fn set_main_window(h: HWND) {
    MAIN_WINDOW.store(h.0 as isize, Ordering::Relaxed);
}

pub fn main_window() -> HWND {
    HWND(MAIN_WINDOW.load(Ordering::Relaxed) as *mut _)
}

pub fn log(msg: &str) {
    let line = format!("[{}] {msg}\n", std::process::id());
    eprint!("{line}");
    if let Some(base) = std::env::var_os("LOCALAPPDATA") {
        let dir = std::path::Path::new(&base).join("OpenWinBox").join("Taskbar");
        let _ = std::fs::create_dir_all(&dir);
        if let Ok(mut f) = std::fs::OpenOptions::new().create(true).append(true).open(dir.join("taskbar.log")) {
            let _ = f.write_all(line.as_bytes());
        }
    }
}

pub fn class_name(h: HWND) -> String {
    let mut buf = [0u16; 128];
    let n = unsafe { GetClassNameW(h, &mut buf) };
    String::from_utf16_lossy(&buf[..n.max(0) as usize])
}

unsafe extern "system" fn on_console_ctrl(_: u32) -> BOOL {
    // Ctrl+C / cierre de consola: salir limpio desde el hilo de la barra.
    unsafe {
        let _ = PostMessageW(Some(main_window()), WM_CLOSE, WPARAM(0), LPARAM(0));
    }
    TRUE
}

fn main() {
    let args: Vec<String> = std::env::args().collect();
    let arg = |name: &str| args.iter().position(|a| a == name).and_then(|i| args.get(i + 1)).and_then(|v| v.parse::<u32>().ok());

    let text_arg = |name: &str| args.iter().position(|a| a == name).and_then(|i| args.get(i + 1)).cloned();
    if let Some(pid) = arg("--watchdog") {
        shell::run_watchdog(pid);
        return;
    }

    unsafe {
        let _ = SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        let _mutex = CreateMutexW(None, false, w!("OpenWinBox.Taskbar"));
        if GetLastError() == ERROR_ALREADY_EXISTS {
            log("ya hay una barra en ejecución; saliendo");
            return;
        }
        shell::recover_if_needed();
        pins::set_data_root(text_arg("--data-root"));
        let _ = CoInitializeEx(None, COINIT_APARTMENTTHREADED);
        let _ = SetConsoleCtrlHandler(Some(on_console_ctrl), true);
        if let Err(e) = bar::run(arg("--exit-after")) {
            log(&format!("error fatal: {e}"));
            shell::show_all();
            shell::clear_mark();
        }
    }
}
