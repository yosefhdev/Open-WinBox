using System.Runtime.InteropServices;

namespace DeskBox.Platform;

/// <summary>One entry of Start's "All apps" list (shell:AppsFolder).</summary>
internal sealed record AppsFolderEntry(string Name, string ParsingName);

/// <summary>
/// Enumerates shell:AppsFolder — the same list as Start's "All apps", Store
/// apps included — through raw vtable calls (AOT-safe, see <see cref="ComVtable"/>).
/// Slots from ShObjIdl_core.h: IShellItem BindToHandler 3, GetDisplayName 5;
/// IEnumShellItems Next 3.
/// </summary>
internal static class AppsFolderInterop
{
    private static readonly Guid s_folderIdAppsFolder = new("1e87508d-89c2-42f0-8a7e-645a0f50ca58");
    private static readonly Guid s_iidIShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
    private static readonly Guid s_bhidEnumItems = new("94f60519-2850-4924-aa5a-d15e84868039");
    private static readonly Guid s_iidIEnumShellItems = new("70629033-e363-4a28-a567-0db78006e6d7");

    private const int SigdnNormalDisplay = 0;
    private const int SigdnParentRelativeParsing = unchecked((int)0x80018001);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int BindToHandlerInvoker(IntPtr self, IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDisplayNameInvoker(IntPtr self, int sigdn, out IntPtr name);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int NextInvoker(IntPtr self, uint celt, out IntPtr item, out uint fetched);

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderItem(ref Guid rfid, uint flags, IntPtr token, ref Guid riid, out IntPtr ppv);

    internal static List<AppsFolderEntry> Enumerate()
    {
        var result = new List<AppsFolderEntry>();
        IntPtr folder = IntPtr.Zero;
        IntPtr enumerator = IntPtr.Zero;
        try
        {
            Guid folderId = s_folderIdAppsFolder;
            Guid iidItem = s_iidIShellItem;
            if (SHGetKnownFolderItem(ref folderId, 0, IntPtr.Zero, ref iidItem, out folder) < 0 || folder == IntPtr.Zero)
            {
                return result;
            }

            Guid bhid = s_bhidEnumItems;
            Guid iidEnum = s_iidIEnumShellItems;
            if (ComVtable.Slot<BindToHandlerInvoker>(folder, 3)(folder, IntPtr.Zero, ref bhid, ref iidEnum, out enumerator) < 0 ||
                enumerator == IntPtr.Zero)
            {
                return result;
            }

            NextInvoker next = ComVtable.Slot<NextInvoker>(enumerator, 3);
            while (next(enumerator, 1, out IntPtr item, out uint fetched) == 0 && fetched == 1 && item != IntPtr.Zero)
            {
                try
                {
                    string? name = GetDisplayName(item, SigdnNormalDisplay);
                    string? parsing = GetDisplayName(item, SigdnParentRelativeParsing);
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(parsing))
                    {
                        result.Add(new AppsFolderEntry(name, parsing));
                    }
                }
                finally
                {
                    Marshal.Release(item);
                }
            }
        }
        finally
        {
            if (enumerator != IntPtr.Zero)
            {
                Marshal.Release(enumerator);
            }

            if (folder != IntPtr.Zero)
            {
                Marshal.Release(folder);
            }
        }

        return result;
    }

    private static string? GetDisplayName(IntPtr item, int sigdn)
    {
        if (ComVtable.Slot<GetDisplayNameInvoker>(item, 5)(item, sigdn, out IntPtr text) < 0 || text == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(text);
        }
        finally
        {
            Marshal.FreeCoTaskMem(text);
        }
    }
}
