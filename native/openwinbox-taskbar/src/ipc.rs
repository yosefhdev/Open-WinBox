//! Pipe de control `\\.\pipe\OpenWinBox.Taskbar.<SID>`. OpenWinBox conecta, escribe una orden
//! (`ping`, `reload` o `shutdown`) terminada en salto de línea y lee una línea JSON de respuesta.
//! El DACL por defecto del pipe solo deja entrar al mismo usuario (y a SYSTEM/administradores).

use windows::Win32::Foundation::*;
use windows::Win32::Security::Authorization::ConvertSidToStringSidW;
use windows::Win32::Security::*;
use windows::Win32::Storage::FileSystem::*;
use windows::Win32::System::Pipes::*;
use windows::Win32::System::Threading::*;
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::core::*;

use crate::log;

/// Pide a la barra que vuelva a leer sus archivos (`state.json`; luego `config.json`/`folders.json`).
pub const WM_APP_RELOAD: u32 = WM_APP + 1;

pub fn spawn() {
    let Some(sid) = user_sid() else {
        log("pipe: no se pudo obtener el SID del usuario; sin control remoto");
        return;
    };
    let name = HSTRING::from(format!(r"\\.\pipe\OpenWinBox.Taskbar.{sid}"));
    std::thread::spawn(move || serve(&name));
}

fn serve(name: &HSTRING) {
    loop {
        let pipe = unsafe {
            CreateNamedPipeW(
                name,
                PIPE_ACCESS_DUPLEX,
                PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
                PIPE_UNLIMITED_INSTANCES,
                512,
                512,
                0,
                None,
            )
        };
        if pipe.is_invalid() {
            log(&format!("pipe: no se pudo crear: {}", Error::from_thread()));
            return;
        }
        unsafe {
            let connected = ConnectNamedPipe(pipe, None).is_ok() || GetLastError() == ERROR_PIPE_CONNECTED;
            if connected {
                handle(pipe);
            }
            let _ = DisconnectNamedPipe(pipe);
            let _ = CloseHandle(pipe);
        }
    }
}

fn handle(pipe: HANDLE) {
    let mut buf = [0u8; 256];
    let mut read = 0u32;
    if unsafe { ReadFile(pipe, Some(&mut buf), Some(&mut read), None) }.is_err() {
        return;
    }
    let command = String::from_utf8_lossy(&buf[..read as usize]).trim().to_owned();
    let window = Some(crate::main_window());
    let reply = match command.as_str() {
        "ping" => format!(r#"{{"ok":true,"pid":{},"version":"{}"}}"#, std::process::id(), env!("CARGO_PKG_VERSION")),
        "reload" => {
            unsafe {
                let _ = PostMessageW(window, WM_APP_RELOAD, WPARAM(0), LPARAM(0));
            }
            r#"{"ok":true}"#.into()
        }
        "shutdown" => {
            log("pipe: shutdown pedido por OpenWinBox");
            unsafe {
                let _ = PostMessageW(window, WM_CLOSE, WPARAM(0), LPARAM(0));
            }
            r#"{"ok":true}"#.into()
        }
        other => format!(r#"{{"ok":false,"error":"unknown command {}"}}"#, other.escape_default()),
    };
    let line = reply + "\n";
    unsafe {
        let _ = WriteFile(pipe, Some(line.as_bytes()), None, None);
        let _ = FlushFileBuffers(pipe);
    }
}

/// SID del usuario del proceso como texto (`S-1-5-21-…`); OpenWinBox lo saca de `WindowsIdentity`.
fn user_sid() -> Option<String> {
    unsafe {
        let mut token = HANDLE::default();
        OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &mut token).ok()?;
        let mut len = 0u32;
        let _ = GetTokenInformation(token, TokenUser, None, 0, &mut len);
        let mut buf = vec![0u8; len as usize];
        let ok = GetTokenInformation(token, TokenUser, Some(buf.as_mut_ptr().cast()), len, &mut len).is_ok();
        let _ = CloseHandle(token);
        if !ok {
            return None;
        }
        let user = &*(buf.as_ptr() as *const TOKEN_USER);
        let mut text = PWSTR::null();
        ConvertSidToStringSidW(user.User.Sid, &mut text).ok()?;
        let sid = text.to_string().ok();
        let _ = LocalFree(Some(HLOCAL(text.0.cast())));
        sid
    }
}
