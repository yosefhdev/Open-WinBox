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
    /// `"acrylic"`, `"blur"`, `"solid"` (opaco, ignora la opacidad) o `"transparent"` (tinte sin desenfoque).
    pub background: String,
    /// `"#RRGGBB"`.
    pub tint_color: String,
    /// 0–100.
    pub tint_opacity: u32,
    /// Lado del icono a 96 DPI, 16–40.
    pub icon_size: u32,
    /// Espacio extra entre botones a 96 DPI, 0–24.
    pub spacing: u32,
    /// Radio del resaltado de los botones a 96 DPI, 0–12.
    pub corner_radius: u32,
    /// Alto de la barra a 96 DPI, 32–96; 0 = el de la barra de Windows.
    pub height: u32,
    /// Separación de la barra respecto a los bordes izquierdo, derecho e inferior a 96 DPI, 0–48.
    pub margin: u32,
    /// Radio de las esquinas de la barra a 96 DPI, 0–32.
    pub bar_radius: u32,
    /// `"full"` (de lado a lado) o `"auto"` (se ajusta a los iconos, estilo dock).
    pub width: String,
    pub show_border: bool,
    pub show_clock: bool,
}

impl Default for Config {
    fn default() -> Self {
        Self {
            version: 1,
            alignment: "center".into(),
            show_search: true,
            show_task_view: true,
            show_on_all_monitors: true,
            background: "acrylic".into(),
            tint_color: "#202020".into(),
            tint_opacity: 80,
            icon_size: 24,
            spacing: 0,
            corner_radius: 4,
            height: 0,
            margin: 0,
            bar_radius: 0,
            width: "full".into(),
            show_border: false,
            show_clock: true,
        }
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

    /// `ACCENT_STATE` de `SetWindowCompositionAttribute`; un valor desconocido es acrílico.
    pub fn accent_state(&self) -> u32 {
        match self.background.to_ascii_lowercase().as_str() {
            "solid" => 1,
            "transparent" => 2,
            "blur" => 3,
            _ => 4,
        }
    }

    /// Color del tinte; uno mal escrito vuelve al gris oscuro por defecto.
    pub fn tint_rgb(&self) -> (u8, u8, u8) {
        let hex = self.tint_color.trim().trim_start_matches('#');
        let byte = |i: usize| hex.get(i..i + 2).and_then(|h| u8::from_str_radix(h, 16).ok());
        match (hex.len(), byte(0), byte(2), byte(4)) {
            (6, Some(r), Some(g), Some(b)) => (r, g, b),
            _ => (0x20, 0x20, 0x20),
        }
    }

    pub fn tint_alpha(&self) -> u8 {
        (self.tint_opacity.min(100) * 255 / 100) as u8
    }

    /// Iconos y texto oscuros cuando el fondo es claro y lo bastante opaco para que se note.
    pub fn dark_foreground(&self) -> bool {
        let (r, g, b) = self.tint_rgb();
        let luminance = (0.2126 * r as f32 + 0.7152 * g as f32 + 0.0722 * b as f32) / 255.0;
        luminance > 0.6 && (self.accent_state() == 1 || self.tint_opacity >= 50)
    }

    pub fn icon_px(&self) -> f32 {
        self.icon_size.clamp(16, 40) as f32
    }

    pub fn spacing_px(&self) -> f32 {
        self.spacing.min(24) as f32
    }

    pub fn radius_px(&self) -> f32 {
        self.corner_radius.min(12) as f32
    }

    /// Alto propio a 96 DPI, o `None` para usar el de la barra de Windows.
    pub fn height_px(&self) -> Option<f32> {
        (self.height != 0).then(|| self.height.clamp(32, 96) as f32)
    }

    pub fn margin_px(&self) -> f32 {
        self.margin.min(48) as f32
    }

    pub fn bar_radius_px(&self) -> f32 {
        self.bar_radius.min(32) as f32
    }

    pub fn auto_width(&self) -> bool {
        self.width.eq_ignore_ascii_case("auto")
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

    #[test]
    fn appearance_values_are_clamped_and_bad_colors_fall_back() {
        let c: Config = serde_json::from_str(
            r##"{"background":"Solid","tintColor":"#F0F0F0","tintOpacity":250,"iconSize":99,"spacing":40,"cornerRadius":50}"##,
        )
        .unwrap();
        assert_eq!(c.accent_state(), 1);
        assert_eq!(c.tint_rgb(), (0xF0, 0xF0, 0xF0));
        assert_eq!(c.tint_alpha(), 255);
        assert!(c.dark_foreground());
        assert_eq!((c.icon_px(), c.spacing_px(), c.radius_px()), (40.0, 24.0, 12.0));
        let bad: Config = serde_json::from_str(r#"{"background":"mica","tintColor":"red","iconSize":2}"#).unwrap();
        assert_eq!(bad.accent_state(), 4);
        assert_eq!(bad.tint_rgb(), (0x20, 0x20, 0x20));
        assert!(!bad.dark_foreground());
        assert_eq!(bad.icon_px(), 16.0);
    }

    #[test]
    fn dock_geometry_is_clamped_and_zero_height_means_native() {
        let c = Config::default();
        assert_eq!((c.height_px(), c.margin_px(), c.bar_radius_px(), c.auto_width()), (None, 0.0, 0.0, false));
        let dock: Config = serde_json::from_str(r#"{"height":200,"margin":99,"barRadius":64,"width":"AUTO"}"#).unwrap();
        assert_eq!((dock.height_px(), dock.margin_px(), dock.bar_radius_px(), dock.auto_width()), (Some(96.0), 48.0, 32.0, true));
        let small: Config = serde_json::from_str(r#"{"height":10}"#).unwrap();
        assert_eq!(small.height_px(), Some(32.0));
    }
}
