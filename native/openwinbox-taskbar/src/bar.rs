//! La ventana de la barra: se coloca sobre la franja de la barra nativa (decisión 6),
//! dibuja botones fijos + grupos de tareas + reloj, y atiende ratón y eventos del sistema.

use std::cell::RefCell;
use std::collections::HashMap;
use std::ffi::c_void;
use std::sync::atomic::{AtomicU32, Ordering};

use windows::Win32::Foundation::*;
use windows::Win32::Graphics::Direct2D::Common::*;
use windows::Win32::Graphics::Direct2D::*;
use windows::Win32::Graphics::DirectWrite::*;
use windows::Win32::Graphics::Dwm::DwmGetColorizationColor;
use windows::Win32::Graphics::Gdi::*;
use windows::Win32::Globalization::*;
use windows::Win32::System::LibraryLoader::{GetModuleHandleW, GetProcAddress};
use windows::Win32::UI::Accessibility::*;
use windows::Win32::UI::HiDpi::GetDpiForWindow;
use windows::Win32::UI::Input::KeyboardAndMouse::*;

use windows::Win32::UI::WindowsAndMessaging::*;
use windows::core::*;

use crate::config::Config;
use crate::render::{Renderer, rect, rgba};
use crate::pins::{self, Pin, State};
use crate::tasks::{self, Tasks};
use crate::{log, shell};

const CLASS: PCWSTR = w!("OpenWinBoxTaskbar");
const TIMER_REFRESH: usize = 1;
const TIMER_CLOCK: usize = 2;
const TIMER_EXIT: usize = 3;
const MENU_CLOSE: usize = 1;
const MENU_QUIT: usize = 2;
const MENU_NEW: usize = 3;
const MENU_PIN: usize = 4;
const MENU_UNPIN: usize = 5;
const WM_MOUSELEAVE: u32 = 0x02A3;

static MSG_TASKBAR_CREATED: AtomicU32 = AtomicU32::new(0);
static MSG_SHELLHOOK: AtomicU32 = AtomicU32::new(0);

#[derive(Clone, Copy, PartialEq, Debug)]
enum Item {
    Start,
    Search,
    TaskView,
    Button(usize),
    Clock,
    ShowDesktop,
}

/// Una barra por monitor: la principal (sobre `Shell_TrayWnd`, dueña de temporizadores, ganchos y
/// pipe) y una por cada `Shell_SecondaryTrayWnd`. Cada una tiene su render, DPI y botones; todas
/// muestran los anclados, y cada una solo las ventanas abiertas en su monitor.
struct App {
    /// Barra nativa que tapa. `None` = la principal, que se busca de nuevo en cada refresco
    /// (cambia de HWND si Explorer se reinicia).
    native: Option<HWND>,
    hwnd: HWND,
    renderer: Renderer,
    tasks: Tasks,
    pins: State,
    buttons: Vec<Button>,
    /// Último resumen de botones escrito en el log (solo se registra si cambia).
    last_summary: String,
    bounds: RECT,
    dpi: u32,
    fonts: Option<Fonts>,
    /// Clave: "h:<HICON>" (icono de ventana) o "s:<nombre de shell>". `None` = falló, no reintentar.
    icons: HashMap<String, Option<ID2D1Bitmap1>>,
    hover: Option<Item>,
    pressed: Option<Item>,
    fullscreen: bool,
}

/// Un botón de app: anclado, abierto o ambos. Anclados primero, en su orden; luego el resto.
struct Button {
    pin: Option<usize>,
    group: Option<usize>,
    icon: String,
}

struct Fonts {
    clock: IDWriteTextFormat,
    glyph: IDWriteTextFormat,
}

thread_local! {
    static APPS: RefCell<Vec<App>> = const { RefCell::new(Vec::new()) };
    static CONFIG: RefCell<Option<Config>> = const { RefCell::new(None) };
}

fn config<R>(f: impl FnOnce(&Config) -> R) -> R {
    CONFIG.with(|c| f(c.borrow_mut().get_or_insert_with(Config::load)))
}

/// Ejecuta `f` con la barra de `hwnd` si no están ya prestadas (mensajes enviados durante un refresco).
fn with_app<R>(hwnd: HWND, f: impl FnOnce(&mut App) -> R) -> Option<R> {
    APPS.with(|a| a.try_borrow_mut().ok().and_then(|mut v| v.iter_mut().find(|b| b.hwnd == hwnd).map(f)))
}

/// Ejecuta `f` con cada barra. Falso si estaban prestadas.
fn with_apps(f: impl FnMut(&mut App)) -> bool {
    APPS.with(|a| a.try_borrow_mut().map(|mut v| v.iter_mut().for_each(f)).is_ok())
}

fn is_main(hwnd: HWND) -> bool {
    hwnd == crate::main_window()
}

