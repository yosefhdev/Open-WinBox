//! Desbordamiento de la bandeja (el "^"): ventana emergente con los iconos que el usuario no tiene
//! visibles en la barra. Se cierra al perder el foco, como la de Windows; se crea al abrir.

use std::cell::RefCell;
use std::collections::HashMap;

use windows::Win32::Foundation::*;
use windows::Win32::Graphics::Direct2D::Common::*;
use windows::Win32::Graphics::Direct2D::*;
use windows::Win32::Graphics::Gdi::*;
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::UI::HiDpi::GetDpiForWindow;
use windows::Win32::UI::Input::KeyboardAndMouse::{ReleaseCapture, SetCapture};
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::core::*;

use crate::render::{Renderer, rect, rgba};
use crate::{log, tray};

const CLASS: PCWSTR = w!("OpenWinBoxTrayFlyout");
const MAX_COLUMNS: usize = 5;

struct Flyout {
    hwnd: HWND,
    renderer: Renderer,
    icons: Vec<(u32, HICON)>,
    bitmaps: HashMap<isize, Option<ID2D1Bitmap1>>,
    hover: Option<usize>,
    /// Borde superior de la barra y centro del "^" (pantalla) para recolocar si cambia el número de iconos.
    anchor: (i32, i32),
    /// Icono presionado (id, x, y de inicio, arrastre activo): soltarlo sobre la barra lo hace visible.
    drag: Option<(u32, i32, i32, bool)>,
}

thread_local! {
    static FLYOUT: RefCell<Option<Flyout>> = const { RefCell::new(None) };
}

pub fn is_open() -> bool {
    FLYOUT.with_borrow(|f| f.is_some())
}

/// El punto (pantalla) cae dentro del desbordamiento abierto.
pub fn contains(pt: POINT) -> bool {
    FLYOUT.with_borrow(|f| {
        f.as_ref().is_some_and(|f| {
            let mut r = RECT::default();
            unsafe { GetWindowRect(f.hwnd, &mut r).is_ok() && PtInRect(&r, pt).as_bool() }
        })
    })
}

/// Abre el desbordamiento encima de `bar_top`, centrado en `center_x`, o lo cierra si ya está abierto.
pub fn toggle(center_x: i32, bar_top: i32) {
    if is_open() {
        close();
        return;
    }
    unsafe {
        let Ok(module) = GetModuleHandleW(None) else { return };
        let instance: HINSTANCE = module.into();
        let wc = WNDCLASSEXW {
            cbSize: size_of::<WNDCLASSEXW>() as u32,
            lpfnWndProc: Some(flyout_proc),
            hInstance: instance,
            hCursor: LoadCursorW(None, IDC_ARROW).unwrap_or_default(),
            lpszClassName: CLASS,
            ..Default::default()
        };
        RegisterClassExW(&wc);
        let Ok(hwnd) = CreateWindowExW(
            WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOREDIRECTIONBITMAP,
            CLASS,
            w!("OpenWinBox Tray Overflow"),
            WS_POPUP,
            center_x,
            bar_top,
            1,
            1,
            None,
            None,
            Some(instance),
            None,
        ) else {
            return;
        };
        crate::bar::apply_background(hwnd);
        let renderer = match Renderer::new(hwnd) {
            Ok(r) => r,
            Err(e) => {
                log(&format!("desbordamiento: sin render: {e}"));
                let _ = DestroyWindow(hwnd);
                return;
            }
        };
        FLYOUT.with_borrow_mut(|f| {
            *f = Some(Flyout { hwnd, renderer, icons: Vec::new(), bitmaps: HashMap::new(), hover: None, anchor: (center_x, bar_top), drag: None });
        });
        refresh();
        let _ = ShowWindow(hwnd, SW_SHOW);
        let _ = SetForegroundWindow(hwnd);
    }
}

pub fn close() {
    if let Some(f) = FLYOUT.with_borrow_mut(|f| f.take()) {
        let hwnd = f.hwnd;
        drop(f);
        unsafe {
            let _ = DestroyWindow(hwnd);
        }
        crate::bar::tray_changed();
    }
}

