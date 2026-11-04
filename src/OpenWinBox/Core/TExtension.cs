using Microsoft.UI.Xaml.Markup;

namespace OpenWinBox;

// En XAML: Text="{local:T Key=settings.theme}". Se resuelve al crear la ventana; por eso cambiar de idioma la recrea
[MarkupExtensionReturnType(ReturnType = typeof(string))]
public sealed class TExtension : MarkupExtension
{
    public string Key { get; set; } = "";

    protected override object ProvideValue() => Lang.T(Key);
}
