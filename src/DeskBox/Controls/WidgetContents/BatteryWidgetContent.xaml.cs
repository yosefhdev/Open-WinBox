using DeskBox.Platform;
using DeskBox.Services;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace DeskBox.Controls.WidgetContents;

/// <summary>
/// OpenWinBox Battery widget: one row per wireless peripheral with its level and charging state.
/// Reads on a worker thread every 30 s, and only while visible.
/// </summary>
public sealed partial class BatteryWidgetContent : UserControl
{
    private const int LowPercent = 15;
    private const int SpinMilliseconds = 700;
    private static readonly Brush s_lowBrush = new SolidColorBrush(Color.FromArgb(255, 232, 17, 35));

    private readonly LocalizationService _localization;
    private readonly DispatcherQueueTimer _timer;
    private readonly DeviceBatteries _batteries;
    private readonly Dictionary<string, Row> _rows = new(StringComparer.OrdinalIgnoreCase);
    private bool _active = true;   // window visible, as last reported by the host
    private readonly Storyboard _spin = new();
    private bool _busy;            // a read is still running: skip this tick

    private sealed record Row(FontIcon BatteryIcon, TextBlock Name, TextBlock Percent, ProgressBar Bar);

    public BatteryWidgetContent(LocalizationService localization)
    {
        _localization = localization;
        _batteries = new DeviceBatteries(localization.T("Battery.System"), localization.T("Battery.Controller"));
        InitializeComponent();
        var turn = new DoubleAnimation { From = 0, To = 360, Duration = TimeSpan.FromMilliseconds(SpinMilliseconds), EnableDependentAnimation = true };
        Storyboard.SetTarget(turn, RefreshRotation);
        Storyboard.SetTargetProperty(turn, "Angle");
        _spin.Children.Add(turn);
        // shortcut: polls every 30 s; batteries change slowly and the Razer read can take seconds.
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(30);
        _timer.Tick += (_, _) => _ = RefreshAsync();
        // Loading must not override a window that is already hidden, so it only applies the current state.
        Loaded += (_, _) => SetActive(_active);
        Unloaded += (_, _) => _timer.Stop();
    }

    public void SetActive(bool active)
    {
        _active = active;
        if (active && IsLoaded)
        {
            _ = RefreshAsync();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_busy || !_active)
        {
            return;
        }

        _busy = true;
        _spin.Begin();
        try
        {
            // The read can finish instantly; keep the icon turning for one full turn so the click visibly did something.
            var read = Task.Run(_batteries.Read);
            await Task.WhenAll(read, Task.Delay(SpinMilliseconds));
            var readings = read.Result;
            if (_active)
            {
                Show(readings);
            }
        }
        catch (Exception ex)
        {
            App.LogVerbose($"[BatteryWidget] Read failed: {ex.Message}");
        }
        finally
        {
            _busy = false;
            _spin.Stop();
        }
    }

    private void Show(List<DeviceBatteries.Reading> readings)
    {
        Status.Text = _localization.T("Battery.None");
        Status.Visibility = readings.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (!readings.Select(r => r.Key).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(_rows.Keys))
        {
            DeviceList.Children.Clear();
            _rows.Clear();
            foreach (var reading in readings.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                _rows[reading.Key] = AddRow(reading);
            }
        }

        foreach (var reading in readings)
        {
            var row = _rows[reading.Key];
            bool known = reading.Percent is not null;
            int percent = reading.Percent ?? 0;
            bool low = known && percent <= LowPercent && !reading.Charging;

            row.Name.Text = reading.Name;
            string shown = known
                ? $"{(reading.Approximate ? "~" : string.Empty)}{percent}%"
                : reading.Charging ? _localization.T("Battery.Charging") : "--";
            row.Percent.Text = shown;
            row.Bar.Value = percent;
            row.BatteryIcon.Glyph = BatteryGlyph(known ? percent : 0, reading.Charging);
            if (low)
            {
                row.Bar.Foreground = s_lowBrush;
                row.BatteryIcon.Foreground = s_lowBrush;
            }
            else
            {
                row.Bar.ClearValue(Control.ForegroundProperty);
                row.BatteryIcon.ClearValue(Control.ForegroundProperty);
            }

            string charging = reading.Charging ? " · " + _localization.T("Battery.Charging") : string.Empty;
            ToolTipService.SetToolTip(row.Percent, $"{reading.Name}: {shown}{charging}");
            AutomationProperties.SetName(row.Percent, $"{reading.Name} {(known ? shown : string.Empty)}{charging}");
        }
    }

    private Row AddRow(DeviceBatteries.Reading reading)
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var deviceIcon = new FontIcon { Glyph = DeviceGlyph(reading.Name), FontSize = 20, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(deviceIcon);

        var name = new TextBlock { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
        var bar = new ProgressBar { Maximum = 100 };
        var middle = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        middle.Children.Add(name);
        middle.Children.Add(bar);
        Grid.SetColumn(middle, 1);
        grid.Children.Add(middle);

        var batteryIcon = new FontIcon { FontSize = 16, VerticalAlignment = VerticalAlignment.Center };
        var percent = new TextBlock { FontSize = 12, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(batteryIcon);
        right.Children.Add(percent);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        DeviceList.Children.Add(grid);
        return new Row(batteryIcon, name, percent, bar);
    }

    // Segoe Fluent Icons: E850-E859 are 0-90 %, E83F is full; E85A-E862 charging 0-80 %, E83E charging full.
    private static string BatteryGlyph(int percent, bool charging)
    {
        int level = Math.Clamp(percent / 10, 0, 10);
        int code = charging
            ? (level >= 9 ? 0xE83E : 0xE85A + level)
            : (level >= 10 ? 0xE83F : 0xE850 + level);
        return char.ConvertFromUtf32(code);
    }

    private static string DeviceGlyph(string name)
    {
        string n = name.ToLowerInvariant();
        string[] mouse = ["mouse", "basilisk", "deathadder", "viper", "naga", "orochi", "mamba", "cobra", "lancehead", "pulsefire", "g502", "mx master"];
        string[] keyboard = ["keyboard", "teclado", "blackwidow", "huntsman", "ornata", "deathstalker", "cynosa", "k380", "mx keys"];
        string[] headset = ["headset", "headphone", "kraken", "barracuda", "blackshark", "hammerhead", "airpods", "buds", "auricular"];
        string[] gamepad = ["controller", "gamepad", "xbox", "dualsense", "dualshock", "wolverine", "kishi"];
        if (mouse.Any(n.Contains))
        {
            return "";
        }

        if (keyboard.Any(n.Contains))
        {
            return "";
        }

        if (headset.Any(n.Contains))
        {
            return "";
        }

        return gamepad.Any(n.Contains) ? "" : "";
    }
}
