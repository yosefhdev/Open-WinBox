using DeskBox.Core.Persistence;
using DeskBox.Helpers;
using DeskBox.Platform;
using DeskBox.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace DeskBox.Views;

/// <summary>
/// The pop-up a taskbar folder opens: a grid of its apps next to the cursor.
/// Reuses the stack popover's borderless host window, created once and parked
/// off-screen between shows.
/// </summary>
internal static class TaskbarFolderPopup
{
    private const double TileWidth = 84;
    private const double TileHeight = 88;
    private const double Padding = 12;
    private const double HeaderHeight = 34;
    // One row until the pop-up would pass this share of the monitor width.
    private const double MaxWidthShare = 0.9;
    private static readonly TimeSpan IconWaitLimit = TimeSpan.FromMilliseconds(500);

    private static StackPopoverHostWindow? s_host;
    private static string? s_openFolderId;
    private static string? s_lastDismissedFolderId;
    private static long s_lastDismissedTicks;
    private static FrameworkElement? s_content;
    private static string? s_contentSignature;
    private static readonly List<UIElement> s_tiles = [];
    private static readonly List<Task> s_iconLoads = [];
    private static IntPtr s_mouseHook;
    private static TaskbarEdge s_edge;
    // Bumped by every show and hide, so a finished exit animation only parks
    // the window if nothing reopened it meanwhile.
    private static int s_visibilityGeneration;
    // Held in a field so the GC never collects the delegate the hook calls.
    private static readonly Win32Helper.LowLevelMouseProc s_mouseProc = OnLowLevelMouse;
    // shortcut: icons are cached by path for the process lifetime; an app that
    // changes its icon shows the new one after the app restarts.
    private static readonly Dictionary<string, ImageSource> s_icons = new(StringComparer.OrdinalIgnoreCase);

    public static async Task ToggleAsync(string folderId)
    {
        // A click on the folder's own taskbar button first deactivates the
        // open pop-up (dismiss) and then re-launches us: treat that pair as
        // "close", not "close and reopen".
        if (s_lastDismissedFolderId == folderId &&
            Environment.TickCount64 - s_lastDismissedTicks < 500)
        {
            s_lastDismissedFolderId = null;
            return;
        }

        if (s_openFolderId == folderId && s_host?.IsVisible == true)
        {
            Hide();
            return;
        }

        TaskbarFolder? folder = TaskbarFolderService.Find(folderId);
        if (folder is null)
        {
            App.Log($"[TaskbarFolders] Activation for unknown folder id={folderId}");
            return;
        }

        Win32Helper.GetCursorPos(out Win32Helper.POINT cursor);
        double scale = Win32Helper.GetDpiScaleForPoint(cursor.X, cursor.Y);

        int count = Math.Max(1, folder.Apps.Count);
        double monitorWidthDip = Win32Helper.TryGetMonitorWorkArea(cursor.X, cursor.Y, out Win32Helper.RECT monitorRect, out _)
            ? (monitorRect.Right - monitorRect.Left) / scale
            : 1920;
        int columns = ComputeColumns(count, monitorWidthDip);
        int rows = (int)Math.Ceiling(count / (double)columns);
        double widthDip = (columns * TileWidth) + (Padding * 2);
        double heightDip = HeaderHeight + (rows * TileHeight) + (Padding * 2);

        var host = EnsureHost();
        // Reuse the built tree while the folder is unchanged: rebuilding it and
        // decoding every icon again on each click was the visible lag.
        string signature = columns + "\n" + folder.Id + "\n" + folder.Name + "\n" + string.Join("\n", folder.Apps.Select(a => a.Path));
        if (s_contentSignature != signature || s_content is null)
        {
            s_tiles.Clear();
            s_iconLoads.Clear();
            s_content = BuildContent(folder, columns);
            s_contentSignature = signature;
            host.SetContent(s_content);
        }

        // Show only once the icons are decoded (cached ones are instant), so
        // tiles never pop in empty and fill a moment later.
        await Task.WhenAny(Task.WhenAll(s_iconLoads), Task.Delay(IconWaitLimit));

        ApplyTheme(host);

        (RectInt32 bounds, TaskbarEdge edge) = PlaceAtTaskbar(
            cursor,
            (int)Math.Ceiling(widthDip * scale),
            (int)Math.Ceiling(heightDip * scale),
            (int)Math.Round(12 * scale));
        // Start from the first frame while still parked, so the reveal never
        // shows the previous show's final frame.
        s_visibilityGeneration++;
        s_edge = edge;
        PlayEntrance(s_content, s_tiles, edge);
        host.PrepareForShow(bounds);
        host.RevealPrepared(bounds);
        s_openFolderId = folderId;
        StartOutsideClickWatch();
    }

