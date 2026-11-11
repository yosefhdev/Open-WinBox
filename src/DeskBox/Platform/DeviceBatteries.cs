using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DeskBox.Platform;

/// <summary>
/// Battery levels for the Battery widget (OpenWinBox). Two sources:
/// (1) Razer devices, read over HID with the same read-only "get battery" /
/// "get charging" feature reports Synapse uses (protocol documented by the
/// OpenRazer project; verified against a Basilisk V4 Pro behind its dongle);
/// (2) anything Windows already publishes as a device battery property
/// (Bluetooth LE devices and other HID peripherals). Only reads, never writes
/// configuration. Call <see cref="Read"/> from a worker thread: it can block for seconds.
/// </summary>
internal sealed unsafe partial class DeviceBatteries
{
    /// <summary>Approximate: the percentage is an estimate (voltage curve, or the 4 coarse XInput levels).</summary>
    public readonly record struct Reading(string Key, string Name, int? Percent, bool Charging, bool Approximate = false);

    private const string RazerHidPrefix = @"HID\VID_1532&PID_";
    private const uint GenericReadWrite = 0xC0000000;
    private const uint ShareReadWrite = 3;
    private const uint OpenExisting = 3;
    private const int RazerPacketLength = 91; // report id + 90-byte packet
    private const byte StatusSuccess = 2;

    // Wireless Razer devices answer on transaction id 0x1F; some newer models use 0x3F or 0x9F.
    private static readonly byte[] TransactionIds = [0x1F, 0x3F, 0x9F];

