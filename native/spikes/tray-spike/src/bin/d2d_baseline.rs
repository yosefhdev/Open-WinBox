//! Fase 0, prueba 5: coste base de la pila de render que usará la barra real
//! (D3D11 + Direct2D + DirectWrite + DirectComposition + acrílico), para medir memoria y CPU
//! en reposo. Dibuja una tira de 48 px con texto una vez y se queda quieta N segundos.

use std::mem::size_of;

use windows::Win32::Foundation::*;
use windows::Win32::Graphics::Direct2D::Common::*;
use windows::Win32::Graphics::Direct2D::*;
use windows::Win32::Graphics::Direct3D::*;
use windows::Win32::Graphics::Direct3D11::*;
use windows::Win32::Graphics::DirectComposition::*;
use windows::Win32::Graphics::DirectWrite::*;
use windows::Win32::Graphics::Dxgi::Common::*;
use windows::Win32::Graphics::Dxgi::*;
use windows::Win32::System::LibraryLoader::{GetModuleHandleW, GetProcAddress};
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::core::*;

const H: i32 = 48;
const W: i32 = 900;

#[repr(C)]
struct AccentPolicy {
    state: u32,
    flags: u32,
    gradient: u32,
    animation: u32,
}
#[repr(C)]
struct CompositionData {
    attribute: u32,
    data: *mut std::ffi::c_void,
    size: usize,
}

fn acrylic(hwnd: HWND) {
    unsafe {
        let user32 = GetModuleHandleW(w!("user32.dll")).unwrap();
        let f: unsafe extern "system" fn(HWND, *mut CompositionData) -> BOOL =
            std::mem::transmute(GetProcAddress(user32, s!("SetWindowCompositionAttribute")).unwrap());
        let mut a = AccentPolicy { state: 4, flags: 2, gradient: 0x99202020, animation: 0 };
        let mut d = CompositionData { attribute: 19, data: &mut a as *mut _ as *mut _, size: size_of::<AccentPolicy>() };
        f(hwnd, &mut d);
    }
}

unsafe extern "system" fn wnd_proc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    unsafe {
        match msg {
            WM_TIMER => {
                PostQuitMessage(0);
                LRESULT(0)
            }
            _ => DefWindowProcW(hwnd, msg, wparam, lparam),
        }
    }
}

fn main() -> Result<()> {
    let seconds: u32 = std::env::args().nth(1).and_then(|a| a.parse().ok()).unwrap_or(90);
    unsafe {
        let instance: HINSTANCE = GetModuleHandleW(None)?.into();
        let wc = WNDCLASSEXW {
            cbSize: size_of::<WNDCLASSEXW>() as u32,
            lpfnWndProc: Some(wnd_proc),
            hInstance: instance,
            lpszClassName: w!("D2DBaseline"),
            ..Default::default()
        };
        RegisterClassExW(&wc);
        let mut work = RECT::default();
        let _ = SystemParametersInfoW(SPI_GETWORKAREA, 0, Some(&mut work as *mut _ as *mut _), SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS(0));
        let hwnd = CreateWindowExW(
            WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_NOREDIRECTIONBITMAP,
            w!("D2DBaseline"),
            None,
            WS_POPUP | WS_VISIBLE,
            (work.left + work.right - W) / 2,
            work.bottom - H - 24,
            W,
            H,
            None,
            None,
            Some(instance),
            None,
        )?;
        acrylic(hwnd);

        // D3D11 → DXGI → D2D device context.
        let mut d3d: Option<ID3D11Device> = None;
        D3D11CreateDevice(
            None,
            D3D_DRIVER_TYPE_HARDWARE,
            HMODULE::default(),
            D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            None,
            D3D11_SDK_VERSION,
            Some(&mut d3d),
            None,
            None,
        )?;
        let dxgi: IDXGIDevice = d3d.unwrap().cast()?;
        let d2d_factory: ID2D1Factory1 = D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, None)?;
        let d2d_device = d2d_factory.CreateDevice(&dxgi)?;
        let dc = d2d_device.CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE)?;

        // DirectComposition: superficie con alfa premultiplicado sobre la ventana (sin bitmap de redirección).
        let dcomp: IDCompositionDevice = DCompositionCreateDevice(&dxgi)?;
        let target = dcomp.CreateTargetForHwnd(hwnd, true)?;
        let visual = dcomp.CreateVisual()?;
        let surface = dcomp.CreateSurface(W as u32, H as u32, DXGI_FORMAT_B8G8R8A8_UNORM, DXGI_ALPHA_MODE_PREMULTIPLIED)?;
        let mut offset = POINT::default();
        let dxgi_surface: IDXGISurface = surface.BeginDraw(None, &mut offset)?;
        let props = D2D1_BITMAP_PROPERTIES1 {
            pixelFormat: D2D1_PIXEL_FORMAT { format: DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode: D2D1_ALPHA_MODE_PREMULTIPLIED },
            dpiX: 96.0,
            dpiY: 96.0,
            bitmapOptions: D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
            ..Default::default()
        };
        let bitmap = dc.CreateBitmapFromDxgiSurface(&dxgi_surface, Some(&props))?;
        dc.SetTarget(&bitmap);
        dc.SetTransform(&windows_numerics::Matrix3x2::translation(offset.x as f32, offset.y as f32));

        let dwrite: IDWriteFactory = DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED)?;
        let format = dwrite.CreateTextFormat(w!("Segoe UI Variable Text"), None, DWRITE_FONT_WEIGHT_NORMAL, DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH_NORMAL, 14.0, w!("es-ES"))?;
        let white = dc.CreateSolidColorBrush(&D2D1_COLOR_F { r: 1.0, g: 1.0, b: 1.0, a: 1.0 }, None)?;

        dc.BeginDraw();
        dc.Clear(Some(&D2D1_COLOR_F { r: 0.0, g: 0.0, b: 0.0, a: 0.0 }));
        let text: Vec<u16> = "Barra de prueba · D2D + DirectComposition + acrílico".encode_utf16().collect();
        dc.DrawText(
            &text,
            &format,
            &D2D_RECT_F { left: 16.0, top: 13.0, right: W as f32, bottom: H as f32 },
            &white,
            D2D1_DRAW_TEXT_OPTIONS_NONE,
            DWRITE_MEASURING_MODE_NATURAL,
        );
        dc.EndDraw(None, None)?;
        dc.SetTarget(None);
        surface.EndDraw()?;
        visual.SetContent(&surface)?;
        target.SetRoot(&visual)?;
        dcomp.Commit()?;
        println!("Dibujado. pid {} quieto {seconds} s.", std::process::id());

        SetTimer(Some(hwnd), 1, seconds * 1000, None);
        let mut msg = MSG::default();
        while GetMessageW(&mut msg, None, 0, 0).as_bool() {
            DispatchMessageW(&msg);
        }
    }
    Ok(())
}
