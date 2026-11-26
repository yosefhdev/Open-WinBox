//! Iconos de sistema de la bandeja (red, volumen, batería), como el grupo de ajustes rápidos de
//! Windows 11. En Windows 11 no son iconos de notificación, así que se leen del sistema y se dibujan
//! con glifos de Segoe Fluent Icons. Se sondea: volumen cada segundo, red cada tres.

use std::cell::RefCell;

use windows::Win32::Media::Audio::Endpoints::IAudioEndpointVolume;
use windows::Win32::Media::Audio::*;
use windows::Win32::NetworkManagement::WiFi::*;
use windows::Win32::Networking::NetworkListManager::*;
use windows::Win32::System::Com::*;
use windows::Win32::System::Power::{GetSystemPowerStatus, SYSTEM_POWER_STATUS};
use windows::Win32::Foundation::HANDLE;

#[derive(Clone, Copy, PartialEq, Default, Debug)]
pub enum Net {
    #[default]
    Unknown,
    Disconnected,
    Ethernet { internet: bool },
    Wifi { quality: u32, internet: bool },
}

#[derive(Clone, Copy, PartialEq, Default, Debug)]
pub struct Status {
    pub net: Net,
    /// (nivel 0–1, silenciado); `None` = sin dispositivo de salida.
    pub volume: Option<(f32, bool)>,
    /// (porcentaje, enchufado); `None` = sin batería (sobremesa).
    pub battery: Option<(u8, bool)>,
}

#[derive(Default)]
struct Sources {
    status: Status,
    tick: u32,
    devices: Option<IMMDeviceEnumerator>,
    nlm: Option<INetworkListManager>,
    /// `Some(None)` = no hay servicio WLAN (no reintentar).
    wlan: Option<Option<HANDLE>>,
}

thread_local! {
    static SOURCES: RefCell<Sources> = RefCell::default();
}

pub fn current() -> Status {
    SOURCES.with_borrow(|s| s.status)
}

/// Sondea; verdadero si algo cambió y hay que redibujar.
pub fn poll() -> bool {
    SOURCES.with_borrow_mut(|s| {
        let mut next = s.status;
        next.volume = volume(s);
        if s.tick % 3 == 0 {
            next.net = network(s);
            next.battery = battery();
        }
        s.tick = s.tick.wrapping_add(1);
        let changed = next != s.status;
        s.status = next;
        changed
    })
}

fn endpoint(s: &mut Sources) -> Option<IAudioEndpointVolume> {
    unsafe {
        if s.devices.is_none() {
            s.devices = CoCreateInstance(&MMDeviceEnumerator, None, CLSCTX_ALL).ok();
        }
        // El dispositivo por defecto puede cambiar (auriculares): pedirlo cada vez.
        let device = s.devices.as_ref()?.GetDefaultAudioEndpoint(eRender, eMultimedia).ok()?;
        device.Activate::<IAudioEndpointVolume>(CLSCTX_ALL, None).ok()
    }
}

fn volume(s: &mut Sources) -> Option<(f32, bool)> {
    let ep = endpoint(s)?;
    unsafe { Some((ep.GetMasterVolumeLevelScalar().ok()?, ep.GetMute().ok()?.as_bool())) }
}

/// Rueda sobre los iconos de sistema: ±2 % por muesca, como Windows.
pub fn adjust_volume(notches: f32) {
    SOURCES.with_borrow_mut(|s| {
        let Some(ep) = endpoint(s) else { return };
        unsafe {
            let level = ep.GetMasterVolumeLevelScalar().unwrap_or(0.0);
            let next = (level + notches * 0.02).clamp(0.0, 1.0);
            let _ = ep.SetMasterVolumeLevelScalar(next, std::ptr::null());
            if next > 0.0 {
                let _ = ep.SetMute(false, std::ptr::null());
            }
        }
        s.status.volume = volume(s);
    });
}

fn network(s: &mut Sources) -> Net {
    unsafe {
        if s.nlm.is_none() {
            s.nlm = CoCreateInstance(&NetworkListManager, None, CLSCTX_ALL).ok();
        }
        let Some(c) = s.nlm.as_ref().and_then(|n| n.GetConnectivity().ok()) else { return Net::Unknown };
        if c == NLM_CONNECTIVITY_DISCONNECTED {
            return Net::Disconnected;
        }
        let internet = c.0 & (NLM_CONNECTIVITY_IPV4_INTERNET.0 | NLM_CONNECTIVITY_IPV6_INTERNET.0) != 0;
        match wifi_quality(s) {
            Some(quality) => Net::Wifi { quality, internet },
            None => Net::Ethernet { internet },
        }
    }
}

