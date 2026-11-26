//! Pila de render: D3D11 WARP → Direct2D → superficie DirectComposition con alfa premultiplicado.
//! Se dibuja en píxeles físicos (DPI del contexto = 96); el escalado lo hace quien dibuja.

use windows::Win32::Foundation::*;
use windows::Win32::Graphics::Direct2D::Common::*;
use windows::Win32::Graphics::Direct2D::*;
use windows::Win32::Graphics::Direct3D::*;
use windows::Win32::Graphics::Direct3D11::*;
use windows::Win32::Graphics::DirectComposition::*;
use windows::Win32::Graphics::DirectWrite::*;
use windows::Win32::Graphics::Dxgi::Common::*;
use windows::Win32::Graphics::Dxgi::*;
use windows::Win32::Graphics::Gdi::{DeleteObject, HPALETTE};
use windows::Win32::Graphics::Imaging::*;
use windows::Win32::UI::Shell::*;
use windows::Win32::System::Com::*;
use windows::Win32::UI::WindowsAndMessaging::HICON;
use windows::core::*;

pub struct Renderer {
    pub dc: ID2D1DeviceContext,
    pub dwrite: IDWriteFactory,
    wic: IWICImagingFactory,
    dcomp: IDCompositionDevice,
    _target: IDCompositionTarget,
    visual: IDCompositionVisual,
    surface: Option<(IDCompositionSurface, u32, u32)>,
    _d3d: ID3D11Device,
}

impl Renderer {
    pub fn new(hwnd: HWND) -> Result<Self> {
        unsafe {
            let mut d3d = None;
            D3D11CreateDevice(
                None,
                D3D_DRIVER_TYPE_WARP,
                HMODULE::default(),
                D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                None,
                D3D11_SDK_VERSION,
                Some(&mut d3d),
                None,
                None,
            )?;
            let d3d = d3d.unwrap();
            let dxgi: IDXGIDevice = d3d.cast()?;
            let factory: ID2D1Factory1 = D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, None)?;
            let dc = factory.CreateDevice(&dxgi)?.CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE)?;
            let dcomp: IDCompositionDevice = DCompositionCreateDevice(&dxgi)?;
            let target = dcomp.CreateTargetForHwnd(hwnd, true)?;
            let visual = dcomp.CreateVisual()?;
            target.SetRoot(&visual)?;
            let wic = CoCreateInstance(&CLSID_WICImagingFactory, None, CLSCTX_INPROC_SERVER)?;
            let dwrite = DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED)?;
            Ok(Self { dc, dwrite, wic, dcomp, _target: target, visual, surface: None, _d3d: d3d })
        }
    }

    /// Redibuja la superficie entera (`w`×`h` px). La recrea si cambió el tamaño.
    pub fn draw(&mut self, w: u32, h: u32, paint: impl FnOnce(&ID2D1DeviceContext)) -> Result<()> {
        if w == 0 || h == 0 {
            return Ok(());
        }
        unsafe {
            if !matches!(self.surface, Some((_, sw, sh)) if sw == w && sh == h) {
                let s = self.dcomp.CreateSurface(w, h, DXGI_FORMAT_B8G8R8A8_UNORM, DXGI_ALPHA_MODE_PREMULTIPLIED)?;
                self.visual.SetContent(&s)?;
                self.surface = Some((s, w, h));
            }
            let surface = &self.surface.as_ref().unwrap().0;
            let mut offset = POINT::default();
            let dxgi_surface: IDXGISurface = surface.BeginDraw(None, &mut offset)?;
            let props = D2D1_BITMAP_PROPERTIES1 {
                pixelFormat: D2D1_PIXEL_FORMAT { format: DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode: D2D1_ALPHA_MODE_PREMULTIPLIED },
                dpiX: 96.0,
                dpiY: 96.0,
                bitmapOptions: D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
                ..Default::default()
            };
            let bitmap = self.dc.CreateBitmapFromDxgiSurface(&dxgi_surface, Some(&props))?;
            self.dc.SetTarget(&bitmap);
            self.dc.SetTransform(&windows_numerics::Matrix3x2::translation(offset.x as f32, offset.y as f32));
            self.dc.BeginDraw();
            self.dc.Clear(Some(&D2D1_COLOR_F::default()));
            paint(&self.dc);
            let result = self.dc.EndDraw(None, None);
            self.dc.SetTarget(None);
            surface.EndDraw()?;
            result?;
            self.dcomp.Commit()
        }
    }

    pub fn bitmap_from_icon(&self, icon: HICON) -> Result<ID2D1Bitmap1> {
        unsafe {
            let source = self.wic.CreateBitmapFromHICON(icon)?;
            let converter = self.wic.CreateFormatConverter()?;
            converter.Initialize(&source, &GUID_WICPixelFormat32bppPBGRA, WICBitmapDitherTypeNone, None, 0.0, WICBitmapPaletteTypeMedianCut)?;
            self.dc.CreateBitmapFromWicBitmap(&converter, None)
        }
    }

    /// Icono de un elemento del shell (`.lnk`, exe o `shell:AppsFolder\<AUMID>`) a `size` px.
    /// Sirve también para apps de la Store, cuyas ventanas no dan icono propio.
    pub fn bitmap_from_shell(&self, parsing_name: &str, size: i32) -> Result<ID2D1Bitmap1> {
        unsafe {
            let wide: Vec<u16> = parsing_name.encode_utf16().chain([0]).collect();
            let item: IShellItem = SHCreateItemFromParsingName(PCWSTR(wide.as_ptr()), None)?;
            let factory: IShellItemImageFactory = item.cast()?;
            let hbitmap = factory.GetImage(SIZE { cx: size, cy: size }, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK)?;
            let source = self.wic.CreateBitmapFromHBITMAP(hbitmap, HPALETTE::default(), WICBitmapUsePremultipliedAlpha);
            let _ = DeleteObject(hbitmap.into());
            let converter = self.wic.CreateFormatConverter()?;
            converter.Initialize(&source?, &GUID_WICPixelFormat32bppPBGRA, WICBitmapDitherTypeNone, None, 0.0, WICBitmapPaletteTypeMedianCut)?;
            self.dc.CreateBitmapFromWicBitmap(&converter, None)
        }
    }

    pub fn text_format(&self, family: PCWSTR, size: f32, weight: DWRITE_FONT_WEIGHT) -> Result<IDWriteTextFormat> {
        unsafe { self.dwrite.CreateTextFormat(family, None, weight, DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH_NORMAL, size, w!("")) }
    }
}

pub fn rgba(r: u8, g: u8, b: u8, a: f32) -> D2D1_COLOR_F {
    D2D1_COLOR_F { r: r as f32 / 255.0, g: g as f32 / 255.0, b: b as f32 / 255.0, a }
}

pub fn rect(l: f32, t: f32, r: f32, b: f32) -> D2D_RECT_F {
    D2D_RECT_F { left: l, top: t, right: r, bottom: b }
}
