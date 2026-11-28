//! Fase 0, prueba 4: fondo Mica/acrílico en ventanas tipo barra
//! (`WS_POPUP`, `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`, nunca activas).
//!
//! Muestra 8 tiras en el centro del monitor principal, numeradas con cuadritos blancos:
//!   fila 1: 1 Mica, 2 Acrílico, 3 Mica Alt (DWMWA_SYSTEMBACKDROP_TYPE), tal cual.
//!   fila 2: 4 Mica, 5 Acrílico, 6 Mica Alt, fingiendo estar activas (WM_NCACTIVATE siempre TRUE).
//!   fila 3: 7 acrílico y 8 desenfoque por SetWindowCompositionAttribute (no documentada,
//!           la que usan TranslucentTB y similares).
//! Sale sola tras N segundos (argumento, por defecto 120).

use std::ffi::c_void;
use std::mem::size_of;

use windows::Win32::Foundation::*;
use windows::Win32::Graphics::Dwm::*;
use windows::Win32::Graphics::Gdi::*;
use windows::Win32::System::LibraryLoader::{GetModuleHandleW, GetProcAddress};
use windows::Win32::UI::Controls::MARGINS;
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::core::*;

const W: i32 = 360;
const H: i32 = 48;
const GAP: i32 = 16;
const TIMER_QUIT: usize = 1;
const FAKE_ACTIVE_PROP: PCWSTR = w!("FakeActive");
const LABEL_PROP: PCWSTR = w!("Label");

#[repr(C)]
struct AccentPolicy {
    state: u32,
    flags: u32,
    gradient: u32, // AABBGGRR
    animation: u32,
}

#[repr(C)]
struct CompositionData {
    attribute: u32, // 19 = WCA_ACCENT_POLICY
    data: *mut c_void,
    size: usize,
}

type SetWca = unsafe extern "system" fn(HWND, *mut CompositionData) -> BOOL;

fn set_accent(hwnd: HWND, state: u32, gradient: u32) -> bool {
    unsafe {
        let Ok(user32) = GetModuleHandleW(w!("user32.dll")) else { return false };
        let Some(proc) = GetProcAddress(user32, s!("SetWindowCompositionAttribute")) else { return false };
        let f: SetWca = std::mem::transmute(proc);
        let mut accent = AccentPolicy { state, flags: 2, gradient, animation: 0 };
        let mut data = CompositionData { attribute: 19, data: &mut accent as *mut _ as *mut c_void, size: size_of::<AccentPolicy>() };
        f(hwnd, &mut data).as_bool()
    }
}

fn dwm_set(hwnd: HWND, attr: DWMWINDOWATTRIBUTE, value: i32) -> HRESULT {
    unsafe {
        match DwmSetWindowAttribute(hwnd, attr, &value as *const i32 as *const c_void, size_of::<i32>() as u32) {
            Ok(()) => S_OK,
            Err(e) => e.code(),
        }
    }
}

