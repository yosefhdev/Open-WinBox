using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using NAudio.CoreAudioApi;

namespace OpenWinBox;

// Dispositivos de salida/entrada y volumen por aplicación del dispositivo de salida predeterminado
public sealed partial class AudioView : UserControl
{
    // Lo pone WidgetWindow: sube el widget mientras el menú está abierto (si no, otros widgets lo tapan)
    public Action<FlyoutBase, FrameworkElement>? ShowPopup { get; set; }

    private const int IconSize = 20;
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly DispatcherQueueTimer _timer;
    private MMDevice? _output, _input;
    private bool _updating;       // cambios hechos por código: no reenviarlos al sistema
    private long _userChangedAt;  // no pisar con el refresco un slider que el usuario está moviendo
    private readonly Dictionary<string, AppRow> _apps = new();
    private readonly Dictionary<uint, (string Name, string? Path)> _processes = new();
    // FriendlyName lee el almacén de propiedades del dispositivo en cada llamada (~50 ms): leerlo una vez por dispositivo
    private readonly Dictionary<string, string> _deviceNames = new();

    private sealed record AppRow(Slider Slider, FontIcon MuteIcon, List<AudioSessionControl> Sessions);

    public AudioView()
    {
        InitializeComponent();
        // shortcut: sondeo cada 1.5 s en vez de las notificaciones COM de Core Audio; suficiente para un widget
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(1500);
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { Refresh(); _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    private bool UserIsChanging => Environment.TickCount64 - _userChangedAt < 1000;

    private void Refresh()
    {
        _updating = true;
        try
        {
            _output = Default(DataFlow.Render, _output);
            _input = Default(DataFlow.Capture, _input);
            ShowDevice(_output, OutputName, OutputSlider, OutputValue, OutputMuteIcon, "", "");
            ShowDevice(_input, InputName, InputSlider, InputValue, InputMuteIcon, "", "");
            RefreshApps();
        }
        finally { _updating = false; }
    }

    // Reutiliza el MMDevice mientras el predeterminado no cambie
    private MMDevice? Default(DataFlow flow, MMDevice? current)
    {
        try
        {
            if (!_enumerator.HasDefaultAudioEndpoint(flow, Role.Multimedia)) return null;
            var device = _enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
            if (current?.ID == device.ID) { device.Dispose(); return current; }
            current?.Dispose();
            return device;
        }
        catch (COMException) { return null; } // dispositivo desconectándose
    }

    private void ShowDevice(MMDevice? device, TextBlock name, Slider slider, TextBlock value, FontIcon muteIcon, string on, string off)
    {
        slider.IsEnabled = device != null;
        if (device == null) { name.Text = "Ninguno"; value.Text = ""; return; }
        try
        {
            if (!_deviceNames.TryGetValue(device.ID, out var friendly)) _deviceNames[device.ID] = friendly = device.FriendlyName;
            name.Text = friendly;
            var volume = device.AudioEndpointVolume;
            if (!UserIsChanging) slider.Value = Math.Round(volume.MasterVolumeLevelScalar * 100);
            value.Text = $"{slider.Value:0}";
            muteIcon.Glyph = volume.Mute ? off : on;
        }
        catch (COMException) { }
    }

    private void RefreshApps()
    {
        var groups = new Dictionary<string, (string Name, string? Path, List<AudioSessionControl> Sessions)>();
        if (_output != null)
        {
            try
            {
                var manager = _output.AudioSessionManager;
                manager.RefreshSessions();
                var sessions = manager.Sessions;
                for (var i = 0; i < sessions.Count; i++)
                {
                    var s = sessions[i];
                    var (key, name, path) = s.State == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateExpired
                        ? (null, "", null) : Describe(s);
                    if (key == null) { s.Dispose(); continue; }
                    // Una fila por app aunque tenga varias sesiones (p. ej. Chrome)
                    if (!groups.TryGetValue(key, out var g)) groups[key] = g = (name, path, new());
                    g.Sessions.Add(s);
                }
            }
            catch (COMException) { }
        }

        if (!groups.Keys.ToHashSet().SetEquals(_apps.Keys))
        {
            AppList.Children.Clear();
            foreach (var old in _apps.Values.SelectMany(r => r.Sessions)) old.Dispose();
            _apps.Clear();
            foreach (var (key, g) in groups.OrderBy(g => g.Key == "system" ? 0 : 1).ThenBy(g => g.Value.Name))
                _apps[key] = AddAppRow(g.Name, g.Path);
        }
        NoApps.Visibility = groups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var (key, g) in groups)
        {
            var row = _apps[key];
            // Cada consulta trae objetos COM nuevos: liberar los anteriores en vez de esperar al recolector
            foreach (var old in row.Sessions) old.Dispose();
            row.Sessions.Clear();
            row.Sessions.AddRange(g.Sessions);
            var volume = g.Sessions[0].SimpleAudioVolume;
            if (!UserIsChanging) row.Slider.Value = Math.Round(volume.Volume * 100);
            row.MuteIcon.Glyph = volume.Mute ? "" : "";
        }
    }

    private (string? Key, string Name, string? Path) Describe(AudioSessionControl session)
    {
        if (session.IsSystemSoundsSession) return ("system", "Sonidos del sistema", null);
        var pid = session.GetProcessID;
        if (pid == 0) return (null, "", null);
        if (!_processes.TryGetValue(pid, out var info))
        {
            try
            {
                using var p = Process.GetProcessById((int)pid);
                string? path = null, description = null;
                try
                {
                    path = p.MainModule?.FileName;
                    description = p.MainModule?.FileVersionInfo.FileDescription;
                }
                catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { } // proceso protegido
                info = (string.IsNullOrWhiteSpace(description) ? p.ProcessName : description!, path);
            }
            catch (ArgumentException) { return (null, "", null); } // el proceso ya terminó
            _processes[pid] = info;
        }
        return ((info.Path ?? info.Name).ToLowerInvariant(), info.Name, info.Path);
    }

    private AppRow AddAppRow(string name, string? path)
    {
        var grid = new Grid { ColumnSpacing = 8, Padding = new Thickness(6, 0, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new Image { Width = IconSize, Height = IconSize, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(path == null
            ? new FontIcon { Glyph = "", FontSize = 16, Width = IconSize } // sonidos del sistema
            : icon);
        if (path != null) LoadIcon(icon, path);

        var label = new TextBlock { Text = name, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        ToolTipService.SetToolTip(label, name);
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        var slider = new Slider { Maximum = 100, VerticalAlignment = VerticalAlignment.Center };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(slider, $"Volumen de {name}");
        Grid.SetColumn(slider, 2);
        grid.Children.Add(slider);

        var muteIcon = new FontIcon { Glyph = "", FontSize = 14 };
        var mute = new Button { Content = muteIcon, Padding = new Thickness(6) };
        ToolTipService.SetToolTip(mute, "Silenciar");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(mute, $"Silenciar {name}");
        Grid.SetColumn(mute, 3);
        grid.Children.Add(mute);

        var row = new AppRow(slider, muteIcon, new());
        slider.ValueChanged += (_, e) =>
        {
            if (_updating) return;
            _userChangedAt = Environment.TickCount64;
            foreach (var s in row.Sessions) Try(() => s.SimpleAudioVolume.Volume = (float)(e.NewValue / 100));
        };
        mute.Click += (_, _) =>
        {
            if (row.Sessions.Count == 0) return;
            var muted = !row.Sessions[0].SimpleAudioVolume.Mute;
            foreach (var s in row.Sessions) Try(() => s.SimpleAudioVolume.Mute = muted);
            muteIcon.Glyph = muted ? "" : "";
        };
        AppList.Children.Add(grid);
        return row;
    }

    private async void LoadIcon(Image image, string path)
    {
        var size = (int)Math.Round(IconSize * (XamlRoot?.RasterizationScale ?? 1));
        var pixels = await ShellIcon.RunOnStaThread(() => ShellIcon.GetPixels(path, size));
        if (pixels != null) image.Source = pixels.ToImage();
    }

    private static void Try(Action action)
    {
        try { action(); }
        catch (COMException) { } // la sesión o el dispositivo desapareció
    }

    private MMDevice? DeviceOf(object sender) => (string)((FrameworkElement)sender).Tag == "Output" ? _output : _input;

    private void DeviceSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_updating || DeviceOf(sender) is not { } device) return;
        _userChangedAt = Environment.TickCount64;
        Try(() => device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(e.NewValue / 100));
        ((string)((FrameworkElement)sender).Tag == "Output" ? OutputValue : InputValue).Text = $"{e.NewValue:0}";
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceOf(sender) is not { } device) return;
        Try(() => device.AudioEndpointVolume.Mute = !device.AudioEndpointVolume.Mute);
        Refresh();
    }

    private void Device_Click(object sender, RoutedEventArgs e)
    {
        var flow = (string)((FrameworkElement)sender).Tag == "Output" ? DataFlow.Render : DataFlow.Capture;
        var current = DeviceOf(sender)?.ID;
        var menu = new MenuFlyout { ShouldConstrainToRootBounds = false };
        foreach (var device in _enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            var id = device.ID;
            var item = new RadioMenuFlyoutItem { Text = device.FriendlyName, IsChecked = id == current, GroupName = "devices" };
            item.Click += (_, _) =>
            {
                try { AudioPolicy.SetDefault(id); }
                catch (Exception ex) when (ex is COMException or InvalidCastException) { } // Windows no lo permitió
                Refresh();
            };
            menu.Items.Add(item);
            device.Dispose();
        }
        if (menu.Items.Count == 0) menu.Items.Add(new MenuFlyoutItem { Text = "No hay dispositivos", IsEnabled = false });
        if (ShowPopup != null) ShowPopup(menu, (FrameworkElement)sender);
        else menu.ShowAt((FrameworkElement)sender);
    }
}