/// Crea la ventana de una barra sobre `bounds`, con acrílico y render.
fn create_bar(native: Option<HWND>, bounds: RECT) -> Result<App> {
    unsafe {
        let instance: HINSTANCE = GetModuleHandleW(None)?.into();
        let hwnd = CreateWindowExW(
            WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_NOREDIRECTIONBITMAP,
            CLASS,
            w!("OpenWinBox Taskbar"),
            WS_POPUP,
            bounds.left,
            bounds.top,
            bounds.right - bounds.left,
            bounds.bottom - bounds.top,
            None,
            None,
            Some(instance),
            None,
        )?;
        acrylic(hwnd);
        let renderer = match Renderer::new(hwnd) {
            Ok(r) => r,
            Err(e) => {
                let _ = DestroyWindow(hwnd);
                return Err(e);
            }
        };
        let _ = ShowWindow(hwnd, SW_SHOWNOACTIVATE);
        Ok(App {
            native,
            hwnd,
            renderer,
            tasks: Tasks::default(),
            pins: State::load(),
            buttons: Vec::new(),
            last_summary: String::new(),
            bounds,
            dpi: GetDpiForWindow(hwnd),
            fonts: None,
            icons: HashMap::new(),
            hover: None,
            pressed: None,
            fullscreen: false,
        })
    }
}

/// Una barra secundaria por cada `Shell_SecondaryTrayWnd`: crea las que faltan (monitor conectado,
/// Explorer reiniciado) y destruye las que sobran.
fn sync_secondary_bars() {
    let natives = if config(|c| c.show_on_all_monitors) { shell::secondary_bars() } else { Vec::new() };
    let Some((gone, missing)) = APPS.with(|a| {
        a.try_borrow_mut().ok().map(|mut v| {
            let mut gone = Vec::new();
            v.retain(|b| {
                let keep = b.native.is_none_or(|n| natives.contains(&n));
                if !keep {
                    gone.push(b.hwnd);
                }
                keep
            });
            let have: Vec<HWND> = v.iter().filter_map(|b| b.native).collect();
            (gone, natives.iter().copied().filter(|n| !have.contains(n)).collect::<Vec<_>>())
        })
    }) else {
        return;
    };
    for hwnd in gone {
        log("barra secundaria retirada (su monitor ya no tiene barra nativa)");
        unsafe {
            let _ = DestroyWindow(hwnd);
        }
    }
    for native in missing {
        let Some(bounds) = shell::window_rect(native) else { continue };
        // Fuera del préstamo: crear la ventana envía mensajes a `wnd_proc`.
        match create_bar(Some(native), bounds) {
            Ok(app) => {
                log(&format!("barra secundaria en {bounds:?}"));
                APPS.with(|a| a.borrow_mut().push(app));
            }
            Err(e) => log(&format!("no se pudo crear la barra secundaria: {e}")),
        }
    }
}

fn refresh_all() {
    sync_secondary_bars();
    if !with_apps(|a| a.refresh()) {
        request_refresh();
    }
}

pub fn run(exit_after: Option<u32>) -> Result<()> {
    unsafe {
        let instance: HINSTANCE = GetModuleHandleW(None)?.into();
        let wc = WNDCLASSEXW {
            cbSize: size_of::<WNDCLASSEXW>() as u32,
            lpfnWndProc: Some(wnd_proc),
            hInstance: instance,
            hCursor: LoadCursorW(None, IDC_ARROW)?,
            lpszClassName: CLASS,
            ..Default::default()
        };
        RegisterClassExW(&wc);
        MSG_TASKBAR_CREATED.store(RegisterWindowMessageW(w!("TaskbarCreated")), Ordering::Relaxed);
        MSG_SHELLHOOK.store(RegisterWindowMessageW(w!("SHELLHOOK")), Ordering::Relaxed);

        let bounds = shell::primary_rect().unwrap_or(RECT { left: 0, top: 1392, right: 2560, bottom: 1440 });
        let main = create_bar(None, bounds)?;
        let hwnd = main.hwnd;
        crate::set_main_window(hwnd);
        crate::ipc::spawn();
        crate::config::watch();
        APPS.with(|a| a.borrow_mut().push(main));

        let _ = RegisterShellHookWindow(hwnd);
        let hooks = [
            (EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND),
            (EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZEEND),
            (EVENT_OBJECT_DESTROY, EVENT_OBJECT_HIDE),
            (EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_NAMECHANGE),
            (EVENT_OBJECT_CLOAKED, EVENT_OBJECT_UNCLOAKED),
        ];
        let hooks: Vec<HWINEVENTHOOK> =
            hooks.iter().map(|&(a, b)| SetWinEventHook(a, b, None, Some(win_event), 0, 0, WINEVENT_OUTOFCONTEXT)).collect();

        // Orden: marca en disco → vigilante → ocultar.
        shell::mark_hidden();
        shell::spawn_watchdog();
        shell::hide_all();
        refresh_all();
        schedule_clock(hwnd);
        if let Some(s) = exit_after {
            SetTimer(Some(hwnd), TIMER_EXIT, s * 1000, None);
        }
        log(&format!("barra lista en {bounds:?}, pid {}", std::process::id()));

        let mut msg = MSG::default();
        while GetMessageW(&mut msg, None, 0, 0).as_bool() {
            let _ = TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }

        for h in hooks {
            let _ = UnhookWinEvent(h);
        }
        let _ = DeregisterShellHookWindow(hwnd);
        // Soltar los render antes de que desaparezcan sus ventanas.
        let rest: Vec<HWND> = APPS.with(|a| a.borrow_mut().drain(..).map(|b| b.hwnd).collect());
        for h in rest {
            let _ = DestroyWindow(h);
        }
        shell::show_all();
        shell::clear_mark();
        log("barra cerrada; barra nativa restaurada");
    }
    Ok(())
}