/// Calidad de señal (0–100) de la interfaz Wi-Fi conectada, si hay.
fn wifi_quality(s: &mut Sources) -> Option<u32> {
    unsafe {
        let handle = *s.wlan.get_or_insert_with(|| {
            let mut version = 0;
            let mut h = HANDLE::default();
            (WlanOpenHandle(2, None, &mut version, &mut h) == 0).then_some(h)
        });
        let handle = handle?;
        let mut list: *mut WLAN_INTERFACE_INFO_LIST = std::ptr::null_mut();
        if WlanEnumInterfaces(handle, None, &mut list) != 0 || list.is_null() {
            return None;
        }
        let count = (*list).dwNumberOfItems as usize;
        let infos = std::slice::from_raw_parts((*list).InterfaceInfo.as_ptr(), count);
        let mut quality = None;
        for info in infos.iter().filter(|i| i.isState == wlan_interface_state_connected) {
            let mut size = 0u32;
            let mut data: *mut std::ffi::c_void = std::ptr::null_mut();
            if WlanQueryInterface(handle, &info.InterfaceGuid, wlan_intf_opcode_current_connection, None, &mut size, &mut data, None) == 0 && !data.is_null() {
                let attrs = &*(data as *const WLAN_CONNECTION_ATTRIBUTES);
                quality = Some(attrs.wlanAssociationAttributes.wlanSignalQuality);
                WlanFreeMemory(data);
                break;
            }
        }
        WlanFreeMemory(list as *const std::ffi::c_void);
        quality
    }
}

fn battery() -> Option<(u8, bool)> {
    let mut p = SYSTEM_POWER_STATUS::default();
    unsafe { GetSystemPowerStatus(&mut p) }.ok()?;
    // 128 = sin batería; 255 = estado desconocido.
    (p.BatteryFlag & 128 == 0 && p.BatteryFlag != 255 && p.BatteryLifePercent <= 100).then_some((p.BatteryLifePercent, p.ACLineStatus == 1))
}

/// Glifos (Segoe Fluent Icons) en orden: red, volumen y batería si hay. Cada uno con su glifo de
/// "fondo" atenuado (Wi-Fi: los arcos que faltan), o `None`.
pub fn glyphs(s: &Status) -> Vec<(char, Option<char>)> {
    let mut out = Vec::with_capacity(3);
    let net = match s.net {
        Net::Wifi { internet: false, .. } => ('\u{EB5E}', None),
        Net::Wifi { quality, .. } => {
            let bars = match quality {
                75.. => '\u{E701}',
                50.. => '\u{E874}',
                25.. => '\u{E873}',
                _ => '\u{E872}',
            };
            (bars, Some('\u{E701}'))
        }
        Net::Ethernet { internet: true } => ('\u{E839}', None),
        Net::Ethernet { internet: false } => ('\u{EB55}', None),
        Net::Disconnected => ('\u{F384}', None),
        Net::Unknown => ('\u{E774}', None),
    };
    out.push(net);
    let vol = match s.volume {
        None => '\u{E74F}',
        Some((_, true)) => '\u{E74F}',
        Some((l, _)) if l <= 0.0 => '\u{E992}',
        Some((l, _)) if l < 0.34 => '\u{E993}',
        Some((l, _)) if l < 0.67 => '\u{E994}',
        _ => '\u{E995}',
    };
    out.push((vol, None));
    if let Some((pct, plugged)) = s.battery {
        let step = (pct as u32 / 10).min(10);
        let c = match (plugged, step) {
            (false, 10) => 0xE83F,
            (true, 10) => 0xEA93,
            (false, n) => 0xE850 + n,
            (true, n) => 0xE85A + n,
        };
        out.push((char::from_u32(c).unwrap_or('\u{E850}'), None));
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn glyphs_follow_state() {
        let s = Status { net: Net::Wifi { quality: 30, internet: true }, volume: Some((0.5, false)), battery: Some((100, true)) };
        assert_eq!(glyphs(&s), vec![('\u{E873}', Some('\u{E701}')), ('\u{E994}', None), ('\u{EA93}', None)]);
        let s = Status { net: Net::Ethernet { internet: true }, volume: Some((0.9, true)), battery: None };
        assert_eq!(glyphs(&s), vec![('\u{E839}', None), ('\u{E74F}', None)]);
        let s = Status { net: Net::Disconnected, volume: None, battery: Some((45, false)) };
        assert_eq!(glyphs(&s)[2], ('\u{E854}', None));
    }
}
