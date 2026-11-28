//! Lista las propiedades (SetProp) de la Shell_TrayWnd de Explorer y de sus hijas.

use windows::Win32::Foundation::*;
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::core::*;

unsafe extern "system" fn print_prop(_hwnd: HWND, name: PCWSTR, data: HANDLE, _: usize) -> BOOL {
    unsafe {
        let label = if (name.0 as usize) >> 16 == 0 {
            format!("átomo #{}", name.0 as usize)
        } else {
            name.to_string().unwrap_or_default()
        };
        println!("    {label} = 0x{:X}", data.0 as usize);
    }
    TRUE
}

unsafe extern "system" fn visit(hwnd: HWND, depth: LPARAM) -> BOOL {
    unsafe {
        let mut buf = [0u16; 64];
        let n = GetClassNameW(hwnd, &mut buf);
        println!("{}{} 0x{:X}", "  ".repeat(depth.0 as usize), String::from_utf16_lossy(&buf[..n as usize]), hwnd.0 as usize);
        EnumPropsExW(hwnd, Some(print_prop), LPARAM(0));
        if depth.0 < 3 {
            let _ = EnumChildWindows(Some(hwnd), Some(visit_direct), LPARAM(hwnd.0 as isize));
        }
    }
    TRUE
}

// EnumChildWindows recorre todos los descendientes: filtrar a hijos directos.
unsafe extern "system" fn visit_direct(hwnd: HWND, parent: LPARAM) -> BOOL {
    unsafe {
        if GetParent(hwnd).map(|p| p.0 as isize) == Ok(parent.0) {
            visit(hwnd, LPARAM(1));
        }
    }
    TRUE
}

fn main() -> Result<()> {
    unsafe {
        let tray = FindWindowW(w!("Shell_TrayWnd"), None)?;
        visit(tray, LPARAM(0));
    }
    Ok(())
}
