//! Área de trabajo extra cuando la barra sale de la franja que Explorer ya reserva (más alta o con
//! margen). Una AppBar propia, en una ventana oculta aparte, se apila encima de la de Explorer: así
//! las ventanas maximizadas no quedan debajo de la barra. Sin ocultación automática (parpadea).

use windows::Win32::Foundation::*;
use windows::Win32::Graphics::Gdi::*;
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::UI::Shell::*;
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::core::*;

use crate::log;

/// Avisos de la AppBar (`ABN_POSCHANGED`…): basta con recolocar.
pub const WM_APP_APPBAR: u32 = WM_APP + 2;

#[derive(Default)]
pub struct AppBar {
    hwnd: Option<HWND>,
    /// (monitor, píxeles reservados) de la última reserva; evita repetirla en cada refresco.
    reserved: Option<(isize, i32)>,
}

impl AppBar {
    /// Reserva `extra` píxeles físicos encima de la franja de Explorer en `monitor`; 0 o menos la libera.
    pub fn update(&mut self, monitor: HMONITOR, extra: i32) {
        if extra <= 0 {
            self.remove();
            return;
        }
        let key = (monitor.0 as isize, extra);
        if self.reserved == Some(key) {
            return;
        }
        let Some(hwnd) = self.window() else { return };
        unsafe {
            let mut data = APPBARDATA { cbSize: size_of::<APPBARDATA>() as u32, hWnd: hwnd, uCallbackMessage: WM_APP_APPBAR, ..Default::default() };
            if self.reserved.is_none() && SHAppBarMessage(ABM_NEW, &mut data) == 0 {
                log("no se pudo registrar la AppBar");
                return;
            }
            let mut mi = MONITORINFO { cbSize: size_of::<MONITORINFO>() as u32, ..Default::default() };
            if !GetMonitorInfoW(monitor, &mut mi).as_bool() {
                return;
            }
            let m = mi.rcMonitor;
            data.uEdge = ABE_BOTTOM;
            data.rc = RECT { left: m.left, top: m.bottom - extra, right: m.right, bottom: m.bottom };
            // El sistema la sube hasta quedar encima de las AppBar que ya hay (la de Explorer).
            SHAppBarMessage(ABM_QUERYPOS, &mut data);
            data.rc.top = data.rc.bottom - extra;
            SHAppBarMessage(ABM_SETPOS, &mut data);
            log(&format!("AppBar: {extra} px reservados encima de la franja ({:?})", data.rc));
        }
        self.reserved = Some(key);
    }

    pub fn remove(&mut self) {
        if self.reserved.take().is_some() {
            if let Some(hwnd) = self.hwnd {
                let mut data = APPBARDATA { cbSize: size_of::<APPBARDATA>() as u32, hWnd: hwnd, ..Default::default() };
                unsafe { SHAppBarMessage(ABM_REMOVE, &mut data) };
                log("AppBar liberada");
            }
        }
    }

    /// Ventana oculta de la misma clase que la barra (sus avisos llegan a `bar::wnd_proc`).
    fn window(&mut self) -> Option<HWND> {
        if self.hwnd.is_none() {
            let hwnd = unsafe {
                let instance: HINSTANCE = GetModuleHandleW(None).ok()?.into();
                CreateWindowExW(WS_EX_TOOLWINDOW, crate::bar::CLASS, w!("OpenWinBox Taskbar AppBar"), WS_POPUP, 0, 0, 0, 0, None, None, Some(instance), None)
            };
            match hwnd {
                Ok(h) => self.hwnd = Some(h),
                Err(e) => log(&format!("no se pudo crear la ventana de la AppBar: {e}")),
            }
        }
        self.hwnd
    }
}

impl Drop for AppBar {
    fn drop(&mut self) {
        self.remove();
        if let Some(hwnd) = self.hwnd.take() {
            unsafe {
                let _ = DestroyWindow(hwnd);
            }
        }
    }
}
