//! Lista de tareas: qué ventanas van en la barra, agrupadas por AUMID (o por exe), y las
//! acciones sobre ellas. El orden de grupos y de ventanas dentro del grupo es estable.

use std::collections::HashSet;
use std::ffi::c_void;

use windows::Win32::Foundation::*;
use windows::Win32::Graphics::Dwm::*;
use windows::Win32::Graphics::Gdi::{HMONITOR, MONITOR_DEFAULTTONEAREST, MonitorFromRect, MonitorFromWindow};
use windows::Win32::System::Com::CoTaskMemFree;
use windows::Win32::System::Com::StructuredStorage::PropVariantToStringAlloc;
use windows::Win32::System::Threading::*;
use windows::Win32::UI::Shell::PropertiesSystem::*;
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::core::*;

pub struct Group {
    pub key: String,
    pub windows: Vec<HWND>,
    /// La ventana del grupo más arriba en el orden Z (la última usada).
    pub top: HWND,
    pub title: String,
    pub icon: HICON,
    /// Nombre del exe en minúsculas: casa ventanas con anclados cuya clave no coincide.
    pub exe: String,
}

impl Group {
    /// Qué abrir para una ventana nueva de esta app: el exe o la app por su AUMID.
    pub fn launch_target(&self) -> String {
        if self.key.contains(":\\") { self.key.clone() } else { format!(r"shell:AppsFolder\{}", self.key) }
    }
}

#[derive(Default)]
pub struct Tasks {
    pub groups: Vec<Group>,
    pub flashing: HashSet<isize>,
}

impl Tasks {
    /// Ventanas elegibles cuyo monitor es `monitor` (cada barra muestra las de su pantalla).
    pub fn refresh(&mut self, own: HWND, monitor: HMONITOR) {
        let mut found: Vec<HWND> = Vec::new();
        unsafe {
            let _ = EnumWindows(Some(collect), LPARAM(&mut found as *mut _ as isize));
        }
        found.retain(|&h| h != own && eligible(h) && window_monitor(h) == monitor);

        let mut by_key: Vec<(String, Vec<HWND>)> = Vec::new();
        for &h in &found {
            let key = group_key(h);
            match by_key.iter_mut().find(|(k, _)| *k == key) {
                Some((_, v)) => v.push(h),
                None => by_key.push((key, vec![h])),
            }
        }

        // Grupos existentes conservan su posición; los nuevos van al final.
        let mut groups: Vec<Group> = Vec::new();
        for old in self.groups.drain(..) {
            if let Some(i) = by_key.iter().position(|(k, _)| *k == old.key) {
                let (key, zorder) = by_key.remove(i);
                let mut windows: Vec<HWND> = old.windows.into_iter().filter(|w| zorder.contains(w)).collect();
                windows.extend(zorder.iter().filter(|w| !windows.contains(w)).copied().collect::<Vec<_>>());
                groups.push(Group { key, top: zorder[0], title: String::new(), icon: HICON::default(), exe: String::new(), windows });
            }
        }
        for (key, zorder) in by_key {
            groups.push(Group { key, top: zorder[0], title: String::new(), icon: HICON::default(), exe: String::new(), windows: zorder });
        }
        for g in &mut groups {
            g.title = window_title(g.top);
            g.icon = window_icon(g.top).unwrap_or_default();
            g.exe = process_path(g.top).map(|p| exe_name(&p)).unwrap_or_default();
        }
        self.groups = groups;
        let alive: HashSet<isize> = found.iter().map(|h| h.0 as isize).collect();
        self.flashing.retain(|h| alive.contains(h));
    }

    pub fn is_flashing(&self, g: &Group) -> bool {
        g.windows.iter().any(|w| self.flashing.contains(&(w.0 as isize)))
    }
}

unsafe extern "system" fn collect(hwnd: HWND, lparam: LPARAM) -> BOOL {
    unsafe { (*(lparam.0 as *mut Vec<HWND>)).push(hwnd) };
    TRUE
}

/// Las mismas reglas que usa Explorer, en esencia.
fn eligible(h: HWND) -> bool {
    unsafe {
        if !IsWindowVisible(h).as_bool() || GetWindowTextLengthW(h) == 0 {
            return false;
        }
        let ex = GetWindowLongW(h, GWL_EXSTYLE) as u32;
        let app_window = ex & WS_EX_APPWINDOW.0 != 0;
        if !app_window {
            if ex & (WS_EX_TOOLWINDOW.0 | WS_EX_NOACTIVATE.0) != 0 {
                return false;
            }
            if GetWindow(h, GW_OWNER).is_ok_and(|o| !o.is_invalid()) {
                return false;
            }
        }
        let mut cloaked = 0u32;
        if DwmGetWindowAttribute(h, DWMWA_CLOAKED, &mut cloaked as *mut _ as *mut c_void, 4).is_ok() && cloaked != 0 {
            return false;
        }
    }
    !matches!(crate::class_name(h).as_str(), "Progman" | "WorkerW" | "Shell_TrayWnd" | "Shell_SecondaryTrayWnd")
}

