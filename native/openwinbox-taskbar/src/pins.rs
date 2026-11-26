//! Anclados, guardados en `<raíz>/taskbar/state.json` (dueño: la barra; OpenWinBox solo lo lee).
//! La primera vez se siembran con los accesos de `User Pinned\TaskBar` de Windows.

use std::path::PathBuf;
use std::sync::OnceLock;

use serde::{Deserialize, Serialize};
use windows::Win32::System::Com::*;
use windows::Win32::UI::Shell::PropertiesSystem::IPropertyStore;
use windows::Win32::UI::Shell::*;
use windows::core::*;

use crate::log;

const STATE_VERSION: u32 = 1;

#[derive(Serialize, Deserialize, Clone, Debug, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct Pin {
    /// Igual que la clave de grupo de las ventanas: AUMID, o ruta del exe en minúsculas.
    pub key: String,
    /// Lo que se abre: un `.lnk`, un exe o `shell:AppsFolder\<AUMID>`.
    pub launch: String,
    #[serde(default)]
    pub name: String,
    /// Nombre del exe que abre (minúsculas). Casa con ventanas sin AUMID propio (VS Code, Explorador…).
    #[serde(default)]
    pub exe: String,
}

#[derive(Serialize, Deserialize, Default, Debug)]
#[serde(rename_all = "camelCase")]
pub struct State {
    #[serde(default)]
    pub version: u32,
    #[serde(default)]
    pub bar_version: String,
    #[serde(default)]
    pub pinned: Vec<Pin>,
    /// Falso si el archivo es de una versión más nueva o no se pudo leer: no lo pisamos.
    #[serde(skip)]
    writable: bool,
}

static DATA_ROOT: OnceLock<PathBuf> = OnceLock::new();

/// `--data-root` si se pasó; si no, la raíz por defecto de OpenWinBox (`%LOCALAPPDATA%\OpenWinBox`).
pub fn set_data_root(root: Option<String>) {
    let root = root.map(PathBuf::from).unwrap_or_else(|| {
        std::env::var_os("LOCALAPPDATA").map(PathBuf::from).unwrap_or_else(std::env::temp_dir).join("OpenWinBox")
    });
    let _ = DATA_ROOT.set(root);
}

/// `<raíz>/taskbar`: `state.json` (de la barra) y `config.json` (de OpenWinBox).
pub fn data_dir() -> PathBuf {
    DATA_ROOT.get().cloned().unwrap_or_default().join("taskbar")
}

fn state_path() -> PathBuf {
    data_dir().join("state.json")
}

impl State {
    pub fn load() -> Self {
        let path = state_path();
        match std::fs::read_to_string(&path) {
            Ok(text) => match serde_json::from_str::<State>(&text) {
                Ok(mut s) => {
                    backfill_exe(&mut s.pinned);
                    s.writable = s.version <= STATE_VERSION;
                    if !s.writable {
                        log(&format!("state.json es de una versión más nueva ({}); solo lectura", s.version));
                    }
                    s
                }
                Err(e) => {
                    log(&format!("state.json ilegible ({e}); se usan anclados vacíos sin sobrescribir"));
                    State::default()
                }
            },
            Err(_) => {
                let pinned = seed_from_windows();
                log(&format!("state.json no existe; sembrados {} anclados de Windows", pinned.len()));
                let s = State { pinned, writable: true, ..Default::default() };
                s.save();
                s
            }
        }
    }

    /// Escritura atómica: temporal + renombrar (en Windows reemplaza el destino).
    pub fn save(&self) {
        if !self.writable {
            return;
        }
        let path = state_path();
        let out = State { version: STATE_VERSION, bar_version: env!("CARGO_PKG_VERSION").into(), pinned: self.pinned.clone(), writable: true };
        let result = (|| -> std::io::Result<()> {
            std::fs::create_dir_all(path.parent().unwrap())?;
            let tmp = path.with_extension("json.tmp");
            std::fs::write(&tmp, serde_json::to_vec_pretty(&out)?)?;
            std::fs::rename(&tmp, &path)
        })();
        if let Err(e) = result {
            log(&format!("no se pudo guardar state.json: {e}"));
        }
    }
}