fn schedule_clock(hwnd: HWND) {
    let now = unsafe { windows::Win32::System::SystemInformation::GetLocalTime() };
    let ms = 60_000 - (now.wSecond as u32 * 1000 + now.wMilliseconds as u32) + 50;
    unsafe { SetTimer(Some(hwnd), TIMER_CLOCK, ms, None) };
}

fn request_refresh() {
    let hwnd = crate::main_window();
    unsafe { SetTimer(Some(hwnd), TIMER_REFRESH, 40, None) };
}

unsafe extern "system" fn win_event(_: HWINEVENTHOOK, event: u32, hwnd: HWND, id_object: i32, id_child: i32, _: u32, _: u32) {
    if id_object != OBJID_WINDOW.0 || id_child != 0 || hwnd.is_invalid() {
        return;
    }
    match event {
        // La barra nativa reaparece (Explorer reiniciado, cambios de pantalla): volver a ocultarla ya.
        EVENT_OBJECT_SHOW if shell::is_native_bar(hwnd) => {
            unsafe {
                let _ = ShowWindow(hwnd, SW_HIDE);
            }
            request_refresh();
        }
        // Solo interesa el movimiento de la ventana en primer plano (pantalla completa).
        EVENT_OBJECT_LOCATIONCHANGE if hwnd != unsafe { GetForegroundWindow() } => {}
        // REORDER / SELECTION* / STATECHANGE caen dentro del rango de LOCATIONCHANGE..NAMECHANGE.
        0x8004..=0x800A => {}
        _ => request_refresh(),
    }
}

unsafe extern "system" fn wnd_proc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    unsafe {
        let handled = match msg {
            WM_MOUSEACTIVATE => return LRESULT(MA_NOACTIVATE as isize),
            WM_TIMER => match wparam.0 {
                TIMER_REFRESH => {
                    let _ = KillTimer(Some(hwnd), TIMER_REFRESH);
                    refresh_all();
                    true
                }
                TIMER_CLOCK => {
                    schedule_clock(hwnd);
                    with_apps(|a| a.draw())
                }
                TIMER_EXIT => {
                    let _ = DestroyWindow(hwnd);
                    true
                }
                _ => false,
            },
            WM_MOUSEMOVE => with_app(hwnd, |a| a.mouse_move(lparam)).is_some(),
            WM_MOUSELEAVE => with_app(hwnd, |a| a.set_hover(None)).is_some(),
            WM_LBUTTONDOWN => with_app(hwnd, |a| {
                a.pressed = a.hit(lparam);
                a.draw();
            })
            .is_some(),
            WM_LBUTTONUP => with_app(hwnd, |a| a.left_click(lparam)).is_some(),
            WM_MBUTTONUP => with_app(hwnd, |a| {
                if let Some(Item::Button(i)) = a.hit(lparam) {
                    a.button_command(i, MENU_NEW);
                }
            })
            .is_some(),
            WM_RBUTTONUP => {
                // El menú hace correr un bucle de mensajes: no puede ir con la app prestada.
                let target = with_app(hwnd, |a| a.hit(lparam)).flatten();
                context_menu(hwnd, target, lparam);
                true
            }
            WM_DISPLAYCHANGE | WM_SETTINGCHANGE | WM_DPICHANGED => {
                request_refresh();
                false
            }
            crate::ipc::WM_APP_RELOAD => {
                CONFIG.with(|c| *c.borrow_mut() = Some(Config::load()));
                with_apps(|a| a.pins = State::load());
                request_refresh();
                true
            }
            // Solo la principal cierra el proceso; las secundarias las retira `sync_secondary_bars`.
            WM_CLOSE => {
                if is_main(hwnd) {
                    let _ = DestroyWindow(hwnd);
                }
                true
            }
            WM_DESTROY => {
                if is_main(hwnd) {
                    PostQuitMessage(0);
                }
                true
            }
            m if m == MSG_TASKBAR_CREATED.load(Ordering::Relaxed) => {
                log("TaskbarCreated: Explorer reiniciado, volviendo a ocultar su barra");
                shell::hide_all();
                request_refresh();
                true
            }
            m if m == MSG_SHELLHOOK.load(Ordering::Relaxed) => {
                const HSHELL_FLASH: usize = 0x8006;
                const HSHELL_WINDOWACTIVATED: usize = 4;
                const HSHELL_RUDEAPPACTIVATED: usize = 0x8004;
                let target = lparam.0;
                match wparam.0 {
                    HSHELL_FLASH => {
                        with_apps(|a| {
                            a.tasks.flashing.insert(target);
                        });
                    }
                    HSHELL_WINDOWACTIVATED | HSHELL_RUDEAPPACTIVATED => {
                        with_apps(|a| {
                            a.tasks.flashing.remove(&target);
                        });
                    }
                    _ => {}
                }
                request_refresh();
                true
            }
            _ => false,
        };
        if handled { LRESULT(0) } else { DefWindowProcW(hwnd, msg, wparam, lparam) }
    }
}