/// La bandeja cambió (o se acaba de abrir): iconos, tamaño, posición y dibujo.
pub fn refresh() {
    let hidden: Vec<(u32, HICON)> = tray::visible().into_iter().filter(|(_, _, promoted)| !promoted).map(|(id, icon, _)| (id, icon)).collect();
    if hidden.is_empty() {
        close();
        return;
    }
    FLYOUT.with_borrow_mut(|f| {
        let Some(f) = f.as_mut() else { return };
        f.icons = hidden;
        let (cell, pad, s) = f.metrics();
        let cols = f.icons.len().min(MAX_COLUMNS);
        let rows = f.icons.len().div_ceil(MAX_COLUMNS);
        let (w, h) = (cols as i32 * cell + pad * 2, rows as i32 * cell + pad * 2);
        let x = f.anchor.0 - w / 2;
        let y = f.anchor.1 - h - (8.0 * s) as i32;
        unsafe {
            let _ = SetWindowPos(f.hwnd, Some(HWND_TOPMOST), x, y, w, h, SWP_NOACTIVATE);
            let r = (8.0 * s) as i32;
            SetWindowRgn(f.hwnd, Some(CreateRoundRectRgn(0, 0, w + 1, h + 1, r * 2, r * 2)), true);
        }
        f.draw(w, h);
    });
}

impl Flyout {
    /// (celda, margen, escala) en píxeles físicos.
    fn metrics(&self) -> (i32, i32, f32) {
        let s = unsafe { GetDpiForWindow(self.hwnd) } as f32 / 96.0;
        ((40.0 * s) as i32, (8.0 * s) as i32, s)
    }

    fn hit(&self, lparam: LPARAM) -> Option<usize> {
        let (cell, pad, _) = self.metrics();
        let (x, y) = ((lparam.0 & 0xFFFF) as i16 as i32 - pad, ((lparam.0 >> 16) & 0xFFFF) as i16 as i32 - pad);
        if x < 0 || y < 0 {
            return None;
        }
        let (col, row) = ((x / cell) as usize, (y / cell) as usize);
        let i = row * MAX_COLUMNS + col;
        (col < MAX_COLUMNS && i < self.icons.len()).then_some(i)
    }

    fn draw(&mut self, w: i32, h: i32) {
        let (cell, pad, s) = self.metrics();
        let wanted: Vec<isize> = self.icons.iter().map(|(_, i)| i.0 as isize).collect();
        self.bitmaps.retain(|k, _| wanted.contains(k));
        for (_, icon) in &self.icons {
            let key = icon.0 as isize;
            if !self.bitmaps.contains_key(&key) {
                let b = self.renderer.bitmap_from_icon(*icon).ok();
                self.bitmaps.insert(key, b);
            }
        }
        let dark = crate::bar::config(|c| c.dark_foreground());
        let ink = move |a: f32| if dark { rgba(0, 0, 0, a) } else { rgba(255, 255, 255, a) };
        let Self { renderer, icons, bitmaps, hover, .. } = self;
        let _ = renderer.draw(w as u32, h as u32, |dc| unsafe {
            let brush = |c: D2D1_COLOR_F| dc.CreateSolidColorBrush(&c, None).unwrap();
            let r = 8.0 * s;
            let edge = D2D1_ROUNDED_RECT { rect: rect(0.5, 0.5, w as f32 - 0.5, h as f32 - 0.5), radiusX: r, radiusY: r };
            dc.DrawRoundedRectangle(&edge, &brush(ink(0.18)), 1.0, None);
            let icon = 16.0 * s;
            for (i, (_, hicon)) in icons.iter().enumerate() {
                let (col, row) = ((i % MAX_COLUMNS) as f32, (i / MAX_COLUMNS) as f32);
                let (x, y) = (pad as f32 + col * cell as f32, pad as f32 + row * cell as f32);
                if *hover == Some(i) {
                    let bg = rect(x + 2.0 * s, y + 2.0 * s, x + cell as f32 - 2.0 * s, y + cell as f32 - 2.0 * s);
                    dc.FillRoundedRectangle(&D2D1_ROUNDED_RECT { rect: bg, radiusX: 4.0 * s, radiusY: 4.0 * s }, &brush(ink(0.1)));
                }
                let (cx, cy) = (x + cell as f32 / 2.0, y + cell as f32 / 2.0);
                if let Some(Some(b)) = bitmaps.get(&(hicon.0 as isize)) {
                    let dst = rect(cx - icon / 2.0, cy - icon / 2.0, cx + icon / 2.0, cy + icon / 2.0);
                    dc.DrawBitmap(b, Some(&dst), 1.0, D2D1_INTERPOLATION_MODE_HIGH_QUALITY_CUBIC, None, None);
                }
            }
        });
    }
}