/// Accesos anclados a la barra de Windows. El orden real vive en un binario del registro
/// (`Taskband\Favorites`); usamos la fecha de creación, que suele coincidir.
/// shortcut: las apps de la Store ancladas no tienen `.lnk` y no se siembran; se anclan desde el menú.
fn seed_from_windows() -> Vec<Pin> {
    let Some(appdata) = std::env::var_os("APPDATA") else { return Vec::new() };
    let dir = PathBuf::from(appdata).join(r"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
    let Ok(entries) = std::fs::read_dir(&dir) else { return Vec::new() };
    let mut links: Vec<(std::time::SystemTime, PathBuf)> = entries
        .flatten()
        .filter(|e| e.path().extension().is_some_and(|x| x.eq_ignore_ascii_case("lnk")))
        .map(|e| (e.metadata().and_then(|m| m.created()).unwrap_or(std::time::UNIX_EPOCH), e.path()))
        .collect();
    links.sort();
    links
        .into_iter()
        .filter_map(|(_, path)| {
            let link = read_link(&path)?;
            let exe = link.exe();
            let key = link.aumid.or_else(|| link.target.map(|t| t.to_lowercase()))?;
            let exe = or_known_exe(exe, &key);
            let name = path.file_stem()?.to_string_lossy().into_owned();
            Some(Pin { key, launch: path.to_string_lossy().into_owned(), name, exe })
        })
        .collect()
}

struct Link {
    aumid: Option<String>,
    target: Option<String>,
    args: String,
}

impl Link {
    /// El exe real: Squirrel (Discord, Teams clásico…) apunta a `Update.exe --processStart X.exe`.
    fn exe(&self) -> String {
        let target = self.target.as_deref().map(crate::tasks::exe_name).unwrap_or_default();
        if target == "update.exe" {
            let mut parts = self.args.split_whitespace();
            while let Some(p) = parts.next() {
                if p.eq_ignore_ascii_case("--processStart") {
                    return parts.next().unwrap_or("").trim_matches('"').to_lowercase();
                }
            }
        }
        target
    }
}

/// Accesos sin exe de destino (apuntan a una ubicación del shell) cuyas ventanas sí tienen exe.
fn or_known_exe(exe: String, key: &str) -> String {
    if exe.is_empty() && key.eq_ignore_ascii_case("Microsoft.Windows.Explorer") { "explorer.exe".into() } else { exe }
}

/// Anclados guardados antes de existir el campo `exe`.
fn backfill_exe(pins: &mut [Pin]) {
    for p in pins.iter_mut().filter(|p| p.exe.is_empty()) {
        p.exe = if p.launch.to_lowercase().ends_with(".lnk") {
            read_link(std::path::Path::new(&p.launch)).map(|l| l.exe()).unwrap_or_default()
        } else if p.key.contains(":\\") {
            crate::tasks::exe_name(&p.key)
        } else {
            String::new()
        };
        p.exe = or_known_exe(std::mem::take(&mut p.exe), &p.key);
    }
}

fn read_link(path: &std::path::Path) -> Option<Link> {
    unsafe {
        let link: IShellLinkW = CoCreateInstance(&ShellLink, None, CLSCTX_INPROC_SERVER).ok()?;
        let wide: Vec<u16> = path.as_os_str().to_string_lossy().encode_utf16().chain([0]).collect();
        link.cast::<IPersistFile>().ok()?.Load(PCWSTR(wide.as_ptr()), STGM_READ).ok()?;
        let mut buf = [0u16; 1024];
        let target = link.GetPath(&mut buf, std::ptr::null_mut(), 0).ok().map(|_| {
            let n = buf.iter().position(|&c| c == 0).unwrap_or(0);
            String::from_utf16_lossy(&buf[..n])
        });
        let mut args = [0u16; 1024];
        let args = link.GetArguments(&mut args).ok().map(|_| {
            let n = args.iter().position(|&c| c == 0).unwrap_or(0);
            String::from_utf16_lossy(&args[..n])
        });
        let aumid = link.cast::<IPropertyStore>().ok().and_then(|s| crate::tasks::aumid_of(&s));
        Some(Link { aumid, target: target.filter(|t| !t.is_empty()), args: args.unwrap_or_default() })
    }
}

pub fn launch(target: &str) {
    let wide: Vec<u16> = target.encode_utf16().chain([0]).collect();
    let r = unsafe {
        ShellExecuteW(None, w!("open"), PCWSTR(wide.as_ptr()), None, None, windows::Win32::UI::WindowsAndMessaging::SW_SHOWNORMAL)
    };
    if r.0 as usize <= 32 {
        log(&format!("no se pudo abrir {target}: código {}", r.0 as usize));
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn unknown_fields_and_missing_ones_are_tolerated() {
        let s: State = serde_json::from_str(r#"{"version":1,"pinned":[{"key":"a","launch":"b","extra":1}],"future":true}"#).unwrap();
        assert_eq!(s.pinned, vec![Pin { key: "a".into(), launch: "b".into(), name: String::new(), exe: String::new() }]);
        let empty: State = serde_json::from_str("{}").unwrap();
        assert!(empty.pinned.is_empty());
    }
}
