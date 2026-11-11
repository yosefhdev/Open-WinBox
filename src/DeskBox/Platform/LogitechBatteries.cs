using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace DeskBox.Platform;

/// <summary>
/// Logitech battery levels over HID++ 2.0 (Unifying, Bolt, Lightspeed receivers, Bluetooth and
/// wired-direct devices). Read-only: it asks the device which battery feature it implements
/// (IRoot.GetFeature) and reads that feature. Protocol framing was verified on a PRO X Wireless
/// receiver; the battery features themselves follow the public Solaar documentation.
/// </summary>
internal sealed unsafe partial class LogitechBatteries
{
    private const string LogitechHidPrefix = @"HID\VID_046D&PID_";
    private const byte LongReport = 0x11;
    private const byte DirectIndex = 0xFF;
    private const int ReplyTimeoutMs = 500;

    // Battery-related HID++ 2.0 features, best first.
    private static readonly ushort[] BatteryFeatures = [0x1004, 0x1000, 0x1001, 0x1F20];

    private sealed record Endpoint(string Path, byte DeviceIndex, byte FeatureIndex, ushort FeatureId, string Name);

    private readonly Dictionary<string, bool> _isHidppInterface = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Endpoint> _endpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _retryAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _lastPercent = new(StringComparer.OrdinalIgnoreCase);

