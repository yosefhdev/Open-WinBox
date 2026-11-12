using DeskBox.Models;
using DeskBox.Services;

namespace DeskBox.Controls.WidgetContents;

/// <summary>OpenWinBox Battery host adapter: reads batteries only while the widget is visible.</summary>
public sealed class BatteryWidgetContentAdapter(WidgetConfig config, LocalizationService localizationService, SettingsService? settingsService)
    : WidgetContentAdapterBase(config, () => new BatteryWidgetContent(localizationService, config, settingsService))
{
    public override void OnWindowVisibilityChanged(bool visible) =>
        (MaterializedView as BatteryWidgetContent)?.SetActive(visible);

    public override void OnWindowLongHidden() =>
        (MaterializedView as BatteryWidgetContent)?.SetActive(false);
}
