using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.System;

namespace OpenWinBox;

public record FileItem(string Name, string Path, ImageSource? Icon);

public sealed partial class WidgetWindow : Window
{
    private readonly WidgetConfig _config;
    private readonly Action _save;
    // Una lista ya armada por carpeta: cambiar de pestaña solo cambia ItemsSource, sin releer disco ni íconos.
    // ObservableCollection porque GridView solo permite reordenar arrastrando con colecciones observables.
    private readonly Dictionary<string, ObservableCollection<FileItem>> _cache = new();
    private readonly List<FileSystemWatcher> _watchers = new();
    private bool _pinned;
    private int _openPopups;
    private bool PopupOpen => _openPopups > 0;
    private bool _closed; // un menú puede cerrarse después de la ventana (p. ej. al reabrirla desde un ítem)
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _lowerTimer;

    public IntPtr Hwnd => Win32Interop.GetWindowFromWindowId(AppWindow.Id);

    private bool IsFileWidget => _config.Kind == "File";

    public static string FeatureTitle(string kind) => kind switch { "Music" => Lang.T("feature.music"), "Weather" => Lang.T("feature.weather"), _ => kind };

    private void SetupFeature()
    {
        TitleRow.Height = new GridLength(0);
        Items.Visibility = Visibility.Collapsed;
        FeatureHost.Content = _config.Kind switch
        {
            "Music" => new MusicView(),
            "Spotify" => new MusicView { SpotifyOnly = true },
            "Weather" => new WeatherView(),
            "Audio" => new AudioView { ShowPopup = (popup, anchor) => ShowPopup(popup, anchor) },
            _ => new GlanceView(),
        };
        FeatureHost.Visibility = Visibility.Visible;

        // Sin barra de título: se mueve desde la agarradera, que aparece al pasar el mouse. Así un clic en el
        // contenido (botones, foto) nunca mueve el widget por accidente.
        Handle.Visibility = Visibility.Visible;
        Root.PointerEntered += (_, _) => Handle.Opacity = 1;
        Root.PointerExited += (_, _) => { if (!IsHandleDragging) Handle.Opacity = 0; };
        Handle.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(Handle).Properties.IsLeftButtonPressed) return;
            Handle.CapturePointer(e.Pointer);
            BeginHandleDrag();
            e.Handled = true;
        };
        Handle.PointerMoved += (_, _) => { if (IsHandleDragging) MoveHandleDrag(); };
        Handle.PointerReleased += (_, e) => { Handle.ReleasePointerCapture(e.Pointer); EndHandleDrag(); };
        Handle.PointerCaptureLost += (_, _) => EndHandleDrag();
        FeatureHost.RightTapped += (_, e) =>
        {
            e.Handled = true;
            var menu = new MenuFlyout { ShouldConstrainToRootBounds = false };
            if (_config.Kind != "Audio") // Audio no tiene opciones
            {
                menu.Items.Add(Entry(Lang.T("widget.configure", FeatureTitle(_config.Kind)), "", () => App.Current.OpenSettings(_config.Kind)));
                menu.Items.Add(new MenuFlyoutSeparator());
            }
            var off = Entry(Lang.T("widget.disable"), "", () => App.Current.SetFeatureEnabled(_config.Kind, false));
            off.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            menu.Items.Add(off);
            ShowPopup(menu, FeatureHost, e.GetPosition(FeatureHost));
        };
    }

    private string ActiveFolder => ActiveFolderOf(_config);
    private static string ActiveFolderOf(WidgetConfig c) => c.Folders[Math.Clamp(c.ActiveIndex, 0, c.Folders.Count - 1)];

    public WidgetWindow(WidgetConfig config, Action save)
    {
        InitializeComponent();
        _config = config;
        _save = save;

        // Sin barra de título del sistema; DragBar sirve para mover la ventana
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragBar);
        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            p.SetBorderAndTitleBar(true, false);
            p.IsMaximizable = false;
            p.IsMinimizable = false;
        }
        // Fuera de la barra de tareas y Alt+Tab, como en el original: solo Configuración cuenta como ventana.
        // IsShownInSwitchers no alcanza; WS_EX_TOOLWINDOW sí (antes de mostrarla, la barra de tareas no lo relee después)
        AppWindow.IsShownInSwitchers = false;
        // WS_EX_LAYERED: invisible desde antes de mostrarla, para que nunca se vea el cuadro negro de la ventana sin pintar
        SetWindowLongPtr(Hwnd, GWL_EXSTYLE, (GetWindowLongPtr(Hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_LAYERED) & ~WS_EX_APPWINDOW);
        SetShown(0);
        // Nombre para lectores de pantalla en vez de "WinUI Desktop"
        Title = _config.Name ?? (IsFileWidget ? TitleOf(_config, _config.Folders[0]) : FeatureTitle(_config.Kind));

        AppWindow.MoveAndResize(FitToScreen(new RectInt32(_config.X, _config.Y, _config.Width, _config.Height)));
        AppWindow.Changed += OnAppWindowChanged;

        // Bajarla con el botón aún presionado cancela el clic (y el arrastre de la ventana); esperar a que se suelte
        _lowerTimer = DispatcherQueue.CreateTimer();
        _lowerTimer.Interval = TimeSpan.FromMilliseconds(100);
        _lowerTimer.Tick += (t, _) =>
        {
            if ((GetAsyncKeyState(VK_LBUTTON) | GetAsyncKeyState(VK_RBUTTON)) < 0) return;
            t.Stop();
            if (!PopupOpen) SendToBottom();
        };
        Closed += (_, _) => { _closed = true; _watchers.ForEach(w => w.Dispose()); _lowerTimer.Stop(); _fade?.Stop(); };

        InitSnapping();
        if (!IsFileWidget)
        {
            SetupFeature();
            return;
        }
        BuildTabs();
        UpdateLockIcon();
        // Pestaña activa primero, el resto se precarga en segundo plano para que el cambio sea instantáneo
        foreach (var folder in _config.Folders.OrderBy(f => f != ActiveFolder))
        {
            Watch(folder);
            var load = LoadFolderAsync(folder);
            if (folder == ActiveFolder) _firstLoad = load;
        }
        ShowFolder();
    }

    // ---------- Animaciones de entrada y salida ----------

    private const int FirstPaintMs = 150, EnterMs = 280, ExitMs = 160, MaxIconWaitMs = 1500;
    private Task _firstLoad = Task.CompletedTask;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _fade;
    private double _shown;
    private bool _leaving;

    // Aparece ya con acrílico e íconos listos. delayMs escalona los widgets al abrir la app.
    public async Task EnterAsync(int delayMs = 0)
    {
        await Task.WhenAny(_firstLoad, Task.Delay(MaxIconWaitMs));
        await Task.Delay(FirstPaintMs + delayMs);
        if (!_leaving && !_closed) await AnimateShown(1, EnterMs, t => 1 - Math.Pow(1 - t, 3));
    }

    public Task FadeOutAsync()
    {
        _leaving = true;
        return AnimateShown(0, ExitMs, t => t * t * t);
    }

    public async Task CloseAnimatedAsync()
    {
        await FadeOutAsync();
        if (!_closed) Close();
    }

    private Task AnimateShown(double to, int ms, Func<double, double> ease)
    {
        _fade?.Stop();
        var done = new TaskCompletionSource();
        if (_closed) { done.SetResult(); return done.Task; }
        // Respeta "Efectos de animación" de Windows
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled) ms = 0;
        var from = _shown;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        _fade = DispatcherQueue.CreateTimer();
        _fade.Interval = TimeSpan.FromMilliseconds(16);
        _fade.Tick += (timer, _) =>
        {
            var t = ms == 0 ? 1 : Math.Min(1, clock.ElapsedMilliseconds / (double)ms);
            SetShown(from + (to - from) * ease(t));
            if (t < 1) return;
            timer.Stop();
            done.TrySetResult();
        };
        _fade.Start();
        return done.Task;
    }

    // 0 = oculto, 1 = normal: la ventana se desvanece y el contenido sube y crece un poco
    private void SetShown(double shown)
    {
        _shown = shown;
        SetLayeredWindowAttributes(Hwnd, 0, (byte)Math.Round(shown * 255), LWA_ALPHA);
        var scale = (float)(0.97 + 0.03 * shown);
        Root.CenterPoint = new System.Numerics.Vector3(Root.ActualSize / 2, 0);
        Root.Scale = new System.Numerics.Vector3(scale, scale, 1);
        Root.Translation = new System.Numerics.Vector3(0, (float)((1 - shown) * 8), 0);
    }

    public void ShowOnDesktop()
    {
        // Win+D u otras apps pueden minimizar el widget; Show() solo no lo restaura
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } p) p.Restore(false);
        AppWindow.Show(false);
    }

    // Activado: el widget nunca sube sobre otras ventanas, ni al abrir la app ni al darle clic.
    // WS_EX_NOACTIVATE: recibe el mouse pero Windows nunca lo activa, así que no roba el foco a otras apps.
    public void SetPinnedToDesktop(bool pinned)
    {
        _pinned = pinned;
        var style = GetWindowLongPtr(Hwnd, GWL_EXSTYLE);
        SetWindowLongPtr(Hwnd, GWL_EXSTYLE, pinned ? style | WS_EX_NOACTIVATE : style & ~WS_EX_NOACTIVATE);
        if (pinned) SendToBottom();
    }

    // Con el escritorio al frente (Win+D) Windows lo deja sobre todo lo que no es topmost: HWND_TOP no basta.
    // Al salir, colocarse detrás de la app que tomó el foco (o al fondo si está fijado); eso también quita el topmost.
    public void SetAboveDesktop(bool above, IntPtr foreground)
    {
        if (_closed) return; // una pasada diferida (300 ms) puede llegar tras cerrar la app
        if (above || _pinned) SendToBottom();
        // Detrás de una app topmost el widget heredaría el topmost para siempre: solo quitárselo
        else if ((GetWindowLongPtr(foreground, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0)
            SetWindowPos(Hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        else SetWindowPos(Hwnd, foreground, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    // Si el monitor guardado ya no existe (o cambió la resolución), mover la ventana al área visible más cercana
    private static RectInt32 FitToScreen(RectInt32 r)
    {
        var area = DisplayArea.GetFromRect(r, DisplayAreaFallback.Nearest).WorkArea;
        r.X = Math.Clamp(r.X, area.X, area.X + Math.Max(0, area.Width - r.Width));
        r.Y = Math.Clamp(r.Y, area.Y, area.Y + Math.Max(0, area.Height - r.Height));
        return r;
    }

    private static string FolderName(string path) =>
        Path.GetFileName(path.TrimEnd('\\')) is { Length: > 0 } n ? n : path;

    private string TitleFor(string folder) => TitleOf(_config, folder);

    internal static string TitleOf(WidgetConfig c, string folder) =>
        c.Titles.GetValueOrDefault(folder) ?? (c.Folders.Count == 1 ? c.Name : null) ?? FolderName(folder);

    // ---------- Barra de título: una pestaña por carpeta ----------

    private void BuildTabs()
    {
        TabsPanel.Children.Clear();
        var grouped = _config.Folders.Count > 1;
        for (var i = 0; i < _config.Folders.Count; i++)
        {
            var index = i;
            var folder = _config.Folders[i];
            var tab = new Button
            {
                Padding = new Thickness(grouped ? 8 : 6, 3, 8, 3),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 7,
                    Children =
                    {
                        IconPicker.Render(_config.Icons.GetValueOrDefault(folder), grouped ? 12 : 13),
                        new TextBlock
                        {
                            Text = TitleFor(folder), FontSize = grouped ? 12 : 13,
                            FontWeight = grouped ? Microsoft.UI.Text.FontWeights.Normal : Microsoft.UI.Text.FontWeights.SemiBold,
                        },
                    },
                },
            };
            tab.Click += (_, _) => { if (!_tabDragged) SelectTab(index); };
            tab.RightTapped += (_, e) =>
            {
                e.Handled = true;
                ShowTitleMenu(tab, folder);
            };
            if (grouped) DragTab(tab, index);
            TabsPanel.Children.Add(tab);
        }
        UpdateTabHighlight();
    }

    // ---------- Arrastre de pestañas: reordenar, o soltar fuera del widget para separarla ----------

    private Button? _dragTab;
    private int _dragFrom;
    private Windows.Foundation.Point _dragStart;
    private bool _tabDragged;  // hubo arrastre: el Click que le sigue no cambia de pestaña
    private bool _dragOutside;
    private Windows.Foundation.Point _grabInTab; // dónde se tomó la pestaña, para que la copia no salte
    private DragGhost? _ghost;

    private void DragTab(Button tab, int index)
    {
        // Button marca el puntero como manejado: handledEventsToo para recibirlo igual
        tab.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            if (!e.GetCurrentPoint(tab).Properties.IsLeftButtonPressed) return;
            _dragTab = tab;
            _dragFrom = index;
            _dragStart = e.GetCurrentPoint(TabsPanel).Position;
            _grabInTab = e.GetCurrentPoint(tab.Content as UIElement ?? tab).Position;
            _tabDragged = _dragOutside = false;
            tab.CapturePointer(e.Pointer); // con captura, PointerMoved llega aunque el cursor salga de la ventana
        }), true);
        tab.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, e) =>
        {
            if (_dragTab == tab) MoveTab(e.GetCurrentPoint(TabsPanel).Position);
        }), true);
        // Button suelta la captura al soltar: según el orden llega primero uno u otro, EndTabDrag solo actúa una vez
        tab.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, e) => EndTabDrag(e.GetCurrentPoint(TabsPanel).Position.X)), true);
        tab.PointerCaptureLost += (_, e) => EndTabDrag(e.GetCurrentPoint(TabsPanel).Position.X);
    }

    private void MoveTab(Windows.Foundation.Point p)
    {
        // Umbral: un clic normal sigue seleccionando la pestaña
        if (!_tabDragged && Math.Abs(p.X - _dragStart.X) < 6 && Math.Abs(p.Y - _dragStart.Y) < 6) return;
        _tabDragged = true;
        GetCursorPos(out var c);
        var b = VisualBounds(Hwnd);
        var m = (int)(20 * GetDpiForWindow(Hwnd) / 96.0);
        var outside = c.X < b.L - m || c.X > b.R + m || c.Y < b.T - m || c.Y > b.B + m;
        if (outside != _dragOutside)
        {
            if (outside) _statusBeforeDrag = StatusText.Text; // no perder un mensaje de error previo
            StatusText.Text = outside ? Lang.T("widget.splitHint") : _statusBeforeDrag;
            // Fuera del widget, una copia semitransparente de la pestaña sigue al cursor
            if (outside && _dragTab!.Content is UIElement content) _ghost = DragGhost.Show(content, c.X - GhostOffsetX, c.Y - GhostOffsetY);
            else { _ghost?.Close(); _ghost = null; }
        }
        else _ghost?.Move(c.X - GhostOffsetX, c.Y - GhostOffsetY);
        _dragOutside = outside;
        // La pestaña sigue al cursor (como en un navegador), por encima de las demás
        var tab = _dragTab!;
        tab.Opacity = outside ? 0.4 : 0.6;
        Canvas.SetZIndex(tab, 1);
        if (tab.RenderTransform is not TranslateTransform t) tab.RenderTransform = t = new TranslateTransform();
        t.X = p.X - _dragStart.X;
    }

    // Cursor sobre el mismo punto de la pestaña donde se tomó (10/6 = margen de la tarjeta de DragGhost)
    private int GhostOffsetX => (int)((_grabInTab.X + 10) * GetDpiForWindow(Hwnd) / 96.0);
    private int GhostOffsetY => (int)((_grabInTab.Y + 6) * GetDpiForWindow(Hwnd) / 96.0);

    private void EndTabDrag(double x)
    {
        if (_dragTab is not { } tab) return;
        _dragTab = null;
        _ghost?.Close();
        _ghost = null;
        tab.Opacity = 1;
        tab.RenderTransform = null;
        Canvas.SetZIndex(tab, 0);
        if (_dragOutside) StatusText.Text = _statusBeforeDrag;
        // Captura perdida con el botón aún presionado (otra ventana la tomó): cancelar
        if (!_tabDragged || GetAsyncKeyState(VK_LBUTTON) < 0) return;
        var folder = _config.Folders[_dragFrom];
        if (_dragOutside)
        {
            GetCursorPos(out var c);
            // Esta ventana se recrea al separar: no cerrarla dentro de su propio evento de puntero
            DispatcherQueue.TryEnqueue(() => App.Current.SplitFolder(_config, folder, new PointInt32(c.X - 20, c.Y - 10)));
            return;
        }
        // Destino: cuántas de las otras pestañas tienen su centro a la izquierda del cursor
        var to = 0;
        for (var i = 0; i < TabsPanel.Children.Count; i++)
        {
            if (i == _dragFrom) continue;
            var other = (FrameworkElement)TabsPanel.Children[i];
            if (other.TransformToVisual(TabsPanel).TransformPoint(default).X + other.ActualWidth / 2 < x) to++;
        }
        if (to == _dragFrom) return;
        var active = ActiveFolder;
        _config.Folders.RemoveAt(_dragFrom);
        _config.Folders.Insert(to, folder);
        _config.ActiveIndex = _config.Folders.IndexOf(active); // la misma carpeta sigue seleccionada
        _save();
        DispatcherQueue.TryEnqueue(BuildTabs); // no quitar la pestaña dentro de su propio evento
    }

    private void UpdateTabHighlight()
    {
        for (var i = 0; i < TabsPanel.Children.Count; i++)
            ((Button)TabsPanel.Children[i]).Background = _config.Folders.Count > 1 && i == _config.ActiveIndex
                ? (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"]
                : new SolidColorBrush(Colors.Transparent);
    }

    private void SelectTab(int index)
    {
        if (index == _config.ActiveIndex) return;
        _config.ActiveIndex = index;
        ShowFolder();
        _save();
    }

    private void ShowFolder()
    {
        StatusText.Text = "";
        UpdateTabHighlight();
        // Si aún no terminó de cargar, LoadFolderAsync la mostrará al acabar
        Items.ItemsSource = _cache.GetValueOrDefault(ActiveFolder);
    }

    // ---------- Carga de carpetas ----------

    private void Watch(string folder)
    {
        if (!Directory.Exists(folder)) return;
        // shortcut: cada evento recarga toda la carpeta; agregar debounce si hay carpetas grandes o muchos cambios seguidos
        var watcher = new FileSystemWatcher(folder) { EnableRaisingEvents = true };
        FileSystemEventHandler reload = (_, _) => DispatcherQueue.TryEnqueue(() => _ = LoadFolderAsync(folder));
        watcher.Created += reload;
        watcher.Deleted += reload;
        watcher.Renamed += (s, e) => reload(s, e);
        _watchers.Add(watcher);
    }

    private async Task LoadFolderAsync(string folder)
    {
        // Pedir el tamaño exacto en píxeles de esta pantalla: si se pide más grande, los íconos sin esa resolución
        // (p. ej. ARC Raiders, que llega hasta 48) vienen centrados con margen y se ven más chicos que en el original
        var iconPx = (int)Math.Ceiling(IconSize * GetDpiForWindow(Hwnd) / 96.0);
        var order = _config.Order.GetValueOrDefault(folder)?.ToList();
        List<(string Name, string Path, IconPixels? Icon)> entries;
        try
        {
            // Disco y shell en un hilo STA aparte; solo la creación de imágenes ocurre en el hilo de UI.
            // Las APIs de Windows.Storage devuelven 0 elementos en esta app sin empaquetar; por eso System.IO.
            entries = await ShellIcon.RunOnStaThread(() => new DirectoryInfo(folder).EnumerateFileSystemInfos()
                .Where(e => !e.Attributes.HasFlag(FileAttributes.Hidden))
                // Igual que el original: sin extensión (".lnk", ".url", ".exe"...) en los archivos
                .Select(e => (Name: e is FileInfo ? Path.GetFileNameWithoutExtension(e.Name) : e.Name,
                    Path: e.FullName, Icon: ShellIcon.GetPixels(e.FullName, iconPx)))
                .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                // Orden manual primero; los archivos nuevos (sin posición guardada) quedan al final, por nombre
                .OrderBy(e => order?.FindIndex(n => n.Equals(Path.GetFileName(e.Path), StringComparison.OrdinalIgnoreCase)) is >= 0 and var i ? i : int.MaxValue)
                .ToList());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (folder == ActiveFolder) StatusText.Text = Directory.Exists(folder) ? Lang.T("widget.error", ex.Message) : Lang.T("widget.missing", folder);
            return;
        }

        _cache[folder] = new(entries.Select(e => new FileItem(e.Name, e.Path, e.Icon?.ToImage())));
        if (folder == ActiveFolder) Items.ItemsSource = _cache[folder];
    }

    private bool _ownDrag; // el arrastre salió de este widget: reordenar lo maneja el GridView

    // Da los archivos reales al arrastre para que se puedan soltar fuera (escritorio, Explorador, otro widget)
    private void Items_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        var paths = e.Items.OfType<FileItem>().Select(i => i.Path).ToList();
        try
        {
            // shortcut: bloquea la UI mientras Windows resuelve las rutas; pasar a SetDataProvider si tarda en unidades de red
            var items = Task.Run(async () =>
            {
                var list = new List<Windows.Storage.IStorageItem>();
                foreach (var path in paths)
                    list.Add(Directory.Exists(path)
                        ? await Windows.Storage.StorageFolder.GetFolderFromPathAsync(path)
                        : await Windows.Storage.StorageFile.GetFileFromPathAsync(path));
                return list;
            }).GetAwaiter().GetResult();
            e.Data.SetStorageItems(items, false); // false: si es de solo lectura, Windows solo permite copiar
            e.Data.RequestedOperation = DataPackageOperation.Move | DataPackageOperation.Copy | DataPackageOperation.Link;
            _ownDrag = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = ex.Message;
            e.Cancel = true;
        }
    }

    // Tras soltar un ícono en otro lugar, guardar el orden actual de la pestaña
    private void Items_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        _ownDrag = false;
        if (_cache.GetValueOrDefault(ActiveFolder) is not { } items) return;
        _config.Order[ActiveFolder] = items.Select(i => Path.GetFileName(i.Path)).ToList();
        _save();
    }

    // ---------- Soltar archivos desde el escritorio o el Explorador ----------

    private static bool IsShortcut(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".lnk" or ".url";

    private void Items_DragOver(object sender, DragEventArgs e)
    {
        // Solo archivos de fuera; reordenar íconos del propio widget lo maneja el GridView
        if (_ownDrag || !e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var copy = e.Modifiers.HasFlag(Windows.ApplicationModel.DataTransfer.DragDrop.DragDropModifiers.Control);
        e.AcceptedOperation = copy ? DataPackageOperation.Copy : DataPackageOperation.Move | DataPackageOperation.Link;
        e.DragUIOverride.Caption = Lang.T("widget.dropAdd", TitleFor(ActiveFolder));
        e.Handled = true;
    }

    // Accesos directos: se mueven a la carpeta del widget, como el original (Ctrl = copiar).
    // Cualquier otra cosa (un .exe de juego, una carpeta): se crea un acceso directo, para no sacar
    // el archivo real de donde debe estar.
    private async void Items_Drop(object sender, DragEventArgs e)
    {
        if (_ownDrag || !e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        e.Handled = true;
        var folder = ActiveFolder;
        var copy = e.Modifiers.HasFlag(Windows.ApplicationModel.DataTransfer.DragDrop.DragDropModifiers.Control);
        var deferral = e.GetDeferral();
        try
        {
            foreach (var item in await e.DataView.GetStorageItemsAsync())
            {
                var path = item.Path;
                if (string.IsNullOrEmpty(path) || Path.GetDirectoryName(path) == folder.TrimEnd('\\')) continue; // ya está aquí
                Try(() =>
                {
                    if (!IsShortcut(path)) FileActions.CreateShortcutTo(path, folder);
                    else if (copy) FileActions.CopyTo(path, folder);
                    else FileActions.MoveTo(path, folder);
                });
            }
        }
        finally { deferral.Complete(); }
        // El FileSystemWatcher recarga la pestaña; lo nuevo aparece al final del orden manual
    }

    // ---------- Menús contextuales (los mismos grupos que el original) ----------

    private void Items_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var menu = new MenuFlyout { ShouldConstrainToRootBounds = false };
        var folder = ActiveFolder;
        if ((e.OriginalSource as FrameworkElement)?.DataContext is FileItem item)
        {
            Items.SelectedItem = item;
            menu.Items.Add(Entry(Lang.T("widget.open"), "", () => FileActions.Open(item.Path), "Enter"));
            menu.Items.Add(Entry(Lang.T("widget.runAsAdmin"), "", () => FileActions.RunAsAdmin(item.Path)));
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(Entry(Lang.T("widget.rename"), "", () => _ = RenameItemAsync(item), "F2"));
            menu.Items.Add(Entry(Lang.T("widget.copyPath"), "", () => CopyText(item.Path)));
            menu.Items.Add(Entry(Lang.T("widget.openLocation"), "", () => FileActions.ShowInExplorer(item.Path)));
            menu.Items.Add(Entry(Lang.T("widget.properties"), "", () => FileActions.ShowProperties(item.Path)));
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(Entry(Lang.T("widget.backToDesktop"), "",
                () => Try(() => FileActions.MoveTo(item.Path, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)))));
            menu.Items.Add(Entry(Lang.T("widget.recycle"), "", () => Try(() => FileActions.MoveToRecycleBin(item.Path)), Lang.T("widget.recycle.key")));
        }
        else
        {
            menu.Items.Add(Entry(Lang.T("common.refresh"), "", () => _ = LoadFolderAsync(folder), "F5"));
            menu.Items.Add(Entry(Lang.T("widget.newFolder"), "", () => Try(() => Directory.CreateDirectory(FileActions.UniquePath(folder, Lang.T("widget.newFolder"))))));
            menu.Items.Add(Entry(Lang.T("widget.openInExplorer"), "", () => FileActions.Open(folder)));
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(Entry(Lang.T("widget.sortByName"), "", () =>
            {
                _config.Order.Remove(folder);
                _save();
                _ = LoadFolderAsync(folder);
            }));
            menu.Items.Add(Entry(Lang.T("widget.customizeIcon"), "", () => PickIcon(folder)));
        }
        ShowPopup(menu, Items, e.GetPosition(Items));
        e.Handled = true;
    }

    private void ShowTitleMenu(FrameworkElement anchor, string folder)
    {
        var app = App.Current;
        var menu = new MenuFlyout { ShouldConstrainToRootBounds = false };
        menu.Items.Add(Entry(Lang.T("widget.rename"), "", () => _ = RenameTitleAsync(anchor, folder)));
        menu.Items.Add(Entry(Lang.T("widget.customizeIcon"), "", () => PickIcon(folder)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Entry(Lang.T("widget.groupFolder"), "", async () =>
        {
            if (await App.PickFolderAsync(this) is { } added) app.AddFolder(_config, added);
        }));
        if (_config.Folders.Count > 1)
            menu.Items.Add(Entry(Lang.T("widget.removeTab"), "", () => app.RemoveFolder(_config, folder)));
        menu.Items.Add(Entry(Lang.T("widget.openInExplorer"), "", () => FileActions.Open(folder)));
        menu.Items.Add(Entry(Lang.T("widget.fileWidgetSettings"), "", () => app.OpenSettings("Widgets")));
        menu.Items.Add(new MenuFlyoutSeparator());
        var remove = Entry(Lang.T("widget.disable"), "", () => app.RemoveWidget(_config));
        remove.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        menu.Items.Add(remove);
        ShowPopup(menu, anchor);
    }

    private static MenuFlyoutItem Entry(string text, string glyph, Action action, string? shortcut = null)
    {
        var item = new MenuFlyoutItem { Text = text, Icon = new FontIcon { Glyph = glyph } };
        if (shortcut != null) item.KeyboardAcceleratorTextOverride = shortcut;
        item.Click += (_, _) => action();
        return item;
    }

    private void PickIcon(string folder)
    {
        var tab = TabsPanel.Children[_config.Folders.IndexOf(folder)] as FrameworkElement ?? TabsPanel;
        ShowPopup(IconPicker.Create(this, spec =>
        {
            if (spec == null) _config.Icons.Remove(folder);
            else _config.Icons[folder] = spec;
            _save();
            BuildTabs();
        }), tab);
    }

    // Los menús son ventanas que quedan pegadas a la del widget; si el widget está al fondo, otros widgets
    // los tapan. Subirlo mientras el menú está abierto y devolverlo al fondo al cerrarlo.
    private void ShowPopup(FlyoutBase popup, FrameworkElement anchor, Windows.Foundation.Point? at = null)
    {
        // Contador: un menú que abre otro (p. ej. Lang.T("widget.customizeIcon")) se cierra después de abrir el segundo
        _openPopups++;
        SetWindowPos(Hwnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        popup.Closed += (_, _) =>
        {
            if (--_openPopups == 0 && _pinned && !_closed) SendToBottom();
        };
        if (at is { } point) popup.ShowAt(anchor, new FlyoutShowOptions { Position = point });
        else popup.ShowAt(anchor);
    }

    private async Task RenameTitleAsync(FrameworkElement anchor, string folder)
    {
        if (await PromptAsync(anchor, Lang.T("widget.widgetName"), TitleFor(folder)) is not { } name) return;
        if (name.Length == 0) _config.Titles.Remove(folder); // vacío = volver al nombre de la carpeta
        else _config.Titles[folder] = name;
        _save();
        BuildTabs();
    }

    private async Task RenameItemAsync(FileItem item)
    {
        var anchor = Items.ContainerFromItem(item) as FrameworkElement ?? Items;
        if (await PromptAsync(anchor, Lang.T("widget.rename"), item.Name) is not { Length: > 0 } name || name == item.Name) return;
        var isFile = File.Exists(item.Path);
        // La extensión no se muestra (como el original), así que se conserva al renombrar
        var target = Path.Combine(Path.GetDirectoryName(item.Path)!, name + (isFile ? Path.GetExtension(item.Path) : ""));
        if (File.Exists(target) || Directory.Exists(target))
        {
            StatusText.Text = Lang.T("widget.exists", Path.GetFileName(target));
            return;
        }
        Try(() =>
        {
            if (isFile) File.Move(item.Path, target);
            else Directory.Move(item.Path, target);
            // Conservar su lugar en el orden manual
            if (_config.Order.GetValueOrDefault(ActiveFolder) is { } order)
            {
                var i = order.FindIndex(n => n.Equals(Path.GetFileName(item.Path), StringComparison.OrdinalIgnoreCase));
                if (i >= 0) order[i] = Path.GetFileName(target);
                _save();
            }
        });
    }

    // Cuadro de texto flotante; puede salir de los límites del widget (los widgets chicos no caben un diálogo)
    private async Task<string?> PromptAsync(FrameworkElement anchor, string title, string initial)
    {
        // Con WS_EX_NOACTIVATE la ventana no recibe teclado: quitarlo mientras se escribe
        SetWindowLongPtr(Hwnd, GWL_EXSTYLE, GetWindowLongPtr(Hwnd, GWL_EXSTYLE) & ~WS_EX_NOACTIVATE);
        Activate();

        var done = new TaskCompletionSource<string?>();
        var box = new TextBox { Text = initial, Width = 240 };
        var ok = new Button { Content = Lang.T("widget.ok"), Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        var flyout = new Flyout
        {
            ShouldConstrainToRootBounds = false,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = title, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] },
                    box, ok,
                },
            },
        };
        ok.Click += (_, _) => { done.TrySetResult(box.Text.Trim()); flyout.Hide(); };
        box.KeyDown += (_, e) =>
        {
            if (e.Key != VirtualKey.Enter) return;
            done.TrySetResult(box.Text.Trim());
            flyout.Hide();
        };
        flyout.Opened += (_, _) => { box.Focus(FocusState.Programmatic); box.SelectAll(); };
        flyout.Closed += (_, _) => done.TrySetResult(null); // cerrar sin Aceptar = cancelar
        ShowPopup(flyout, anchor);

        try { return await done.Task; }
        finally { SetPinnedToDesktop(_pinned); }
    }

    private static void CopyText(string text)
    {
        var data = new DataPackage();
        data.SetText(text);
        Clipboard.SetContent(data);
    }

    // Errores de disco visibles en la barra del widget en vez de cerrar la app
    private void Try(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StatusText.Text = ex.Message; }
    }

    // ---------- Ventana ----------

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        // WinUI sube la ventana al hacer clic aunque tenga WS_EX_NOACTIVATE: devolverla al fondo al soltar el mouse
        if (_pinned && !PopupOpen && !App.DesktopInFront && args.DidZOrderChange && !args.IsZOrderAtBottom) _lowerTimer.Start();
        if (!args.DidPositionChange && !args.DidSizeChange) return;
        // Minimizada, Windows la manda a (-32000,-32000) con tamaño mínimo: no guardar eso
        if (sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } p)
        {
            // Win+D (u otra app) lo minimizó; un widget de escritorio no se minimiza nunca
            if (sender.IsVisible) DispatcherQueue.TryEnqueue(() => { if (_closed) return; p.Restore(false); if (_pinned) SendToBottom(); });
            return;
        }
        _config.X = sender.Position.X;
        _config.Y = sender.Position.Y;
        _config.Width = sender.Size.Width;
        _config.Height = sender.Size.Height;
        _save();
    }

    private void Items_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is FileItem item) FileActions.Open(item.Path);
    }

    private void Items_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && Items.SelectedItem is FileItem item) FileActions.Open(item.Path);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var menu = new MenuFlyout { ShouldConstrainToRootBounds = false };
        menu.Items.Add(Entry(Lang.T("common.newWidget"), "", App.Current.AddEmptyWidget));
        menu.Items.Add(Entry(Lang.T("widget.newTab"), "", () => App.Current.AddEmptyTab(_config)));
        ShowPopup(menu, (FrameworkElement)sender);
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        var folder = ActiveFolder;
        // Renombrar lo que se ve junto al icono de la pestaña activa
        var tab = TabsPanel.Children[_config.Folders.IndexOf(folder)] as FrameworkElement ?? TabsPanel;
        var menu = new MenuFlyout { ShouldConstrainToRootBounds = false };
        menu.Items.Add(Entry(Lang.T("widget.rename"), "", () => _ = RenameTitleAsync(tab, folder)));
        menu.Items.Add(Entry(Lang.T("widget.changeIcon"), "", () => PickIcon(folder)));
        menu.Items.Add(new MenuFlyoutSeparator());
        var lockItem = new ToggleMenuFlyoutItem { Text = Lang.T("widget.lockPosition"), IsChecked = _config.IsLocked, Icon = new FontIcon { Glyph = "\uE72E" } };
        lockItem.Click += (_, _) =>
        {
            _config.IsLocked = !_config.IsLocked;
            _save();
            UpdateLockIcon();
        };
        menu.Items.Add(lockItem);
        menu.Items.Add(GroupMenu());
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Entry(Lang.T("widget.hide"), "", () => AppWindow.Hide()));
        var off = Entry(Lang.T("widget.disable"), "", () => App.Current.RemoveWidget(_config));
        off.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        menu.Items.Add(off);
        ShowPopup(menu, (FrameworkElement)sender);
    }

    private void UpdateLockIcon() => LockIcon.Visibility = _config.IsLocked ? Visibility.Visible : Visibility.Collapsed;

    // Un elemento por cada otro widget de archivos: sus pestañas se suman a este y el otro se cierra
    private MenuFlyoutSubItem GroupMenu()
    {
        var sub = new MenuFlyoutSubItem { Text = Lang.T("widget.groupWith"), Icon = new FontIcon { Glyph = "\uE71B" } };
        foreach (var other in App.Current.FileWidgets.Where(w => w != _config).ToList())
        {
            var folder = ActiveFolderOf(other);
            var text = other.Folders.Count > 1 ? Lang.T("widget.tabCount", TitleOf(other, folder), other.Folders.Count) : TitleOf(other, folder);
            var item = new MenuFlyoutItem { Text = text, Icon = IconPicker.MenuIcon(other.Icons.GetValueOrDefault(folder)) };
            // Esta ventana se recrea al unir: no cerrarla dentro de su propio evento
            item.Click += (_, _) => DispatcherQueue.TryEnqueue(() => App.Current.MergeWidgets(_config, other));
            sub.Items.Add(item);
        }
        if (sub.Items.Count == 0)
        {
            sub.IsEnabled = false;
            sub.Items.Add(new MenuFlyoutItem { Text = Lang.T("widget.noOthers"), IsEnabled = false });
        }
        return sub;
    }

    // Con el escritorio al frente, el fondo queda bajo Progman y el widget desaparecería: topmost en su lugar
    private void SendToBottom() =>
        SetWindowPos(Hwnd, App.DesktopInFront ? HWND_TOPMOST : HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

    private const int IconSize = 30; // igual que iconSize en la configuración del DeskBox original
    private static readonly IntPtr HWND_BOTTOM = new(1), HWND_TOP = IntPtr.Zero, HWND_TOPMOST = new(-1), HWND_NOTOPMOST = new(-2);
    private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;
    private const int GWL_EXSTYLE = -20, VK_LBUTTON = 0x01, VK_RBUTTON = 0x02;
    private const nint WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOOLWINDOW = 0x80, WS_EX_APPWINDOW = 0x40000, WS_EX_TOPMOST = 0x8, WS_EX_LAYERED = 0x80000;
    private const uint LWA_ALPHA = 0x2;

    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint key, byte alpha, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(IntPtr hwnd, int index, nint value);
}
