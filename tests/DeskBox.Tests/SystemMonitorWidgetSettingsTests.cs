using DeskBox.Models;
using DeskBox.Services;

namespace DeskBox.Tests;

public sealed class SystemMonitorWidgetSettingsTests
{
    private static WidgetConfig NewConfig() => new() { WidgetKind = WidgetKind.SystemMonitor };

    [Fact]
    public void GetHidden_DefaultsToEverythingVisible()
    {
        Assert.Empty(SystemMonitorWidgetSettings.GetHidden(NewConfig()));
    }

    [Fact]
    public void SetHidden_RoundTripsInStableOrder()
    {
        var config = NewConfig();

        SystemMonitorWidgetSettings.SetHidden(config, [SystemMonitorWidgetSettings.Network, SystemMonitorWidgetSettings.Gpu]);

        Assert.Equal("Gpu,Network", config.Metadata[SystemMonitorWidgetSettings.MetadataKey]);
        Assert.Equal(
            new HashSet<string> { "Gpu", "Network" },
            SystemMonitorWidgetSettings.GetHidden(config));
    }

    [Fact]
    public void SetHidden_Empty_RemovesTheKey()
    {
        var config = NewConfig();
        SystemMonitorWidgetSettings.SetHidden(config, [SystemMonitorWidgetSettings.Cpu]);

        SystemMonitorWidgetSettings.SetHidden(config, []);

        Assert.DoesNotContain(SystemMonitorWidgetSettings.MetadataKey, config.Metadata.Keys);
    }

    [Fact]
    public void GetHidden_IgnoresUnknownIds()
    {
        var config = NewConfig();
        config.Metadata[SystemMonitorWidgetSettings.MetadataKey] = "Cpu, Bogus,,Drives";

        Assert.Equal(
            new HashSet<string> { "Cpu", "Drives" },
            SystemMonitorWidgetSettings.GetHidden(config));
    }
}
