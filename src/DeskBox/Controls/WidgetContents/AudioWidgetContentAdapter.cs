using DeskBox.Models;
using DeskBox.Services;

namespace DeskBox.Controls.WidgetContents;

/// <summary>OpenWinBox Audio widget host adapter: polls Core Audio only while the widget is visible.</summary>
public sealed class AudioWidgetContentAdapter(WidgetConfig config, LocalizationService localizationService)
    : WidgetContentAdapterBase(config, () => new AudioWidgetContent(localizationService))
{
    public override void OnWindowVisibilityChanged(bool visible) =>
        (MaterializedView as AudioWidgetContent)?.SetActive(visible);

    public override void OnWindowLongHidden() =>
        (MaterializedView as AudioWidgetContent)?.SetActive(false);
}
