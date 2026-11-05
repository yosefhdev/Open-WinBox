using System.Runtime.InteropServices;

namespace DeskBox.Platform;

/// <summary>
/// Minimal Core Audio access for the Audio widget (OpenWinBox): endpoint
/// volume and mute, the default-device switch Windows Settings uses, and
/// per-app session volume. Calls go straight through the COM vtables (same
/// style as <c>IFileOperationNative</c>) so it works under Native AOT without
/// the Rust module. Every pointer is released before returning; nothing is
/// cached across calls.
/// </summary>
internal static unsafe partial class CoreAudio
{
    public enum Flow { Render = 0, Capture = 1 }

    public readonly record struct Device(string Id, string Name, float Volume, bool Muted);

    public readonly record struct Session(uint ProcessId, bool IsSystemSounds, float Volume, bool Muted);

    private static readonly Guid ClsidMMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IidMMDeviceEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
    private static readonly Guid IidAudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private static readonly Guid IidAudioSessionManager2 = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
    private static readonly Guid IidAudioSessionControl2 = new("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D");
    private static readonly Guid IidSimpleAudioVolume = new("87CE5498-68D6-44E5-9215-6DA47EF883D8");
    // IPolicyConfig is undocumented; it is what Windows Settings and the Game Bar use to change the default device.
    private static readonly Guid ClsidPolicyConfig = new("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9");
    private static readonly Guid IidPolicyConfig = new("F8679F50-850A-41CF-9C72-430F290290C8");
    private static readonly Guid FriendlyNameFormat = new("A45C254E-DF1C-4EFD-8020-67D146A850E0");

    private const uint ClsCtxAll = 23;
    private const uint DeviceStateActive = 1;
    private const int RoleMultimedia = 1;
    private const int AudioSessionStateExpired = 2;
    private const ushort VtLpwstr = 31;

    public static Device? GetDefault(Flow flow)
    {
        using var enumerator = CreateEnumerator();
        using var device = GetDefaultDevice(enumerator.Pointer, flow);
        return device.Pointer == IntPtr.Zero ? null : Describe(device.Pointer);
    }

    public static List<Device> GetActiveDevices(Flow flow)
    {
        var devices = new List<Device>();
        using var enumerator = CreateEnumerator();
        IntPtr collection;
        // IMMDeviceEnumerator::EnumAudioEndpoints
        Check(((delegate* unmanaged[Stdcall]<IntPtr, int, uint, IntPtr*, int>)Fn(enumerator.Pointer, 3))(
            enumerator.Pointer, (int)flow, DeviceStateActive, &collection));
        using var ownedCollection = new ComPtr(collection);
        uint count;
        // IMMDeviceCollection::GetCount / Item
        Check(((delegate* unmanaged[Stdcall]<IntPtr, uint*, int>)Fn(collection, 3))(collection, &count));
        for (uint i = 0; i < count; i++)
        {
            IntPtr device;
            if (((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)Fn(collection, 4))(collection, i, &device) < 0)
            {
                continue;
            }

            using var owned = new ComPtr(device);
            devices.Add(Describe(device));
        }

        return devices;
    }

    public static void SetVolume(string deviceId, float scalar)
    {
        using var volume = OpenEndpointVolume(deviceId);
        // IAudioEndpointVolume::SetMasterVolumeLevelScalar
        Check(((delegate* unmanaged[Stdcall]<IntPtr, float, Guid*, int>)Fn(volume.Pointer, 7))(
            volume.Pointer, Math.Clamp(scalar, 0f, 1f), null));
    }

    public static void SetMute(string deviceId, bool muted)
    {
        using var volume = OpenEndpointVolume(deviceId);
        // IAudioEndpointVolume::SetMute
        Check(((delegate* unmanaged[Stdcall]<IntPtr, int, Guid*, int>)Fn(volume.Pointer, 14))(
            volume.Pointer, muted ? 1 : 0, null));
    }

    public static void SetDefault(string deviceId)
    {
        Guid clsid = ClsidPolicyConfig, iid = IidPolicyConfig;
        IntPtr policy;
        Check(CoCreateInstance(&clsid, IntPtr.Zero, ClsCtxAll, &iid, &policy));
        using var owned = new ComPtr(policy);
        fixed (char* id = deviceId)
        {
            // IPolicyConfig::SetDefaultEndpoint for roles 0 console, 1 multimedia, 2 communications,
            // as when picking a device in Windows.
            for (int role = 0; role < 3; role++)
            {
                Check(((delegate* unmanaged[Stdcall]<IntPtr, char*, int, int>)Fn(policy, 13))(policy, id, role));
            }
        }
    }