    /// <summary>
    /// A single row of tiles; a second row only when one row would be wider
    /// than <see cref="MaxWidthShare"/> of the monitor.
    /// </summary>
    internal static int ComputeColumns(int appCount, double monitorWidthDip)
    {
        int fit = (int)Math.Floor(((monitorWidthDip * MaxWidthShare) - (Padding * 2)) / TileWidth);
        return Math.Clamp(appCount, 1, Math.Max(1, fit));
    }

    /// <summary>
    /// Creates the host window and decodes every folder's icons at idle after
    /// startup, so even the first click only has to move a ready window.
    /// </summary>
    public static async void Prewarm()
    {
        try
        {
            TaskbarFolderDocument document = TaskbarFolderService.Load();
            if (document.Folders.Count == 0)
            {
                return;
            }

            int repaired = TaskbarFolderService.RepairShortcutTargets(document);
            if (repaired > 0)
            {
                App.Log($"[TaskbarFolders] Repointed {repaired} folder shortcut(s) at {Environment.ProcessPath}");
            }

            EnsureHost();
            var scratch = new Image();
            foreach (TaskbarFolderApp app in document.Folders.SelectMany(f => f.Apps))
            {
                await LoadIconAsync(scratch, app.Path);
            }
        }
        catch (Exception ex)
        {
            App.Log($"[TaskbarFolders] Prewarm failed: {ex.Message}");
        }
    }

