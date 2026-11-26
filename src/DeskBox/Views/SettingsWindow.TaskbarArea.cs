using DeskBox.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeskBox.Views;

/// <summary>
/// Splits the settings menu into two areas — desktop widgets and the taskbar
/// — so taskbar folders get their own menu instead of one more row in the
/// widget list. The selector only swaps which nav items are shown.
/// </summary>
public sealed partial class SettingsWindow
{
    private const int WidgetsArea = 0;
    private const int TaskbarArea = 1;
    private bool _isSyncingSettingsArea;

    private static bool IsTaskbarAreaNavTag(string navTag) => navTag.StartsWith("Taskbar", StringComparison.Ordinal);

    /// <summary>General and Appearance belong to the own taskbar add-on; builds without it only have folders.</summary>
    private static bool IsTaskbarAddonNavTag(string navTag) => navTag is "TaskbarGeneral" or "TaskbarAppearance";

    private void SettingsAreaSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // The initial selection is set from code (SyncSettingsAreaForSection)
        // once the menu exists; ignore anything earlier or self-inflicted.
        if (_isSyncingSettingsArea || SettingsAreaSelector is null || SettingsNavigationView is null)
        {
            return;
        }

        int area = SettingsAreaSelector.SelectedIndex;
        if (area < 0)
        {
            return;
        }
        ApplySettingsAreaVisibility(area);
        NavigateToSettingsSection(area != TaskbarArea ? "General"
            : File.Exists(TaskbarHostService.ExePath) ? "TaskbarGeneral" : "TaskbarFolders");
    }

    /// <summary>Keeps the selector on the right area when a section is opened by search or deep link.</summary>
    private void SyncSettingsAreaForSection(string navTag)
    {
        if (SettingsAreaSelector is null || SettingsNavigationView is null)
        {
            return;
        }

        int area = IsTaskbarAreaNavTag(navTag) ? TaskbarArea : WidgetsArea;
        if (SettingsAreaSelector.SelectedIndex != area)
        {
            _isSyncingSettingsArea = true;
            try
            {
                SettingsAreaSelector.SelectedIndex = area;
            }
            finally
            {
                _isSyncingSettingsArea = false;
            }
        }

        ApplySettingsAreaVisibility(area);
    }

    private void ApplySettingsAreaVisibility(int area)
    {
        bool addonAvailable = File.Exists(TaskbarHostService.ExePath);
        foreach (object item in SettingsNavigationView.MenuItems)
        {
            if (item is NavigationViewItem { Tag: string tag } navItem)
            {
                bool inTaskbarArea = IsTaskbarAreaNavTag(tag);
                navItem.Visibility = inTaskbarArea == (area == TaskbarArea) && (addonAvailable || !IsTaskbarAddonNavTag(tag))
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }
    }
}
