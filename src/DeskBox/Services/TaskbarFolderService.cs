using System.Text.Json;
using System.Text.Json.Serialization;
using DeskBox.Helpers;
using DeskBox.Platform;
using DeskBox.Services;

// Local persistence of app-owned files (folder data, copied shortcuts,
// generated icons), so this store sits in Core.Persistence like the other
// stores: the module boundary keeps file mutation out of plain Services code.
namespace DeskBox.Core.Persistence;

public enum TaskbarFolderIconKind
{
    Default,
    Emoji,
    Image
}

public sealed class TaskbarFolderApp
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Launch path: a .lnk copied into the folder's own directory, or an executable.</summary>
    public string Path { get; set; } = string.Empty;
}

public sealed class TaskbarFolder
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public TaskbarFolderIconKind IconKind { get; set; }

    public string? Emoji { get; set; }

    /// <summary>Source image copied into the folder directory (IconKind == Image).</summary>
    public string? ImagePath { get; set; }

    /// <summary>Generated .ico the taskbar shortcut points at.</summary>
    public string? IconFile { get; set; }

    /// <summary>File name of the copy in "User Pinned\TaskBar" when OpenWinBox pinned the folder.</summary>
    public string? PinnedFileName { get; set; }

    public List<TaskbarFolderApp> Apps { get; set; } = [];
}

public sealed class TaskbarFolderDocument
{
    public int Version { get; set; } = 1;
    public List<TaskbarFolder> Folders { get; set; } = [];
}

/// <summary>An app from Start's "All apps" list offered by the folder app picker.</summary>
public sealed record TaskbarSystemApp(string Name, string ParsingName);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(TaskbarFolderDocument))]
internal sealed partial class TaskbarFolderJsonContext : JsonSerializerContext;

/// <summary>
/// Taskbar folders: groups of apps behind one pinnable shortcut. The data
/// lives in its own file (not settings.json, whose wire schema is frozen) so
/// the settings host and the main process both read and write it directly.
/// Windows offers no API to pin or to drag out of the taskbar, so each folder
/// is a Start-menu .lnk to this exe with --taskbar-folder &lt;id&gt; and its own
/// AppUserModelID; the user pins it once.
/// </summary>
public static class TaskbarFolderService
{
    public const string LaunchArgument = "--taskbar-folder";
    private const string AumidPrefix = "OpenWinBox.TaskbarFolder.";

    private static readonly object s_gate = new();

    public static string RootDirectory =>
        System.IO.Path.Combine(DeskBoxDataPathService.Current.RootPath, "taskbar-folders");

    private static string DocumentPath => System.IO.Path.Combine(RootDirectory, "folders.json");

