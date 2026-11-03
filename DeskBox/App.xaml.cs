using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Windows.Storage.Pickers;

namespace OpenWinBox;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;

    public AppConfig Config { get; private set; } = new();
    public bool HotkeyRegistered => _shell?.HotkeyRegistered ?? false;

    private readonly Dictionary<WidgetConfig, WidgetWindow> _widgets = new();
    private ShellHost? _shell;
    private Mutex? _singleInstance;
    private SettingsWindow? _settings;
    private OnboardingWindow? _onboarding;
    private TrayMenu? _trayMenu;
    private WinEventProc? _foregroundProc; // referencia viva: Windows llama a este delegado
    private IntPtr _foregroundHook;

    // Win+D trae el escritorio (Progman/WorkerW) al frente y tapa a los widgets; mientras tanto van encima de él
    public static bool DesktopInFront { get; private set; }

    public App()
    {
        InitializeComponent();
        // Vive en la bandeja: no salir al cerrar la última ventana (p. ej. Configuración sin widgets)
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Una sola instancia: con autoinicio + abrirla a mano saldrían widgets duplicados
        _singleInstance = new Mutex(true, "OpenWinBox.SingleInstance", out var isFirst);
        // Como el original: abrir la app cuando ya está corriendo muestra la Configuración
        var openSettings = new EventWaitHandle(false, EventResetMode.AutoReset, "OpenWinBox.OpenSettings");
        if (!isFirst)
        {
            openSettings.Set();
            Exit();
            return;
        }
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        ThreadPool.RegisterWaitForSingleObject(openSettings,
            (_, _) => dispatcher.TryEnqueue(() => OpenSettings()), null, Timeout.Infinite, executeOnlyOnce: false);

        Config = AppConfig.Load();
        ShellHost.MigrateAutoStart();
        Updater.CleanDownloads();
        foreach (var widget in Config.Widgets.Where(w => !w.IsDisabled && (w.Kind != "File" || w.Folders.Count > 0)))
            OpenWidget(widget);

        // Out-of-context desde el hilo de UI: el callback llega por su loop de mensajes, sin TryEnqueue.
        // Sin los eventos propios: abrir un menú del widget no debe contar como "el escritorio dejó de estar al frente"
        _foregroundProc = OnForegroundChanged;
        _foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _foregroundProc, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        _shell = new ShellHost();
        _shell.ToggleRequested += ToggleWidgets;
        _shell.MenuRequested += (x, y) => (_trayMenu ??= new TrayMenu()).ShowAt(x, y);

        if (!Config.HasCompletedOnboarding) OpenOnboarding();
    }

    // El instalador se niega a seguir mientras exista el mutex: hay que soltarlo antes de lanzarlo y salir enseguida
    public void InstallUpdate(string setupPath)
    {
        _singleInstance?.ReleaseMutex();
        try
        {
            System.Diagnostics.Process.Start(setupPath, new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" });
        }
        catch
        {
            _singleInstance?.WaitOne(0);
            throw;
        }
        Quit();
    }

    public void Quit()
    {
        _shell?.Dispose();
        if (_foregroundHook != IntPtr.Zero) UnhookWinEvent(_foregroundHook);
        foreach (var w in _widgets.Values) w.Close();
        _trayMenu?.Close();
        Exit();
    }

    // Widgets de archivos abiertos (para "Agrupar con…")
    public IEnumerable<WidgetConfig> FileWidgets => _widgets.Keys.Where(w => w.Kind == "File");

    private void OpenWidget(WidgetConfig widget)
    {
        var window = new WidgetWindow(widget, Config.Save);
        _widgets[widget] = window;
        ApplyTheme(window);
        window.Activate();
        window.SetPinnedToDesktop(Config.PinToDesktop); // después de Activate, que siempre la sube al frente
    }

    public void AddWidget(string folder, PointInt32? at = null, int width = 400, int height = 500)
    {
        // Sin posición: centrado en la pantalla principal; el usuario lo arrastra a donde quiera
        var area = DisplayArea.Primary.WorkArea;
        var widget = new WidgetConfig
        {
            Folders = { folder },
            X = at?.X ?? area.X + (area.Width - width) / 2,
            Y = at?.Y ?? area.Y + (area.Height - height) / 2,
            Width = width,
            Height = height,
        };
        // Sin posición y ya hay un widget ahí: 32 px más abajo y a la derecha, para que no queden encimados
        if (at == null)
            while (Config.Widgets.Any(w => w.X == widget.X && w.Y == widget.Y)) { widget.X += 32; widget.Y += 32; }
        Config.Widgets.Add(widget);
        Config.Save();
        OpenWidget(widget);
    }

    // Carpeta vacía propia para widgets/pestañas nuevas: "Nuevo widget", "Nuevo widget (2)"...
    public static string NewEmptyFolder()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenWinBox", "widgets");
        Directory.CreateDirectory(root);
        return Directory.CreateDirectory(FileActions.UniquePath(root, "Nuevo widget")).FullName;
    }

    public void AddEmptyWidget() => AddWidget(NewEmptyFolder());

    public void AddEmptyTab(WidgetConfig widget) => AddFolder(widget, NewEmptyFolder());

    public void RemoveWidget(WidgetConfig widget)
    {
        if (_widgets.Remove(widget, out var window)) window.Close();
        Config.Widgets.Remove(widget);
        Config.Save();
    }

    public void AddFolder(WidgetConfig widget, string folder)
    {
        widget.Folders.Add(folder);
        widget.ActiveIndex = widget.Folders.Count - 1;
        Config.Save();
        Reopen(widget);
    }

    public void RemoveFolder(WidgetConfig widget, string folder)
    {
        widget.Folders.Remove(folder);
        widget.Order.Remove(folder);
        widget.Icons.Remove(folder);
        widget.Titles.Remove(folder);
        widget.ActiveIndex = 0;
        Config.Save();
        Reopen(widget);
    }

    // Las pestañas se arman al crear la ventana: recrearla es lo más simple
    private void Reopen(WidgetConfig widget)
    {
        if (_widgets.Remove(widget, out var window)) window.Close();
        OpenWidget(widget);
    }

    // Con una sola carpeta el nombre visible es Name; al pasar a pestañas se copia a Titles para que no cambie
    private static void KeepName(WidgetConfig w)
    {
        if (w.Name != null && w.Folders.Count == 1 && !w.Titles.ContainsKey(w.Folders[0])) w.Titles[w.Folders[0]] = w.Name;
        // Sin esto, al quitar esa pestaña la que queda heredaría el nombre
        if (w.Folders.Count == 1) w.Name = null;
    }

    // Las pestañas de source se agregan al final de target; source se cierra (sus carpetas siguen en disco)
    public void MergeWidgets(WidgetConfig target, WidgetConfig source)
    {
        if (target == source || target.Kind != "File" || source.Kind != "File") return;
        KeepName(target);
        KeepName(source);
        var first = target.Folders.Count;
        foreach (var folder in source.Folders)
        {
            if (target.Folders.Contains(folder, StringComparer.OrdinalIgnoreCase)) continue;
            target.Folders.Add(folder);
            if (source.Order.TryGetValue(folder, out var order)) target.Order[folder] = order;
            if (source.Icons.TryGetValue(folder, out var icon)) target.Icons[folder] = icon;
            if (source.Titles.TryGetValue(folder, out var title)) target.Titles[folder] = title;
        }
        if (target.Folders.Count > first) target.ActiveIndex = first;
        RemoveWidget(source); // guarda
        Reopen(target);
    }

    // Saca una pestaña a un widget propio con el tamaño del original, con su esquina en "at"
    public void SplitFolder(WidgetConfig source, string folder, PointInt32 at)
    {
        if (source.Folders.Count < 2 || !source.Folders.Contains(folder)) return;
        var widget = new WidgetConfig { Folders = { folder }, X = at.X, Y = at.Y, Width = source.Width, Height = source.Height };
        if (source.Order.TryGetValue(folder, out var order)) widget.Order[folder] = order;
        if (source.Icons.TryGetValue(folder, out var icon)) widget.Icons[folder] = icon;
        if (source.Titles.TryGetValue(folder, out var title)) widget.Titles[folder] = title;
        Config.Widgets.Add(widget);
        RemoveFolder(source, folder); // limpia sus diccionarios, guarda y reabre el original
        OpenWidget(widget);
    }

    // Widgets funcionales: apagar conserva su posición para cuando se vuelvan a encender
    public void SetFeatureEnabled(string kind, bool enabled)
    {
        var widget = Config.Feature(kind);
        widget.IsDisabled = !enabled;
        Config.Save();
        if (!enabled && _widgets.Remove(widget, out var window)) window.Close();
        else if (enabled && !_widgets.ContainsKey(widget)) OpenWidget(widget);
        _settings?.RefreshFeatures();
    }

    // Los widgets funcionales abiertos vuelven a aplicar sus opciones cuando cambian en Configuración
    public static event Action? FeatureOptionsChanged;

    public void SaveFeatureOptions()
    {
        Config.Save();
        FeatureOptionsChanged?.Invoke();
    }

    public void SetWeatherLocation(WeatherPlace? place)
    {
        Config.WeatherLocation = place;
        Config.Save();
        WeatherView.NotifyLocationChanged();
    }

    public void SetPinned(bool pinned)
    {
        Config.PinToDesktop = pinned;
        Config.Save();
        foreach (var w in _widgets.Values) w.SetPinnedToDesktop(pinned);
    }

    public void SetTheme(string theme)
    {
        Config.Theme = theme;
        Config.Save();
        foreach (var w in _widgets.Values) ApplyTheme(w);
        if (_settings != null) ApplyTheme(_settings);
        if (_onboarding != null) ApplyTheme(_onboarding);
    }

    public static void ApplyTheme(Window window)
    {
        if (window.Content is FrameworkElement root)
            root.RequestedTheme = Enum.TryParse<ElementTheme>(Current.Config.Theme, out var t) ? t : ElementTheme.Default;
    }

    public void OpenSettings(string page = "General")
    {
        if (_settings == null)
        {
            _settings = new SettingsWindow(page);
            _settings.Closed += (_, _) => _settings = null;
        }
        else _settings.ShowPage(page);
        // El hook omite ventanas propias: sin esto los widgets topmost taparían Configuración
        if (DesktopInFront) SetDesktopInFront(false, WinRT.Interop.WindowNative.GetWindowHandle(_settings));
        _settings.Activate();
    }

    public void OpenOnboarding()
    {
        if (_onboarding == null)
        {
            _onboarding = new OnboardingWindow();
            _onboarding.Closed += (_, _) => _onboarding = null;
        }
        _onboarding.Activate();
    }

    public static async Task<string?> PickFolderAsync(Window owner)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Desktop };
        picker.FileTypeFilter.Add("*");
        // Apps sin empaquetar: el selector necesita saber cuál es su ventana dueña
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(owner));
        return (await picker.PickSingleFolderAsync())?.Path;
    }

    public void ToggleWidgets()
    {
        // Minimizado cuenta como oculto: F7 debe traerlo de vuelta, no esconderlo
        var anyVisible = _widgets.Values.Any(w => w.AppWindow.IsVisible
            && w.AppWindow.Presenter is not OverlappedPresenter { State: OverlappedPresenterState.Minimized });
        foreach (var w in _widgets.Values)
        {
            if (anyVisible) w.AppWindow.Hide();
            else w.ShowOnDesktop();
        }
    }

    private void OnForegroundChanged(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (hwnd == IntPtr.Zero) return;
        var name = new StringBuilder(64);
        GetClassName(hwnd, name, name.Capacity);
        // Barra de tareas, Inicio, bandeja y popups del shell no cambian si el escritorio está al frente (Win+D → Win+T)
        if (name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Windows.UI.Core.CoreWindow" or "XamlExplorerHostIslandWindow"
            or "NotifyIconOverflowWindow" or "TopLevelWindowForOverflowXamlIsland" or "Xaml_WindowedPopupClass") return;
        var desktop = name.ToString() is "Progman" or "WorkerW";
        if (desktop == DesktopInFront) return;
        // Un clic en el fondo también lo pone en primer plano sin subirlo en Z: solo cuenta si de verdad tapa a algún widget.
        // No subirlos antes de que los tape: si cambian de orden mientras Win+D minimiza, el shell lo cancela y restaura las apps.
        if (desktop && !CoversWidget(hwnd)) { RecheckDesktop(hwnd); return; }
        SetDesktopInFront(desktop, hwnd);
    }

    // Con Win+D el evento llega ~200 ms antes de que el shell suba el escritorio: mirar cada ~16 ms durante 1 s
    // y subir los widgets en cuanto los tape
    private async void RecheckDesktop(IntPtr hwnd)
    {
        for (var i = 0; i < 60; i++)
        {
            await Task.Delay(16);
            if (DesktopInFront || GetForegroundWindow() != hwnd) return;
            if (CoversWidget(hwnd)) { SetDesktopInFront(true, hwnd); return; }
        }
    }

    private bool CoversWidget(IntPtr hwnd)
    {
        var widgets = _widgets.Values.Select(w => w.Hwnd).ToHashSet();
        for (var h = GetWindow(hwnd, GW_HWNDNEXT); h != IntPtr.Zero; h = GetWindow(h, GW_HWNDNEXT))
            if (widgets.Contains(h)) return true;
        return false;
    }

    private async void SetDesktopInFront(bool front, IntPtr foreground)
    {
        DesktopInFront = front;
        foreach (var w in _widgets.Values) w.SetAboveDesktop(front, foreground);
        if (front || !Config.PinToDesktop) return;
        // Al salir de Win+D el shell termina de restaurar el orden después del evento y puede subir algún widget: repetir
        await Task.Delay(300);
        if (!DesktopInFront) foreach (var w in _widgets.Values) w.SetAboveDesktop(false, foreground);
    }

    private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
    private const uint EVENT_SYSTEM_FOREGROUND = 0x3, WINEVENT_OUTOFCONTEXT = 0, WINEVENT_SKIPOWNPROCESS = 0x2;
    private const uint GW_HWNDNEXT = 2;
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc proc, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
}
