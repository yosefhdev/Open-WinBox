using System;
using System.IO;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace OpenWinBox;

// Asistente de primeros pasos, con la misma estructura que el del DeskBox original:
// ilustración a la izquierda, texto a la derecha, puntos de progreso y botón "Siguiente"
public sealed partial class OnboardingWindow : Window
{
    private const int StepCount = 4;
    private readonly App _app = App.Current;
    private int _step;

    public OnboardingWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragArea);
        AppWindow.SetIcon(ShellHost.IconPath);
        App.ApplyTheme(this);

        // shortcut: tamaño en píxeles físicos; con escala de pantalla >100% se ve más pequeña, escalar por DPI si molesta
        var size = new SizeInt32(940, 580);
        var area = DisplayArea.Primary.WorkArea;
        AppWindow.MoveAndResize(new RectInt32(area.X + (area.Width - size.Width) / 2,
            area.Y + (area.Height - size.Height) / 2, size.Width, size.Height));

        // Cerrar con la X también cuenta como visto: no volver a mostrarlo en cada arranque
        Closed += (_, _) =>
        {
            _app.Config.HasCompletedOnboarding = true;
            _app.Config.Save();
        };

        ShowStep(0);
    }

    private void ShowStep(int step)
    {
        _step = step;
        Pips.SelectedPageIndex = step;
        BackButton.Visibility = step > 0 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Content = step == StepCount - 1 ? "Empezar" : "Siguiente";
        IllustrationKey.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        IllustrationIcon.Visibility = step == 2 ? Visibility.Collapsed : Visibility.Visible;

        switch (step)
        {
            case 0:
                IllustrationIcon.Glyph = "";
                StepTitle.Text = "Una capa de organización ligera en tu escritorio";
                StepText.Text = "OpenWinBox pone tus carpetas en widgets sobre el escritorio: tus accesos directos a mano, " +
                                "sin tapar las apps en las que trabajas.";
                var count = _app.Config.Widgets.Count;
                StepExtra.Content = Check(count > 0
                    ? $"Tienes {count} widget{(count == 1 ? "" : "s")} listo{(count == 1 ? "" : "s")} en el escritorio."
                    : "Aún no tienes widgets; crearás el primero en el siguiente paso.");
                break;

            case 1:
                IllustrationIcon.Glyph = "";
                StepTitle.Text = "Tus carpetas, en widgets";
                StepText.Text = "Cada widget muestra el contenido de una carpeta. Si agregas varias carpetas a un widget, " +
                                "aparecen como pestañas. Doble clic abre el elemento.";
                var result = new StackPanel { Spacing = 10 };
                var create = new Button { Content = "Nuevo widget", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
                create.Click += async (_, _) =>
                {
                    if (await App.PickFolderAsync(this) is not { } folder) return;
                    _app.AddWidget(folder);
                    result.Children.Add(Check($"Widget creado: {Path.GetFileName(folder.TrimEnd('\\'))}"));
                };
                result.Children.Add(create);
                StepExtra.Content = result;
                break;

            case 2:
                StepTitle.Text = "Convocar en cualquier momento, despedir en cualquier momento";
                StepText.Text = "Pulsa F7 o haz clic izquierdo en el ícono de OpenWinBox en la bandeja.";
                var toggle = new Button { Content = "Mostrar u ocultar", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
                toggle.Click += (_, _) => _app.ToggleWidgets();
                StepExtra.Content = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        toggle,
                        _app.HotkeyRegistered
                            ? Check("Prueba la acción o continúa al siguiente paso.")
                            : Check("F7 está ocupado por otra app (¿el DeskBox original?). Ciérrala y reinicia OpenWinBox.", ""),
                    },
                };
                break;

            case 3:
                IllustrationIcon.Glyph = "";
                StepTitle.Text = "Siempre detrás de tus apps";
                StepText.Text = "Los widgets viven en el escritorio y nunca tapan la ventana en la que trabajas. " +
                                "Puedes cambiar esto cuando quieras en Configuración.";
                var pin = new ToggleSwitch { Header = "Mantener los widgets en el escritorio", IsOn = _app.Config.PinToDesktop };
                pin.Toggled += (_, _) => _app.SetPinned(pin.IsOn);
                var autoStart = new ToggleSwitch { Header = "Lanzar al iniciar Windows", IsOn = ShellHost.AutoStart };
                autoStart.Toggled += (_, _) => ShellHost.AutoStart = autoStart.IsOn;
                StepExtra.Content = new StackPanel { Spacing = 8, Children = { pin, autoStart } };
                break;
        }
    }

    private static StackPanel Check(string text, string glyph = "") => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        Children =
        {
            new FontIcon { Glyph = glyph, FontSize = 14, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"] },
            new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
        },
    };

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_step == StepCount - 1) Close();
        else ShowStep(_step + 1);
    }

    private void Back_Click(object sender, RoutedEventArgs e) => ShowStep(Math.Max(0, _step - 1));

    private void Pips_SelectedIndexChanged(PipsPager sender, PipsPagerSelectedIndexChangedEventArgs args)
    {
        if (sender.SelectedPageIndex != _step) ShowStep(sender.SelectedPageIndex);
    }
}
