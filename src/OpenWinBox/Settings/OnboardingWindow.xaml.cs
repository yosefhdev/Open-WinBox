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
        Title = Lang.T("onboarding.title");
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
        NextButton.Content = step == StepCount - 1 ? Lang.T("onboarding.start") : Lang.T("onboarding.next");
        IllustrationKey.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        IllustrationIcon.Visibility = step == 2 ? Visibility.Collapsed : Visibility.Visible;

        switch (step)
        {
            case 0:
                IllustrationIcon.Glyph = "";
                StepTitle.Text = Lang.T("onboarding.step1.title");
                StepText.Text = Lang.T("onboarding.step1.text");
                var count = _app.Config.Widgets.Count;
                StepExtra.Content = Check(count > 0
                    ? (count == 1 ? Lang.T("onboarding.step1.readyOne") : Lang.T("onboarding.step1.readyMany", count))
                    : Lang.T("onboarding.step1.none"));
                break;

            case 1:
                IllustrationIcon.Glyph = "";
                StepTitle.Text = Lang.T("onboarding.step2.title");
                StepText.Text = Lang.T("onboarding.step2.text");
                var result = new StackPanel { Spacing = 10 };
                var create = new Button { Content = Lang.T("common.newWidget"), Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
                create.Click += async (_, _) =>
                {
                    if (await App.PickFolderAsync(this) is not { } folder) return;
                    _app.AddWidget(folder);
                    result.Children.Add(Check(Lang.T("onboarding.step2.created", Path.GetFileName(folder.TrimEnd('\\')))));
                };
                result.Children.Add(create);
                StepExtra.Content = result;
                break;

            case 2:
                StepTitle.Text = Lang.T("onboarding.step3.title");
                StepText.Text = Lang.T("onboarding.step3.text");
                var toggle = new Button { Content = Lang.T("onboarding.step3.toggle"), Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
                toggle.Click += (_, _) => _app.ToggleWidgets();
                StepExtra.Content = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        toggle,
                        _app.HotkeyRegistered
                            ? Check(Lang.T("onboarding.step3.try"))
                            : Check(Lang.T("onboarding.step3.busy"), ""),
                    },
                };
                break;

            case 3:
                IllustrationIcon.Glyph = "";
                StepTitle.Text = Lang.T("onboarding.step4.title");
                StepText.Text = Lang.T("onboarding.step4.text");
                var pin = new ToggleSwitch { Header = Lang.T("onboarding.step4.pin"), IsOn = _app.Config.PinToDesktop };
                pin.Toggled += (_, _) => _app.SetPinned(pin.IsOn);
                var autoStart = new ToggleSwitch { Header = Lang.T("onboarding.step4.autoStart"), IsOn = ShellHost.AutoStart };
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
