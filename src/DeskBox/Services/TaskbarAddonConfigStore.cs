using System.Text.Json;
using System.Text.Json.Serialization;
using DeskBox.Services;

// Owns <data root>/taskbar/config.json, read by the own taskbar add-on (native/openwinbox-taskbar,
// src/config.rs). Kept out of settings.json on purpose, like taskbar-folders/folders.json.
namespace DeskBox.Core.Persistence;

public sealed class TaskbarAddonConfig
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>"center" (Windows 11 style) or "left".</summary>
    public string Alignment { get; set; } = "center";

    public bool ShowSearch { get; set; } = true;

    public bool ShowTaskView { get; set; } = true;

    /// <summary>False = no bar on secondary monitors, like Windows' "show on all displays".</summary>
    public bool ShowOnAllMonitors { get; set; } = true;
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TaskbarAddonConfig))]
internal sealed partial class TaskbarAddonConfigJsonContext : JsonSerializerContext;

public static class TaskbarAddonConfigStore
{
    private static string ConfigPath => Path.Combine(DeskBoxDataPathService.Current.RootPath, "taskbar", "config.json");

    public static TaskbarAddonConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                return JsonSerializer.Deserialize(File.ReadAllText(ConfigPath), TaskbarAddonConfigJsonContext.Default.TaskbarAddonConfig)
                    ?? new TaskbarAddonConfig();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            App.Log($"[Taskbar] config.json unreadable, using defaults: {ex.Message}");
        }

        return new TaskbarAddonConfig();
    }

    /// <summary>
    /// Atomic write (temp + move). A file from a newer app version is left alone, as with settings.json:
    /// rewriting it would drop the fields this version does not know.
    /// </summary>
    public static void Save(TaskbarAddonConfig config)
    {
        if (config.Version > TaskbarAddonConfig.CurrentVersion)
        {
            App.Log($"[Taskbar] config.json is version {config.Version}; not overwriting it");
            return;
        }

        config.Version = TaskbarAddonConfig.CurrentVersion;
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        string temp = ConfigPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(config, TaskbarAddonConfigJsonContext.Default.TaskbarAddonConfig));
        File.Move(temp, ConfigPath, overwrite: true);
    }

    /// <summary>Writes the defaults on install so the file is there to inspect and edit.</summary>
    public static void EnsureExists()
    {
        if (!File.Exists(ConfigPath))
        {
            Save(new TaskbarAddonConfig());
        }
    }
}
