//! La barra nativa de Explorer: encontrarla, ocultarla, restaurarla y el vigilante.
//!
//! Regla de seguridad: antes de ocultar se escribe una marca en disco. Quien la encuentre al
//! arrancar (la barra, el vigilante u OpenWinBox) restaura la barra nativa. Así un cierre a la
//! fuerza de barra y vigilante juntos nunca deja al usuario sin barra tras el siguiente arranque.

use std::os::windows::process::CommandExt;
use std::path::PathBuf;

use windows::Win32::Foundation::*;
use windows::Win32::System::Threading::*;
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::core::*;

use crate::log;

pub const PRIMARY_CLASS: PCWSTR = w!("Shell_TrayWnd");
pub const SECONDARY_CLASS: PCWSTR = w!("Shell_SecondaryTrayWnd");

/// Barra principal y todas las secundarias de Explorer.
pub fn native_bars() -> Vec<HWND> {
    let mut bars = Vec::new();
    unsafe {
        if let Ok(h) = FindWindowW(PRIMARY_CLASS, None) {
            bars.push(h);
        }
        let mut after = None;
        while let Ok(h) = FindWindowExW(None, after, SECONDARY_CLASS, None) {
            bars.push(h);
            after = Some(h);
        }
    }
    bars
}

/// Las barras de Explorer de los monitores secundarios.
pub fn secondary_bars() -> Vec<HWND> {
    native_bars().into_iter().filter(|&h| crate::class_name(h) == "Shell_SecondaryTrayWnd").collect()
}

pub fn primary_rect() -> Option<RECT> {
    window_rect(unsafe { FindWindowW(PRIMARY_CLASS, None) }.ok()?)
}

/// Rectángulo en pantalla; sirve también con la ventana oculta.
pub fn window_rect(h: HWND) -> Option<RECT> {
    let mut r = RECT::default();
    unsafe { GetWindowRect(h, &mut r) }.ok()?;
    Some(r)
}

pub fn is_native_bar(hwnd: HWND) -> bool {
    let class = crate::class_name(hwnd);
    class == "Shell_TrayWnd" || class == "Shell_SecondaryTrayWnd"
}

pub fn hide_all() {
    for h in native_bars() {
        unsafe {
            let _ = ShowWindow(h, SW_HIDE);
        }
    }
}

pub fn show_all() {
    for h in native_bars() {
        unsafe {
            let _ = ShowWindow(h, SW_SHOWNOACTIVATE);
        }
    }
}

fn mark_path() -> PathBuf {
    let base = std::env::var_os("LOCALAPPDATA").map(PathBuf::from).unwrap_or_else(std::env::temp_dir);
    base.join("OpenWinBox").join("Taskbar").join("native-hidden")
}

pub fn mark_hidden() {
    let path = mark_path();
    if let Some(dir) = path.parent() {
        let _ = std::fs::create_dir_all(dir);
    }
    if let Err(e) = std::fs::write(&path, std::process::id().to_string()) {
        log(&format!("no se pudo escribir la marca {}: {e}", path.display()));
    }
}

pub fn clear_mark() {
    let _ = std::fs::remove_file(mark_path());
}

pub fn left_hidden() -> bool {
    mark_path().exists()
}

/// Restaura la barra nativa si una ejecución anterior la dejó oculta.
pub fn recover_if_needed() {
    if left_hidden() {
        log("marca encontrada: una ejecución anterior no restauró la barra nativa; restaurando");
        show_all();
        clear_mark();
    }
}

/// Lanza `--watchdog <pid>` como proceso aparte. Intenta salir del job del padre (si lo hay)
/// para que matar el árbol de procesos no se lleve también al vigilante.
pub fn spawn_watchdog() {
    const DETACHED_PROCESS: u32 = 0x8;
    const CREATE_BREAKAWAY_FROM_JOB: u32 = 0x0100_0000;
    let Ok(exe) = std::env::current_exe() else { return };
    let pid = std::process::id().to_string();
    let spawn = |flags| std::process::Command::new(&exe).args(["--watchdog", &pid]).creation_flags(flags).spawn();
    match spawn(DETACHED_PROCESS | CREATE_BREAKAWAY_FROM_JOB).or_else(|_| spawn(DETACHED_PROCESS)) {
        Ok(child) => log(&format!("vigilante lanzado, pid {}", child.id())),
        Err(e) => log(&format!("no se pudo lanzar el vigilante: {e}")),
    }
}

pub fn run_watchdog(pid: u32) {
    unsafe {
        match OpenProcess(PROCESS_SYNCHRONIZE, false, pid) {
            Ok(h) => {
                WaitForSingleObject(h, INFINITE);
                let _ = CloseHandle(h);
            }
            Err(e) => log(&format!("vigilante: no se pudo abrir el pid {pid}: {e}")),
        }
    }
    if left_hidden() {
        log(&format!("vigilante: la barra {pid} terminó sin restaurar; restaurando la nativa"));
        show_all();
        clear_mark();
    }
}
