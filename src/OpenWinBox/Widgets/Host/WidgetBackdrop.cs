using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace OpenWinBox;

// Acrílico que se ve translúcido siempre, como el fondo de los widgets del original.
// DesktopAcrylicBackdrop pinta un color opaco cuando la ventana está inactiva, y los widgets nunca se activan.
public sealed class WidgetBackdrop : SystemBackdrop
{
    private DesktopAcrylicController? _controller;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot root)
    {
        base.OnTargetConnected(target, root);
        var config = new SystemBackdropConfiguration { IsInputActive = true };
        if (root.Content is FrameworkElement content)
        {
            config.Theme = ToBackdropTheme(content.ActualTheme);
            content.ActualThemeChanged += (s, _) => config.Theme = ToBackdropTheme(s.ActualTheme);
        }
        _controller = new DesktopAcrylicController();
        _controller.AddSystemBackdropTarget(target);
        _controller.SetSystemBackdropConfiguration(config);
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        base.OnTargetDisconnected(target);
        _controller?.RemoveSystemBackdropTarget(target);
        _controller?.Dispose();
        _controller = null;
    }

    private static SystemBackdropTheme ToBackdropTheme(ElementTheme theme) =>
        theme == ElementTheme.Light ? SystemBackdropTheme.Light : SystemBackdropTheme.Dark;
}