/// Monitor de la ventana. Una minimizada está en (-32000, -32000): se usa su posición restaurada.
fn window_monitor(h: HWND) -> HMONITOR {
    unsafe {
        if IsIconic(h).as_bool() {
            let mut wp = WINDOWPLACEMENT { length: size_of::<WINDOWPLACEMENT>() as u32, ..Default::default() };
            if GetWindowPlacement(h, &mut wp).is_ok() {
                // rcNormalPosition va en coordenadas del área de trabajo; basta para elegir monitor.
                return MonitorFromRect(&wp.rcNormalPosition, MONITOR_DEFAULTTONEAREST);
            }
        }
        MonitorFromWindow(h, MONITOR_DEFAULTTONEAREST)
    }
}

/// Como Windows: AUMID de la ventana, si no el de su paquete (apps de la Store), si no el exe.
fn group_key(h: HWND) -> String {
    aumid(h).or_else(|| package_aumid(h)).or_else(|| process_path(h).map(|p| p.to_lowercase())).unwrap_or_else(|| format!("hwnd:{:x}", h.0 as usize))
}

pub const PKEY_AUMID: PROPERTYKEY = PROPERTYKEY { fmtid: GUID::from_u128(0x9F4C2855_9F79_4B39_A8D0_E1D42DE1D5F3), pid: 5 };

fn aumid(h: HWND) -> Option<String> {
    let store: IPropertyStore = unsafe { SHGetPropertyStoreForWindow(h) }.ok()?;
    aumid_of(&store)
}

pub fn aumid_of(store: &IPropertyStore) -> Option<String> {
    unsafe {
        let value = store.GetValue(&PKEY_AUMID).ok()?;
        let p = PropVariantToStringAlloc(&value).ok()?;
        let s = p.to_string().ok();
        CoTaskMemFree(Some(p.0 as *const c_void));
        s.filter(|s| !s.is_empty())
    }
}

pub fn exe_name(path: &str) -> String {
    std::path::Path::new(path).file_name().map(|n| n.to_string_lossy().to_lowercase()).unwrap_or_default()
}

fn package_aumid(h: HWND) -> Option<String> {
    unsafe {
        let mut pid = 0;
        GetWindowThreadProcessId(h, Some(&mut pid));
        let process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid).ok()?;
        let mut buf = [0u16; 512];
        let mut len = buf.len() as u32;
        let err = windows::Win32::Storage::Packaging::Appx::GetApplicationUserModelId(process, &mut len, Some(PWSTR(buf.as_mut_ptr())));
        let _ = CloseHandle(process);
        (err == ERROR_SUCCESS && len > 1).then(|| String::from_utf16_lossy(&buf[..len as usize - 1]))
    }
}

fn process_path(h: HWND) -> Option<String> {
    unsafe {
        let mut pid = 0;
        GetWindowThreadProcessId(h, Some(&mut pid));
        let process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid).ok()?;
        let mut buf = [0u16; 1024];
        let mut len = buf.len() as u32;
        let ok = QueryFullProcessImageNameW(process, PROCESS_NAME_WIN32, PWSTR(buf.as_mut_ptr()), &mut len).is_ok();
        let _ = CloseHandle(process);
        ok.then(|| String::from_utf16_lossy(&buf[..len as usize]))
    }
}

pub fn window_title(h: HWND) -> String {
    let mut buf = [0u16; 256];
    let n = unsafe { GetWindowTextW(h, &mut buf) };
    String::from_utf16_lossy(&buf[..n.max(0) as usize])
}

/// Icono grande de la ventana. `SendMessageTimeout` para no colgarnos con apps colgadas.
fn window_icon(h: HWND) -> Option<HICON> {
    unsafe {
        for kind in [ICON_BIG, ICON_SMALL2] {
            let mut result = 0usize;
            let ok = SendMessageTimeoutW(h, WM_GETICON, WPARAM(kind as usize), LPARAM(0), SMTO_ABORTIFHUNG | SMTO_BLOCK, 50, Some(&mut result));
            if ok.0 != 0 && result != 0 {
                return Some(HICON(result as *mut c_void));
            }
        }
        let class_icon = GetClassLongPtrW(h, GCLP_HICON);
        (class_icon != 0).then(|| HICON(class_icon as *mut c_void))
    }
}

pub fn activate(h: HWND) {
    unsafe {
        if IsIconic(h).as_bool() {
            let _ = ShowWindow(h, SW_RESTORE);
        }
        // Tras un clic en la barra somos el proceso con la última entrada: se nos permite.
        if !SetForegroundWindow(h).as_bool() {
            SwitchToThisWindow(h, true);
        }
    }
}

/// Clic en un grupo: alterna minimizar/activar (una ventana) o recorre las ventanas (varias).
pub fn click(g: &Group) {
    unsafe {
        let fg = GetForegroundWindow();
        if g.windows.len() == 1 {
            let w = g.windows[0];
            if w == fg && !IsIconic(w).as_bool() {
                let _ = ShowWindow(w, SW_MINIMIZE);
            } else {
                activate(w);
            }
        } else if let Some(i) = g.windows.iter().position(|&w| w == fg) {
            activate(g.windows[(i + 1) % g.windows.len()]);
        } else {
            activate(g.top);
        }
    }
}

pub fn close(g: &Group) {
    for &w in &g.windows {
        unsafe {
            let _ = PostMessageW(Some(w), WM_CLOSE, WPARAM(0), LPARAM(0));
        }
    }
}
