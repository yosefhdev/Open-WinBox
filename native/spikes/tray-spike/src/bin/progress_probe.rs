//! Cliente de la prueba 2: una ventana propia que usa ITaskbarList3 como cualquier app
//! (progreso, estado, overlay, tooltip) para ver qué mensajes produce cada llamada.

use std::mem::size_of;
use std::thread::sleep;
use std::time::Duration;

use windows::Win32::Foundation::*;
use windows::Win32::System::Com::*;
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::UI::Shell::*;
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::core::*;

unsafe extern "system" fn wnd_proc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    unsafe { DefWindowProcW(hwnd, msg, wparam, lparam) }
}

fn pump() {
    let mut msg = MSG::default();
    unsafe {
        while PeekMessageW(&mut msg, None, 0, 0, PM_REMOVE).as_bool() {
            let _ = TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
    }
}

fn step(name: &str, r: Result<()>) {
    println!("[probe] {name}: {}", r.map(|_| "OK".to_string()).unwrap_or_else(|e| e.message()));
    pump();
    sleep(Duration::from_millis(400));
}

fn main() -> Result<()> {
    unsafe {
        CoInitializeEx(None, COINIT_APARTMENTTHREADED).ok()?;
        let instance: HINSTANCE = GetModuleHandleW(None)?.into();
        let wc = WNDCLASSEXW {
            cbSize: size_of::<WNDCLASSEXW>() as u32,
            lpfnWndProc: Some(wnd_proc),
            hInstance: instance,
            lpszClassName: w!("ProgressProbe"),
            ..Default::default()
        };
        RegisterClassExW(&wc);
        let hwnd = CreateWindowExW(
            WINDOW_EX_STYLE(0),
            w!("ProgressProbe"),
            w!("Progress probe"),
            WS_OVERLAPPEDWINDOW | WS_VISIBLE,
            100,
            100,
            300,
            150,
            None,
            None,
            Some(instance),
            None,
        )?;
        println!("[probe] hwnd=0x{:X} pid={}", hwnd.0 as usize, std::process::id());
        pump();
        sleep(Duration::from_millis(500));

        let tbl: ITaskbarList3 = CoCreateInstance(&TaskbarList, None, CLSCTX_INPROC_SERVER)?;
        step("HrInit", tbl.HrInit());
        if std::env::args().any(|a| a == "--late") {
            // Simula una app que inicializó ITaskbarList antes de que arrancara la barra.
            println!("[probe] esperando a la barra...");
            sleep(Duration::from_secs(4));
        }
        step("SetProgressState NORMAL", tbl.SetProgressState(hwnd, TBPF_NORMAL));
        step("SetProgressValue 40/100", tbl.SetProgressValue(hwnd, 40, 100));
        step("SetProgressValue 75/100", tbl.SetProgressValue(hwnd, 75, 100));
        step("SetProgressState ERROR", tbl.SetProgressState(hwnd, TBPF_ERROR));
        let icon = LoadIconW(None, IDI_WARNING)?;
        step("SetOverlayIcon", tbl.SetOverlayIcon(hwnd, icon, w!("aviso")));
        step("SetThumbnailTooltip", tbl.SetThumbnailTooltip(hwnd, w!("tooltip de prueba")));
        step("SetProgressState NOPROGRESS", tbl.SetProgressState(hwnd, TBPF_NOPROGRESS));
        step("SetOverlayIcon null", tbl.SetOverlayIcon(hwnd, HICON::default(), None));
        let _ = DestroyWindow(hwnd);
        drop(tbl); // liberar antes de CoUninitialize, si no Release revienta
        CoUninitialize();
    }
    Ok(())
}
