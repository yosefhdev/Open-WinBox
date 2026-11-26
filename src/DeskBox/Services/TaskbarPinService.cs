using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using DeskBox.Platform;
using Microsoft.Win32;

namespace DeskBox.Core.Persistence;

/// <summary>
/// Pins a shortcut to the taskbar by writing Explorer's own pin list
/// (HKCU\...\Explorer\Taskband) and restarting Explorer. Windows has no public
/// pin API for desktop apps and blocks the internal IPinnedList interface, so
/// this mirrors what Explorer stores for a manual pin: a copy of the .lnk in
/// "User Pinned\TaskBar", a Favorites entry (flag byte, size, PIDL rooted at
/// the User Pinned folder; the list ends with 0xFF) and, at the same index, a
/// FavoritesResolve entry (size + serialized shell link).
/// shortcut: undocumented format verified on Windows 11 build 26300; if a future
/// build rejects the entry Explorer drops it and the user pins by hand.
/// Unpackaged builds only: MSIX virtualizes HKCU and %APPDATA% writes.
/// </summary>
public static class TaskbarPinService
{
    private const string TaskbandKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband";
    private const byte ListEnd = 0xFF;

    public static string PinnedDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar");

    public static bool IsPinned(string pinnedFileName)
    {
        if (string.IsNullOrEmpty(pinnedFileName) || !File.Exists(Path.Combine(PinnedDirectory, pinnedFileName)))
        {
            return false;
        }

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(TaskbandKey);
        return key?.GetValue("Favorites") is byte[] favorites && FindEntryIndex(favorites, pinnedFileName) >= 0;
    }

    /// <summary>Pins <paramref name="shortcutPath"/> and restarts Explorer. Returns the pinned file name.</summary>
    public static string Pin(string shortcutPath, string appUserModelId)
    {
        string fileName = Path.GetFileName(shortcutPath);
        using RegistryKey key = Registry.CurrentUser.OpenSubKey(TaskbandKey, writable: true)
            ?? throw new InvalidOperationException("Taskband key not found.");
        byte[] favorites = key.GetValue("Favorites") as byte[] ?? throw new InvalidOperationException("Taskband pin list not found.");
        byte[] resolve = key.GetValue("FavoritesResolve") as byte[] ?? [];
        if (FindEntryIndex(favorites, fileName) >= 0)
        {
            return fileName;
        }

        byte[] prefix = FindTaskBarPrefix(favorites)
            ?? throw new InvalidOperationException("No existing taskbar pin to copy the folder layout from.");

        Directory.CreateDirectory(PinnedDirectory);
        string pinnedPath = Path.Combine(PinnedDirectory, fileName);
        File.Copy(shortcutPath, pinnedPath, overwrite: true);

        byte[] pidl = BuildPinnedPidl(prefix, GetLastItem(ParseDisplayName(pinnedPath)), appUserModelId, DateTime.UtcNow);
        byte[] link = File.ReadAllBytes(pinnedPath);
        key.SetValue("Favorites", AppendEntry(favorites, pidl), RegistryValueKind.Binary);
        byte[] newResolve = [.. resolve, .. BitConverter.GetBytes(link.Length), .. link];
        key.SetValue("FavoritesResolve", newResolve, RegistryValueKind.Binary);
        BumpChanges(key);
        RestartExplorer();
        return fileName;
    }

    /// <summary>Removes a pin this app created and restarts Explorer when the pin list changed.</summary>
    public static void Unpin(string pinnedFileName)
    {
        if (string.IsNullOrEmpty(pinnedFileName))
        {
            return;
        }

        bool changed = false;
        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(TaskbandKey, writable: true))
        {
            if (key?.GetValue("Favorites") is byte[] favorites &&
                key.GetValue("FavoritesResolve") is byte[] resolve &&
                FindEntryIndex(favorites, pinnedFileName) is int index and >= 0)
            {
                key.SetValue("Favorites", RemoveEntry(favorites, index), RegistryValueKind.Binary);
                key.SetValue("FavoritesResolve", RemoveResolveEntry(resolve, index), RegistryValueKind.Binary);
                BumpChanges(key);
                changed = true;
            }
        }

