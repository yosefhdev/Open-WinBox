using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DeskBox.Platform;

/// <summary>
/// Samples CPU, memory, GPU, disk and network load for the System Monitor widget.
/// Plain Win32 (GetSystemTimes, GlobalMemoryStatusEx) plus PDH for the counters
/// Task Manager shows (GPU engines, disk activity), so it works under Native AOT
/// without PerformanceCounter. Rate-based values need two samples: the first
/// <see cref="Sample"/> after creation reports zeros. Call it from a worker thread.
/// </summary>
internal sealed unsafe partial class SystemMetrics : IDisposable
{
    public readonly record struct DriveUsage(string Name, long Total, long Free);

    public sealed record Snapshot(
        double Cpu,
        double MemoryPercent,
        ulong MemoryUsed,
        ulong MemoryTotal,
        double? Gpu,
        double? GpuMemoryBytes,
        double? DiskActive,
        double? DiskBytesPerSecond,
        double NetDownBytesPerSecond,
        double NetUpBytesPerSecond,
        IReadOnlyList<DriveUsage> Drives);

    private const uint PdhFormatDouble = 0x200;
    private const uint PdhMoreData = 0x800007D2;

    private readonly object _gate = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private nint _query;
    private nint _gpuEngine, _gpuMemory, _diskIdle, _diskBytes;
    private ulong _lastIdle, _lastBusy;
    private long _lastRx, _lastTx;
    private double _lastNetSeconds;
    private bool _disposed;

    public SystemMetrics()
    {
        if (PdhOpenQueryW(null, 0, out _query) != 0)
        {
            _query = 0;
            return;
        }

        // A counter that does not exist (no GPU, counters disabled) stays 0 and its value is reported as null.
        _gpuEngine = AddCounter(@"\GPU Engine(*)\Utilization Percentage");
        _gpuMemory = AddCounter(@"\GPU Adapter Memory(*)\Dedicated Usage");
        _diskIdle = AddCounter(@"\PhysicalDisk(*)\% Idle Time");
        _diskBytes = AddCounter(@"\PhysicalDisk(*)\Disk Bytes/sec");
        PdhCollectQueryData(_query);
        (_lastIdle, _lastBusy) = ReadCpuTimes();
        (_lastRx, _lastTx) = ReadNetwork();
        _lastNetSeconds = _clock.Elapsed.TotalSeconds;
    }