    /// <summary>Sessions of the default render device; expired sessions are skipped.</summary>
    public static List<Session> GetSessions()
    {
        var sessions = new List<Session>();
        ForEachSession((volume, pid, system) =>
        {
            float level;
            int mute;
            // ISimpleAudioVolume::GetMasterVolume / GetMute
            Check(((delegate* unmanaged[Stdcall]<IntPtr, float*, int>)Fn(volume, 4))(volume, &level));
            Check(((delegate* unmanaged[Stdcall]<IntPtr, int*, int>)Fn(volume, 6))(volume, &mute));
            sessions.Add(new Session(pid, system, level, mute != 0));
        });
        return sessions;
    }

    /// <summary>Applies volume and/or mute to every session that <paramref name="match"/> accepts.</summary>
    public static void SetSessions(Func<uint, bool, bool> match, float? scalar, bool? muted)
    {
        ForEachSession((volume, pid, system) =>
        {
            if (!match(pid, system))
            {
                return;
            }

            if (scalar is float level)
            {
                // ISimpleAudioVolume::SetMasterVolume
                Check(((delegate* unmanaged[Stdcall]<IntPtr, float, Guid*, int>)Fn(volume, 3))(
                    volume, Math.Clamp(level, 0f, 1f), null));
            }

            if (muted is bool mute)
            {
                // ISimpleAudioVolume::SetMute
                Check(((delegate* unmanaged[Stdcall]<IntPtr, int, Guid*, int>)Fn(volume, 5))(volume, mute ? 1 : 0, null));
            }
        });
    }