impl App {
    fn scale(&self) -> f32 {
        self.dpi as f32 / 96.0
    }

    fn size(&self) -> (f32, f32) {
        ((self.bounds.right - self.bounds.left) as f32, (self.bounds.bottom - self.bounds.top) as f32)
    }

    fn refresh(&mut self) {
        // Posición: la de la barra nativa (oculta) que tapa esta barra.
        let native = match self.native {
            Some(n) => shell::window_rect(n),
            None => shell::primary_rect(),
        };
        if let Some(r) = native {
            if r != self.bounds {
                self.bounds = r;
                unsafe {
                    let _ = SetWindowPos(self.hwnd, Some(HWND_TOPMOST), r.left, r.top, r.right - r.left, r.bottom - r.top, SWP_NOACTIVATE);
                }
            }
        }
        let dpi = unsafe { GetDpiForWindow(self.hwnd) };
        if dpi != self.dpi {
            self.dpi = dpi;
            self.fonts = None;
        }
        self.update_fullscreen();
        let monitor = unsafe { MonitorFromWindow(self.hwnd, MONITOR_DEFAULTTONEAREST) };
        self.tasks.refresh(self.hwnd, monitor);
        self.rebuild_buttons();
        self.draw();
    }

    /// Una ventana en primer plano que cubre el monitor de la barra la oculta (juegos, vídeo).
    fn update_fullscreen(&mut self) {
        let full = unsafe {
            let fg = GetForegroundWindow();
            let mut r = RECT::default();
            let mut mi = MONITORINFO { cbSize: size_of::<MONITORINFO>() as u32, ..Default::default() };
            !fg.is_invalid()
                && fg != self.hwnd
                && !matches!(crate::class_name(fg).as_str(), "Progman" | "WorkerW")
                && GetWindowRect(fg, &mut r).is_ok()
                && GetMonitorInfoW(MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST), &mut mi).as_bool()
                && MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST) == MonitorFromWindow(self.hwnd, MONITOR_DEFAULTTONEAREST)
                && r.left <= mi.rcMonitor.left
                && r.top <= mi.rcMonitor.top
                && r.right >= mi.rcMonitor.right
                && r.bottom >= mi.rcMonitor.bottom
        };
        if full != self.fullscreen {
            self.fullscreen = full;
            unsafe {
                let _ = ShowWindow(self.hwnd, if full { SW_HIDE } else { SW_SHOWNOACTIVATE });
                if !full {
                    let _ = SetWindowPos(self.hwnd, Some(HWND_TOPMOST), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
        }
    }

    /// Botones fijos y grupos centrados (como Windows 11); reloj y "mostrar escritorio" a la derecha.
    fn layout(&self) -> Vec<(Item, D2D_RECT_F)> {
        let s = self.scale();
        let (w, h) = self.size();
        let slot = (44.0 * s).round();
        let (search, task_view, left) = config(|c| (c.show_search, c.show_task_view, c.left_aligned()));
        let mut items = vec![Item::Start];
        if search {
            items.push(Item::Search);
        }
        if task_view {
            items.push(Item::TaskView);
        }
        items.extend((0..self.buttons.len()).map(Item::Button));
        let peek = (8.0 * s).round();
        let clock_w = (92.0 * s).round();
        let available = w - clock_w - peek;
        let total = slot * items.len() as f32;
        let mut x = if left { (4.0 * s).round() } else { ((w - total) / 2.0).max(0.0).min((available - total).max(0.0)).round() };
        let mut out = Vec::with_capacity(items.len() + 2);
        for item in items {
            out.push((item, rect(x, 0.0, x + slot, h)));
            x += slot;
        }
        out.push((Item::Clock, rect(w - peek - clock_w, 0.0, w - peek, h)));
        out.push((Item::ShowDesktop, rect(w - peek, 0.0, w, h)));
        out
    }

    fn hit(&self, lparam: LPARAM) -> Option<Item> {
        let (x, y) = ((lparam.0 & 0xFFFF) as i16 as f32, ((lparam.0 >> 16) & 0xFFFF) as i16 as f32);
        self.layout().into_iter().find(|(_, r)| x >= r.left && x < r.right && y >= r.top && y < r.bottom).map(|(i, _)| i)
    }

    fn mouse_move(&mut self, lparam: LPARAM) {
        unsafe {
            let mut tme = TRACKMOUSEEVENT { cbSize: size_of::<TRACKMOUSEEVENT>() as u32, dwFlags: TME_LEAVE, hwndTrack: self.hwnd, dwHoverTime: 0 };
            let _ = TrackMouseEvent(&mut tme);
        }
        let hit = self.hit(lparam);
        self.set_hover(hit);
    }

    fn set_hover(&mut self, hover: Option<Item>) {
        if hover != self.hover {
            self.hover = hover;
            if hover.is_none() {
                self.pressed = None;
            }
            self.draw();
        }
    }

    fn left_click(&mut self, lparam: LPARAM) {
        let hit = self.hit(lparam);
        let pressed = self.pressed.take();
        if hit.is_some() && hit == pressed {
            match hit.unwrap() {
                Item::Start => send_keys(&[VK_LWIN]),
                Item::Search => send_keys(&[VK_LWIN, VK_S]),
                Item::TaskView => send_keys(&[VK_LWIN, VK_TAB]),
                Item::Clock => send_keys(&[VK_LWIN, VK_N]),
                Item::ShowDesktop => send_keys(&[VK_LWIN, VK_D]),
                Item::Button(i) => self.activate_button(i),
            }
        }
        self.draw();
    }

    /// Anclados primero (en su orden, con su grupo si está abierto); luego las apps abiertas sin anclar.
    fn rebuild_buttons(&mut self) {
        let mut used = vec![false; self.tasks.groups.len()];
        let mut buttons = Vec::new();
        for (pi, pin) in self.pins.pinned.iter().enumerate() {
            // Por clave; si no, por exe (la ventana no siempre lleva el AUMID del acceso directo).
            let groups = &self.tasks.groups;
            let free = |g: &usize| !used[*g];
            let group = (0..groups.len()).filter(free).find(|&g| groups[g].key.eq_ignore_ascii_case(&pin.key)).or_else(|| {
                (0..groups.len()).filter(free).find(|&g| {
                    !pin.exe.is_empty() && pin.exe != "applicationframehost.exe" && groups[g].exe == pin.exe
                })
            });
            if let Some(g) = group {
                used[g] = true;
            }
            buttons.push(Button { pin: Some(pi), group, icon: String::new() });
        }
        buttons.extend(used.iter().enumerate().filter(|(_, u)| !**u).map(|(g, _)| Button { pin: None, group: Some(g), icon: String::new() }));
        for b in &mut buttons {
            let window_icon = b.group.map(|g| self.tasks.groups[g].icon).filter(|i| !i.is_invalid());
            b.icon = match window_icon {
                Some(i) => format!("h:{:x}", i.0 as usize),
                None => format!("s:{}", self.launch_target(b)),
            };
        }
        let (groups, pinned) = (&self.tasks.groups, &self.pins.pinned);
        let summary = buttons
            .iter()
            .map(|b| match (b.pin, b.group) {
                (Some(p), Some(g)) => format!("[A+] {} = {} ({})", pinned[p].name, groups[g].key, groups[g].exe),
                (Some(p), None) => format!("[A ] {} ({} / {})", pinned[p].name, pinned[p].key, pinned[p].exe),
                (None, Some(g)) => format!("[ +] {} ({})", groups[g].key, groups[g].exe),
                _ => String::new(),
            })
            .collect::<Vec<_>>()
            .join("\n    ");
        if summary != self.last_summary {
            log(&format!("botones ({}, {}):\n    {summary}", self.bounds.left, self.bounds.top));
            self.last_summary = summary;
        }
        self.buttons = buttons;
    }

    fn launch_target(&self, b: &Button) -> String {
        match (b.pin, b.group) {
            (Some(p), _) => self.pins.pinned[p].launch.clone(),
            (None, Some(g)) => self.tasks.groups[g].launch_target(),
            _ => String::new(),
        }
    }

    fn activate_button(&mut self, i: usize) {
        let Some(b) = self.buttons.get(i) else { return };
        match b.group.and_then(|g| self.tasks.groups.get(g)) {
            Some(g) => tasks::click(g),
            None => pins::launch(&self.launch_target(b)),
        }
    }

    fn button_info(&self, i: usize) -> Option<(usize, bool)> {
        let b = self.buttons.get(i)?;
        Some((b.group.map_or(0, |g| self.tasks.groups[g].windows.len()), b.pin.is_some()))
    }

    fn button_command(&mut self, i: usize, cmd: usize) {
        let Some(b) = self.buttons.get(i) else { return };
        let (pin, group) = (b.pin, b.group);
        match cmd {
            MENU_NEW => pins::launch(&self.launch_target(&self.buttons[i])),
            MENU_CLOSE => {
                if let Some(g) = group.and_then(|g| self.tasks.groups.get(g)) {
                    tasks::close(g);
                }
            }
            MENU_PIN => {
                if let Some(g) = group.and_then(|g| self.tasks.groups.get(g)) {
                    let name = std::path::Path::new(&g.key).file_stem().map(|s| s.to_string_lossy().into_owned()).unwrap_or_else(|| g.title.clone());
                    self.pins.pinned.push(Pin { key: g.key.clone(), launch: g.launch_target(), name, exe: g.exe.clone() });
                    self.pins.save();
                    reload_pins_everywhere();
                }
            }
            MENU_UNPIN => {
                if let Some(p) = pin {
                    self.pins.pinned.remove(p);
                    self.pins.save();
                    reload_pins_everywhere();
                }
            }
            _ => return,
        }
        self.rebuild_buttons();
        self.draw();
    }

    fn ensure_resources(&mut self) {
        if self.fonts.is_none() {
            let s = self.scale();
            let clock = self.renderer.text_format(w!("Segoe UI Variable Text"), 12.0 * s, DWRITE_FONT_WEIGHT_NORMAL);
            let glyph = self.renderer.text_format(w!("Segoe Fluent Icons"), 16.0 * s, DWRITE_FONT_WEIGHT_NORMAL);
            if let (Ok(clock), Ok(glyph)) = (clock, glyph) {
                unsafe {
                    let _ = clock.SetTextAlignment(DWRITE_TEXT_ALIGNMENT_TRAILING);
                    let _ = clock.SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER);
                    let _ = glyph.SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);
                    let _ = glyph.SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER);
                }
                self.fonts = Some(Fonts { clock, glyph });
            }
        }
        // Bitmaps de iconos: crear los nuevos, soltar los que ya no se usan.
        let size = (32.0 * self.scale()).round() as i32;
        let wanted: Vec<String> = self.buttons.iter().map(|b| b.icon.clone()).collect();
        self.icons.retain(|k, _| wanted.contains(k));
        for key in wanted {
            if self.icons.contains_key(&key) {
                continue;
            }
            let bitmap = match key.split_once(':') {
                Some(("h", hex)) => self.renderer.bitmap_from_icon(HICON(isize::from_str_radix(hex, 16).unwrap_or(0) as *mut c_void)),
                Some((_, name)) => self.renderer.bitmap_from_shell(name, size),
                None => continue,
            };
            if let Err(e) = &bitmap {
                log(&format!("icono {key}: {e}"));
            }
            self.icons.insert(key, bitmap.ok());
        }
    }

    fn draw(&mut self) {
        self.ensure_resources();
        let layout = self.layout();
        let (w, h) = self.size();
        let s = self.scale();
        let fg = unsafe { GetForegroundWindow() };
        let accent = accent_color();
        let clock_text = clock_text();
        let Self { renderer, tasks, buttons, fonts, icons, hover, pressed, .. } = self;
        let Some(fonts) = fonts.as_ref() else { return };
        let result = renderer.draw(w as u32, h as u32, |dc| unsafe {
            let brush = |c: D2D1_COLOR_F| dc.CreateSolidColorBrush(&c, None).unwrap();
            let white = brush(rgba(255, 255, 255, 1.0));
            // Línea superior sutil, como la barra de Windows 11.
            dc.FillRectangle(&rect(0.0, 0.0, w, s.max(1.0)), &brush(rgba(255, 255, 255, 0.08)));
            for (item, r) in &layout {
                let hovered = *hover == Some(*item);
                let is_pressed = hovered && *pressed == Some(*item);
                let group = match item {
                    Item::Button(i) => buttons.get(*i).and_then(|b| b.group).and_then(|g| tasks.groups.get(g)),
                    _ => None,
                };
                let active = group.is_some_and(|g| g.windows.contains(&fg));
                let flashing = group.is_some_and(|g| tasks.is_flashing(g));
                // Fondo del botón.
                if *item != Item::ShowDesktop {
                    let pad = 4.0 * s;
                    let bg = rect(r.left + 2.0 * s, r.top + pad, r.right - 2.0 * s, r.bottom - pad);
                    let alpha = if flashing { 0.0 } else if is_pressed { 0.04 } else if hovered || active { 0.08 } else { 0.0 };
                    if flashing {
                        dc.FillRoundedRectangle(&D2D1_ROUNDED_RECT { rect: bg, radiusX: 4.0 * s, radiusY: 4.0 * s }, &brush(rgba(233, 140, 40, 0.55)));
                    } else if alpha > 0.0 {
                        dc.FillRoundedRectangle(&D2D1_ROUNDED_RECT { rect: bg, radiusX: 4.0 * s, radiusY: 4.0 * s }, &brush(rgba(255, 255, 255, alpha)));
                    }
                } else if hovered {
                    dc.FillRectangle(&rect(r.left + 3.0 * s, r.top, r.left + 4.0 * s, r.bottom), &brush(rgba(255, 255, 255, 0.4)));
                }
                let (cx, cy) = ((r.left + r.right) / 2.0, (r.top + r.bottom) / 2.0 - if is_pressed { -1.0 * s } else { 0.0 });
                let icon = 24.0 * s;
                let icon_rect = rect(cx - icon / 2.0, cy - icon / 2.0, cx + icon / 2.0, cy + icon / 2.0);
                match item {
                    Item::Start => draw_start_logo(dc, cx, cy, 20.0 * s, &brush),
                    Item::Search => draw_glyph(dc, "\u{E721}", &fonts.glyph, *r, &white),
                    Item::TaskView => draw_glyph(dc, "\u{E7C4}", &fonts.glyph, *r, &white),
                    Item::Clock => {
                        let text: Vec<u16> = clock_text.encode_utf16().collect();
                        let tr = rect(r.left, r.top, r.right - 8.0 * s, r.bottom);
                        dc.DrawText(&text, &fonts.clock, &tr, &white, D2D1_DRAW_TEXT_OPTIONS_NONE, DWRITE_MEASURING_MODE_NATURAL);
                    }
                    Item::ShowDesktop => {}
                    Item::Button(i) => {
                        if let Some(Some(b)) = icons.get(&buttons[*i].icon) {
                            dc.DrawBitmap(b, Some(&icon_rect), 1.0, D2D1_INTERPOLATION_MODE_HIGH_QUALITY_CUBIC, None, None);
                        } else {
                            dc.FillRoundedRectangle(&D2D1_ROUNDED_RECT { rect: icon_rect, radiusX: 4.0 * s, radiusY: 4.0 * s }, &brush(rgba(255, 255, 255, 0.3)));
                        }
                        // Indicador: nada = anclada sin abrir; corto y gris = abierta; largo y de acento = en primer plano.
                        if group.is_none() {
                            continue;
                        }
                        let iw = if active { 16.0 * s } else { 6.0 * s };
                        let ih = 3.0 * s;
                        let iy = r.bottom - 4.0 * s - ih;
                        let color = if active { accent } else { rgba(255, 255, 255, 0.55) };
                        dc.FillRoundedRectangle(
                            &D2D1_ROUNDED_RECT { rect: rect(cx - iw / 2.0, iy, cx + iw / 2.0, iy + ih), radiusX: ih / 2.0, radiusY: ih / 2.0 },
                            &brush(color),
                        );
                    }
                }
            }
        });
        if let Err(e) = result {
            log(&format!("error al dibujar: {e}"));
        }
    }
}