fn main() -> Result<()> {
    let seconds: u32 = std::env::args().nth(1).and_then(|a| a.parse().ok()).unwrap_or(120);
    unsafe {
        let instance: HINSTANCE = GetModuleHandleW(None)?.into();
        let wc = WNDCLASSEXW {
            cbSize: size_of::<WNDCLASSEXW>() as u32,
            lpfnWndProc: Some(wnd_proc),
            hInstance: instance,
            lpszClassName: w!("BackdropSpike"),
            // Negro = transparente sobre el marco extendido de DWM: deja ver el fondo del sistema.
            hbrBackground: HBRUSH(GetStockObject(BLACK_BRUSH).0),
            ..Default::default()
        };
        RegisterClassExW(&wc);

        let mut work = RECT::default();
        let _ = SystemParametersInfoW(SPI_GETWORKAREA, 0, Some(&mut work as *mut _ as *mut c_void), SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS(0));
        let cx = (work.left + work.right) / 2;
        let top = (work.top + work.bottom) / 2 - (3 * H + 2 * GAP) / 2;

        let variants: [(&str, i32, i32, bool); 8] = [
            ("1 Mica", 0, 0, false),
            ("2 Acrílico", 1, 0, false),
            ("3 Mica Alt", 2, 0, false),
            ("4 Mica + activa fingida", 0, 1, true),
            ("5 Acrílico + activa fingida", 1, 1, true),
            ("6 Mica Alt + activa fingida", 2, 1, true),
            ("7 acrílico (SetWindowCompositionAttribute)", 0, 2, false),
            ("8 desenfoque (SetWindowCompositionAttribute)", 1, 2, false),
        ];
        let mut first = HWND::default();
        for (i, (name, col, row, fake_active)) in variants.iter().enumerate() {
            let x = cx - (3 * W + 2 * GAP) / 2 + col * (W + GAP);
            let y = top + row * (H + GAP);
            let hwnd = CreateWindowExW(
                WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST,
                w!("BackdropSpike"),
                None,
                WS_POPUP,
                x,
                y,
                W,
                H,
                None,
                None,
                Some(instance),
                None,
            )?;
            if first.is_invalid() {
                first = hwnd;
            }
            let _ = SetPropW(hwnd, LABEL_PROP, Some(HANDLE((i + 1) as *mut c_void)));
            if *fake_active {
                let _ = SetPropW(hwnd, FAKE_ACTIVE_PROP, Some(HANDLE(1 as *mut c_void)));
            }
            let dark = dwm_set(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, 1);
            let _ = dwm_set(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_DONOTROUND.0);
            let result = if i < 6 {
                let margins = MARGINS { cxLeftWidth: -1, cxRightWidth: -1, cyTopHeight: -1, cyBottomHeight: -1 };
                let ext = DwmExtendFrameIntoClientArea(hwnd, &margins);
                // 2 = DWMSBT_MAINWINDOW (Mica), 3 = TRANSIENTWINDOW (acrílico), 4 = TABBEDWINDOW (Mica Alt)
                let kind = [2, 3, 4][*col as usize];
                let hr = dwm_set(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, kind);
                format!("extend={:?} backdrop=0x{:X}", ext.is_ok(), hr.0)
            } else {
                // 4 = ACCENT_ENABLE_ACRYLICBLURBEHIND, 3 = ACCENT_ENABLE_BLURBEHIND
                let ok = set_accent(hwnd, if i == 6 { 4 } else { 3 }, 0x99202020);
                format!("SetWindowCompositionAttribute={ok}")
            };
            println!("{name}: darkmode=0x{:X} {result}", dark.0);
            let _ = ShowWindow(hwnd, SW_SHOWNOACTIVATE);
        }
        SetTimer(Some(first), TIMER_QUIT, seconds * 1000, None);
        println!("Mostrando {seconds} s.");

        let mut msg = MSG::default();
        while GetMessageW(&mut msg, None, 0, 0).as_bool() {
            let _ = TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
    }
    Ok(())
}

unsafe extern "system" fn wnd_proc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    unsafe {
        match msg {
            // La barra nunca está activa; DWM apaga Mica/acrílico en ventanas inactivas.
            // Truco: decirle a DefWindowProc que seguimos activas.
            WM_NCACTIVATE if !GetPropW(hwnd, FAKE_ACTIVE_PROP).is_invalid() => DefWindowProcW(hwnd, msg, WPARAM(1), lparam),
            WM_PAINT => {
                let mut ps = PAINTSTRUCT::default();
                let hdc = BeginPaint(hwnd, &mut ps);
                let n = GetPropW(hwnd, LABEL_PROP).0 as i32;
                let white = GetStockObject(WHITE_BRUSH);
                for k in 0..n {
                    let r = RECT { left: 12 + k * 14, top: 18, right: 22 + k * 14, bottom: 28 };
                    FillRect(hdc, &r, HBRUSH(white.0));
                }
                let _ = EndPaint(hwnd, &ps);
                LRESULT(0)
            }
            WM_TIMER if wparam.0 == TIMER_QUIT => {
                PostQuitMessage(0);
                LRESULT(0)
            }
            _ => DefWindowProcW(hwnd, msg, wparam, lparam),
        }
    }
}
