using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OpenWinBox;

// Un widget = una ventana; con varias carpetas se muestran como pestañas (los "grupos" del DeskBox original)
public class WidgetConfig
{
    // "File" (carpetas) o un widget funcional: "Music", "Glance"
    public string Kind { get; set; } = "File";
    // Widget funcional apagado en Configuración: se conserva para recordar su posición al reactivarlo
    public bool IsDisabled { get; set; }
    public string? Name { get; set; }
    public List<string> Folders { get; set; } = new();
    public int ActiveIndex { get; set; }
    // Bloqueado: no se puede mover (sí redimensionar)
    public bool IsLocked { get; set; }
    public int X { get; set; } = 100;
    public int Y { get; set; } = 100;
    public int Width { get; set; } = 400;
    public int Height { get; set; } = 500;
    // Por carpeta: orden manual (nombres de archivo; los nuevos van al final por nombre),
    // ícono de título ("emoji:🎨" o "image:<ruta>"; sin entrada = carpeta azul) y nombre visible
    public Dictionary<string, List<string>> Order { get; set; } = new();
    public Dictionary<string, string> Icons { get; set; } = new();
    public Dictionary<string, string> Titles { get; set; } = new();
}

public record WeatherPlace(string Name, double Latitude, double Longitude);

// Opciones de cada widget funcional (Configuración › Widgets funcionales › <widget>)
public class MusicOptions
{
    public string Style { get; set; } = "Standard"; // "Standard", "Compact" o "Backdrop" (carátula de fondo)
    public bool ShowArtwork { get; set; } = true;
    public bool ShowArtist { get; set; } = true;
    public bool ShowStatus { get; set; } = true;
    public bool ShowProgress { get; set; } = true;
    public bool ShowControls { get; set; } = true;
}

public class GlanceOptions
{
    public bool ShowDate { get; set; } = true;
    public bool ShowWeekday { get; set; } = true;
    public bool ShowYear { get; set; }
    public bool ShowSecondClock { get; set; }
    // Sin valor guardado: el formato del sistema
    public bool Use24Hour { get; set; } = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains('H');
    public string SecondTimeZone { get; set; } = "UTC"; // Id de TimeZoneInfo
    public string? SecondClockLabel { get; set; }
    public string? LocalClockLabel { get; set; } // con reloj dual; vacío = ciudad de la zona del sistema
}

public class WeatherOptions
{
    public bool ShowFeelsLike { get; set; } = true;
    public bool ShowHumidity { get; set; } = true;
    public bool ShowWind { get; set; } = true;
    public bool ShowRain { get; set; } = true;
}

public class AppConfig
{
    public List<WidgetConfig> Widgets { get; set; } = new();
    // Widgets siempre detrás de las demás ventanas, sobre el escritorio
    public bool PinToDesktop { get; set; } = true;
    // Tiempo sobre la barra de otro widget para unirlos como pestañas (Configuración › Widgets de archivos)
    public int MergeDelayMs { get; set; } = 800;
    // "Default" (sistema), "Light" o "Dark": nombres de Microsoft.UI.Xaml.ElementTheme
    public string Theme { get; set; } = "Default";
    // Lang.FollowWindows o un código de Lang.Available; las configs anteriores siguen a Windows
    public string Language { get; set; } = Lang.FollowWindows;
    public bool HasCompletedOnboarding { get; set; }
    // Canal de actualizaciones; null = según la versión instalada (quien usa una beta sigue en betas)
    public bool? UpdateBetas { get; set; }
    // Ciudad elegida para el widget Tiempo; null = automática (Windows o aproximada por IP)
    public WeatherPlace? WeatherLocation { get; set; }
    public MusicOptions Music { get; set; } = new();
    public MusicOptions Spotify { get; set; } = new();
    public GlanceOptions Glance { get; set; } = new();
    public WeatherOptions Weather { get; set; } = new();
    // El orden manual y los emojis del original se agregaron después; importarlos una vez a configs existentes
    public bool ImportedOrderAndIcons { get; set; }

    public static readonly string[] FeatureKinds = { "Music", "Glance", "Weather", "Spotify", "Audio" };