/// Las demás barras releen `state.json` (la que ancló ya lo tiene al día).
fn reload_pins_everywhere() {
    unsafe {
        let _ = PostMessageW(Some(crate::main_window()), crate::ipc::WM_APP_RELOAD, WPARAM(0), LPARAM(0));
    }
}

fn draw_glyph(dc: &ID2D1DeviceContext, glyph: &str, format: &IDWriteTextFormat, r: D2D_RECT_F, brush: &ID2D1SolidColorBrush) {
    let text: Vec<u16> = glyph.encode_utf16().collect();
    unsafe { dc.DrawText(&text, format, &r, brush, D2D1_DRAW_TEXT_OPTIONS_NONE, DWRITE_MEASURING_MODE_NATURAL) };
}

/// Logo de Windows: cuatro cuadrados con degradado azul.
fn draw_start_logo(dc: &ID2D1DeviceContext, cx: f32, cy: f32, size: f32, brush: &dyn Fn(D2D1_COLOR_F) -> ID2D1SolidColorBrush) {
    let gap = (size * 0.06).max(1.0);
    let q = (size - gap) / 2.0;
    let (x0, y0) = (cx - size / 2.0, cy - size / 2.0);
    let colors = [rgba(0, 174, 255, 1.0), rgba(0, 156, 245, 1.0), rgba(0, 140, 235, 1.0), rgba(0, 120, 220, 1.0)];
    for (i, c) in colors.iter().enumerate() {
        let (col, row) = ((i % 2) as f32, (i / 2) as f32);
        let x = x0 + col * (q + gap);
        let y = y0 + row * (q + gap);
        unsafe { dc.FillRectangle(&rect(x, y, x + q, y + q), &brush(*c)) };
    }
}