    public List<DeviceBatteries.Reading> Read(List<string> ids, Func<string, string> fallbackName)
    {
        var readings = new List<DeviceBatteries.Reading>();
        foreach (string id in ids.Where(i => i.StartsWith(LogitechHidPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            if (!_isHidppInterface.TryGetValue(id, out bool isHidpp))
            {
                _isHidppInterface[id] = isHidpp = IsHidppInterface(HidPath(id));
            }

            if (!isHidpp)
            {
                continue;
            }

            string path = HidPath(id);
            string pid = id.Substring(LogitechHidPrefix.Length, 4);
            using var channel = new HidppChannel(path);
            if (!channel.IsOpen)
            {
                continue;
            }

            // A direct device answers on 0xFF; a receiver serves up to six paired devices on 1-6.
            bool directFound = false;
            foreach (byte index in new byte[] { DirectIndex, 1, 2, 3, 4, 5, 6 })
            {
                if (directFound)
                {
                    break;
                }

                string key = $"{id}|{index}";
                if (!_endpoints.TryGetValue(key, out var endpoint))
                {
                    if (_retryAt.TryGetValue(key, out var retryAt) && DateTime.UtcNow < retryAt)
                    {
                        continue;
                    }

                    endpoint = Discover(channel, path, index, fallbackName(pid));
                    if (endpoint is null)
                    {
                        _retryAt[key] = DateTime.UtcNow.AddSeconds(30);
                        continue;
                    }

                    _endpoints[key] = endpoint;
                }

                directFound |= index == DirectIndex;
                var level = ReadLevel(channel, endpoint);
                string readingKey = $"logi:{pid}:{index}";
                if (level is { } l)
                {
                    if (l.Percent is { } known)
                    {
                        _lastPercent[readingKey] = known;
                    }

                    readings.Add(new DeviceBatteries.Reading(readingKey, endpoint.Name, l.Percent, l.Charging, l.Approximate));
                }
                else
                {
                    // Device asleep or off: keep its row with the last known value (or none).
                    readings.Add(new DeviceBatteries.Reading(
                        readingKey,
                        endpoint.Name,
                        _lastPercent.TryGetValue(readingKey, out int last) ? last : null,
                        false,
                        true));
                }
            }
        }

        return readings;
    }

    private static Endpoint? Discover(HidppChannel channel, string path, byte index, string fallbackName)
    {
        foreach (ushort feature in BatteryFeatures)
        {
            var reply = channel.Call(index, 0x00, 0x0, [(byte)(feature >> 8), (byte)feature]);
            if (reply is null)
            {
                // No answer at all: this index has no device behind it.
                return null;
            }

            if (reply.Error is not null)
            {
                // Receiver says "no device at this index" (or the device is unreachable).
                return null;
            }

            if (reply.Data[0] != 0)
            {
                string name = ReadName(channel, index) ?? (index == DirectIndex ? fallbackName : "Logitech");
                return new Endpoint(path, index, reply.Data[0], feature, name);
            }
        }

        return null;
    }

    private static (int? Percent, bool Charging, bool Approximate)? ReadLevel(HidppChannel channel, Endpoint e)
    {
        // 0x1004 UnifiedBattery uses function 1 (getStatus); the others use function 0.
        byte function = e.FeatureId == 0x1004 ? (byte)1 : (byte)0;
        var reply = channel.Call(e.DeviceIndex, e.FeatureIndex, function, []);
        if (reply is null || reply.Error is not null)
        {
            return null;
        }

        byte[] d = reply.Data;
        switch (e.FeatureId)
        {
            case 0x1004:
            {
                // [state of charge %, level bitmask, charging status, external power]
                int percent = d[0];
                if (percent == 0)
                {
                    percent = (d[1] & 8) != 0 ? 100 : (d[1] & 4) != 0 ? 50 : (d[1] & 2) != 0 ? 20 : 5;
                    return (percent, d[2] is 1 or 2, true);
                }

                return (Math.Clamp(percent, 0, 100), d[2] is 1 or 2, false);
            }

            case 0x1000:
                // [discharge level %, next level %, status]: 1 recharging, 2 almost full, 4 slow recharge
                return (Math.Clamp((int)d[0], 0, 100), d[2] is 1 or 2 or 4, false);

            default:
            {
                // 0x1001 / 0x1F20 report millivolts and flags; the percentage is estimated from a LiPo curve.
                int millivolts = (d[0] << 8) | d[1];
                bool charging = e.FeatureId == 0x1F20 ? (d[2] & 0x02) != 0 : (d[2] & 0x80) != 0;
                // The charger lifts the cell voltage (4198 mV read 100 % while the headset really had 76 %),
                // so while charging the voltage says nothing about the level.
                // shortcut: no level while charging, upgrade if a voltage-to-level table per model turns up.
                return (charging ? null : PercentFromMillivolts(millivolts), charging, true);
            }
        }
    }

    // Single-cell LiPo discharge curve (same shape Solaar uses for HID++ voltage reports).
    private static readonly (int Millivolts, int Percent)[] VoltageCurve =
    [
        (4186, 100), (4067, 90), (3989, 80), (3922, 70), (3859, 60), (3811, 50),
        (3778, 40), (3751, 30), (3717, 20), (3671, 10), (3646, 5), (3579, 2), (3500, 0)
    ];

    internal static int PercentFromMillivolts(int millivolts)
    {
        if (millivolts >= VoltageCurve[0].Millivolts)
        {
            return 100;
        }

        for (int i = 1; i < VoltageCurve.Length; i++)
        {
            var (upperMv, upperPct) = VoltageCurve[i - 1];
            var (lowerMv, lowerPct) = VoltageCurve[i];
            if (millivolts >= lowerMv)
            {
                return lowerPct + (int)Math.Round((upperPct - lowerPct) * (millivolts - lowerMv) / (double)(upperMv - lowerMv));
            }
        }

        return 0;
    }

    // Feature 0x0005 (DeviceName): function 0 = length, function 1 = 16-character chunks.
    private static string? ReadName(HidppChannel channel, byte index)
    {
        var root = channel.Call(index, 0x00, 0x0, [0x00, 0x05]);
        if (root is null || root.Error is not null || root.Data[0] == 0)
        {
            return null;
        }

        byte featureIndex = root.Data[0];
        var count = channel.Call(index, featureIndex, 0x0, []);
        if (count is null || count.Error is not null || count.Data[0] == 0)
        {
            return null;
        }

        int length = count.Data[0];
        var name = new List<byte>(length);
        while (name.Count < length)
        {
            var chunk = channel.Call(index, featureIndex, 0x1, [(byte)name.Count]);
            if (chunk is null || chunk.Error is not null)
            {
                return null;
            }

            int take = Math.Min(16, length - name.Count);
            name.AddRange(chunk.Data.Take(take));
        }

        string text = System.Text.Encoding.ASCII.GetString(name.ToArray()).TrimEnd('\0').Trim();
        return text.Length == 0 ? null : text;
    }

    private static string HidPath(string instanceId) =>
        @"\\?\" + instanceId.Replace('\\', '#') + "#{4d1e55b2-f16f-11cf-88cb-001111000030}";

    // HID++ long reports travel on a vendor-defined collection with 20-byte input and output reports.
    private static bool IsHidppInterface(string path)
    {
        using var handle = CreateFile(path, 0, 3, 0, 3, 0, 0);
        if (handle.IsInvalid || !HidD_GetPreparsedData(handle, out nint preparsed))
        {
            return false;
        }

        try
        {
            byte* caps = stackalloc byte[64];
            if (HidP_GetCaps(preparsed, caps) < 0)
            {
                return false;
            }

            ushort usagePage = *(ushort*)(caps + 2);
            ushort inLength = *(ushort*)(caps + 4);
            ushort outLength = *(ushort*)(caps + 6);
            return usagePage >= 0xFF00 && inLength == 20 && outLength == 20;
        }
        finally
        {
            HidD_FreePreparsedData(preparsed);
        }
    }

    internal sealed record HidppReply(byte[] Data, byte? Error);

    /// <summary>One overlapped HID++ long-report channel; every request/reply pair is matched by device index, feature and software id.</summary>
    private sealed class HidppChannel : IDisposable
    {
        private readonly SafeFileHandle _handle;
        private readonly nint _event;
        private byte _softwareId;

        public HidppChannel(string path)
        {
            _handle = CreateFile(path, 0xC0000000, 3, 0, 3, 0x40000000, 0); // read/write, shared, overlapped
            _event = CreateEventW(0, true, false, null);
        }

        public bool IsOpen => !_handle.IsInvalid;

        public HidppReply? Call(byte deviceIndex, byte featureIndex, byte function, byte[] parameters)
        {
            _softwareId = (byte)((_softwareId % 15) + 1);
            byte functionAndSoftwareId = (byte)((function << 4) | _softwareId);
            var request = new byte[20];
            request[0] = LongReport;
            request[1] = deviceIndex;
            request[2] = featureIndex;
            request[3] = functionAndSoftwareId;
            Array.Copy(parameters, 0, request, 4, Math.Min(parameters.Length, 16));

            if (!Write(request))
            {
                return null;
            }

            int deadline = Environment.TickCount + ReplyTimeoutMs;
            while (Environment.TickCount < deadline)
            {
                var report = ReadReport(Math.Max(deadline - Environment.TickCount, 1));
                if (report is null)
                {
                    return null;
                }

                if (report[0] != LongReport)
                {
                    continue;
                }

                // HID++ 2.0 error: [0x11, index, 0xFF, feature index, function/sw id, error code].
                // Receivers answer "unknown device" with index 0xFF whatever index was asked.
                if (report[2] == 0xFF && report[3] == featureIndex && report[4] == functionAndSoftwareId)
                {
                    return new HidppReply(report[4..], report[5]);
                }

                if (report[1] == deviceIndex && report[2] == featureIndex && report[3] == functionAndSoftwareId)
                {
                    return new HidppReply(report[4..], null);
                }
            }

            return null;
        }

        private bool Write(byte[] request)
        {
            NativeOverlapped overlapped = new() { EventHandle = _event };
            ResetEvent(_event);
            fixed (byte* p = request)
            {
                if (WriteFile(_handle, p, (uint)request.Length, out uint written, &overlapped))
                {
                    return written == request.Length;
                }

                if (Marshal.GetLastWin32Error() != 997)
                {
                    return false;
                }

                return Finish(&overlapped, 1000, out written) && written == request.Length;
            }
        }

        private byte[]? ReadReport(int timeoutMs)
        {
            var buffer = new byte[20];
            NativeOverlapped overlapped = new() { EventHandle = _event };
            ResetEvent(_event);
            fixed (byte* p = buffer)
            {
                uint read;
                if (ReadFile(_handle, p, (uint)buffer.Length, out read, &overlapped))
                {
                    return buffer;
                }

                if (Marshal.GetLastWin32Error() != 997)
                {
                    return null;
                }

                return Finish(&overlapped, timeoutMs, out read) ? buffer : null;
            }
        }

        // Waits for an overlapped operation; on timeout cancels it and waits for the cancellation so the buffer is never written afterwards.
        private bool Finish(NativeOverlapped* overlapped, int timeoutMs, out uint transferred)
        {
            if (WaitForSingleObject(_event, (uint)timeoutMs) != 0)
            {
                CancelIoEx(_handle, overlapped);
                GetOverlappedResult(_handle, overlapped, out transferred, true);
                return false;
            }

            return GetOverlappedResult(_handle, overlapped, out transferred, false);
        }

        public void Dispose()
        {
            _handle.Dispose();
            if (_event != 0)
            {
                CloseHandle(_event);
            }
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial SafeFileHandle CreateFile(string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateEventW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateEventW(nint attributes, [MarshalAs(UnmanagedType.Bool)] bool manualReset, [MarshalAs(UnmanagedType.Bool)] bool initialState, string? name);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ResetEvent(nint handle);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    [LibraryImport("kernel32.dll")]
    private static partial uint WaitForSingleObject(nint handle, uint milliseconds);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WriteFile(SafeFileHandle handle, byte* buffer, uint length, out uint written, NativeOverlapped* overlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReadFile(SafeFileHandle handle, byte* buffer, uint length, out uint read, NativeOverlapped* overlapped);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CancelIoEx(SafeFileHandle handle, NativeOverlapped* overlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetOverlappedResult(SafeFileHandle handle, NativeOverlapped* overlapped, out uint transferred, [MarshalAs(UnmanagedType.Bool)] bool wait);

    [LibraryImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool HidD_GetPreparsedData(SafeFileHandle handle, out nint preparsed);

    [LibraryImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool HidD_FreePreparsedData(nint preparsed);

    [LibraryImport("hid.dll")]
    private static partial int HidP_GetCaps(nint preparsed, byte* caps);
}
