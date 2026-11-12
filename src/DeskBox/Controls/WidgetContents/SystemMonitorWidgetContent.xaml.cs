using DeskBox.Models;
using DeskBox.Platform;
using DeskBox.Services;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DeskBox.Controls.WidgetContents;

/// <summary>
/// OpenWinBox System Monitor widget: CPU, memory, GPU, disk activity, network
/// throughput and per-drive usage. Samples on a worker thread, and only while visible.
/// </summary>
public sealed partial class SystemMonitorWidgetContent : UserControl
{
    private const double WarnPercent = 90;
    private static readonly Brush s_warnBrush = new SolidColorBrush(Color.FromArgb(255, 232, 17, 35));

    private readonly LocalizationService _localization;
    private readonly WidgetConfig _config;
    private readonly SettingsService? _settingsService;
    private readonly HashSet<string> _hidden;
    private readonly DispatcherQueueTimer _timer;
    private readonly Dictionary<string, (TextBlock Text, ProgressBar Bar)> _drives = new(StringComparer.OrdinalIgnoreCase);
    private SystemMetrics? _metrics;
    private bool _active = true;   // window visible, as last reported by the host
    private bool _busy;            // a sample is still running: skip this tick

    public SystemMonitorWidgetContent(LocalizationService localization, WidgetConfig config, SettingsService? settingsService)
    {
        _localization = localization;
        _config = config;
        _settingsService = settingsService;
        _hidden = SystemMonitorWidgetSettings.GetHidden(config);
        InitializeComponent();
        ApplyVisibility();
        CpuDetail.Text = SystemMetrics.CpuName() ?? string.Empty;
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(1000);
        _timer.Tick += (_, _) => _ = SampleAsync();
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

            Stop();
        };
    }

    public void SetActive(bool active)
    {
        _active = active;
        if (active && IsLoaded)
        {
            _metrics ??= new SystemMetrics();
            _timer.Start();
        }
        else
        {
            Stop();
        }
    }

    // Disposing the sampler also drops the rate baselines, so a widget shown again never averages over the hidden time.
    private void Stop()
    {
        _timer.Stop();
        _metrics?.Dispose();
        _metrics = null;
    }

    private async Task SampleAsync()
    {
        if (_busy || !_active || _metrics is not { } metrics)
        {
            return;
        }

        _busy = true;
        try
        {
            // Hidden sections are not even sampled; GPU and disk counters are the costly ones.
            bool gpu = !_hidden.Contains(SystemMonitorWidgetSettings.Gpu);
            bool disk = !_hidden.Contains(SystemMonitorWidgetSettings.Storage);
            bool drives = !_hidden.Contains(SystemMonitorWidgetSettings.Drives);
            var snapshot = await Task.Run(() => metrics.Sample(gpu, disk, drives));
            if (snapshot is not null && _active && ReferenceEquals(metrics, _metrics))
            {
                Show(snapshot);
            }
        }
        catch (Exception ex)
        {
            App.LogVerbose($"[SystemMonitorWidget] Sample failed: {ex.Message}");
        }
        finally
        {
            _busy = false;
        }
    }

    private void ApplyVisibility()
    {
        void Set(UIElement section, string id) =>
            section.Visibility = _hidden.Contains(id) ? Visibility.Collapsed : Visibility.Visible;

        Set(CpuSection, SystemMonitorWidgetSettings.Cpu);
        Set(MemorySection, SystemMonitorWidgetSettings.Memory);
        Set(GpuSection, SystemMonitorWidgetSettings.Gpu);
        Set(StorageSection, SystemMonitorWidgetSettings.Storage);
        Set(NetworkSection, SystemMonitorWidgetSettings.Network);
        Set(DriveSection, SystemMonitorWidgetSettings.Drives);
    }

    private const string DriveGlyph = "";

    // Sections are edited in Settings > functional widgets; SettingsChanged may arrive on any thread.
    private void OnSettingsChanged() =>
        DispatcherQueue.TryEnqueue(() =>
        {
            var hidden = SystemMonitorWidgetSettings.GetHidden(_config);
            if (hidden.SetEquals(_hidden))
            {
                return;
            }

            _hidden.Clear();
            _hidden.UnionWith(hidden);
            ApplyVisibility();
        });

    private void Show(SystemMetrics.Snapshot s)
    {
        SetBar(CpuBar, CpuValue, s.Cpu);

        SetBar(MemoryBar, MemoryValue, s.MemoryPercent);
        MemoryDetail.Text = $"{FormatBytes(s.MemoryUsed)} / {FormatBytes(s.MemoryTotal)}";

        if (s.Gpu is { } gpu)
        {
            SetBar(GpuBar, GpuValue, gpu);
            GpuDetail.Text = s.GpuMemoryBytes is { } vram ? $"VRAM {FormatBytes(vram)}" : string.Empty;
        }
        else
        {
            GpuBar.Value = 0;
            GpuValue.Text = "--";
            GpuDetail.Text = _localization.T("SystemMonitor.NoGpu");
        }

        if (s.DiskActive is { } disk)
        {
            SetBar(DiskBar, DiskValue, disk);
            DiskDetail.Text = s.DiskBytesPerSecond is { } rate ? $"{FormatBytes(rate)}/s" : string.Empty;
        }
        else
        {
            DiskBar.Value = 0;
            DiskValue.Text = "--";
        }

        NetworkValue.Text = $"↓ {FormatBytes(s.NetDownBytesPerSecond)}/s   ↑ {FormatBytes(s.NetUpBytesPerSecond)}/s";
        ShowDrives(s.Drives);
    }

    private static void SetBar(ProgressBar bar, TextBlock value, double percent)
    {
        bar.Value = percent;
        value.Text = $"{percent:0}%";
        // The number is always shown; the color only reinforces it.
        if (percent >= WarnPercent)
        {
            bar.Foreground = s_warnBrush;
        }
        else
        {
            bar.ClearValue(Control.ForegroundProperty);
        }
    }

    private void ShowDrives(IReadOnlyList<SystemMetrics.DriveUsage> drives)
    {
        if (!drives.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(_drives.Keys))
        {
            DriveList.Children.Clear();
            _drives.Clear();
            foreach (var drive in drives)
            {
                var text = new TextBlock { FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };
                var bar = new ProgressBar { Maximum = 100 };
                var panel = new StackPanel { Spacing = 4 };
                var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                title.Children.Add(new FontIcon { Glyph = DriveGlyph, FontSize = 14 });
                title.Children.Add(text);
                panel.Children.Add(title);
                panel.Children.Add(bar);
                DriveList.Children.Add(panel);
                _drives[drive.Name] = (text, bar);
            }
        }

        foreach (var drive in drives)
        {
            var (text, bar) = _drives[drive.Name];
            long used = drive.Total - drive.Free;
            double percent = drive.Total == 0 ? 0 : used * 100.0 / drive.Total;
            text.Text = $"{drive.Name}  {FormatBytes(used)} / {FormatBytes(drive.Total)}  ({percent:0}%)";
            bar.Value = percent;
            if (percent >= WarnPercent)
            {
                bar.Foreground = s_warnBrush;
            }
            else
            {
                bar.ClearValue(Control.ForegroundProperty);
            }
        }
    }

    private static string FormatBytes(double bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        int unit = 0;
        while (bytes >= 1024 && unit < units.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }

        return unit < 2 ? $"{bytes:0} {units[unit]}" : $"{bytes:0.0} {units[unit]}";
    }
}
