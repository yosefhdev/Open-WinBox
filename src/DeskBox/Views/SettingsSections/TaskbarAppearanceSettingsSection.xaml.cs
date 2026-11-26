using DeskBox.Core.Persistence;
using DeskBox.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeskBox.Views.SettingsSections;

/// <summary>
/// Own taskbar add-on look: icon alignment and the Search / Task view buttons. Writes
/// taskbar/config.json; the bar reloads on its own. Install state is owned by
/// <see cref="TaskbarGeneralSettingsSection"/>, so it is re-checked each time this page is shown.
/// </summary>
public sealed partial class TaskbarAppearanceSettingsSection : UserControl
{
    private LocalizationService? _localization;
    private bool _loading;

    public TaskbarAppearanceSettingsSection()
    {
        InitializeComponent();
        // Sections are cached and only toggled; re-read state whenever the page comes back.
        RegisterPropertyChangedCallback(VisibilityProperty, (_, _) =>
        {
            if (Visibility == Visibility.Visible && _localization is not null)
            {
                _ = RefreshAsync();
            }
        });
    }

    public void Configure(LocalizationService localization)
    {
        _localization = localization;
        _ = RefreshAsync();
    }

    private string T(string key) => _localization?.T(key) ?? key;

    private async Task RefreshAsync()
    {
        TaskbarAddonState state = await Task.Run(TaskbarHostService.GetState);
        TaskbarAddonConfig config = await Task.Run(TaskbarAddonConfigStore.Load);
        bool installed = state is TaskbarAddonState.Running or TaskbarAddonState.Paused or TaskbarAddonState.Stopped;

        _loading = true;
        TaskbarAlignmentCenterItem.Content = T("Taskbar.Addon.Alignment.Center");
        TaskbarAlignmentLeftItem.Content = T("Taskbar.Addon.Alignment.Left");
        TaskbarAlignmentComboBox.SelectedItem = string.Equals(config.Alignment, "left", StringComparison.OrdinalIgnoreCase)
            ? TaskbarAlignmentLeftItem
            : TaskbarAlignmentCenterItem;
        TaskbarShowSearchToggle.IsOn = config.ShowSearch;
        TaskbarShowTaskViewToggle.IsOn = config.ShowTaskView;
        _loading = false;

        TaskbarAlignmentComboBox.IsEnabled = TaskbarShowSearchToggle.IsEnabled = TaskbarShowTaskViewToggle.IsEnabled = installed;
        NeedsInstallInfoBar.Message = T("Taskbar.Addon.NeedsInstall");
        NeedsInstallInfoBar.IsOpen = !installed;
    }

    private async void Option_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        string alignment = (TaskbarAlignmentComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "center";
        bool search = TaskbarShowSearchToggle.IsOn, taskView = TaskbarShowTaskViewToggle.IsOn;
        try
        {
            await Task.Run(() => TaskbarAddonConfigStore.Update(config =>
            {
                config.Alignment = alignment;
                config.ShowSearch = search;
                config.ShowTaskView = taskView;
            }));
        }
        catch (Exception ex)
        {
            App.Log($"[Taskbar] Saving config.json failed: {ex}");
            ErrorInfoBar.Message = string.Format(T("Taskbar.Addon.SaveError"), ex.Message);
            ErrorInfoBar.IsOpen = true;
        }
    }
}