    // Tamaño inicial de cada widget funcional (los del original)
    private static (int Width, int Height) DefaultSize(string kind) => kind switch
    {
        "Glance" => (912, 173),
        "Weather" => (380, 200),
        "Audio" => (380, 440),
        _ => (378, 218),
    };

    // Widget funcional de ese tipo (existe siempre tras Load; puede estar desactivado)
    public WidgetConfig Feature(string kind) => Widgets.First(w => w.Kind == kind);

    public static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenWinBox", "config.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppConfig Load()
    {
        MigrateFromDeskBox2();
        AppConfig? config = null;
        try
        {
            if (File.Exists(FilePath)) config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath));
        }
        catch (JsonException) { } // archivo corrupto: arrancar con valores por defecto

        if (config == null)
        {
            // Primer arranque: traer los widgets de carpeta del DeskBox original si está instalado
            config = ImportFromDeskBox() ?? new AppConfig();
            if (config.Widgets.Count == 0)
                config.Widgets.Add(new WidgetConfig { Folders = { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) } });
        }
        if (!config.ImportedOrderAndIcons)
        {
            ImportOrderAndIcons(config);
            config.ImportedOrderAndIcons = true;
        }
        // Cada widget funcional nuevo en esta versión se importa del original (solo agrega los que faltan)
        if (FeatureKinds.Any(k => config.Widgets.All(w => w.Kind != k))) ImportFeatureWidgets(config);
        // Un registro por widget funcional; los que no vienen del original empiezan apagados
        foreach (var kind in FeatureKinds.Where(k => config.Widgets.All(w => w.Kind != k)))
        {
            var (width, height) = DefaultSize(kind);
            config.Widgets.Add(new WidgetConfig { Kind = kind, IsDisabled = true, Width = width, Height = height });
        }
        config.Save();
        return config;
    }

    // La app se llamaba "DeskBox 2.0" y guardaba todo en %LOCALAPPDATA%\DeskBox2: moverlo una vez y
    // corregir las rutas guardadas (carpetas de "Nuevo widget", íconos importados)
    private static void MigrateFromDeskBox2()
    {
        var newDir = Path.GetDirectoryName(FilePath)!;
        var oldDir = Path.Combine(Path.GetDirectoryName(newDir)!, "DeskBox2");
        if (Directory.Exists(newDir) || !Directory.Exists(oldDir)) return;
        try { Directory.Move(oldDir, newDir); }
        catch (IOException) { return; } // algún archivo en uso: seguir con los datos viejos hasta el próximo arranque
        if (!File.Exists(FilePath)) return;
        string Escaped(string path) => JsonSerializer.Serialize(path)[1..^1];
        File.WriteAllText(FilePath, File.ReadAllText(FilePath).Replace(Escaped(oldDir), Escaped(newDir), StringComparison.OrdinalIgnoreCase));
    }

    private static string OriginalLayoutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskBox", "data", "widget-layout.json");

    // Del original: posición/tamaño de Música y Glance y si están encendidos (layout.featureWidgetEnabledStates)
    private static void ImportFeatureWidgets(AppConfig config)
    {
        if (!File.Exists(OriginalLayoutPath)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(OriginalLayoutPath));
            var layout = doc.RootElement.GetProperty("layout");
            var enabled = layout.TryGetProperty("featureWidgetEnabledStates", out var states) ? states : default;
            foreach (var w in layout.GetProperty("widgets").EnumerateArray())
            {
                var kind = w.GetProperty("widgetKind").GetString();
                if (!FeatureKinds.Contains(kind) || config.Widgets.Any(c => c.Kind == kind)) continue;
                config.Widgets.Add(new WidgetConfig
                {
                    Kind = kind!,
                    IsDisabled = enabled.ValueKind == JsonValueKind.Object && enabled.TryGetProperty(kind!, out var on) && !on.GetBoolean(),
                    X = (int)w.GetProperty("x").GetDouble(),
                    Y = (int)w.GetProperty("y").GetDouble(),
                    Width = (int)w.GetProperty("width").GetDouble(),
                    Height = (int)w.GetProperty("height").GetDouble(),
                });
            }
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or IOException) { }
    }

    // Del original: metadata.FileStackGroupOrder = "[\"Item:C:\\\\RUTA\\\\NOMBRE.LNK\", ...]" y metadata.TitleIconEmoji
    private static void ImportOrderAndIcons(AppConfig config)
    {
        if (!File.Exists(OriginalLayoutPath)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(OriginalLayoutPath));
            foreach (var w in doc.RootElement.GetProperty("layout").GetProperty("widgets").EnumerateArray())
            {
                if (!w.TryGetProperty("mappedFolderPath", out var p) || p.ValueKind != JsonValueKind.String) continue;
                var folder = p.GetString()!;
                var target = config.Widgets.FirstOrDefault(c => c.Folders.Contains(folder, StringComparer.OrdinalIgnoreCase));
                if (target == null || !w.TryGetProperty("metadata", out var meta) || meta.ValueKind != JsonValueKind.Object) continue;
                var key = target.Folders.First(f => f.Equals(folder, StringComparison.OrdinalIgnoreCase));

                if (meta.TryGetProperty("FileStackGroupOrder", out var order) && order.ValueKind == JsonValueKind.String
                    && JsonSerializer.Deserialize<List<string>>(order.GetString()!) is { } entries)
                    target.Order[key] = entries.Where(e => e.StartsWith("Item:")).Select(e => Path.GetFileName(e[5..])).ToList();
                if (meta.TryGetProperty("TitleIconEmoji", out var emoji) && emoji.GetString() is { Length: > 0 } emojiText)
                    target.Icons[key] = "emoji:" + emojiText;
            }
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or IOException) { }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        // Escribir a .tmp y renombrar evita dejar un JSON a medias si la app se cierra al guardar
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Options));
        File.Move(tmp, FilePath, overwrite: true);
    }

    // Lee %LOCALAPPDATA%\DeskBox\data\widget-layout.json (DeskBox 1.5.x); solo widgets "File" con carpeta mapeada
    private static AppConfig? ImportFromDeskBox()
    {
        var path = OriginalLayoutPath;
        if (!File.Exists(path)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var layout = doc.RootElement.GetProperty("layout");
            var files = layout.GetProperty("widgets").EnumerateArray()
                .Where(w => w.GetProperty("widgetKind").GetString() == "File"
                            && w.TryGetProperty("mappedFolderPath", out var p) && p.ValueKind == JsonValueKind.String)
                .ToDictionary(w => w.GetProperty("id").GetString()!);

            var config = new AppConfig();
            if (layout.TryGetProperty("widgetGroups", out var groups))
            {
                foreach (var g in groups.EnumerateArray())
                {
                    var members = g.GetProperty("memberIds").EnumerateArray()
                        .Select(m => m.GetString()!).Where(files.ContainsKey).ToList();
                    if (members.Count == 0) continue;
                    var widget = FromOriginal(files[members[0]], members.Select(id => files[id]));
                    widget.Name = g.GetProperty("name").GetString();
                    widget.ActiveIndex = Math.Max(0, members.IndexOf(g.GetProperty("activeMemberId").GetString()!));
                    config.Widgets.Add(widget);
                    members.ForEach(id => files.Remove(id));
                }
            }
            foreach (var w in files.Values)
            {
                var widget = FromOriginal(w, new[] { w });
                if (!w.GetProperty("isDefaultTitle").GetBoolean()) widget.Name = w.GetProperty("name").GetString();
                config.Widgets.Add(widget);
            }
            return config;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or IOException)
        {
            return null; // formato distinto al esperado: arrancar sin importar
        }
    }

    private static WidgetConfig FromOriginal(JsonElement bounds, IEnumerable<JsonElement> members) => new()
    {
        Folders = members.Select(m => m.GetProperty("mappedFolderPath").GetString()!).ToList(),
        X = (int)bounds.GetProperty("x").GetDouble(),
        Y = (int)bounds.GetProperty("y").GetDouble(),
        Width = (int)bounds.GetProperty("width").GetDouble(),
        Height = (int)bounds.GetProperty("height").GetDouble(),
    };
}