    public static void Hide()
    {
        StopOutsideClickWatch();
        s_openFolderId = null;
        StackPopoverHostWindow? host = s_host;
        if (host?.IsVisible != true)
        {
            return;
        }

        int generation = ++s_visibilityGeneration;
        if (s_content is null || !AnimationsEnabled())
        {
            host.HidePopover();
            return;
        }

        // Exit: a quicker fade and a short sink back toward the taskbar
        // (accelerate curve), then park the window once the compositor is done.
        Visual visual = ElementCompositionPreview.GetElementVisual(s_content);
        Compositor compositor = visual.Compositor;
        CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        CubicBezierEasingFunction accelerate = compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0f), new Vector2(1f, 1f));
        ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(1f, 0f, accelerate);
        fade.Duration = TimeSpan.FromMilliseconds(140);
        Vector3KeyFrameAnimation slide = compositor.CreateVector3KeyFrameAnimation();
        slide.InsertKeyFrame(1f, EdgeOffset(s_edge, 8f), accelerate);
        slide.Duration = TimeSpan.FromMilliseconds(160);
        visual.StartAnimation("Opacity", fade);
        visual.StartAnimation("Translation", slide);
        batch.End();
        batch.Completed += (_, _) =>
        {
            if (generation == s_visibilityGeneration)
            {
                host.HidePopover();
            }
        };
    }

    private static bool AnimationsEnabled() => new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;

    /// <summary>An offset pointing from the panel toward the taskbar.</summary>
    private static Vector3 EdgeOffset(TaskbarEdge edge, float distance) => edge switch
    {
        TaskbarEdge.Top => new Vector3(0, -distance, 0),
        TaskbarEdge.Left => new Vector3(-distance, 0, 0),
        TaskbarEdge.Right => new Vector3(distance, 0, 0),
        _ => new Vector3(0, distance, 0)
    };

    private static void DismissFromOutside()
    {
        if (s_openFolderId is not null)
        {
            s_lastDismissedFolderId = s_openFolderId;
            s_lastDismissedTicks = Environment.TickCount64;
        }

        Hide();
    }

    /// <summary>
    /// Window deactivation alone misses outside clicks when Windows refuses
    /// the pop-up the foreground (it was opened from a background process),
    /// so a low-level mouse hook watches clicks while the pop-up is shown.
    /// It runs on the UI thread, which pumps messages, and is removed on hide.
    /// </summary>
    private static void StartOutsideClickWatch()
    {
        if (s_mouseHook != IntPtr.Zero)
        {
            return;
        }

        s_mouseHook = Win32Helper.SetWindowsMouseHookEx(
            Win32Helper.WH_MOUSE_LL, s_mouseProc, Win32Helper.GetModuleHandle(null), 0);
        if (s_mouseHook == IntPtr.Zero)
        {
            App.Log($"[TaskbarFolders] Outside-click hook failed: {Marshal.GetLastWin32Error()}");
        }
    }

    private static void StopOutsideClickWatch()
    {
        if (s_mouseHook != IntPtr.Zero)
        {
            _ = Win32Helper.UnhookWindowsHookEx(s_mouseHook);
            s_mouseHook = IntPtr.Zero;
        }
    }

    private static IntPtr OnLowLevelMouse(int nCode, IntPtr wParam, IntPtr lParam)
    {
        IntPtr hook = s_mouseHook;
        int message = (int)wParam;
        if (nCode >= 0 &&
            message is Win32Helper.WM_LBUTTONDOWN or Win32Helper.WM_RBUTTONDOWN or Win32Helper.WM_MBUTTONDOWN &&
            s_host?.IsVisible == true)
        {
            Win32Helper.MSLLHOOKSTRUCT data = Marshal.PtrToStructure<Win32Helper.MSLLHOOKSTRUCT>(lParam);
            bool inside = Win32Helper.GetWindowRect(s_host.WindowHandle, out Win32Helper.RECT rect) &&
                data.pt.X >= rect.Left && data.pt.X < rect.Right &&
                data.pt.Y >= rect.Top && data.pt.Y < rect.Bottom;
            if (!inside)
            {
                // Never do window work inside the hook callback itself.
                App.UiDispatcherQueue?.TryEnqueue(DismissFromOutside);
            }
        }

        return Win32Helper.CallNextHookEx(hook, nCode, wParam, lParam);
    }

    private static StackPopoverHostWindow EnsureHost()
    {
        if (s_host is not null)
        {
            return s_host;
        }

        s_host = new StackPopoverHostWindow(IntPtr.Zero) { Title = "OpenWinBox Taskbar Folder" };
        s_host.DeactivatedByOutsideClick += DismissFromOutside;
        s_host.EscapeRequested += Hide;
        return s_host;
    }

    private static void ApplyTheme(StackPopoverHostWindow host)
    {
        ElementTheme theme = App.Current?.ThemeService?.EffectiveTheme ?? ElementTheme.Default;
        bool isDark = theme == ElementTheme.Dark ||
            (theme == ElementTheme.Default && Application.Current.RequestedTheme == ApplicationTheme.Dark);
        if (host.Content is FrameworkElement root)
        {
            root.RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;
        }

        host.UpdateAppearance(
            new WidgetMaterialBackdropAppearance("Acrylic", isDark, default, 1, 1),
            followMaterialStyle: false);
    }

    private static FrameworkElement BuildContent(TaskbarFolder folder, int columns)
    {
        var root = new Grid { Padding = new Thickness(Padding) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HeaderHeight) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var title = new TextBlock
        {
            Text = folder.Name,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(4, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        root.Children.Add(title);

        var grid = new Grid();
        Grid.SetRow(grid, 1);
        for (int c = 0; c < columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TileWidth) });
        }

        if (folder.Apps.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = App.Current?.LocalizationService?.T("TaskbarFolders.Popup.Empty") ?? string.Empty,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4)
            };
            grid.Children.Add(empty);
        }

        for (int i = 0; i < folder.Apps.Count; i++)
        {
            if (i % columns == 0)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TileHeight) });
            }

            FrameworkElement tile = BuildTile(folder.Apps[i]);
            Grid.SetRow(tile, i / columns);
            Grid.SetColumn(tile, i % columns);
            grid.Children.Add(tile);
        }

        root.Children.Add(grid);
        return root;
    }

    /// <summary>
    /// Compositor-driven entrance: the panel fades and glides in from the
    /// taskbar side, then the tiles fade and rise one after another. Start
    /// values are set while the window is still parked, so no stale frame
    /// shows. The decelerate curve has no overshoot (the old front-loaded
    /// curve snapped in and read as a small bounce). Skipped when Windows
    /// animations are off.
    /// </summary>
    private static void PlayEntrance(FrameworkElement root, IReadOnlyList<UIElement> tiles, TaskbarEdge edge)
    {
        bool animate = AnimationsEnabled();
        Vector3 panelFrom = EdgeOffset(edge, 10f);

        Animate(root, panelFrom, TimeSpan.Zero, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(280), animate);
        for (int i = 0; i < tiles.Count; i++)
        {
            // The panel settles first; the cascade is capped so big folders
            // do not wait long for their last tile.
            var delay = TimeSpan.FromMilliseconds(90 + (Math.Min(i, 10) * 28));
            Animate(tiles[i], new Vector3(0, 8, 0), delay, TimeSpan.FromMilliseconds(220), TimeSpan.FromMilliseconds(300), animate);
        }
    }

    private static void Animate(UIElement element, Vector3 from, TimeSpan delay, TimeSpan fadeDuration, TimeSpan slideDuration, bool animate)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation("Opacity");
        visual.StopAnimation("Translation");
        if (!animate)
        {
            visual.Opacity = 1f;
            visual.Properties.InsertVector3("Translation", Vector3.Zero);
            return;
        }

        visual.Opacity = 0f;
        visual.Properties.InsertVector3("Translation", from);

        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction decelerate = compositor.CreateCubicBezierEasingFunction(new Vector2(0f, 0f), new Vector2(0.2f, 1f));
        ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0f, 0f);
        fade.InsertKeyFrame(1f, 1f, decelerate);
        fade.Duration = fadeDuration;
        fade.DelayTime = delay;
        fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        Vector3KeyFrameAnimation slide = compositor.CreateVector3KeyFrameAnimation();
        slide.InsertKeyFrame(0f, from);
        slide.InsertKeyFrame(1f, Vector3.Zero, decelerate);
        slide.Duration = slideDuration;
        slide.DelayTime = delay;
        slide.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.StartAnimation("Opacity", fade);
        visual.StartAnimation("Translation", slide);
    }

    private static FrameworkElement BuildTile(TaskbarFolderApp app)
    {
        var image = new Image { Width = 32, Height = 32, Margin = new Thickness(0, 4, 0, 6) };
        var label = new TextBlock
        {
            Text = app.Name,
            FontSize = 12,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 2,
            TextWrapping = TextWrapping.Wrap
        };
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(image);
        panel.Children.Add(label);

        var button = new Button
        {
            Content = panel,
            Width = TileWidth - 4,
            Height = TileHeight - 4,
            Padding = new Thickness(4),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8)
        };
        ToolTipService.SetToolTip(button, app.Name);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, app.Name);
        button.Click += (_, _) =>
        {
            Hide();
            Win32Helper.OpenFile(app.Path);
        };

        s_iconLoads.Add(LoadIconAsync(image, app.Path));
        s_tiles.Add(button);
        return button;
    }

    private static async Task LoadIconAsync(Image image, string path)
    {
        if (s_icons.TryGetValue(path, out ImageSource? cached))
        {
            image.Source = cached;
            return;
        }

        try
        {
            ImageSource? source = await IconHelper.GetIconAsync(path, hideShortcutArrowOverlay: true, decodePixelWidth: 64);
            if (source is not null)
            {
                s_icons[path] = source;
            }

            image.Source = source;
        }
        catch (Exception ex)
        {
            App.LogVerbose($"[TaskbarFolders] Icon load failed '{path}': {ex.Message}");
        }
    }

    internal enum TaskbarEdge { Bottom, Top, Left, Right }

    /// <summary>
    /// Docks the pop-up to the taskbar of the cursor's monitor, centered on the
    /// folder's button (found under the cursor), never in the middle of the
    /// screen. Without a button (keyboard launch, auto-hide) it still sits
    /// against the taskbar edge, centered on the cursor along that edge.
    /// </summary>
    internal static (RectInt32 Bounds, TaskbarEdge Edge) PlaceAtTaskbar(Win32Helper.POINT cursor, int width, int height, int gap)
    {
        if (!Win32Helper.TryGetMonitorWorkArea(cursor.X, cursor.Y, out Win32Helper.RECT monitor, out Win32Helper.RECT work))
        {
            return (new RectInt32(cursor.X - (width / 2), cursor.Y - height - gap, width, height), TaskbarEdge.Bottom);
        }

        int anchorX = cursor.X;
        int anchorY = cursor.Y;
        try
        {
            if (TaskbarButtonLocator.TryGetButtonBounds(cursor, work, out Win32Helper.RECT button))
            {
                anchorX = (button.Left + button.Right) / 2;
                anchorY = (button.Top + button.Bottom) / 2;
            }
        }
        catch (Exception ex)
        {
            App.LogVerbose($"[TaskbarFolders] Taskbar button lookup failed: {ex.Message}");
        }

        return (ComputeTaskbarBounds(monitor, work, anchorX, anchorY, width, height, gap, out TaskbarEdge edge), edge);
    }

    /// <summary>Pure placement math, unit-tested.</summary>
    internal static RectInt32 ComputeTaskbarBounds(
        Win32Helper.RECT monitor, Win32Helper.RECT work, int anchorX, int anchorY, int width, int height, int gap, out TaskbarEdge edge)
    {
        // The taskbar is the side where the work area is inset from the monitor.
        edge = work.Bottom < monitor.Bottom ? TaskbarEdge.Bottom
            : work.Top > monitor.Top ? TaskbarEdge.Top
            : work.Left > monitor.Left ? TaskbarEdge.Left
            : work.Right < monitor.Right ? TaskbarEdge.Right
            : TaskbarEdge.Bottom;

        int x;
        int y;
        switch (edge)
        {
            case TaskbarEdge.Top:
                x = anchorX - (width / 2);
                y = work.Top + gap;
                break;
            case TaskbarEdge.Left:
                x = work.Left + gap;
                y = anchorY - (height / 2);
                break;
            case TaskbarEdge.Right:
                x = work.Right - width - gap;
                y = anchorY - (height / 2);
                break;
            default:
                x = anchorX - (width / 2);
                y = work.Bottom - height - gap;
                break;
        }

        x = Math.Clamp(x, work.Left + gap, Math.Max(work.Left + gap, work.Right - width - gap));
        y = Math.Clamp(y, work.Top + gap, Math.Max(work.Top + gap, work.Bottom - height - gap));
        return new RectInt32(x, y, width, height);
    }
}