    private static readonly DevPropKey BatteryLevelKey = new(new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), 2);
    private static readonly DevPropKey FriendlyNameKey = new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
    private static readonly DevPropKey DeviceDescKey = new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 2);

    private readonly LogitechBatteries _logitech = new();
    private readonly string _systemName;
    private readonly string _controllerFormat;
    private readonly Dictionary<string, (string Path, byte Tx)> _razerEndpoints = [];
    private readonly Dictionary<string, DateTime> _razerRetryAt = [];
    private readonly Dictionary<string, int> _razerLastPercent = [];

    /// <param name="systemName">Row name for the system battery or UPS.</param>
    /// <param name="controllerFormat">Row name for a game controller, with the number as {0}.</param>
    public DeviceBatteries(string systemName, string controllerFormat)
    {
        _systemName = systemName;
        _controllerFormat = controllerFormat;
    }

    public List<Reading> Read()
    {
        var ids = PresentDeviceIds();
        var readings = ReadRazer(ids);
        readings.AddRange(_logitech.Read(ids, pid => FriendlyNameFor(ids, "046D", pid, "Logi") ?? "Logitech " + pid));
        readings.AddRange(ReadControllers());
        if (ReadSystem() is { } system)
        {
            readings.Add(system);
        }

        var seen = new HashSet<string>(readings.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);
        foreach (string id in ids)
        {
            if (id.StartsWith(@"HID\VID_1532&", StringComparison.OrdinalIgnoreCase) ||
                !TryLocate(id, out uint node) ||
                GetByte(node, BatteryLevelKey) is not { } level ||
                level > 100)
            {
                continue;
            }

            string name = GetString(node, FriendlyNameKey) ?? GetString(node, DeviceDescKey) ?? id;
            // A Bluetooth device exposes the same battery on several nodes.
            if (seen.Add(name))
            {
                readings.Add(new Reading("pnp:" + name, name, level, false));
            }
        }

        return readings;
    }

    private List<Reading> ReadRazer(List<string> ids)
    {
        var readings = new List<Reading>();
        var byPid = ids
            .Where(id => id.StartsWith(RazerHidPrefix, StringComparison.OrdinalIgnoreCase))
            .GroupBy(id => id.Substring(RazerHidPrefix.Length, 4), StringComparer.OrdinalIgnoreCase);

        foreach (var group in byPid)
        {
            string pid = group.Key;
            if (!_razerEndpoints.TryGetValue(pid, out var endpoint))
            {
                if (_razerRetryAt.TryGetValue(pid, out var retryAt) && DateTime.UtcNow < retryAt)
                {
                    continue;
                }

                if (!TryDiscover(group, out endpoint))
                {
                    // Not a battery device (or Synapse is busy): look again soon, but not on every poll.
                    _razerRetryAt[pid] = DateTime.UtcNow.AddSeconds(30);
                    continue;
                }

                _razerEndpoints[pid] = endpoint;
            }

            string name = RazerName(ids, pid);
            if (Query(endpoint.Path, endpoint.Tx, 0x80, attempts: 40) is { } raw)
            {
                int percent = (int)Math.Round(raw * 100.0 / 255);
                bool charging = Query(endpoint.Path, endpoint.Tx, 0x84, attempts: 15) is > 0;
                _razerLastPercent[pid] = percent;
                readings.Add(new Reading("razer:" + pid, name, percent, charging));
            }
            else
            {
                // Synapse held the channel or the device sleeps: keep showing the last value.
                readings.Add(new Reading(
                    "razer:" + pid,
                    name,
                    _razerLastPercent.TryGetValue(pid, out int last) ? last : null,
                    false));
            }
        }

        return readings;
    }

    private static bool TryDiscover(IEnumerable<string> hidIds, out (string Path, byte Tx) endpoint)
    {
        foreach (string id in hidIds)
        {
            string path = HidPath(id);
            foreach (byte tx in TransactionIds)
            {
                // 0x1F is by far the common one and Synapse may hold the channel for ~1 s: give it more tries.
                if (Query(path, tx, 0x80, attempts: tx == TransactionIds[0] ? 40 : 5) is not null)
                {
                    endpoint = (path, tx);
                    return true;
                }
            }
        }

        endpoint = default;
        return false;
    }

    private static string RazerName(List<string> ids, string pid) =>
        FriendlyNameFor(ids, "1532", pid, "Razer") ?? "Razer " + pid;

    /// <summary>The product name Windows shows for any node of this vendor/product (the HID nodes themselves are generic).</summary>
    internal static string? FriendlyNameFor(List<string> ids, string vid, string pid, string namePrefix)
    {
        string tag = $"VID_{vid}&PID_{pid}";
        foreach (string id in ids)
        {
            if (id.Contains(tag, StringComparison.OrdinalIgnoreCase) &&
                TryLocate(id, out uint node) &&
                (GetString(node, FriendlyNameKey) ?? GetString(node, DeviceDescKey)) is { } name &&
                name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase) &&
                !name.Contains("Control Device", StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return null;
    }

    // XInput reports four coarse levels (empty/low/medium/full) for Xbox-style controllers and headsets.
    private List<Reading> ReadControllers()
    {
        var readings = new List<Reading>();
        for (uint user = 0; user < 4; user++)
        {
            if (XInputGetBatteryInformation(user, 0, out var info) != 0 ||
                info.BatteryType is 0 or 1) // disconnected or wired: no battery to show
            {
                continue;
            }

            int percent = info.BatteryLevel switch { 0 => 5, 1 => 25, 2 => 60, _ => 100 };
            readings.Add(new Reading(
                "xinput:" + user,
                string.Format(_controllerFormat, user + 1),
                percent,
                false,
                true));
        }

        return readings;
    }

    // Laptop battery, or a UPS: Windows exposes a UPS as the system battery.
    private Reading? ReadSystem()
    {
        const byte noBattery = 128;
        const byte unknown = 255;
        if (!GetSystemPowerStatus(out var status) ||
            status.BatteryFlag is noBattery or unknown ||
            status.BatteryLifePercent > 100)
        {
            return null;
        }

        return new Reading("system", _systemName, status.BatteryLifePercent, (status.BatteryFlag & 8) != 0);
    }

    private static string HidPath(string instanceId) =>
        @"\\?\" + instanceId.Replace('\\', '#') + "#{4d1e55b2-f16f-11cf-88cb-001111000030}";

    /// <summary>
    /// Sends one read-only command (class 0x07) and waits for the reply that echoes it. The control
    /// interface is shared with Synapse, so unrelated replies are skipped and the request repeated.
    /// Returns the first argument byte (args[1]) or null.
    /// </summary>
    private static byte? Query(string path, byte transactionId, byte command, int attempts)
    {
        using var handle = CreateFile(path, GenericReadWrite, ShareReadWrite, 0, OpenExisting, 0, 0);
        if (handle.IsInvalid)
        {
            return null;
        }

        var request = new byte[RazerPacketLength];
        request[2] = transactionId;
        request[6] = 2;       // data size
        request[7] = 0x07;    // command class: power
        request[8] = command;
        byte crc = 0;
        for (int i = 3; i < 89; i++)
        {
            crc ^= request[i];
        }

        request[89] = crc;

        var reply = new byte[RazerPacketLength];
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            fixed (byte* req = request)
            {
                if (!HidD_SetFeature(handle, req, (uint)request.Length))
                {
                    // Interfaces that are not the control channel refuse at once: do not retry them.
                    if (Marshal.GetLastWin32Error() == 1 && attempt == 0)
                    {
                        return null;
                    }

                    Thread.Sleep(50);
                    continue;
                }
            }

            for (int poll = 0; poll < 8; poll++)
            {
                Thread.Sleep(15);
                fixed (byte* rep = reply)
                {
                    Array.Clear(reply);
                    if (!HidD_GetFeature(handle, rep, (uint)reply.Length))
                    {
                        continue;
                    }
                }

                if (reply[7] == 0x07 && reply[8] == command && reply[2] == transactionId)
                {
                    return reply[1] == StatusSuccess ? reply[10] : null;
                }
            }
        }

        return null;
    }

    private static List<string> PresentDeviceIds()
    {
        const uint filterPresent = 0x100;
        var ids = new List<string>();
        if (CM_Get_Device_ID_List_SizeW(out uint length, null, filterPresent) != 0 || length < 2)
        {
            return ids;
        }

        var buffer = new char[length];
        fixed (char* p = buffer)
        {
            if (CM_Get_Device_ID_ListW(null, p, length, filterPresent) != 0)
            {
                return ids;
            }
        }

        // Double-null-terminated list of null-terminated strings.
        int start = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] != '\0')
            {
                continue;
            }

            if (i == start)
            {
                break;
            }

            ids.Add(new string(buffer, start, i - start));
            start = i + 1;
        }

        return ids;
    }

    private static bool TryLocate(string id, out uint node) => CM_Locate_DevNodeW(out node, id, 0) == 0;

    private static byte? GetByte(uint node, DevPropKey key)
    {
        DevPropKey k = key;
        byte value;
        uint size = 1;
        return CM_Get_DevNode_PropertyW(node, &k, out uint type, &value, ref size, 0) == 0 && type == 3 ? value : null;
    }

    private static string? GetString(uint node, DevPropKey key)
    {
        DevPropKey k = key;
        var buffer = new char[256];
        uint size = (uint)(buffer.Length * sizeof(char));
        fixed (char* p = buffer)
        {
            if (CM_Get_DevNode_PropertyW(node, &k, out uint type, (byte*)p, ref size, 0) != 0 || type != 0x12)
            {
                return null;
            }
        }

        string text = new string(buffer, 0, Math.Max((int)size / sizeof(char) - 1, 0)).Trim();
        return text.Length == 0 ? null : text;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputBattery
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct DevPropKey(Guid id, uint propertyId)
    {
        public readonly Guid Id = id;
        public readonly uint PropertyId = propertyId;
    }

    [LibraryImport("xinput1_4.dll")]
    private static partial uint XInputGetBatteryInformation(uint userIndex, byte deviceType, out XInputBattery information);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemPowerStatus(out SystemPowerStatus status);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial SafeFileHandle CreateFile(string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);

    [LibraryImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool HidD_SetFeature(SafeFileHandle handle, byte* buffer, uint length);

    [LibraryImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool HidD_GetFeature(SafeFileHandle handle, byte* buffer, uint length);

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint CM_Get_Device_ID_List_SizeW(out uint length, string? filter, uint flags);

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint CM_Get_Device_ID_ListW(string? filter, char* buffer, uint length, uint flags);

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

    [LibraryImport("cfgmgr32.dll")]
    private static partial uint CM_Get_DevNode_PropertyW(uint devInst, DevPropKey* key, out uint propertyType, byte* buffer, ref uint size, uint flags);
}
