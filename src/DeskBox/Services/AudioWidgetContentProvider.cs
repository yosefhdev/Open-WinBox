using DeskBox.Contracts;
using DeskBox.Controls.WidgetContents;
using DeskBox.Models;

namespace DeskBox.Services;

internal sealed class AudioWidgetContentProvider : IWidgetContentProvider
{
    public WidgetKind WidgetKind => WidgetKind.Audio;

    public bool CanCreateDetachedContent => true;

    public IWidgetContent CreateDetachedContent(WidgetConfig config, WidgetContentProviderContext context)
    {
        if (config.WidgetKind != WidgetKind)
        {
            throw new ArgumentException("Audio content requires an Audio widget config.", nameof(config));
        }

        return new AudioWidgetContentAdapter(config, context.LocalizationService);
    }
}
