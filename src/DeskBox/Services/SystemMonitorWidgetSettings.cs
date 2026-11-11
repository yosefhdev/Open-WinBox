using DeskBox.Models;

namespace DeskBox.Services;

/// <summary>Per-widget choice of which System Monitor sections are shown. Stored as the hidden ids, so a new section defaults to visible.</summary>
internal static class SystemMonitorWidgetSettings
{
    internal const string MetadataKey = "SystemMonitor.Hidden";

    internal const string Cpu = "Cpu";
    internal const string Memory = "Memory";
    internal const string Gpu = "Gpu";
    internal const string Storage = "Storage";
    internal const string Network = "Network";
    internal const string Drives = "Drives";

    internal static readonly IReadOnlyList<string> Sections = [Cpu, Memory, Gpu, Storage, Network, Drives];

    internal static HashSet<string> GetHidden(WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Metadata ??= [];
        // Unknown ids (older or newer builds) are dropped instead of failing.
        return config.Metadata.TryGetValue(MetadataKey, out string? value) && value is not null
            ? value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(Sections.Contains)
                .ToHashSet()
            : [];
    }

    internal static void SetHidden(WidgetConfig config, IEnumerable<string> hidden)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Metadata ??= [];
        string value = string.Join(',', Sections.Where(hidden.Contains));
        if (value.Length == 0)
        {
            config.Metadata.Remove(MetadataKey);
        }
        else
        {
            config.Metadata[MetadataKey] = value;
        }
    }
}
