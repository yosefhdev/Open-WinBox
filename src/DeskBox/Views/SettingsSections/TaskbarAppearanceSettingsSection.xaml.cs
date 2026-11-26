using CommunityToolkit.WinUI.Controls;
using DeskBox.Controls;
using DeskBox.Core.Persistence;
using DeskBox.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace DeskBox.Views.SettingsSections;

/// <summary>
/// Own taskbar add-on look: shape (width, height, margin, corners, border), background, icons,
/// alignment and which buttons show, plus quick presets. Writes taskbar/config.json; the bar
/// reloads on its own. Install state is owned by <see cref="TaskbarGeneralSettingsSection"/>, so it
/// is re-checked each time this page is shown.
/// </summary>
public sealed partial class TaskbarAppearanceSettingsSection : UserControl
{
    // Sliders fire on every step while dragging; write once the user pauses.
    private const int SaveDelayMs = 200;
    private const int DefaultCustomHeight = 48;

    private static readonly SettingsColorStringConverter ColorConverter = new();

    private LocalizationService? _localization;
    private bool _loading;
    private bool _installed;
    private CancellationTokenSource? _pendingSave;

    public TaskbarAppearanceSettingsSection()
    {
        InitializeComponent();
        TintColorPicker.RegisterPropertyChangedCallback(ColorPickerButton.SelectedColorProperty, (_, _) => Option_Changed(this, null));
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
        _installed = state is TaskbarAddonState.Running or TaskbarAddonState.Paused or TaskbarAddonState.Stopped;

        WidthFullItem.Content = T("Taskbar.Addon.Width.Full");
        WidthAutoItem.Content = T("Taskbar.Addon.Width.Auto");
        BackgroundAcrylicItem.Content = T("Taskbar.Addon.Background.Acrylic");
        BackgroundBlurItem.Content = T("Taskbar.Addon.Background.Blur");
        BackgroundSolidItem.Content = T("Taskbar.Addon.Background.Solid");
        BackgroundTransparentItem.Content = T("Taskbar.Addon.Background.Transparent");
        TaskbarAlignmentCenterItem.Content = T("Taskbar.Addon.Alignment.Center");
        TaskbarAlignmentLeftItem.Content = T("Taskbar.Addon.Alignment.Left");
        ShowConfig(config);

        foreach (UIElement child in OptionsPanel.Children)
        {
            if (child is Control control)
            {
                control.IsEnabled = _installed;
            }
        }

        UpdateDependentControls();
        NeedsInstallInfoBar.Message = T("Taskbar.Addon.NeedsInstall");
        NeedsInstallInfoBar.IsOpen = !_installed;
    }

    private void ShowConfig(TaskbarAddonConfig config)
    {
        _loading = true;
        WidthComboBox.SelectedItem = SelectByTag(WidthComboBox, config.Width) ?? WidthFullItem;
        NativeHeightToggle.IsOn = config.Height == 0;
        HeightSlider.Value = config.Height == 0 ? DefaultCustomHeight : Math.Clamp(config.Height, 32, 96);
        MarginSlider.Value = Math.Clamp(config.Margin, 0, 48);
        BarRadiusSlider.Value = Math.Clamp(config.BarRadius, 0, 32);
        ShowBorderToggle.IsOn = config.ShowBorder;
        BackgroundComboBox.SelectedItem = SelectByTag(BackgroundComboBox, config.Background) ?? BackgroundAcrylicItem;
        TintColorPicker.SelectedColor = (Color)ColorConverter.Convert(config.TintColor, typeof(Color), null!, string.Empty);
        TintOpacitySlider.Value = Math.Clamp(config.TintOpacity, 0, 100);
        TaskbarAlignmentComboBox.SelectedItem = SelectByTag(TaskbarAlignmentComboBox, config.Alignment) ?? TaskbarAlignmentCenterItem;
        IconSizeSlider.Value = Math.Clamp(config.IconSize, 16, 40);
        SpacingSlider.Value = Math.Clamp(config.Spacing, 0, 24);
        CornerRadiusSlider.Value = Math.Clamp(config.CornerRadius, 0, 12);
        TaskbarShowSearchToggle.IsOn = config.ShowSearch;
        TaskbarShowTaskViewToggle.IsOn = config.ShowTaskView;
        ShowClockToggle.IsOn = config.ShowClock;
        _loading = false;
    }

