//! `<raíz>/taskbar/config.json` (dueño: OpenWinBox; la barra solo lo lee). Campos que falten o no se
//! entiendan toman su valor por defecto; un archivo de una versión más nueva se lee igual.
//! Un vigilante de la carpeta `taskbar/` pide recargar cuando cambia algo (también `state.json`).

use serde::Deserialize;
use windows::Win32::Foundation::*;
use windows::Win32::Storage::FileSystem::*;
use windows::Win32::System::Threading::*;
use windows::Win32::UI::WindowsAndMessaging::PostMessageW;
use windows::core::*;

use crate::log;

#[derive(Deserialize, Clone, Debug, PartialEq)]
#[serde(rename_all = "camelCase", default)]
pub struct Config {
    pub version: u32,
    /// `"center"` (como Windows 11) o `"left"`. Texto y no enum: un valor futuro no rompe la lectura.
    pub alignment: String,
    pub show_search: bool,
    pub show_task_view: bool,
    /// Falso = sin barra en los monitores secundarios (como "mostrar en todas las pantallas" de Windows).
    pub show_on_all_monitors: bool,
}

impl Default for Config {
    fn default() -> Self {
        Self { version: 1, alignment: "center".into(), show_search: true, show_task_view: true, show_on_all_monitors: true }
    }
}

impl Config {
    pub fn load() -> Self {
        let path = crate::pins::data_dir().join("config.json");
        let Ok(text) = std::fs::read_to_string(&path) else { return Self::default() };
        serde_json::from_str(&text).unwrap_or_else(|e| {
            log(&format!("config.json ilegible ({e}); se usan los valores por defecto"));
            Self::default()
        })
    }

    pub fn left_aligned(&self) -> bool {
        self.alignment.eq_ignore_ascii_case("left")
    }
}

/// Vigila `taskbar/` y manda `WM_APP_RELOAD` a la barra principal tras cada ráfaga de cambios.
pub fn watch() {
    let dir = crate::pins::data_dir();
    let _ = std::fs::create_dir_all(&dir);
    std::thread::spawn(move || unsafe {
        let wide = HSTRING::from(dir.as_os_str());
        let Ok(handle) = FindFirstChangeNotificationW(&wide, false, FILE_NOTIFY_CHANGE_LAST_WRITE | FILE_NOTIFY_CHANGE_FILE_NAME) else {
            log(&format!("no se pudo vigilar {}", dir.display()));
            return;
        };
        loop {
            if WaitForSingleObject(handle, INFINITE) != WAIT_OBJECT_0 {
                return;
            }
            // Una escritura atómica (temporal + renombrar) son varios avisos: esperar a que acabe.
            std::thread::sleep(std::time::Duration::from_millis(150));
            if FindNextChangeNotification(handle).is_err() {
                return;
            }
            let _ = PostMessageW(Some(crate::main_window()), crate::ipc::WM_APP_RELOAD, WPARAM(0), LPARAM(0));
        }
    });
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn missing_unknown_and_future_values_fall_back_to_defaults() {
        let c: Config = serde_json::from_str(r#"{"version":9,"alignment":"right","showSearch":false,"extra":{"a":1}}"#).unwrap();
        assert_eq!(c.version, 9);
        assert!(!c.show_search);
        assert!(c.show_task_view && c.show_on_all_monitors);
        assert!(!c.left_aligned());
        assert_eq!(serde_json::from_str::<Config>("{}").unwrap(), Config::default());
    }
}
