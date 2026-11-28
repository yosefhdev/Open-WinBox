//! Fase 0, prueba 5b: ¿de dónde salen los ~50 MB de la pila gráfica y hay algo más ligero?
//!
//! `stack_probe <modo> [segundos]`, todas con una tira de 48 px y acrílico:
//!   gdi     → solo la ventana con acrílico, sin pila gráfica (referencia)
//!   d3d     → + dispositivo D3D11 de hardware, sin dibujar nada (aísla el coste del dispositivo)
//!   hw      → D3D11 hardware + D2D + DWrite + DComp, texto dibujado (la pila elegida)
//!   warp    → igual que hw pero con D3D11 WARP (render por software)
//!   layered → sin D3D: D2D por software sobre un DIB + UpdateLayeredWindow (alfa por píxel)

use std::ffi::c_void;
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
use windows::Win32::Graphics::Gdi::*;
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
    data: *mut c_void,
    size: usize,
}

fn acrylic(hwnd: HWND) {
    unsafe {
        let user32 = GetModuleHandleW(w!("user32.dll")).unwrap();
        let f: unsafe extern "system" fn(HWND, *mut CompositionData) -> BOOL =
            std::mem::transmute(GetProcAddress(user32, s!("SetWindowCompositionAttribute")).unwrap());
        let mut a = AccentPolicy { state: 4, flags: 2, gradient: 0x99202020, animation: 0 };
        let mut d = CompositionData { attribute: 19, data: &mut a as *mut _ as *mut c_void, size: size_of::<AccentPolicy>() };
        let _ = f(hwnd, &mut d);
    }
}

unsafe extern "system" fn wnd_proc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    unsafe {
        if msg == WM_TIMER {
            PostQuitMessage(0);
            return LRESULT(0);
        }
        DefWindowProcW(hwnd, msg, wparam, lparam)
    }
}

const LABEL: &str = "Barra de prueba · texto con DirectWrite";

fn text_format() -> Result<IDWriteTextFormat> {
    unsafe {
        let dwrite: IDWriteFactory = DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED)?;
        dwrite.CreateTextFormat(w!("Segoe UI Variable Text"), None, DWRITE_FONT_WEIGHT_NORMAL, DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH_NORMAL, 14.0, w!("es-ES"))
    }
}

fn draw_label(rt: &ID2D1RenderTarget) -> Result<()> {
    unsafe {
        let format = text_format()?;
        let white = rt.CreateSolidColorBrush(&D2D1_COLOR_F { r: 1.0, g: 1.0, b: 1.0, a: 1.0 }, None)?;
        let text: Vec<u16> = LABEL.encode_utf16().collect();
        rt.DrawText(&text, &format, &D2D_RECT_F { left: 16.0, top: 13.0, right: W as f32, bottom: H as f32 }, &white, D2D1_DRAW_TEXT_OPTIONS_NONE, DWRITE_MEASURING_MODE_NATURAL);
        Ok(())
    }
}

fn d3d_device(warp: bool) -> Result<ID3D11Device> {
    let mut d3d: Option<ID3D11Device> = None;
    unsafe {
        D3D11CreateDevice(
            None,
            if warp { D3D_DRIVER_TYPE_WARP } else { D3D_DRIVER_TYPE_HARDWARE },
            HMODULE::default(),
            D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            None,
            D3D11_SDK_VERSION,
            Some(&mut d3d),
            None,
            None,
        )?;
    }
    Ok(d3d.unwrap())
}

/// D3D11 → D2D → superficie DComp con alfa premultiplicado. Devuelve los objetos a mantener vivos.
fn compose(hwnd: HWND, warp: bool) -> Result<Vec<IUnknown>> {
    unsafe {
        let d3d = d3d_device(warp)?;
        let dxgi: IDXGIDevice = d3d.cast()?;
        let factory: ID2D1Factory1 = D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, None)?;
        let device = factory.CreateDevice(&dxgi)?;
        let dc = device.CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE)?;
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
        dc.BeginDraw();
        dc.Clear(Some(&D2D1_COLOR_F::default()));
        draw_label(&dc.cast()?)?;
        dc.EndDraw(None, None)?;
        dc.SetTarget(None);
        surface.EndDraw()?;
        visual.SetContent(&surface)?;
        target.SetRoot(&visual)?;
        dcomp.Commit()?;
        Ok(vec![d3d.cast()?, factory.cast()?, device.cast()?, dc.cast()?, dcomp.cast()?, target.cast()?, visual.cast()?, surface.cast()?])
    }
}

