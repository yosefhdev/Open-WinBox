using DeskBox.Models;
using DeskBox.Services;

namespace DeskBox.Tests;

public sealed class BatteryWidgetSettingsTests
{
    private static WidgetConfig NewConfig() => new() { WidgetKind = WidgetKind.Battery };

    [Fact]
    public void GetStyle_DefaultsToBar()
    {
        Assert.Equal(BatteryWidgetSettings.Bar, BatteryWidgetSettings.GetStyle(NewConfig()));
    }

    [Theory]
    [InlineData(BatteryWidgetSettings.Ring)]
    [InlineData(BatteryWidgetSettings.Battery)]
    [InlineData(BatteryWidgetSettings.Segments)]
    public void SetStyle_RoundTrips(string style)
    {
        var config = NewConfig();
        BatteryWidgetSettings.SetStyle(config, style);
        Assert.Equal(style, BatteryWidgetSettings.GetStyle(config));
    }

    [Fact]
    public void SetStyle_BarClearsTheKey_AndUnknownValuesFallBack()
    {
        var config = NewConfig();
        BatteryWidgetSettings.SetStyle(config, BatteryWidgetSettings.Ring);
        BatteryWidgetSettings.SetStyle(config, BatteryWidgetSettings.Bar);
        Assert.DoesNotContain(BatteryWidgetSettings.MetadataKey, config.Metadata!.Keys);

        config.Metadata[BatteryWidgetSettings.MetadataKey] = "Hologram";
        Assert.Equal(BatteryWidgetSettings.Bar, BatteryWidgetSettings.GetStyle(config));
    }
}
