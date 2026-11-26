using DeskBox.Core.Persistence;
using DeskBox.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeskBox.Views.SettingsSections;

/// <summary>
/// Own taskbar add-on (<see cref="TaskbarHostService"/>): install, pause, resume, uninstall,
/// plus the options that decide where it shows. Appearance options live in
/// <see cref="TaskbarAppearanceSettingsSection"/>; both write taskbar/config.json, which the
/// bar watches and reloads on its own.
/// </summary>
public sealed partial class TaskbarGeneralSettingsSection : UserControl
{
    private LocalizationService? _localization;
    private bool _loading;

    public TaskbarGeneralSettingsSection()
    {
        InitializeComponent();
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
        TaskbarAddonCard.Description = T(state switch
        {
            TaskbarAddonState.Running => "Taskbar.Addon.State.Running",
            TaskbarAddonState.Paused => "Taskbar.Addon.State.Paused",
            TaskbarAddonState.Stopped => "Taskbar.Addon.State.Stopped",
            _ => "Taskbar.Addon.State.NotInstalled"
        });
        bool installed = state is TaskbarAddonState.Running or TaskbarAddonState.Paused or TaskbarAddonState.Stopped;
        TaskbarAddonStartButton.Visibility = state == TaskbarAddonState.Running ? Visibility.Collapsed : Visibility.Visible;
        TaskbarAddonStartText.Text = T(installed ? "Taskbar.Addon.Resume" : "Taskbar.Addon.Install");
        TaskbarAddonPauseButton.Visibility = state == TaskbarAddonState.Running ? Visibility.Visible : Visibility.Collapsed;
        TaskbarAddonUninstallButton.Visibility = installed ? Visibility.Visible : Visibility.Collapsed;

        TaskbarAddonConfig config = await Task.Run(TaskbarAddonConfigStore.Load);
        _loading = true;
        TaskbarAllMonitorsToggle.IsOn = config.ShowOnAllMonitors;
        _loading = false;
        TaskbarAllMonitorsCard.IsEnabled = installed;
    }

    private async void TaskbarAllMonitorsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        bool allMonitors = TaskbarAllMonitorsToggle.IsOn;
        await SaveConfigAsync(config => config.ShowOnAllMonitors = allMonitors);
    }

    private async Task SaveConfigAsync(Action<TaskbarAddonConfig> change)
    {
        try
        {
            await Task.Run(() => TaskbarAddonConfigStore.Update(change));
        }
        catch (Exception ex)
        {
            App.Log($"[Taskbar] Saving config.json failed: {ex}");
            ShowError(string.Format(T("Taskbar.Addon.SaveError"), ex.Message));
        }
    }

    private async Task RunActionAsync(Action action, bool waitForStart)
    {
        ErrorInfoBar.IsOpen = false;
        SetBusy(true);
        try
        {
            await Task.Run(action);
            // The bar needs a moment before it answers its pipe.
            for (int i = 0; waitForStart && i < 10 && TaskbarHostService.GetState() != TaskbarAddonState.Running; i++)
            {
                await Task.Delay(300);
            }
        }
        catch (Exception ex)
        {
            App.Log($"[Taskbar] Add-on action failed: {ex}");
            ShowError(string.Format(T("TaskbarFolders.Error"), ex.Message));
        }

        await RefreshAsync();
        SetBusy(false);
    }

    private void SetBusy(bool busy)
    {
        TaskbarAddonBusy.IsActive = busy;
        TaskbarAddonBusy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        TaskbarAddonStartButton.IsEnabled = TaskbarAddonPauseButton.IsEnabled = TaskbarAddonUninstallButton.IsEnabled = !busy;
    }

    private async void TaskbarAddonStartButton_Click(object sender, RoutedEventArgs e) =>
        await RunActionAsync(TaskbarHostService.Install, waitForStart: true);

    private async void TaskbarAddonPauseButton_Click(object sender, RoutedEventArgs e) =>
        await RunActionAsync(TaskbarHostService.Pause, waitForStart: false);

    private async void TaskbarAddonUninstallButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = T("Taskbar.Addon.Uninstall.Confirm.Title"),
            Content = T("Taskbar.Addon.Uninstall.Confirm.Body"),
            PrimaryButtonText = T("Taskbar.Addon.Uninstall"),
            CloseButtonText = T("Common.Cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await RunActionAsync(TaskbarHostService.Uninstall, waitForStart: false);
        }
    }

    private void ShowError(string message)
    {
        ErrorInfoBar.Message = message;
        ErrorInfoBar.IsOpen = true;
    }
}