/// Sin D3D: D2D por software sobre un DIB de 32 bits y `UpdateLayeredWindow` con alfa por píxel.
fn layered(hwnd: HWND, x: i32, y: i32) -> Result<()> {
    unsafe {
        let screen = GetDC(None);
        let mem = CreateCompatibleDC(Some(screen));
        let bmi = BITMAPINFO {
            bmiHeader: BITMAPINFOHEADER {
                biSize: size_of::<BITMAPINFOHEADER>() as u32,
                biWidth: W,
                biHeight: -H, // de arriba abajo
                biPlanes: 1,
                biBitCount: 32,
                ..Default::default()
            },
            ..Default::default()
        };
        let mut bits: *mut c_void = std::ptr::null_mut();
        let dib = CreateDIBSection(Some(mem), &bmi, DIB_RGB_COLORS, &mut bits, None, 0)?;
        let old = SelectObject(mem, dib.into());

        let factory: ID2D1Factory = D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, None)?;
        let props = D2D1_RENDER_TARGET_PROPERTIES {
            r#type: D2D1_RENDER_TARGET_TYPE_SOFTWARE,
            pixelFormat: D2D1_PIXEL_FORMAT { format: DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode: D2D1_ALPHA_MODE_PREMULTIPLIED },
            ..Default::default()
        };
        let rt = factory.CreateDCRenderTarget(&props)?;
        rt.BindDC(mem, &RECT { left: 0, top: 0, right: W, bottom: H })?;
        rt.BeginDraw();
        // Casi transparente (alfa 1/255): mantiene la tira clicable y deja ver el acrílico.
        rt.Clear(Some(&D2D1_COLOR_F { r: 0.0, g: 0.0, b: 0.0, a: 1.0 / 255.0 }));
        rt.SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        draw_label(&rt.cast()?)?;
        rt.EndDraw(None, None)?;

        let blend = BLENDFUNCTION { BlendOp: AC_SRC_OVER as u8, BlendFlags: 0, SourceConstantAlpha: 255, AlphaFormat: AC_SRC_ALPHA as u8 };
        UpdateLayeredWindow(
            hwnd,
            Some(screen),
            Some(&POINT { x, y }),
            Some(&SIZE { cx: W, cy: H }),
            Some(mem),
            Some(&POINT::default()),
            COLORREF(0),
            Some(&blend),
            ULW_ALPHA,
        )?;
        SelectObject(mem, old);
        let _ = DeleteObject(dib.into());
        let _ = DeleteDC(mem);
        ReleaseDC(None, screen);
    }
    Ok(())
}

fn main() -> Result<()> {
    let args: Vec<String> = std::env::args().collect();
    let mode = args.get(1).cloned().unwrap_or_else(|| "hw".into());
    let seconds: u32 = args.get(2).and_then(|a| a.parse().ok()).unwrap_or(40);
    unsafe {
        let instance: HINSTANCE = GetModuleHandleW(None)?.into();
        let wc = WNDCLASSEXW {
            cbSize: size_of::<WNDCLASSEXW>() as u32,
            lpfnWndProc: Some(wnd_proc),
            hInstance: instance,
            lpszClassName: w!("StackProbe"),
            ..Default::default()
        };
        RegisterClassExW(&wc);
        let mut work = RECT::default();
        let _ = SystemParametersInfoW(SPI_GETWORKAREA, 0, Some(&mut work as *mut _ as *mut c_void), SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS(0));
        let lift: i32 = args.get(3).and_then(|a| a.parse().ok()).unwrap_or(0);
        let (x, y) = ((work.left + work.right - W) / 2, work.bottom - H - 24 - lift);
        let ex = WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST
            | match mode.as_str() {
                "layered" => WS_EX_LAYERED,
                "hw" | "warp" => WS_EX_NOREDIRECTIONBITMAP,
                _ => WINDOW_EX_STYLE(0),
            };
        let hwnd = CreateWindowExW(ex, w!("StackProbe"), None, WS_POPUP, x, y, W, H, None, None, Some(instance), None)?;
        acrylic(hwnd);

        let _keep: Vec<IUnknown> = match mode.as_str() {
            "d3d" => vec![d3d_device(false)?.cast()?],
            "hw" => compose(hwnd, false)?,
            "warp" => compose(hwnd, true)?,
            "layered" => {
                layered(hwnd, x, y)?;
                vec![]
            }
            _ => vec![],
        };
        let _ = ShowWindow(hwnd, SW_SHOWNOACTIVATE);
        println!("modo {mode}: listo, pid {}", std::process::id());

        SetTimer(Some(hwnd), 1, seconds * 1000, None);
        let mut msg = MSG::default();
        while GetMessageW(&mut msg, None, 0, 0).as_bool() {
            DispatchMessageW(&msg);
        }
    }
    Ok(())
}
