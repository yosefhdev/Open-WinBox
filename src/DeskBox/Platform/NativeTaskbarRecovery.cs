namespace DeskBox.Platform;

/// <summary>
/// Brings back the Explorer taskbar that the own taskbar add-on hides. The add-on writes
/// %LOCALAPPDATA%\OpenWinBox\Taskbar\native-hidden before hiding it; whoever finds the mark restores.
/// </summary>
public static class NativeTaskbarRecovery
{
    private static string HiddenMarkPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenWinBox", "Taskbar", "native-hidden");

    /// <summary>Returns true when the mark was found and the native taskbar was shown again.</summary>
    public static bool RestoreIfLeftHidden()
    {
        if (!File.Exists(HiddenMarkPath))
        {
            return false;
        }

        foreach (string className in new[] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" })
        {
            IntPtr bar = IntPtr.Zero;
            while ((bar = Win32Helper.FindWindowEx(IntPtr.Zero, bar, className, null)) != IntPtr.Zero)
            {
                Win32Helper.ShowWindow(bar, Win32Helper.SW_SHOWNOACTIVATE);
            }
        }

        try
        {
            File.Delete(HiddenMarkPath);
        }
        catch (IOException)
        {
            // The bar or its watchdog may be deleting it at the same moment; showing is what matters.
        }
        catch (UnauthorizedAccessException)
        {
        }

        return true;
    }
}