    private static void ForEachSession(Action<IntPtr, uint, bool> visit)
    {
        using var enumerator = CreateEnumerator();
        using var device = GetDefaultDevice(enumerator.Pointer, Flow.Render);
        if (device.Pointer == IntPtr.Zero)
        {
            return;
        }

        using var manager = Activate(device.Pointer, IidAudioSessionManager2);
        IntPtr sessionEnumerator;
        // IAudioSessionManager2::GetSessionEnumerator
        Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)Fn(manager.Pointer, 5))(manager.Pointer, &sessionEnumerator));
        using var ownedEnumerator = new ComPtr(sessionEnumerator);
        int count;
        // IAudioSessionEnumerator::GetCount / GetSession
        Check(((delegate* unmanaged[Stdcall]<IntPtr, int*, int>)Fn(sessionEnumerator, 3))(sessionEnumerator, &count));
        for (int i = 0; i < count; i++)
        {
            IntPtr control;
            if (((delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr*, int>)Fn(sessionEnumerator, 4))(sessionEnumerator, i, &control) < 0)
            {
                continue;
            }

            using var ownedControl = new ComPtr(control);
            int state;
            // IAudioSessionControl::GetState
            if (((delegate* unmanaged[Stdcall]<IntPtr, int*, int>)Fn(control, 3))(control, &state) < 0 ||
                state == AudioSessionStateExpired)
            {
                continue;
            }

            using var control2 = QueryInterface(control, IidAudioSessionControl2);
            using var volume = QueryInterface(control, IidSimpleAudioVolume);
            if (control2.Pointer == IntPtr.Zero || volume.Pointer == IntPtr.Zero)
            {
                continue;
            }

            uint pid = 0;
            // IAudioSessionControl2::GetProcessId; IsSystemSoundsSession returns S_OK (0) only for system sounds.
            ((delegate* unmanaged[Stdcall]<IntPtr, uint*, int>)Fn(control2.Pointer, 14))(control2.Pointer, &pid);
            bool system = ((delegate* unmanaged[Stdcall]<IntPtr, int>)Fn(control2.Pointer, 15))(control2.Pointer) == 0;
            try
            {
                visit(volume.Pointer, pid, system);
            }
            catch (COMException)
            {
                // The session ended between enumeration and the call.
            }
        }
    }

    private static Device Describe(IntPtr device)
    {
        IntPtr idPointer;
        // IMMDevice::GetId
        Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)Fn(device, 5))(device, &idPointer));
        string id = Marshal.PtrToStringUni(idPointer) ?? string.Empty;
        Marshal.FreeCoTaskMem(idPointer);

        float level = 0;
        int mute = 0;
        using (var volume = Activate(device, IidAudioEndpointVolume))
        {
            // IAudioEndpointVolume::GetMasterVolumeLevelScalar / GetMute
            ((delegate* unmanaged[Stdcall]<IntPtr, float*, int>)Fn(volume.Pointer, 9))(volume.Pointer, &level);
            ((delegate* unmanaged[Stdcall]<IntPtr, int*, int>)Fn(volume.Pointer, 15))(volume.Pointer, &mute);
        }

        return new Device(id, ReadFriendlyName(device) ?? id, level, mute != 0);
    }

    private static string? ReadFriendlyName(IntPtr device)
    {
        IntPtr store;
        // IMMDevice::OpenPropertyStore(STGM_READ)
        if (((delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr*, int>)Fn(device, 4))(device, 0, &store) < 0)
        {
            return null;
        }

        using var ownedStore = new ComPtr(store);
        var key = new PropertyKey { FormatId = FriendlyNameFormat, PropertyId = 14 };
        var value = default(PropVariant);
        try
        {
            // IPropertyStore::GetValue(PKEY_Device_FriendlyName)
            if (((delegate* unmanaged[Stdcall]<IntPtr, PropertyKey*, PropVariant*, int>)Fn(store, 5))(store, &key, &value) < 0 ||
                value.Type != VtLpwstr)
            {
                return null;
            }

            return Marshal.PtrToStringUni(value.Pointer);
        }
        finally
        {
            PropVariantClear(&value);
        }
    }

    private static ComPtr GetDefaultDevice(IntPtr enumerator, Flow flow)
    {
        IntPtr device;
        // IMMDeviceEnumerator::GetDefaultAudioEndpoint; fails with E_NOTFOUND when there is no device.
        int hr = ((delegate* unmanaged[Stdcall]<IntPtr, int, int, IntPtr*, int>)Fn(enumerator, 4))(
            enumerator, (int)flow, RoleMultimedia, &device);
        return new ComPtr(hr < 0 ? IntPtr.Zero : device);
    }

    private static ComPtr OpenEndpointVolume(string deviceId)
    {
        using var enumerator = CreateEnumerator();
        IntPtr device;
        fixed (char* id = deviceId)
        {
            // IMMDeviceEnumerator::GetDevice
            Check(((delegate* unmanaged[Stdcall]<IntPtr, char*, IntPtr*, int>)Fn(enumerator.Pointer, 5))(enumerator.Pointer, id, &device));
        }

        using var ownedDevice = new ComPtr(device);
        return Activate(device, IidAudioEndpointVolume);
    }

    private static ComPtr CreateEnumerator()
    {
        Guid clsid = ClsidMMDeviceEnumerator, iid = IidMMDeviceEnumerator;
        IntPtr pointer;
        Check(CoCreateInstance(&clsid, IntPtr.Zero, ClsCtxAll, &iid, &pointer));
        return new ComPtr(pointer);
    }

    private static ComPtr Activate(IntPtr device, Guid iid)
    {
        IntPtr result;
        // IMMDevice::Activate
        Check(((delegate* unmanaged[Stdcall]<IntPtr, Guid*, uint, IntPtr, IntPtr*, int>)Fn(device, 3))(
            device, &iid, ClsCtxAll, IntPtr.Zero, &result));
        return new ComPtr(result);
    }

    private static ComPtr QueryInterface(IntPtr pointer, Guid iid) =>
        new(Marshal.QueryInterface(pointer, in iid, out IntPtr result) < 0 ? IntPtr.Zero : result);

    private static void* Fn(IntPtr pointer, int slot) => (*(void***)pointer)[slot];

    private static void Check(int hr)
    {
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }
    }

    private readonly struct ComPtr(IntPtr pointer) : IDisposable
    {
        public IntPtr Pointer { get; } = pointer;

        public void Dispose()
        {
            if (Pointer != IntPtr.Zero)
            {
                Marshal.Release(Pointer);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public uint PropertyId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public IntPtr Pointer;
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(Guid* clsid, IntPtr outer, uint context, Guid* iid, IntPtr* result);

    [LibraryImport("ole32.dll")]
    private static partial int PropVariantClear(PropVariant* value);
}
