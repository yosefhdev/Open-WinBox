using DeskBox.Contracts;
using DeskBox.Controls.WidgetContents;
using DeskBox.Models;

namespace DeskBox.Services;

internal sealed class SystemMonitorWidgetContentProvider : IWidgetContentProvider
{
    public WidgetKind WidgetKind => WidgetKind.SystemMonitor;

    public bool CanCreateDetachedContent => true;

    public IWidgetContent CreateDetachedContent(WidgetConfig config, WidgetContentProviderContext context)
    {
        if (config.WidgetKind != WidgetKind)
        {
            throw new ArgumentException("System Monitor content requires a SystemMonitor widget config.", nameof(config));
        }

        return new SystemMonitorWidgetContentAdapter(config, context.LocalizationService, context.SettingsService);
    }
}
