using System.Diagnostics;
using System.Runtime.InteropServices;
using DeskBox.Helpers;
using DeskBox.Platform;
using DeskBox.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace DeskBox.Controls.WidgetContents;

/// <summary>
/// OpenWinBox Audio widget: default output/input device (switch, volume,
/// mute) and per-app volume on the default output device.
/// </summary>
public sealed partial class AudioWidgetContent : UserControl
{
    private const string MutedGlyph = "";
    private const string OutputGlyph = "";
    private const string InputGlyph = "";
    private const int IconSize = 20;

    private readonly LocalizationService _localization;
    private readonly DispatcherQueueTimer _timer;
    private readonly Dictionary<string, AppRow> _apps = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, (string Key, string Name, string? Path)> _processes = [];
    private CoreAudio.Device? _output, _input;
    private bool _updating;        // changes made by code: don't send them back to Windows
    private long _userChangedAt;   // don't overwrite a slider the user is dragging
    private bool _active;

    private sealed record AppRow(Slider Slider, FontIcon MuteIcon, HashSet<uint> ProcessIds);

    public AudioWidgetContent(LocalizationService localization)
    {
        _localization = localization;
        InitializeComponent();
        // shortcut: polls every 1.5 s instead of Core Audio COM notifications; enough for a widget, and only while visible.
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(1500);
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) => SetActive(true);
        Unloaded += (_, _) => SetActive(false);
    }

    public void SetActive(bool active)
    {
        _active = active;
        if (active && IsLoaded)
        {
            Refresh();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private bool UserIsChanging => Environment.TickCount64 - _userChangedAt < 1000;

    private void Refresh()
    {
        if (!_active)
        {
            return;
        }

        _updating = true;
        try
        {
            _output = TryGet(() => CoreAudio.GetDefault(CoreAudio.Flow.Render));
            _input = TryGet(() => CoreAudio.GetDefault(CoreAudio.Flow.Capture));
            ShowDevice(_output, OutputName, OutputSlider, OutputValue, OutputMuteIcon, OutputGlyph);
            ShowDevice(_input, InputName, InputSlider, InputValue, InputMuteIcon, InputGlyph);
            RefreshApps();
        }
        finally
        {
            _updating = false;
        }
    }

    private void ShowDevice(CoreAudio.Device? device, TextBlock name, Slider slider, TextBlock value, FontIcon muteIcon, string glyph)
    {
        slider.IsEnabled = device is not null;
        if (device is not { } d)
        {
            name.Text = _localization.T("Audio.None");
            value.Text = string.Empty;
            return;
        }

        name.Text = d.Name;
        ToolTipService.SetToolTip(name, d.Name);
        if (!UserIsChanging)
        {
            slider.Value = Math.Round(d.Volume * 100);
        }

        value.Text = $"{slider.Value:0}";
        muteIcon.Glyph = d.Muted ? MutedGlyph : glyph;
    }

    private void RefreshApps()
    {
        List<CoreAudio.Session> sessions = TryGet(CoreAudio.GetSessions) ?? [];
        // One row per app even when it has several sessions (browsers, for example).
        var groups = new Dictionary<string, (string Name, string? Path, CoreAudio.Session First, HashSet<uint> Pids)>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in sessions)
        {
            var (key, name, path) = Describe(session);
            if (key is null)
            {
                continue;
            }

            if (!groups.TryGetValue(key, out var group))
            {
                groups[key] = group = (name, path, session, []);
            }

            group.Pids.Add(session.ProcessId);
        }

        if (!groups.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(_apps.Keys))
        {
            AppList.Children.Clear();
            _apps.Clear();
            foreach (var (key, group) in groups.OrderBy(g => g.Key == "system" ? 0 : 1).ThenBy(g => g.Value.Name))
            {
                _apps[key] = AddAppRow(key, group.Name, group.Path);
            }
        }

        NoApps.Visibility = groups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (key, group) in groups)
        {
            var row = _apps[key];
            row.ProcessIds.Clear();
            row.ProcessIds.UnionWith(group.Pids);
            if (!UserIsChanging)
            {
                row.Slider.Value = Math.Round(group.First.Volume * 100);
            }

            row.MuteIcon.Glyph = group.First.Muted ? MutedGlyph : OutputGlyph;
        }
    }

    private (string? Key, string Name, string? Path) Describe(CoreAudio.Session session)
    {
        if (session.IsSystemSounds)
        {
            return ("system", _localization.T("Audio.SystemSounds"), null);
        }

        if (session.ProcessId == 0)
        {
            return (null, string.Empty, null);
        }

        if (!_processes.TryGetValue(session.ProcessId, out var info))
        {
            try
            {
                using var process = Process.GetProcessById((int)session.ProcessId);
                string? path = null, description = null;
                try
                {
                    path = process.MainModule?.FileName;
                    description = process.MainModule?.FileVersionInfo.FileDescription;
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    // Protected or elevated process: fall back to its name.
                }

                string name = string.IsNullOrWhiteSpace(description) ? process.ProcessName : description;
                info = (path ?? name, name, path);
            }
            catch (ArgumentException)
            {
                return (null, string.Empty, null); // the process already exited
            }

            _processes[session.ProcessId] = info;
        }

        return info;
    }

    private AppRow AddAppRow(string key, string name, string? path)
    {
        var grid = new Grid { ColumnSpacing = 8, Padding = new Thickness(8, 0, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (path is null)
        {
            grid.Children.Add(new FontIcon { Glyph = "", FontSize = 16, Width = IconSize }); // system sounds
        }
        else
        {
            var icon = new Image { Width = IconSize, Height = IconSize, VerticalAlignment = VerticalAlignment.Center };
            grid.Children.Add(icon);
            _ = LoadIconAsync(icon, path);
        }

        var label = new TextBlock
        {
            Text = name,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        ToolTipService.SetToolTip(label, name);
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        var slider = new Slider { Maximum = 100, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(slider, _localization.Format("Audio.AppVolume", name));
        Grid.SetColumn(slider, 2);
        grid.Children.Add(slider);

        var muteIcon = new FontIcon { Glyph = OutputGlyph, FontSize = 14 };
        var mute = new Button { Content = muteIcon, Style = (Style)Resources["AudioIconButtonStyle"] };
        ToolTipService.SetToolTip(mute, _localization.T("Audio.Mute"));
        AutomationProperties.SetName(mute, _localization.Format("Audio.MuteApp", name));
        Grid.SetColumn(mute, 3);
        grid.Children.Add(mute);

        var row = new AppRow(slider, muteIcon, []);
        bool system = key == "system";
        bool Matches(uint pid, bool isSystem) => system ? isSystem : !isSystem && row.ProcessIds.Contains(pid);
        slider.ValueChanged += (_, e) =>
        {
            if (_updating)
            {
                return;
            }

            _userChangedAt = Environment.TickCount64;
            Try(() => CoreAudio.SetSessions(Matches, (float)(e.NewValue / 100), null));
        };
        mute.Click += (_, _) =>
        {
            bool muted = muteIcon.Glyph != MutedGlyph;
            Try(() => CoreAudio.SetSessions(Matches, null, muted));
            muteIcon.Glyph = muted ? MutedGlyph : OutputGlyph;
        };
        AppList.Children.Add(grid);
        return row;
    }

    private static async Task LoadIconAsync(Image image, string path)
    {
        try
        {
            image.Source = await IconHelper.GetIconAsync(path, decodePixelWidth: IconSize * 2);
        }
        catch (Exception ex)
        {
            App.LogVerbose($"[AudioWidget] Icon failed for '{path}': {ex.Message}");
        }
    }

    private static T? TryGet<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            return default; // device unplugging or audio service restarting
        }
    }

    private static void Try(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            // The session or device disappeared, or Windows refused the change.
        }
    }

    private CoreAudio.Device? DeviceOf(object sender) => IsOutput(sender) ? _output : _input;

    private static bool IsOutput(object sender) => (string)((FrameworkElement)sender).Tag == "Output";

    private void DeviceSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_updating || DeviceOf(sender) is not { } device)
        {
            return;
        }

        _userChangedAt = Environment.TickCount64;
        Try(() => CoreAudio.SetVolume(device.Id, (float)(e.NewValue / 100)));
        (IsOutput(sender) ? OutputValue : InputValue).Text = $"{e.NewValue:0}";
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceOf(sender) is not { } device)
        {
            return;
        }

        Try(() => CoreAudio.SetMute(device.Id, !device.Muted));
        Refresh();
    }

    private void Device_Click(object sender, RoutedEventArgs e)
    {
        var flow = IsOutput(sender) ? CoreAudio.Flow.Render : CoreAudio.Flow.Capture;
        string? current = DeviceOf(sender)?.Id;
        var menu = new MenuFlyout();
        foreach (var device in TryGet(() => CoreAudio.GetActiveDevices(flow)) ?? [])
        {
            string id = device.Id;
            var item = new RadioMenuFlyoutItem { Text = device.Name, IsChecked = id == current, GroupName = "devices" };
            item.Click += (_, _) =>
            {
                Try(() => CoreAudio.SetDefault(id));
                Refresh();
            };
            menu.Items.Add(item);
        }

        if (menu.Items.Count == 0)
        {
            menu.Items.Add(new MenuFlyoutItem { Text = _localization.T("Audio.NoDevices"), IsEnabled = false });
        }

        menu.ShowAt((FrameworkElement)sender);
    }
}
