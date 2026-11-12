using DeskBox.Models;

namespace DeskBox.Services;

/// <summary>Per-widget choice of how battery levels are drawn. Unknown values (older or newer builds) fall back to the bar.</summary>
internal static class BatteryWidgetSettings
{
    internal const string MetadataKey = "Battery.Style";

    internal const string Bar = "Bar";
    internal const string Ring = "Ring";
    internal const string Battery = "Battery";
    internal const string Segments = "Segments";

    internal static readonly IReadOnlyList<string> Styles = [Bar, Ring, Battery, Segments];

    internal static string GetStyle(WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Metadata is not null
            && config.Metadata.TryGetValue(MetadataKey, out string? value)
            && value is not null
            && Styles.Contains(value)
            ? value
            : Bar;
    }

    internal static void SetStyle(WidgetConfig config, string style)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Metadata ??= [];
        if (style == Bar || !Styles.Contains(style))
        {
            config.Metadata.Remove(MetadataKey);
        }
        else
        {
            config.Metadata[MetadataKey] = style;
        }
    }
}
