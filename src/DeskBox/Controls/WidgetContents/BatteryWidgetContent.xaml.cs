using DeskBox.Models;
using DeskBox.Platform;
using DeskBox.Services;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
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

    private static readonly Brush s_greenBrush = new SolidColorBrush(Color.FromArgb(255, 46, 194, 106));
    private static readonly Brush s_trackBrush = new SolidColorBrush(Color.FromArgb(48, 128, 128, 128));

    private readonly WidgetConfig _config;
    private readonly SettingsService? _settingsService;
    private string _style;
    private string? _builtStyle;   // style the current rows were built with
    private List<DeviceBatteries.Reading>? _lastReadings;

    /// <summary>SetGauge draws the level: percent 0-100 and an override brush (null = green).</summary>
    private sealed record Row(TextBlock Name, TextBlock Percent, Action<int, Brush?> SetGauge);

    public BatteryWidgetContent(LocalizationService localization, WidgetConfig config, SettingsService? settingsService)
    {
        _localization = localization;
        _config = config;
        _settingsService = settingsService;
        _style = BatteryWidgetSettings.GetStyle(config);
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
        Loaded += (_, _) =>
        {
            if (_settingsService is not null)
            {
                _settingsService.SettingsChanged += OnSettingsChanged;
            }

            SetActive(_active);
        };
        Unloaded += (_, _) =>
        {
            if (_settingsService is not null)
            {
                _settingsService.SettingsChanged -= OnSettingsChanged;
            }

            _timer.Stop();
        };
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

    // The style is edited in Settings > functional widgets; SettingsChanged may arrive on any thread.
    private void OnSettingsChanged() =>
        DispatcherQueue.TryEnqueue(() =>
        {
            string style = BatteryWidgetSettings.GetStyle(_config);
            if (style != _style)
            {
                _style = style;
                if (_lastReadings is not null)
                {
                    Show(_lastReadings);
                }
            }
        });

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
        _lastReadings = readings;
        Status.Text = _localization.T("Battery.None");
        Status.Visibility = readings.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (_builtStyle != _style || !readings.Select(r => r.Key).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(_rows.Keys))
        {
            DeviceList.Children.Clear();
            _rows.Clear();
            _builtStyle = _style;
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
            row.SetGauge(percent, low ? s_lowBrush : null);

            string charging = reading.Charging ? " · " + _localization.T("Battery.Charging") : string.Empty;
            ToolTipService.SetToolTip(row.Percent, $"{reading.Name}: {shown}{charging}");
            AutomationProperties.SetName(row.Percent, $"{reading.Name} {(known ? shown : string.Empty)}{charging}");
        }
    }

    private Row AddRow(DeviceBatteries.Reading reading)
    {
        var deviceIcon = new FontIcon { Glyph = DeviceGlyph(reading.Name), FontSize = 20, VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var percent = new TextBlock { FontSize = 12, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, MinWidth = 52, TextAlignment = TextAlignment.Right };
        var grid = new Grid { ColumnSpacing = 10 };
        Action<int, Brush?> setGauge;

        if (_style == BatteryWidgetSettings.Ring)
        {
            // Ring on the left with the percentage inside; name and battery glyph beside it.
            var ring = BuildRing(percent, out setGauge);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(ring);
            var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            label.Children.Add(deviceIcon);
            label.Children.Add(name);
            Grid.SetColumn(label, 1);
            grid.Children.Add(label);
        }
        else
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(deviceIcon);

            FrameworkElement gauge = _style switch
            {
                BatteryWidgetSettings.Battery => BuildBatteryShape(out setGauge),
                BatteryWidgetSettings.Segments => BuildSegments(out setGauge),
                _ => BuildBar(out setGauge)
            };
            var middle = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            middle.Children.Add(name);
            middle.Children.Add(gauge);
            Grid.SetColumn(middle, 1);
            grid.Children.Add(middle);

            var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            right.Children.Add(percent);
            Grid.SetColumn(right, 2);
            grid.Children.Add(right);
        }

        DeviceList.Children.Add(grid);
        return new Row(name, percent, setGauge);
    }


    private static FrameworkElement BuildBar(out Action<int, Brush?> set)
    {
        var bar = new ProgressBar { Maximum = 100 };
        set = (percent, brush) =>
        {
            bar.Value = percent;
            bar.Foreground = brush ?? s_greenBrush;
        };
        return bar;
    }

    // Ten rounded blocks, lit from the left; a block lights as soon as its tenth has any charge.
    private static FrameworkElement BuildSegments(out Action<int, Brush?> set)
    {
        const int count = 10;
        var grid = new Grid { ColumnSpacing = 3, Height = 12 };
        var blocks = new Border[count];
        for (int i = 0; i < count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            blocks[i] = new Border { CornerRadius = new CornerRadius(3), Background = s_trackBrush };
            Grid.SetColumn(blocks[i], i);
            grid.Children.Add(blocks[i]);
        }

        set = (percent, brush) =>
        {
            for (int i = 0; i < count; i++)
            {
                blocks[i].Background = percent > i * 10 ? brush ?? s_greenBrush : s_trackBrush;
            }
        };
        return grid;
    }

    // A horizontal battery: outlined body, proportional fill and a terminal nub.
    private static FrameworkElement BuildBatteryShape(out Action<int, Brush?> set)
    {
        var edge = new SolidColorBrush(Color.FromArgb(110, 128, 128, 128));
        var fill = new Border { CornerRadius = new CornerRadius(4), Margin = new Thickness(3) };
        var left = new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) };
        var right = new ColumnDefinition { Width = new GridLength(100, GridUnitType.Star) };
        var inner = new Grid();
        inner.ColumnDefinitions.Add(left);
        inner.ColumnDefinitions.Add(right);
        inner.Children.Add(fill);

        var body = new Border
        {
            Height = 24,
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(2),
            BorderBrush = edge,
            Child = inner
        };
        var nub = new Border
        {
            Width = 4,
            Height = 10,
            CornerRadius = new CornerRadius(0, 2, 2, 0),
            Background = edge,
            VerticalAlignment = VerticalAlignment.Center
        };
        var shape = new Grid { ColumnSpacing = 1 };
        shape.ColumnDefinitions.Add(new ColumnDefinition());
        shape.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        shape.Children.Add(body);
        Grid.SetColumn(nub, 1);
        shape.Children.Add(nub);

        set = (percent, brush) =>
        {
            left.Width = new GridLength(percent, GridUnitType.Star);
            right.Width = new GridLength(100 - percent, GridUnitType.Star);
            fill.Background = brush ?? s_greenBrush;
            fill.Visibility = percent > 0 ? Visibility.Visible : Visibility.Collapsed;
        };
        return shape;
    }

    // A 48 px ring that fills clockwise from the top, with the percentage text in the centre.
    private static FrameworkElement BuildRing(TextBlock centre, out Action<int, Brush?> set)
    {
        const double size = 48, stroke = 5, radius = (size - stroke) / 2, c = size / 2;
        // Track and progress are both Paths in one Canvas, so they share the exact same coordinate origin.
        var track = new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = new EllipseGeometry { Center = new Windows.Foundation.Point(c, c), RadiusX = radius, RadiusY = radius },
            Stroke = s_trackBrush,
            StrokeThickness = stroke
        };
        var arc = new ArcSegment { Size = new Windows.Foundation.Size(radius, radius), SweepDirection = SweepDirection.Clockwise };
        var figure = new PathFigure { StartPoint = new Windows.Foundation.Point(c, c - radius), IsClosed = false };
        figure.Segments.Add(arc);
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        var progress = new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = geometry,
            StrokeThickness = stroke,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        centre.FontSize = 11;
        centre.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        centre.HorizontalAlignment = HorizontalAlignment.Center;
        centre.VerticalAlignment = VerticalAlignment.Center;
        centre.MinWidth = 0;
        centre.TextAlignment = TextAlignment.Center;

        var canvas = new Canvas { Width = size, Height = size };
        canvas.Children.Add(track);
        canvas.Children.Add(progress);
        var host = new Grid { Width = size, Height = size };
        host.Children.Add(canvas);
        host.Children.Add(centre);

        set = (percent, brush) =>
        {
            double angle = Math.Min(percent, 99.9) / 100 * 2 * Math.PI - Math.PI / 2;
            arc.Point = new Windows.Foundation.Point(c + radius * Math.Cos(angle), c + radius * Math.Sin(angle));
            arc.IsLargeArc = percent > 50;
            progress.Stroke = brush ?? s_greenBrush;
            progress.Visibility = percent > 0 ? Visibility.Visible : Visibility.Collapsed;
        };
        return host;
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