    public static string? CpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return (key?.GetValue("ProcessorNameString") as string)?.Trim();
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    /// <summary>Returns null once disposed. Skipped parts come back null or empty; the counters stay registered, so showing them again needs no warm-up.</summary>
    public Snapshot? Sample(bool includeGpu = true, bool includeDisk = true, bool includeDrives = true)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return null;
            }

            var (idle, busy) = ReadCpuTimes();
            ulong total = (idle - _lastIdle) + (busy - _lastBusy);
            double cpu = total == 0 ? 0 : (busy - _lastBusy) * 100.0 / total;
            (_lastIdle, _lastBusy) = (idle, busy);

            var memory = new MemoryStatusEx { Length = (uint)sizeof(MemoryStatusEx) };
            GlobalMemoryStatusEx(ref memory);
            ulong memoryUsed = memory.TotalPhys - memory.AvailPhys;

            double seconds = _clock.Elapsed.TotalSeconds;
            var (rx, tx) = ReadNetwork();
            double elapsed = Math.Max(seconds - _lastNetSeconds, 0.001);
            double down = Math.Max(rx - _lastRx, 0) / elapsed;
            double up = Math.Max(tx - _lastTx, 0) / elapsed;
            (_lastRx, _lastTx, _lastNetSeconds) = (rx, tx, seconds);

            if (_query != 0)
            {
                PdhCollectQueryData(_query);
            }

            return new Snapshot(
                Math.Clamp(cpu, 0, 100),
                memory.TotalPhys == 0 ? 0 : memoryUsed * 100.0 / memory.TotalPhys,
                memoryUsed,
                memory.TotalPhys,
                includeGpu ? ReadGpu() : null,
                includeGpu ? Sum(_gpuMemory) : null,
                includeDisk ? ReadDiskActive() : null,
                includeDisk ? Total(_diskBytes) : null,
                down,
                up,
                includeDrives ? ReadDrives() : []);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_query != 0)
            {
                PdhCloseQuery(_query);
                _query = 0;
            }
        }
    }

    private nint AddCounter(string path) =>
        PdhAddEnglishCounterW(_query, path, 0, out nint counter) == 0 ? counter : 0;

    private static (ulong Idle, ulong Busy) ReadCpuTimes()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return (0, 0);
        }

        // Kernel time includes idle time.
        return (idle.Value, kernel.Value + user.Value - idle.Value);
    }

    private static (long Rx, long Tx) ReadNetwork()
    {
        long rx = 0, tx = 0;
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up ||
                    nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                try
                {
                    var stats = nic.GetIPv4Statistics();
                    rx += stats.BytesReceived;
                    tx += stats.BytesSent;
                }
                catch (NetworkInformationException)
                {
                    // Adapter without IPv4 statistics.
                }
            }
        }
        catch (NetworkInformationException)
        {
            // The network stack is restarting; the next sample recovers.
        }

        return (rx, tx);
    }

    private static List<DriveUsage> ReadDrives()
    {
        var drives = new List<DriveUsage>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                {
                    drives.Add(new DriveUsage(drive.Name.TrimEnd('\\'), drive.TotalSize, drive.AvailableFreeSpace));
                }
            }
            catch (IOException)
            {
                // Drive removed or locked while reading.
            }
        }

        return drives;
    }

    // Same rule as Task Manager: sum the engines of each type, then show the busiest type.
    private double? ReadGpu()
    {
        var items = Read(_gpuEngine);
        if (items is null)
        {
            return null;
        }

        var byType = new Dictionary<string, double>();
        foreach (var (name, value) in items)
        {
            int at = name.IndexOf("engtype_", StringComparison.OrdinalIgnoreCase);
            string type = at < 0 ? string.Empty : name[(at + 8)..];
            byType[type] = byType.GetValueOrDefault(type) + value;
        }

        return byType.Count == 0 ? 0 : Math.Clamp(byType.Values.Max(), 0, 100);
    }

    private double? ReadDiskActive() =>
        Instance(_diskIdle, "_Total") is { } idle ? Math.Clamp(100 - idle, 0, 100) : null;

    private double? Total(nint counter) => Instance(counter, "_Total");

    private double? Sum(nint counter) => Read(counter)?.Sum(item => item.Value);

    private static double? Instance(nint counter, string instance) =>
        Read(counter)?.FirstOrDefault(item => item.Name == instance) is { Name: not null } hit ? hit.Value : null;

    // PDH_FMT_COUNTERVALUE_ITEM_W is 24 bytes on 64-bit: name pointer, status (+ padding), double value.
    private static List<(string Name, double Value)>? Read(nint counter)
    {
        if (counter == 0)
        {
            return null;
        }

        uint size = 0;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            uint status = PdhGetFormattedCounterArrayW(counter, PdhFormatDouble, ref size, out uint count, 0);
            if (status != PdhMoreData && status != 0)
            {
                return null;
            }

            byte* buffer = (byte*)NativeMemory.Alloc(Math.Max(size, 1u));
            try
            {
                status = PdhGetFormattedCounterArrayW(counter, PdhFormatDouble, ref size, out count, (nint)buffer);
                if (status == PdhMoreData)
                {
                    continue; // the instance list grew between the two calls
                }

                if (status != 0)
                {
                    return null;
                }

                var items = new List<(string, double)>((int)count);
                for (uint i = 0; i < count; i++)
                {
                    byte* item = buffer + i * 24;
                    uint valueStatus = *(uint*)(item + 8);
                    if (valueStatus is 0 or 1) // valid or new data; the first rate sample is not valid yet
                    {
                        items.Add((Marshal.PtrToStringUni(*(nint*)item) ?? string.Empty, *(double*)(item + 16)));
                    }
                }

                return items;
            }
            finally
            {
                NativeMemory.Free(buffer);
            }
        }

        return null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
        public readonly ulong Value => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    [LibraryImport("pdh.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint PdhOpenQueryW(string? dataSource, nint userData, out nint query);

    [LibraryImport("pdh.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint PdhAddEnglishCounterW(nint query, string path, nint userData, out nint counter);

    [LibraryImport("pdh.dll")]
    private static partial uint PdhCollectQueryData(nint query);

    [LibraryImport("pdh.dll")]
    private static partial uint PdhGetFormattedCounterArrayW(nint counter, uint format, ref uint bufferSize, out uint itemCount, nint buffer);

    [LibraryImport("pdh.dll")]
    private static partial uint PdhCloseQuery(nint query);
}