    public static string ShortcutDirectory => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        "OpenWinBox Folders");

    public static string GetFolderDirectory(string folderId) =>
        System.IO.Path.Combine(RootDirectory, folderId);

    // ── Persistence ──

    public static TaskbarFolderDocument Load()
    {
        lock (s_gate)
        {
            try
            {
                if (File.Exists(DocumentPath))
                {
                    string json = File.ReadAllText(DocumentPath);
                    return JsonSerializer.Deserialize(json, TaskbarFolderJsonContext.Default.TaskbarFolderDocument)
                        ?? new TaskbarFolderDocument();
                }
            }
            catch (Exception ex)
            {
                App.Log($"[TaskbarFolders] Load failed: {ex.Message}");
            }

            return new TaskbarFolderDocument();
        }
    }

    public static void Save(TaskbarFolderDocument document)
    {
        lock (s_gate)
        {
            Directory.CreateDirectory(RootDirectory);
            string temp = DocumentPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(document, TaskbarFolderJsonContext.Default.TaskbarFolderDocument));
            File.Move(temp, DocumentPath, overwrite: true);
        }
    }

    public static TaskbarFolder? Find(string folderId) =>
        Load().Folders.FirstOrDefault(f => string.Equals(f.Id, folderId, StringComparison.OrdinalIgnoreCase));

    public static TaskbarFolder CreateFolder(TaskbarFolderDocument document, string name)
    {
        var folder = new TaskbarFolder
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Name = name
        };
        document.Folders.Add(folder);
        Directory.CreateDirectory(GetFolderDirectory(folder.Id));
        return folder;
    }

    public static void DeleteFolder(TaskbarFolderDocument document, TaskbarFolder folder)
    {
        document.Folders.Remove(folder);
        try
        {
            Unpin(folder);
        }
        catch (Exception ex)
        {
            App.Log($"[TaskbarFolders] Unpin on delete failed: {ex.Message}");
        }

        TryDeleteFile(GetShortcutPath(folder));
        try
        {
            string dir = GetFolderDirectory(folder.Id);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch (Exception ex)
        {
            App.Log($"[TaskbarFolders] Folder directory cleanup failed: {ex.Message}");
        }
    }

    // ── Apps ──

    /// <summary>
    /// Every app in Start's "All apps" list (Store apps included), minus the
    /// folder shortcuts this app created.
    /// </summary>
    public static IReadOnlyList<TaskbarSystemApp> GetSystemApps()
    {
        var result = new List<TaskbarSystemApp>();
        try
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AppsFolderEntry entry in AppsFolderInterop.Enumerate())
            {
                if (!entry.ParsingName.StartsWith(AumidPrefix, StringComparison.OrdinalIgnoreCase) &&
                    seen.Add(entry.ParsingName))
                {
                    result.Add(new TaskbarSystemApp(entry.Name, entry.ParsingName));
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[TaskbarFolders] All-apps scan failed: {ex.Message}");
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        return result;
    }

    /// <summary>
    /// Adds a Start app as a shortcut to its shell:AppsFolder item, the only
    /// form that also launches Store apps (they have no exe path).
    /// </summary>
    public static TaskbarFolderApp? AddSystemApp(TaskbarFolder folder, TaskbarSystemApp app)
    {
        if (folder.Apps.Any(a => string.Equals(a.Name, app.Name, StringComparison.CurrentCultureIgnoreCase)))
        {
            return null;
        }

        string dir = GetFolderDirectory(folder.Id);
        Directory.CreateDirectory(dir);
        string launchPath = UniquePath(dir, SanitizeFileName(app.Name), ".lnk");
        ShortcutHelper.CreateShellNamespaceShortcut(launchPath, "shell:AppsFolder\\" + app.ParsingName, app.Name);
        var added = new TaskbarFolderApp { Name = app.Name, Path = launchPath };
        folder.Apps.Add(added);
        return added;
    }

    /// <summary>
    /// Adds an app to the folder. Shortcuts are copied into the folder's own
    /// directory: Windows deletes the User Pinned .lnk the moment the user
    /// unpins the original, which is exactly what grouping invites.
    /// </summary>
    public static TaskbarFolderApp? AddApp(TaskbarFolder folder, string sourcePath, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        string name = string.IsNullOrWhiteSpace(displayName)
            ? System.IO.Path.GetFileNameWithoutExtension(sourcePath)
            : displayName.Trim();
        string launchPath = sourcePath;
        if (ShortcutHelper.IsShortcutPath(sourcePath))
        {
            string dir = GetFolderDirectory(folder.Id);
            Directory.CreateDirectory(dir);
            launchPath = UniquePath(dir, SanitizeFileName(name), System.IO.Path.GetExtension(sourcePath));
            File.Copy(sourcePath, launchPath);
        }

        if (folder.Apps.Any(a => string.Equals(a.Name, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            if (!string.Equals(launchPath, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(launchPath);
            }

            return null;
        }

        var app = new TaskbarFolderApp { Name = name, Path = launchPath };
        folder.Apps.Add(app);
        return app;
    }

    public static void RemoveApp(TaskbarFolder folder, TaskbarFolderApp app)
    {
        folder.Apps.Remove(app);
        if (app.Path.StartsWith(GetFolderDirectory(folder.Id), StringComparison.OrdinalIgnoreCase))
        {
            TryDeleteFile(app.Path);
        }
    }

    // ── Icon + shortcut ──

    public static string GetShortcutPath(TaskbarFolder folder) =>
        System.IO.Path.Combine(ShortcutDirectory, $"{SanitizeFileName(folder.Name)} ({folder.Id[..4]}).lnk");

    /// <summary>
    /// Stores a new icon under a fresh file name. Windows caches shortcut
    /// icons by path, so overwriting the old file would keep showing it.
    /// </summary>
    public static void SetIcon(TaskbarFolder folder, byte[] icoBytes)
    {
        string dir = GetFolderDirectory(folder.Id);
        Directory.CreateDirectory(dir);
        string? previous = folder.IconFile;
        string iconPath = System.IO.Path.Combine(dir, $"icon-{DateTime.UtcNow.Ticks}.ico");
        File.WriteAllBytes(iconPath, icoBytes);
        folder.IconFile = iconPath;
        if (!string.IsNullOrEmpty(previous) &&
            !string.Equals(previous, iconPath, StringComparison.OrdinalIgnoreCase))
        {
            TryDeleteFile(previous);
        }
    }

    /// <summary>
    /// Points every folder shortcut (and its pinned copy) at the running exe.
    /// A reinstall to another path, or a shortcut made by a dev build, would
    /// otherwise keep launching a stale or missing exe from the taskbar.
    /// Returns how many shortcuts were rewritten.
    /// </summary>
    public static int RepairShortcutTargets(TaskbarFolderDocument document)
    {
        string? exePath = Environment.ProcessPath;
        if (exePath is null)
        {
            return 0;
        }

        int repaired = 0;
        foreach (TaskbarFolder folder in document.Folders)
        {
            string shortcutPath = GetShortcutPath(folder);
            string? target = ShortcutHelper.ReadStoredMetadata(shortcutPath)?.TargetPath;
            if (target is not null && !string.Equals(target, exePath, StringComparison.OrdinalIgnoreCase))
            {
                WriteShortcut(folder);
                repaired++;
            }
        }

        return repaired;
    }

    /// <summary>
    /// Writes the pinnable shortcut. A previous shortcut under another name
    /// (rename) is removed. Each folder gets its own AppUserModelID, without
    /// which Windows treats every folder as the same app and pins only one.
    /// </summary>
    public static string WriteShortcut(TaskbarFolder folder, string? previousShortcutPath = null)
    {
        string exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Process path unknown.");
        string shortcutPath = GetShortcutPath(folder);
        Directory.CreateDirectory(ShortcutDirectory);
        if (!string.IsNullOrEmpty(previousShortcutPath) &&
            !string.Equals(previousShortcutPath, shortcutPath, StringComparison.OrdinalIgnoreCase))
        {
            TryDeleteFile(previousShortcutPath);
        }

        var metadata = new ShortcutInfo(
            exePath,
            folder.Name,
            $"{LaunchArgument} {folder.Id}",
            System.IO.Path.GetDirectoryName(exePath) ?? string.Empty,
            folder.IconFile ?? exePath,
            0);
        ShortcutHelper.WriteShortcut(shortcutPath, metadata);

        JumpListService.TrySetShortcutAppUserModelId(shortcutPath, AumidPrefix + folder.Id);
        Win32Helper.NotifyShellItemUpdated(shortcutPath);
        TaskbarPinService.RefreshPinnedCopy(PinnedName(folder), shortcutPath);

        return shortcutPath;
    }

    public static string GetAppUserModelId(TaskbarFolder folder) => AumidPrefix + folder.Id;

    /// <summary>Pins the folder shortcut (restarts Explorer). The shortcut must exist.</summary>
    public static void Pin(TaskbarFolder folder) =>
        folder.PinnedFileName = TaskbarPinService.Pin(GetShortcutPath(folder), GetAppUserModelId(folder));

    /// <summary>Also true for a folder the user pinned by hand (same shortcut file name).</summary>
    public static bool IsPinned(TaskbarFolder folder) => TaskbarPinService.IsPinned(PinnedName(folder));

    private static string PinnedName(TaskbarFolder folder) =>
        string.IsNullOrEmpty(folder.PinnedFileName) ? System.IO.Path.GetFileName(GetShortcutPath(folder)) : folder.PinnedFileName;

    /// <summary>Removes the pin OpenWinBox created (restarts Explorer if it was still pinned).</summary>
    public static void Unpin(TaskbarFolder folder)
    {
        TaskbarPinService.Unpin(PinnedName(folder));
        folder.PinnedFileName = null;
    }

    // ── Activation ──

    private static string PendingActivationPath =>
        System.IO.Path.Combine(DeskBoxDataPathService.Current.RootPath, "pending-taskbar-folder.txt");

    /// <summary>Secondary instance: leaves the clicked folder id for the running primary.</summary>
    public static void StorePendingActivation(string folderId)
    {
        try
        {
            Directory.CreateDirectory(DeskBoxDataPathService.Current.RootPath);
            File.WriteAllText(PendingActivationPath, folderId);
        }
        catch (Exception ex)
        {
            App.Log($"[TaskbarFolders] Failed to forward folder activation: {ex.Message}");
        }
    }

    /// <summary>Primary instance: takes (and clears) the forwarded folder id.</summary>
    public static string? TakePendingActivation()
    {
        try
        {
            if (!File.Exists(PendingActivationPath))
            {
                return null;
            }

            string id = File.ReadAllText(PendingActivationPath).Trim();
            File.Delete(PendingActivationPath);
            return TryGetFolderIdArgument([LaunchArgument, id]);
        }
        catch (Exception ex)
        {
            App.Log($"[TaskbarFolders] Failed to read forwarded folder activation: {ex.Message}");
            return null;
        }
    }

    /// <summary>Copies the chosen icon image into the folder and drops the previous one.</summary>
    public static void ImportImage(TaskbarFolder folder, string sourcePath)
    {
        string dir = GetFolderDirectory(folder.Id);
        Directory.CreateDirectory(dir);
        string target = System.IO.Path.Combine(dir, $"image-{DateTime.UtcNow.Ticks}{System.IO.Path.GetExtension(sourcePath)}");
        File.Copy(sourcePath, target);
        string? previous = folder.ImagePath;
        folder.ImagePath = target;
        folder.IconKind = TaskbarFolderIconKind.Image;
        TryDeleteFile(previous);
    }

    /// <summary>Returns the folder id after --taskbar-folder, or null.</summary>
    public static string? TryGetFolderIdArgument(IReadOnlyList<string> arguments)
    {
        for (int i = 0; i < arguments.Count - 1; i++)
        {
            if (string.Equals(arguments[i], LaunchArgument, StringComparison.OrdinalIgnoreCase))
            {
                string id = arguments[i + 1].Trim().Trim('"');
                return id.Length is > 0 and <= 64 && id.All(char.IsLetterOrDigit) ? id : null;
            }
        }

        return null;
    }

    // ── ICO ──

    /// <summary>
    /// Packs PNG images into an .ico container (PNG entries are valid since
    /// Windows Vista). Each entry is (pixel size, PNG bytes); 256 is written
    /// as 0 per the format.
    /// </summary>
    public static byte[] BuildIco(IReadOnlyList<(int Size, byte[] Png)> images)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)images.Count);
        int offset = 6 + (16 * images.Count);
        foreach ((int size, byte[] png) in images)
        {
            byte dimension = size >= 256 ? (byte)0 : (byte)size;
            writer.Write(dimension);
            writer.Write(dimension);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(png.Length);
            writer.Write(offset);
            offset += png.Length;
        }

        foreach ((_, byte[] png) in images)
        {
            writer.Write(png);
        }

        writer.Flush();
        return stream.ToArray();
    }

    // ── Helpers ──

    internal static string SanitizeFileName(string name)
    {
        char[] invalid = System.IO.Path.GetInvalidFileNameChars();
        string cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        return cleaned.Length == 0 ? "Folder" : cleaned.Length > 60 ? cleaned[..60] : cleaned;
    }

    private static string UniquePath(string dir, string baseName, string extension)
    {
        string candidate = System.IO.Path.Combine(dir, baseName + extension);
        for (int i = 2; File.Exists(candidate); i++)
        {
            candidate = System.IO.Path.Combine(dir, $"{baseName} ({i}){extension}");
        }

        return candidate;
    }

    private static void TryDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            App.Log($"[TaskbarFolders] Delete failed '{path}': {ex.Message}");
        }
    }
}
