using DeskBox.Models;
using DeskBox.Services;

namespace DeskBox.Controls.WidgetContents;

/// <summary>OpenWinBox System Monitor host adapter: samples only while the widget is visible.</summary>
public sealed class SystemMonitorWidgetContentAdapter(
    WidgetConfig config,
    LocalizationService localizationService,
    SettingsService? settingsService)
    : WidgetContentAdapterBase(config, () => new SystemMonitorWidgetContent(localizationService, config, settingsService))
{
    public override void OnWindowVisibilityChanged(bool visible) =>
        (MaterializedView as SystemMonitorWidgetContent)?.SetActive(visible);

    public override void OnWindowLongHidden() =>
        (MaterializedView as SystemMonitorWidgetContent)?.SetActive(false);
}