fn accent_color() -> D2D1_COLOR_F {
    let mut argb = 0u32;
    let mut opaque = BOOL(0);
    if unsafe { DwmGetColorizationColor(&mut argb, &mut opaque) }.is_ok() {
        // Aclarar un poco el acento para que destaque sobre fondo oscuro.
        let lift = |c: u32| (((c & 0xFF) as f32 / 255.0) * 0.6 + 0.4).min(1.0);
        return D2D1_COLOR_F { r: lift(argb >> 16), g: lift(argb >> 8), b: lift(argb), a: 1.0 };
    }
    rgba(96, 205, 255, 1.0)
}

fn clock_text() -> String {
    let mut time = [0u16; 64];
    let mut date = [0u16; 64];
    unsafe {
        let t = GetTimeFormatEx(None, TIME_NOSECONDS, None, None, Some(&mut time));
        let d = GetDateFormatEx(None, DATE_SHORTDATE, None, None, Some(&mut date), None);
        let s = |b: &[u16], n: i32| String::from_utf16_lossy(&b[..(n - 1).max(0) as usize]);
        format!("{}\n{}", s(&time, t), s(&date, d))
    }
}

fn send_keys(keys: &[VIRTUAL_KEY]) {
    let input = |vk: VIRTUAL_KEY, up: bool| INPUT {
        r#type: INPUT_KEYBOARD,
        Anonymous: INPUT_0 { ki: KEYBDINPUT { wVk: vk, dwFlags: if up { KEYEVENTF_KEYUP } else { KEYBD_EVENT_FLAGS(0) }, ..Default::default() } },
    };
    let mut seq: Vec<INPUT> = keys.iter().map(|&k| input(k, false)).collect();
    seq.extend(keys.iter().rev().map(|&k| input(k, true)));
    unsafe { SendInput(&seq, size_of::<INPUT>() as i32) };
}