unsafe extern "system" fn flyout_proc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    unsafe {
        match msg {
            // Al perder el foco (clic fuera, la app abrió su ventana o menú) se cierra. Diferido: no
            // destruir la ventana dentro de su propia activación.
            WM_ACTIVATE if (wparam.0 & 0xFFFF) as u32 == WA_INACTIVE => {
                let _ = PostMessageW(Some(hwnd), WM_CLOSE, WPARAM(0), LPARAM(0));
                LRESULT(0)
            }
            WM_CLOSE => {
                close();
                LRESULT(0)
            }
            WM_LBUTTONDOWN => {
                FLYOUT.with_borrow_mut(|f| {
                    if let Some(f) = f.as_mut() {
                        if let Some(i) = f.hit(lparam) {
                            f.drag = Some((f.icons[i].0, (lparam.0 & 0xFFFF) as i16 as i32, ((lparam.0 >> 16) & 0xFFFF) as i16 as i32, false));
                            SetCapture(hwnd);
                        }
                    }
                });
                LRESULT(0)
            }
            WM_MOUSEMOVE => {
                FLYOUT.with_borrow_mut(|f| {
                    if let Some(f) = f.as_mut() {
                        if let Some((id, sx, sy, active)) = f.drag.as_mut() {
                            let (x, y) = ((lparam.0 & 0xFFFF) as i16 as i32, ((lparam.0 >> 16) & 0xFFFF) as i16 as i32);
                            if !*active && ((x - *sx).abs() > GetSystemMetrics(SM_CXDRAG) || (y - *sy).abs() > GetSystemMetrics(SM_CYDRAG)) {
                                *active = true;
                            }
                            if *active {
                                // El cursor es el propio icono mientras se arrastra.
                                if let Some(icon) = tray::icon_of(*id) {
                                    SetCursor(Some(HCURSOR(icon.0)));
                                }
                                return;
                            }
                        }
                        let hit = f.hit(lparam);
                        if hit != f.hover {
                            f.hover = hit;
                            let mut r = RECT::default();
                            let _ = GetClientRect(f.hwnd, &mut r);
                            f.draw(r.right, r.bottom);
                        }
                    }
                });
                LRESULT(0)
            }
            WM_LBUTTONUP => {
                let drag = FLYOUT.with_borrow_mut(|f| f.as_mut().and_then(|f| f.drag.take()));
                if drag.is_some() {
                    let _ = ReleaseCapture();
                }
                match drag {
                    Some((id, _, _, true)) => {
                        // Soltado sobre la barra principal: pasa a estar visible en ella.
                        let mut pt = POINT::default();
                        let mut bar = RECT::default();
                        let _ = GetCursorPos(&mut pt);
                        if GetWindowRect(crate::main_window(), &mut bar).is_ok() && PtInRect(&bar, pt).as_bool() {
                            tray::set_promoted(id, true);
                        }
                    }
                    Some((id, _, _, false)) => tray::click(id, false),
                    None => {}
                }
                LRESULT(0)
            }
            WM_RBUTTONUP => {
                let id = FLYOUT.with_borrow(|f| f.as_ref().and_then(|f| f.hit(lparam).map(|i| f.icons[i].0)));
                if let Some(id) = id {
                    tray::click(id, true);
                }
                LRESULT(0)
            }
            WM_CAPTURECHANGED => {
                FLYOUT.with_borrow_mut(|f| {
                    if let Some(f) = f.as_mut() {
                        f.drag = None;
                    }
                });
                LRESULT(0)
            }
            _ => DefWindowProcW(hwnd, msg, wparam, lparam),
        }
    }
}
