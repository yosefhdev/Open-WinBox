using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Windows.Foundation;
using Windows.Graphics;

namespace OpenWinBox;

// Menú del ícono de bandeja con el estilo de la app (acrílico, íconos, interruptores) en vez del menú Win32.
// Se crea una vez y se oculta al perder el foco, como un menú contextual.
public sealed partial class TrayMenu : Window
{
    private const double MenuWidth = 300;
    private readonly App _app = App.Current;

    public TrayMenu()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(NoTitleBar);
        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            p.SetBorderAndTitleBar(true, false);
            p.IsResizable = false;
            p.IsMaximizable = false;
            p.IsMinimizable = false;
            p.IsAlwaysOnTop = true;
        }
        // Fuera de la barra de tareas y Alt+Tab (ver WidgetWindow)
        AppWindow.IsShownInSwitchers = false;
        SetWindowLongPtr(Hwnd, GWL_EXSTYLE, (GetWindowLongPtr(Hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
        Title = "Menú de OpenWinBox";
        AppIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(ShellHost.IconPath));
        Activated += (_, e) => { if (e.WindowActivationState == WindowActivationState.Deactivated) AppWindow.Hide(); };
    }

    private IntPtr Hwnd => Win32Interop.GetWindowFromWindowId(AppWindow.Id);

    // x, y: posición del cursor en píxeles físicos. El menú sale arriba del cursor, centrado y dentro del área de trabajo.
    public void ShowAt(int x, int y)
    {
        App.ApplyTheme(this);
        Status.Text = _app.HotkeyRegistered ? "F7 muestra u oculta los widgets" : "F7 está ocupado por otra app";
        PinSwitch.IsOn = _app.Config.PinToDesktop;
        AutoStartSwitch.IsOn = ShellHost.AutoStart;

        AppWindow.Move(new PointInt32(x, y)); // primero al monitor del cursor, para leer su escala
        var scale = GetDpiForWindow(Hwnd) / 96.0;
        Panel.Measure(new Size(MenuWidth, double.PositiveInfinity));
        var w = (int)Math.Ceiling(MenuWidth * scale);
        var h = (int)Math.Ceiling(Panel.DesiredSize.Height * scale);
        var area = DisplayArea.GetFromPoint(new PointInt32(x, y), DisplayAreaFallback.Nearest).WorkArea;
        var gap = (int)(8 * scale);
        AppWindow.MoveAndResize(new RectInt32(
            Math.Clamp(x - w / 2, area.X + gap, area.X + area.Width - w - gap),
            Math.Clamp(y - h - gap, area.Y + gap, area.Y + area.Height - h - gap),
            w, h));

        Activate();
        // Activate() a veces la muestra sin darle el foco (la app está en segundo plano) y entonces nunca llega
        // Deactivated: sin foco el clic afuera no la cierra. El clic en la bandeja sí nos deja tomar el primer plano.
        SetForegroundWindow(Hwnd);
        PlayEntrance();
    }

    // Aparece subiendo 8 px con un fundido, como los menús de Windows 11
    private void PlayEntrance()
    {
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled) return;
        var visual = ElementCompositionPreview.GetElementVisual(Panel);
        var compositor = visual.Compositor;
        var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1f));
        var offset = compositor.CreateVector3KeyFrameAnimation();
        offset.InsertKeyFrame(0, new Vector3(0, 8, 0));
        offset.InsertKeyFrame(1, Vector3.Zero, easing);
        offset.Duration = TimeSpan.FromMilliseconds(200);
        var fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0, 0);
        fade.InsertKeyFrame(1, 1, easing);
        fade.Duration = TimeSpan.FromMilliseconds(150);
        visual.StartAnimation("Offset", offset);
        visual.StartAnimation("Opacity", fade);
    }

    private void Root_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape) AppWindow.Hide();
    }

    private void Toggle_Click(object sender, RoutedEventArgs e) { AppWindow.Hide(); _app.ToggleWidgets(); }

    // Los interruptores cambian en el lugar sin cerrar el menú
    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        _app.SetPinned(!_app.Config.PinToDesktop);
        PinSwitch.IsOn = _app.Config.PinToDesktop;
    }

    private void AutoStart_Click(object sender, RoutedEventArgs e)
    {
        ShellHost.AutoStart = !ShellHost.AutoStart;
        AutoStartSwitch.IsOn = ShellHost.AutoStart;
    }

    private void Settings_Click(object sender, RoutedEventArgs e) { AppWindow.Hide(); _app.OpenSettings(); }

    private void Exit_Click(object sender, RoutedEventArgs e) => _app.Quit();

    private const int GWL_EXSTYLE = -20;
    private const nint WS_EX_TOOLWINDOW = 0x80, WS_EX_APPWINDOW = 0x40000;

    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(IntPtr hwnd, int index, nint value);
}