fn context_menu(hwnd: HWND, target: Option<Item>, lparam: LPARAM) {
    if target == Some(Item::Start) {
        send_keys(&[VK_LWIN, VK_X]);
        return;
    }
    let button = match target {
        Some(Item::Button(i)) => Some(i),
        _ => None,
    };
    // (ventanas abiertas, anclado) del botón, leído antes de abrir el menú.
    let info = button.and_then(|i| with_app(hwnd, |a| a.button_info(i)).flatten());
    unsafe {
        let Ok(menu) = CreatePopupMenu() else { return };
        match info {
            Some((windows, pinned)) => {
                let _ = AppendMenuW(menu, MF_STRING, MENU_NEW, w!("Abrir nueva ventana"));
                let (id, label) = if pinned { (MENU_UNPIN, w!("Desanclar de la barra de tareas")) } else { (MENU_PIN, w!("Anclar a la barra de tareas")) };
                let _ = AppendMenuW(menu, MF_STRING, id, label);
                if windows > 0 {
                    let _ = AppendMenuW(menu, MF_SEPARATOR, 0, None);
                    let label = if windows > 1 { w!("Cerrar todas las ventanas") } else { w!("Cerrar ventana") };
                    let _ = AppendMenuW(menu, MF_STRING, MENU_CLOSE, label);
                }
            }
            None => {
                let _ = AppendMenuW(menu, MF_STRING, MENU_QUIT, w!("Salir de la barra (restaurar la de Windows)"));
            }
        }
        let mut pt = POINT { x: (lparam.0 & 0xFFFF) as i16 as i32, y: ((lparam.0 >> 16) & 0xFFFF) as i16 as i32 };
        let _ = ClientToScreen(hwnd, &mut pt);
        let _ = SetForegroundWindow(hwnd);
        let cmd = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_BOTTOMALIGN | TPM_RIGHTBUTTON, pt.x, pt.y, None, hwnd, None);
        let _ = PostMessageW(Some(hwnd), WM_NULL, WPARAM(0), LPARAM(0));
        let _ = DestroyMenu(menu);
        let cmd = cmd.0 as usize;
        if cmd == MENU_QUIT {
            let _ = PostMessageW(Some(crate::main_window()), WM_CLOSE, WPARAM(0), LPARAM(0));
        } else if let Some(i) = button {
            with_app(hwnd, |a| a.button_command(i, cmd));
        }
    }
}

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

/// Acrílico por la API no documentada (la oficial se apaga en ventanas inactivas; Fase 0, prueba 4).
/// Si falla, la barra queda transparente sin desenfoque: se ve, pero peor.
fn acrylic(hwnd: HWND) {
    unsafe {
        let Ok(user32) = GetModuleHandleW(w!("user32.dll")) else { return };
        let Some(proc) = GetProcAddress(user32, s!("SetWindowCompositionAttribute")) else {
            log("SetWindowCompositionAttribute no disponible");
            return;
        };
        let f: unsafe extern "system" fn(HWND, *mut CompositionData) -> BOOL = std::mem::transmute(proc);
        // ACCENT_ENABLE_ACRYLICBLURBEHIND, tinte AABBGGRR.
        let mut accent = AccentPolicy { state: 4, flags: 2, gradient: 0xCC20_2020, animation: 0 };
        let mut data = CompositionData { attribute: 19, data: &mut accent as *mut _ as *mut c_void, size: size_of::<AccentPolicy>() };
        if !f(hwnd, &mut data).as_bool() {
            log("no se pudo aplicar el acrílico");
        }
    }
}