    /// <summary>The page's values; ShowOnAllMonitors belongs to the General page and is left out.</summary>
    private Action<TaskbarAddonConfig> ReadPage() => CopyLook(new TaskbarAddonConfig
    {
        Width = Tag(WidthComboBox, "full"),
        Height = NativeHeightToggle.IsOn ? 0 : (int)HeightSlider.Value,
        Margin = (int)MarginSlider.Value,
        BarRadius = (int)BarRadiusSlider.Value,
        ShowBorder = ShowBorderToggle.IsOn,
        Background = Tag(BackgroundComboBox, "acrylic"),
        TintColor = (string)ColorConverter.ConvertBack(TintColorPicker.SelectedColor, typeof(string), null!, string.Empty),
        TintOpacity = (int)TintOpacitySlider.Value,
        Alignment = Tag(TaskbarAlignmentComboBox, "center"),
        IconSize = (int)IconSizeSlider.Value,
        Spacing = (int)SpacingSlider.Value,
        CornerRadius = (int)CornerRadiusSlider.Value,
        ShowSearch = TaskbarShowSearchToggle.IsOn,
        ShowTaskView = TaskbarShowTaskViewToggle.IsOn,
        ShowClock = ShowClockToggle.IsOn
    });

    private static string Tag(ComboBox box, string fallback) => (box.SelectedItem as ComboBoxItem)?.Tag as string ?? fallback;

    private static ComboBoxItem? SelectByTag(ComboBox box, string value) =>
        box.Items.OfType<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Tag as string, value, StringComparison.OrdinalIgnoreCase));

    private void UpdateDependentControls()
    {
        // A solid background is always opaque; Windows' height leaves no height to pick.
        TintOpacityCard.IsEnabled = _installed && !ReferenceEquals(BackgroundComboBox.SelectedItem, BackgroundSolidItem);
        HeightCard.IsEnabled = _installed && !NativeHeightToggle.IsOn;
    }

    private async void Option_Changed(object sender, object? e)
    {
        if (_loading)
        {
            return;
        }

        UpdateDependentControls();
        Action<TaskbarAddonConfig> change = ReadPage();
        _pendingSave?.Cancel();
        var pending = _pendingSave = new CancellationTokenSource();
        try
        {
            await Task.Delay(SaveDelayMs, pending.Token);
            await SaveAsync(change);
        }
        catch (OperationCanceledException)
        {
            // A newer change replaced this one.
        }
    }

    /// <summary>A floating, centred bar that fits its icons, like the macOS dock.</summary>
    private async void DockPreset_Click(object sender, RoutedEventArgs e) => await ApplyPresetAsync(config =>
    {
        config.Width = "auto";
        config.Height = 64;
        config.Margin = 8;
        config.BarRadius = 18;
        config.ShowBorder = true;
        config.Background = "acrylic";
        config.TintColor = "#2A2A2E";
        config.TintOpacity = 55;
        config.Alignment = "center";
        config.IconSize = 40;
        config.Spacing = 4;
        config.CornerRadius = 10;
        config.ShowSearch = false;
        config.ShowTaskView = false;
        config.ShowClock = false;
    });

    /// <summary>Back to the Windows 11 look; keeps the General page's options.</summary>
    private async void ResetPreset_Click(object sender, RoutedEventArgs e) => await ApplyPresetAsync(config =>
    {
        CopyLook(new TaskbarAddonConfig())(config);
    });

    /// <summary>Copies every look field (all but ShowOnAllMonitors and Version) from <paramref name="source"/>.</summary>
    private static Action<TaskbarAddonConfig> CopyLook(TaskbarAddonConfig source) => config =>
    {
        config.Width = source.Width;
        config.Height = source.Height;
        config.Margin = source.Margin;
        config.BarRadius = source.BarRadius;
        config.ShowBorder = source.ShowBorder;
        config.Background = source.Background;
        config.TintColor = source.TintColor;
        config.TintOpacity = source.TintOpacity;
        config.Alignment = source.Alignment;
        config.IconSize = source.IconSize;
        config.Spacing = source.Spacing;
        config.CornerRadius = source.CornerRadius;
        config.ShowSearch = source.ShowSearch;
        config.ShowTaskView = source.ShowTaskView;
        config.ShowClock = source.ShowClock;
    };

    private async Task ApplyPresetAsync(Action<TaskbarAddonConfig> preset)
    {
        _pendingSave?.Cancel();
        if (await SaveAsync(preset))
        {
            ShowConfig(await Task.Run(TaskbarAddonConfigStore.Load));
            UpdateDependentControls();
        }
    }

    private async Task<bool> SaveAsync(Action<TaskbarAddonConfig> change)
    {
        try
        {
            await Task.Run(() => TaskbarAddonConfigStore.Update(change));
            return true;
        }
        catch (Exception ex)
        {
            App.Log($"[Taskbar] Saving config.json failed: {ex}");
            ErrorInfoBar.Message = string.Format(T("Taskbar.Addon.SaveError"), ex.Message);
            ErrorInfoBar.IsOpen = true;
            return false;
        }
    }
}