        try
        {
            File.Delete(Path.Combine(PinnedDirectory, pinnedFileName));
        }
        catch (Exception ex)
        {
            App.Log($"[TaskbarPin] Pinned copy cleanup failed: {ex.Message}");
        }

        if (changed)
        {
            RestartExplorer();
        }
    }

    /// <summary>Keeps the pinned copy in step with the folder's current shortcut (icon, target).</summary>
    public static void RefreshPinnedCopy(string pinnedFileName, string shortcutPath)
    {
        string pinnedPath = Path.Combine(PinnedDirectory, pinnedFileName);
        if (File.Exists(pinnedPath) && File.Exists(shortcutPath))
        {
            File.Copy(shortcutPath, pinnedPath, overwrite: true);
            Win32Helper.NotifyShellItemUpdated(pinnedPath);
        }
    }

    // ── Pin list format (pure, unit-tested) ──

    internal static List<(int Offset, int Length)> ReadEntries(byte[] favorites)
    {
        var entries = new List<(int, int)>();
        int i = 0;
        while (i < favorites.Length && favorites[i] != ListEnd)
        {
            if (i + 5 > favorites.Length)
            {
                throw new InvalidDataException("Truncated pin entry.");
            }

            int length = BitConverter.ToInt32(favorites, i + 1);
            if (length <= 0 || i + 5 + length > favorites.Length)
            {
                throw new InvalidDataException("Invalid pin entry size.");
            }

            entries.Add((i, 5 + length));
            i += 5 + length;
        }

        if (i >= favorites.Length)
        {
            throw new InvalidDataException("Pin list has no end marker.");
        }

        return entries;
    }

    internal static int FindEntryIndex(byte[] favorites, string fileName)
    {
        byte[] needle = Encoding.Unicode.GetBytes(fileName + '\0');
        List<(int Offset, int Length)> entries = ReadEntries(favorites);
        for (int index = 0; index < entries.Count; index++)
        {
            if (favorites.AsSpan(entries[index].Offset, entries[index].Length).IndexOf(needle) >= 0)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>The "User Pinned" root item plus the "TaskBar" item of an existing pin.</summary>
    internal static byte[]? FindTaskBarPrefix(byte[] favorites)
    {
        foreach ((int offset, _) in ReadEntries(favorites))
        {
            int pidl = offset + 5;
            if (favorites[pidl + 2] != 0x1F || favorites[pidl + 3] != 0x80)
            {
                continue;
            }

            int root = BitConverter.ToUInt16(favorites, pidl);
            int second = BitConverter.ToUInt16(favorites, pidl + root);
            if (second >= 22 && Encoding.ASCII.GetString(favorites, pidl + root + 14, 8) == "TaskBar\0")
            {
                return favorites[pidl..(pidl + root + second)];
            }
        }

        return null;
    }

    /// <summary>
    /// Explorer's pin PIDL: root + TaskBar items, then the .lnk file item with
    /// three extra blocks Explorer adds (UserPinned marker, pin time, AUMID).
    /// Each block ends with the item's extension-block offset, as in the file item.
    /// </summary>
    internal static byte[] BuildPinnedPidl(byte[] prefix, byte[] fileItem, string appUserModelId, DateTime pinnedUtc)
    {
        byte[] offset = fileItem[^2..];
        byte[] item =
        [
            .. fileItem,
            .. Block(0xBEEF001E, [.. BitConverter.GetBytes((ushort)2), .. Encoding.Unicode.GetBytes("UserPinned\0"), .. offset]),
            .. Block(0xBEEF002B, [.. BitConverter.GetBytes(pinnedUtc.ToFileTimeUtc()), .. offset]),
            .. Block(0xBEEF001D, [.. BitConverter.GetBytes((ushort)2), .. Encoding.Unicode.GetBytes(appUserModelId + "\0"), .. offset]),
        ];
        BitConverter.GetBytes((ushort)item.Length).CopyTo(item, 0);
        return [.. prefix, .. item, 0, 0];
    }

    private static byte[] Block(uint signature, byte[] body) =>
        [.. BitConverter.GetBytes((ushort)(8 + body.Length)), 0, 0, .. BitConverter.GetBytes(signature), .. body];

    internal static byte[] AppendEntry(byte[] favorites, byte[] pidl)
    {
        ReadEntries(favorites); // validates before anything is written
        return [.. favorites[..^1], 0, .. BitConverter.GetBytes(pidl.Length), .. pidl, ListEnd];
    }

    internal static byte[] RemoveEntry(byte[] favorites, int index)
    {
        (int offset, int length) = ReadEntries(favorites)[index];
        return [.. favorites[..offset], .. favorites[(offset + length)..]];
    }

    internal static byte[] RemoveResolveEntry(byte[] resolve, int index)
    {
        int i = 0;
        for (int n = 0; i + 4 <= resolve.Length; n++)
        {
            int length = BitConverter.ToInt32(resolve, i);
            if (length < 0 || i + 4 + length > resolve.Length)
            {
                break;
            }

            if (n == index)
            {
                return [.. resolve[..i], .. resolve[(i + 4 + length)..]];
            }

            i += 4 + length;
        }

        throw new InvalidDataException("Pin resolve list does not match the pin list.");
    }

    internal static byte[] GetLastItem(byte[] pidl)
    {
        int offset = 0;
        int last = -1;
        while (offset + 2 <= pidl.Length)
        {
            int size = BitConverter.ToUInt16(pidl, offset);
            if (size == 0)
            {
                break;
            }

            last = offset;
            offset += size;
        }

        if (last < 0)
        {
            throw new InvalidDataException("Empty PIDL.");
        }

        return pidl[last..offset];
    }

    private static byte[] ParseDisplayName(string path)
    {
        int hr = ShellIconNativeMethods.SHParseDisplayName(path, IntPtr.Zero, out IntPtr pidl, 0, out _);
        if (hr < 0 || pidl == IntPtr.Zero)
        {
            Marshal.ThrowExceptionForHR(hr < 0 ? hr : unchecked((int)0x80004005));
        }

        try
        {
            int size = ShellIconNativeMethods.ILGetSize(pidl);
            var bytes = new byte[size];
            Marshal.Copy(pidl, bytes, 0, size);
            return bytes;
        }
        finally
        {
            ShellIconNativeMethods.ILFree(pidl);
        }
    }

    private static void BumpChanges(RegistryKey key)
    {
        int changes = key.GetValue("FavoritesChanges") is int value ? value : 0;
        key.SetValue("FavoritesChanges", changes + 1, RegistryValueKind.DWord);
    }

    /// <summary>
    /// A forced kill: a normal exit makes Explorer write its in-memory pin
    /// list back over ours. Winlogon restarts the shell; start it if it does not.
    /// </summary>
    private static void RestartExplorer()
    {
        int session = Process.GetCurrentProcess().SessionId;
        foreach (Process explorer in Process.GetProcessesByName("explorer"))
        {
            using (explorer)
            {
                if (explorer.SessionId != session)
                {
                    continue;
                }

                try
                {
                    explorer.Kill();
                    explorer.WaitForExit(5000);
                }
                catch (Exception ex)
                {
                    App.Log($"[TaskbarPin] Explorer stop failed: {ex.Message}");
                }
            }
        }

        for (int i = 0; i < 30; i++)
        {
            Thread.Sleep(100);
            if (Process.GetProcessesByName("explorer").Any(p => p.SessionId == session))
            {
                return;
            }
        }

        Process.Start(new ProcessStartInfo(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"))
        {
            UseShellExecute = false
        });
    }
}
