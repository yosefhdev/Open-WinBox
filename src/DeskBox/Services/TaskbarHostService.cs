using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using DeskBox.Platform;
using Microsoft.Win32;

namespace DeskBox.Services;

public enum TaskbarAddonState
{
    /// <summary>openwinbox-taskbar.exe is not next to the app (build without the add-on).</summary>
    Unavailable,
    NotInstalled,
    Paused,
    /// <summary>Installed and not paused, but not answering (crashed or still starting).</summary>
    Stopped,
    Running
}

/// <summary>
/// The own taskbar add-on (native/openwinbox-taskbar). It ships next to OpenWinBox.exe and stays
/// off until the user installs it in Settings. Installed/Paused live in
/// HKCU\Software\OpenWinBox\Taskbar; the bar starts with OpenWinBox and is controlled through
/// the pipe \\.\pipe\OpenWinBox.Taskbar.&lt;user SID&gt; (ping / reload / shutdown).
/// </summary>
public static class TaskbarHostService
{
    private const string RegistryPath = @"Software\OpenWinBox\Taskbar";
    private const string ProcessName = "openwinbox-taskbar";
    private const int StopTimeoutMs = 3000;

    public static string ExePath => Path.Combine(AppContext.BaseDirectory, ProcessName + ".exe");

    private static string PipeName => "OpenWinBox.Taskbar." + WindowsIdentity.GetCurrent().User?.Value;

    public static TaskbarAddonState GetState()
    {
        if (!File.Exists(ExePath))
        {
            return TaskbarAddonState.Unavailable;
        }

        if (!ReadFlag("Installed"))
        {
            return TaskbarAddonState.NotInstalled;
        }

        if (Send("ping") is not null)
        {
            return TaskbarAddonState.Running;
        }

        return ReadFlag("Paused") ? TaskbarAddonState.Paused : TaskbarAddonState.Stopped;
    }

    /// <summary>
    /// App startup: start the bar when installed and not paused (a second bar exits on its own
    /// mutex). Otherwise make sure a bar that died did not leave the native taskbar hidden.
    /// </summary>
    public static void StartWithApp()
    {
        try
        {
            if (File.Exists(ExePath) && ReadFlag("Installed") && !ReadFlag("Paused"))
            {
                Launch();
            }
            else if (Process.GetProcessesByName(ProcessName).Length == 0)
            {
                RestoreNativeTaskbarIfLeftHidden();
            }
        }
        catch (Exception ex)
        {
            App.Log($"[Taskbar] Start with app failed: {ex.Message}");
        }
    }

    /// <summary>Install and Resume are the same: mark it on and start it.</summary>
    public static void Install()
    {
        WriteFlags(installed: true, paused: false);
        DeskBox.Core.Persistence.TaskbarAddonConfigStore.EnsureExists();
        Launch();
    }

    /// <summary>Stops the bar (the native one comes back) and keeps it off until resumed, across restarts.</summary>
    public static void Pause()
    {
        WriteFlags(installed: true, paused: true);
        Stop();
    }

    /// <summary>Stops the bar and turns the add-on off. Keeps taskbar/state.json for a later reinstall.</summary>
    public static void Uninstall()
    {
        WriteFlags(installed: false, paused: false);
        Stop();
    }

    /// <summary>Asks a running bar to re-read its files. Harmless when it is not running.</summary>
    public static void Reload() => Send("reload");

    private static void Launch()
    {
        var startInfo = new ProcessStartInfo(ExePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        startInfo.ArgumentList.Add("--data-root");
        startInfo.ArgumentList.Add(DeskBoxDataPathService.Current.RootPath);
        Process.Start(startInfo)?.Dispose();
    }

    /// <summary>
    /// Clean stop through the pipe; the bar restores the native taskbar before exiting and its
    /// watchdog follows it. If anything is still alive after the timeout, kill it and restore here.
    /// </summary>
    private static void Stop()
    {
        Send("shutdown");
        var deadline = Stopwatch.StartNew();
        foreach (Process process in Process.GetProcessesByName(ProcessName))
        {
            using (process)
            {
                int left = Math.Max(0, StopTimeoutMs - (int)deadline.ElapsedMilliseconds);
                if (!process.WaitForExit(left))
                {
                    App.Log($"[Taskbar] Bar process {process.Id} did not exit in time; killing it");
                    try
                    {
                        process.Kill();
                        process.WaitForExit(1000);
                    }
                    catch (Exception ex)
                    {
                        App.Log($"[Taskbar] Kill failed: {ex.Message}");
                    }
                }
            }
        }

        RestoreNativeTaskbarIfLeftHidden();
    }

    private static void RestoreNativeTaskbarIfLeftHidden()
    {
        if (NativeTaskbarRecovery.RestoreIfLeftHidden())
        {
            App.Log("[Taskbar] Native taskbar was left hidden; restored it");
        }
    }

    /// <summary>One command per connection; returns the bar's JSON reply line, or null if it is not running.</summary>
    private static string? Send(string command)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            pipe.Connect(500);
            byte[] request = Encoding.UTF8.GetBytes(command + "\n");
            pipe.Write(request);
            pipe.Flush();
            using var reader = new StreamReader(pipe, Encoding.UTF8);
            return reader.ReadLine();
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool ReadFlag(string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryPath);
        return key?.GetValue(name) is int value && value != 0;
    }

    private static void WriteFlags(bool installed, bool paused)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        key.SetValue("Installed", installed ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("Paused", paused ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("DataRoot", DeskBoxDataPathService.Current.RootPath, RegistryValueKind.String);
    }
}
