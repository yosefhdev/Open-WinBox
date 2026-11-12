using CommunityToolkit.WinUI.Controls;
using DeskBox.Contracts;
using DeskBox.Models;
using DeskBox.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeskBox.Views.SettingsSections;

/// <summary>System Monitor options: which sections the widget shows. Values go through <see cref="ISettingsWidgetActions"/> because the widgets live in the main process.</summary>
public sealed partial class SystemMonitorWidgetSettingsSection : UserControl
{
    private readonly Dictionary<string, ToggleSwitch> _toggles = new(StringComparer.Ordinal);
    private ISettingsWidgetActions? _actions;
    private HashSet<string> _hidden = [];
    private bool _loading;

    public SystemMonitorWidgetSettingsSection()
    {
        InitializeComponent();
    }

    public void Configure(ISettingsWidgetActions actions, LocalizationService localization)
    {
        _actions = actions;
        foreach (string id in SystemMonitorWidgetSettings.Sections)
        {
            string section = id;
            var toggle = new ToggleSwitch { MinWidth = 0, OnContent = string.Empty, OffContent = string.Empty };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle, SectionLabel(section, localization));
            toggle.Toggled += async (_, _) => await ToggleAsync(section, toggle.IsOn);
            _toggles[section] = toggle;
            SectionList.Children.Add(new SettingsCard
            {
                Header = SectionLabel(section, localization),
                HeaderIcon = new FontIcon { Glyph = SectionGlyph(section) },
                HorizontalContentAlignment = HorizontalAlignment.Right,
                Content = toggle
            });
        }

        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (_actions is null)
        {
            return;
        }

        string? stored = await _actions.GetWidgetMetadataAsync(WidgetKind.SystemMonitor, SystemMonitorWidgetSettings.MetadataKey);
        _hidden = SystemMonitorWidgetSettings.ParseHidden(stored);
        _loading = true;
        try
        {
            foreach ((string id, ToggleSwitch toggle) in _toggles)
            {
                toggle.IsOn = !_hidden.Contains(id);
            }
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ToggleAsync(string section, bool visible)
    {
        if (_loading || _actions is null)
        {
            return;
        }

        if (visible)
        {
            _hidden.Remove(section);
        }
        else
        {
            _hidden.Add(section);
        }

        try
        {
            await _actions.SetWidgetMetadataAsync(
                WidgetKind.SystemMonitor,
                SystemMonitorWidgetSettings.MetadataKey,
                SystemMonitorWidgetSettings.FormatHidden(_hidden));
        }
        catch (Exception ex)
        {
            App.Log($"[SystemMonitorSettings] Save failed: {ex}");
        }
    }

    private static string SectionLabel(string id, LocalizationService localization) => id switch
    {
        SystemMonitorWidgetSettings.Cpu => "CPU",
        SystemMonitorWidgetSettings.Memory => "RAM",
        SystemMonitorWidgetSettings.Gpu => "GPU",
        SystemMonitorWidgetSettings.Storage => localization.T("SystemMonitor.Storage"),
        SystemMonitorWidgetSettings.Network => localization.T("SystemMonitor.Network"),
        _ => localization.T("SystemMonitor.Drives")
    };

    // Segoe Fluent Icons, same glyphs the widget uses for each section.
    private static string SectionGlyph(string id) => id switch
    {
        SystemMonitorWidgetSettings.Cpu => "",
        SystemMonitorWidgetSettings.Memory => "",
        SystemMonitorWidgetSettings.Gpu => "",
        SystemMonitorWidgetSettings.Storage => "",
        SystemMonitorWidgetSettings.Network => "",
        _ => ""
    };
}
