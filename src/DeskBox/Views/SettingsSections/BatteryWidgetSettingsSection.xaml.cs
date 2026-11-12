using DeskBox.Contracts;
using DeskBox.Models;
using DeskBox.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeskBox.Views.SettingsSections;

/// <summary>Battery widget options. The widgets live in the main process, so values go through <see cref="ISettingsWidgetActions"/>.</summary>
public sealed partial class BatteryWidgetSettingsSection : UserControl
{
    private ISettingsWidgetActions? _actions;
    private bool _loading;

    public BatteryWidgetSettingsSection()
    {
        InitializeComponent();
    }

    public void Configure(ISettingsWidgetActions actions, LocalizationService localization)
    {
        _actions = actions;
        foreach (string style in BatteryWidgetSettings.Styles)
        {
            StyleComboBox.Items.Add(new ComboBoxItem { Tag = style, Content = localization.T("Battery.Style." + style) });
        }

        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (_actions is null)
        {
            return;
        }

        string? stored = await _actions.GetWidgetMetadataAsync(WidgetKind.Battery, BatteryWidgetSettings.MetadataKey);
        string style = BatteryWidgetSettings.Styles.Contains(stored ?? string.Empty)
            ? stored!
            : BatteryWidgetSettings.Bar;
        _loading = true;
        try
        {
            StyleComboBox.SelectedIndex = BatteryWidgetSettings.Styles.ToList().IndexOf(style);
        }
        finally
        {
            _loading = false;
        }
    }

    private async void StyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _actions is null || StyleComboBox.SelectedItem is not ComboBoxItem { Tag: string style })
        {
            return;
        }

        try
        {
            await _actions.SetWidgetMetadataAsync(WidgetKind.Battery, BatteryWidgetSettings.MetadataKey, style);
        }
        catch (Exception ex)
        {
            App.Log($"[BatterySettings] Save failed: {ex}");
        }
    }
}
