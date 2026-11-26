using CommunityToolkit.WinUI.Controls;
using DeskBox.Core.Persistence;
using DeskBox.Helpers;
using DeskBox.Platform;
using DeskBox.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.Storage.Pickers;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;

namespace DeskBox.Views.SettingsSections;

/// <summary>
/// Taskbar folders: create folders, pick their apps, choose an icon and get
/// the pinnable shortcut. Data lives in <see cref="TaskbarFolderService"/>'s
/// own file, so this section writes it directly (no main-process round trip);
/// the main process reads it fresh on every folder click.
/// </summary>
public sealed partial class TaskbarFoldersSettingsSection : UserControl
{
    private static readonly int[] IconSizes = [256, 48, 32, 24, 16];
    private const int MinImageSize = 48;

    private LocalizationService? _localization;
    private IntPtr _hwnd;
    private TaskbarFolderDocument _document = new();
    private string? _expandedFolderId;
    private readonly Dictionary<string, SettingsExpander> _expanders = [];
    private readonly Dictionary<string, Button> _emojiButtons = [];

    public TaskbarFoldersSettingsSection()
    {
        InitializeComponent();
    }

    public void Configure(LocalizationService localization, IntPtr hwnd)
    {
        _localization = localization;
        _hwnd = hwnd;
        Reload();
    }

    private string T(string key) => _localization?.T(key) ?? key;

    private void Reload()
    {
        _document = TaskbarFolderService.Load();
        Render();
    }

    /// <summary>Full rebuild; only for adding or removing folders.</summary>
    private void Render()
    {
        FoldersHost.Children.Clear();
        _expanders.Clear();
        _emojiButtons.Clear();
        EmptyCard.Visibility = _document.Folders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (TaskbarFolder folder in _document.Folders)
        {
            FoldersHost.Children.Add(BuildFolderExpander(folder));
        }
    }

    /// <summary>
    /// Updates one folder in place after a save. Rebuilding every expander
    /// re-ran the expand animation and reloaded every icon, which read as the
    /// whole window flashing; the name and icon cards keep their controls.
    /// </summary>
    private void RefreshFolder(TaskbarFolder folder)
    {
        if (!_expanders.TryGetValue(folder.Id, out SettingsExpander? expander) || !_document.Folders.Contains(folder))
        {
            Render();
            return;
        }

        expander.Header = folder.Name;
        expander.Description = string.Format(T("TaskbarFolders.AppCount"), folder.Apps.Count);
        expander.HeaderIcon = new ImageIcon { Source = LoadIconPreview(folder) };
        if (_emojiButtons.TryGetValue(folder.Id, out Button? emoji))
        {
            emoji.Content = BuildEmojiButtonContent(folder.Emoji);
        }

        while (expander.Items.Count > 2)
        {
            expander.Items.RemoveAt(expander.Items.Count - 1);
        }

        AddAppAndPinCards(expander, folder);
    }

    // ── Folder card ──

    private SettingsExpander BuildFolderExpander(TaskbarFolder folder)
    {
        var expander = new SettingsExpander
        {
            Header = folder.Name,
            Description = string.Format(T("TaskbarFolders.AppCount"), folder.Apps.Count),
            HeaderIcon = new ImageIcon { Source = LoadIconPreview(folder) },
            IsExpanded = folder.Id == _expandedFolderId
        };
        expander.Expanded += (_, _) => _expandedFolderId = folder.Id;
        _expanders[folder.Id] = expander;

        expander.Items.Add(BuildNameCard(folder));
        expander.Items.Add(BuildIconCard(folder));
        AddAppAndPinCards(expander, folder);
        return expander;
    }

    private void AddAppAndPinCards(SettingsExpander expander, TaskbarFolder folder)
    {
        expander.Items.Add(BuildAppsCard(folder));
        foreach (TaskbarFolderApp app in folder.Apps)
        {
            expander.Items.Add(BuildAppRow(folder, app));
        }

        expander.Items.Add(BuildPinCard(folder));
    }

    private static ImageSource? LoadIconPreview(TaskbarFolder folder)
    {
        if (string.IsNullOrEmpty(folder.IconFile) || !File.Exists(folder.IconFile))
        {
            return null;
        }

        return new BitmapImage(new Uri(folder.IconFile)) { DecodePixelWidth = 32 };
    }

    private SettingsCard BuildNameCard(TaskbarFolder folder)
    {
        var box = new TextBox { Text = folder.Name, MinWidth = 220, MaxLength = 60 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(box, T("TaskbarFolders.Name"));
        async void Commit()
        {
            string name = box.Text.Trim();
            if (name.Length == 0 || name == folder.Name)
            {
                box.Text = folder.Name;
                return;
            }

            string previousShortcut = TaskbarFolderService.GetShortcutPath(folder);
            folder.Name = name;
            await SaveAsync(folder, regenerateIcon: false, previousShortcut);
        }

        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                Commit();
            }
        };
        return new SettingsCard
        {
            Header = T("TaskbarFolders.Name"),
            HeaderIcon = new FontIcon { Glyph = "" },
            Content = box
        };
    }

    private SettingsCard BuildIconCard(TaskbarFolder folder)
    {
        var kind = new ComboBox { MinWidth = 160 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(kind, T("TaskbarFolders.Icon.Title"));
        foreach (TaskbarFolderIconKind value in Enum.GetValues<TaskbarFolderIconKind>())
        {
            kind.Items.Add(new ComboBoxItem { Content = T("TaskbarFolders.Icon." + value), Tag = value });
        }

        kind.SelectedIndex = (int)folder.IconKind;

        var emoji = new Button
        {
            Content = BuildEmojiButtonContent(folder.Emoji),
            MinWidth = 72,
            Visibility = folder.IconKind == TaskbarFolderIconKind.Emoji ? Visibility.Visible : Visibility.Collapsed
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(emoji, T("TaskbarFolders.Icon.PickEmoji"));
        _emojiButtons[folder.Id] = emoji;
        emoji.Flyout = BuildEmojiFlyout(async picked =>
        {
            folder.Emoji = picked;
            folder.IconKind = TaskbarFolderIconKind.Emoji;
            await SaveAsync(folder, regenerateIcon: true);
        });

        var pickImage = new Button
        {
            Content = T("TaskbarFolders.Icon.ChooseImage"),
            Style = FindStyle("ActionButtonStyle"),
            Visibility = folder.IconKind == TaskbarFolderIconKind.Image ? Visibility.Visible : Visibility.Collapsed
        };

        kind.SelectionChanged += async (_, _) =>
        {
            if (kind.SelectedItem is not ComboBoxItem { Tag: TaskbarFolderIconKind selected } || selected == folder.IconKind)
            {
                return;
            }

            emoji.Visibility = selected == TaskbarFolderIconKind.Emoji ? Visibility.Visible : Visibility.Collapsed;
            pickImage.Visibility = selected == TaskbarFolderIconKind.Image ? Visibility.Visible : Visibility.Collapsed;
            if (selected == TaskbarFolderIconKind.Default ||
                (selected == TaskbarFolderIconKind.Emoji && !string.IsNullOrWhiteSpace(folder.Emoji)) ||
                (selected == TaskbarFolderIconKind.Image && File.Exists(folder.ImagePath)))
            {
                folder.IconKind = selected;
                await SaveAsync(folder, regenerateIcon: true);
            }
            else if (selected == TaskbarFolderIconKind.Emoji)
            {
                emoji.Flyout.ShowAt(emoji);
            }
        };

        pickImage.Click += async (_, _) => await PickImageAsync(folder);

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(emoji);
        panel.Children.Add(pickImage);
        panel.Children.Add(kind);
        return new SettingsCard
        {
            Header = T("TaskbarFolders.Icon.Title"),
            Description = T("TaskbarFolders.Icon.Description"),
            HorizontalContentAlignment = HorizontalAlignment.Right,
            HeaderIcon = new FontIcon { Glyph = "" },
            Content = panel
        };
    }

    private SettingsCard BuildAppsCard(TaskbarFolder folder)
    {
        var addPinned = new Button { Content = T("TaskbarFolders.Apps.AddApps"), Style = FindStyle("ActionButtonStyle") };
        addPinned.Click += async (_, _) => await AddSystemAppsAsync(folder);
        var addFile = new Button { Content = T("TaskbarFolders.Apps.AddFile"), Style = FindStyle("ActionButtonStyle") };
        addFile.Click += async (_, _) => await AddFileAsync(folder);

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(addPinned);
        panel.Children.Add(addFile);
        return new SettingsCard
        {
            Header = T("TaskbarFolders.Apps.Title"),
            Description = folder.Apps.Count == 0 ? T("TaskbarFolders.Apps.None") : T("TaskbarFolders.Apps.Description"),
            HorizontalContentAlignment = HorizontalAlignment.Right,
            HeaderIcon = new FontIcon { Glyph = "" },
            Content = panel
        };
    }

    private SettingsCard BuildAppRow(TaskbarFolder folder, TaskbarFolderApp app)
    {
        var icon = new Image { Width = 20, Height = 20 };
        _ = LoadAppIconAsync(icon, app.Path);
        var remove = new Button { Content = T("TaskbarFolders.Apps.Remove"), Style = FindStyle("ActionButtonStyle") };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(remove, $"{T("TaskbarFolders.Apps.Remove")} {app.Name}");
        remove.Click += async (_, _) =>
        {
            TaskbarFolderService.RemoveApp(folder, app);
            await SaveAsync(folder, regenerateIcon: false);
        };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        header.Children.Add(icon);
        header.Children.Add(new TextBlock { Text = app.Name, VerticalAlignment = VerticalAlignment.Center });
        return new SettingsCard
        {
            Header = header,
            HorizontalContentAlignment = HorizontalAlignment.Right,
            Content = remove
        };
    }

    /// <summary>
    /// One primary action plus an overflow menu. Three buttons side by side
    /// squeezed the header to a sliver, so the description wrapped one word
    /// per line and the card grew hundreds of pixels tall.
    /// </summary>
    private SettingsCard BuildPinCard(TaskbarFolder folder)
    {
        bool pinned = TaskbarFolderService.IsPinned(folder);
        var pin = new Button
        {
            Content = T(pinned ? "TaskbarFolders.Unpin.Button" : "TaskbarFolders.Pin.Button"),
            Style = FindStyle(pinned ? "ActionButtonStyle" : "AccentActionButtonStyle")
        };
        pin.Click += async (_, _) => await TogglePinAsync(folder, pinned);

        var show = new MenuFlyoutItem { Text = T("TaskbarFolders.Pin.ShowShortcut"), Icon = new FontIcon { Glyph = "" } };
        show.Click += async (_, _) =>
        {
            string path = TaskbarFolderService.GetShortcutPath(folder);
            if (!File.Exists(path))
            {
                await SaveAsync(folder, regenerateIcon: folder.IconFile is null || !File.Exists(folder.IconFile));
            }

            Win32Helper.ShowInExplorer(path);
        };

        var delete = new MenuFlyoutItem { Text = T("TaskbarFolders.Delete"), Icon = new FontIcon { Glyph = "" } };
        if (Application.Current.Resources.TryGetValue("SystemFillColorCriticalBrush", out object critical) && critical is Brush criticalBrush)
        {
            delete.Foreground = criticalBrush;
        }

        delete.Click += async (_, _) => await DeleteFolderAsync(folder);

        var menu = new MenuFlyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight };
        menu.Items.Add(show);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(delete);
        var more = new Button
        {
            Content = new FontIcon { Glyph = "", FontSize = 14 },
            Width = 34,
            Height = 34,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            Flyout = menu
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(more, T("Common.More"));
        ToolTipService.SetToolTip(more, T("Common.More"));

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(pin);
        panel.Children.Add(more);
        return new SettingsCard
        {
            Header = T("TaskbarFolders.Pin.Title"),
            Description = T(pinned ? "TaskbarFolders.Pin.Pinned" : "TaskbarFolders.Pin.Description"),
            HeaderIcon = new FontIcon { Glyph = "" },
            HorizontalContentAlignment = HorizontalAlignment.Right,
            Content = panel
        };
    }

    private Style? FindStyle(string key) =>
        Resources.TryGetValue(key, out object local) && local is Style style ? style :
        Application.Current.Resources.TryGetValue(key, out object app) && app is Style appStyle ? appStyle : null;

    // ── Actions ──

    private async Task TogglePinAsync(TaskbarFolder folder, bool pinned)
    {
        var dialog = new ContentDialog
        {
            Title = T(pinned ? "TaskbarFolders.Unpin.Confirm.Title" : "TaskbarFolders.Pin.Confirm.Title"),
            Content = new TextBlock { Text = T("TaskbarFolders.Pin.Confirm.Body"), TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = T(pinned ? "TaskbarFolders.Unpin.Button" : "TaskbarFolders.Pin.Button"),
            CloseButtonText = T("Common.Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        ErrorInfoBar.IsOpen = false;
        try
        {
            if (pinned)
            {
                await Task.Run(() => TaskbarFolderService.Unpin(folder));
            }
            else
            {
                if (!File.Exists(TaskbarFolderService.GetShortcutPath(folder)) || folder.IconFile is null || !File.Exists(folder.IconFile))
                {
                    await SaveAsync(folder, regenerateIcon: folder.IconFile is null || !File.Exists(folder.IconFile));
                }

                await Task.Run(() => TaskbarFolderService.Pin(folder));
            }

            TaskbarFolderService.Save(_document);
        }
        catch (Exception ex)
        {
            App.Log($"[TaskbarFolders] Pin toggle failed: {ex}");
            ShowError(string.Format(T("TaskbarFolders.Pin.Failed"), ex.Message));
        }

        RefreshFolder(folder);
    }

    private async void NewFolderButton_Click(object sender, RoutedEventArgs e)
    {
        TaskbarFolder folder = TaskbarFolderService.CreateFolder(_document, T("TaskbarFolders.DefaultName"));
        _expandedFolderId = folder.Id;
        await SaveAsync(folder, regenerateIcon: true);
    }

    private async Task AddSystemAppsAsync(TaskbarFolder folder)
    {
        var search = new AutoSuggestBox
        {
            PlaceholderText = T("TaskbarFolders.Apps.Search"),
            QueryIcon = new SymbolIcon(Symbol.Find)
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(search, T("TaskbarFolders.Apps.Search"));
        var grid = new GridView
        {
            SelectionMode = ListViewSelectionMode.Multiple,
            Height = 420,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var status = new TextBlock
        {
            Text = T("TaskbarFolders.Apps.NoApps"),
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var loading = new ProgressRing { IsActive = true, Margin = new Thickness(0, 24, 0, 0) };
        var content = new StackPanel { Width = 640 };
        content.Children.Add(search);
        content.Children.Add(loading);
        content.Children.Add(status);
        content.Children.Add(grid);

        var dialog = new ContentDialog
        {
            Title = T("TaskbarFolders.Apps.AddApps"),
            Content = content,
            PrimaryButtonText = T("TaskbarFolders.Apps.AddSelected"),
            IsPrimaryButtonEnabled = false,
            CloseButtonText = T("Common.Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        dialog.Resources["ContentDialogMaxWidth"] = 720.0;

        var selected = new HashSet<TaskbarSystemApp>();
        var tiles = new List<GridViewItem>();
        bool loaded = false;
        bool refiltering = false;
        bool closed = false;
        dialog.Closed += (_, _) => closed = true;

        grid.SelectionChanged += (_, e) =>
        {
            if (refiltering)
            {
                return;
            }

            foreach (object item in e.AddedItems)
            {
                if (item is GridViewItem { Tag: TaskbarSystemApp app })
                {
                    selected.Add(app);
                }
            }

            foreach (object item in e.RemovedItems)
            {
                if (item is GridViewItem { Tag: TaskbarSystemApp app })
                {
                    selected.Remove(app);
                }
            }

            dialog.IsPrimaryButtonEnabled = selected.Count > 0;
        };

        // Rebuilds the visible tiles; the selection lives in `selected` so it
        // survives typing a different search.
        void ApplyFilter()
        {
            string query = search.Text.Trim();
            refiltering = true;
            grid.Items.Clear();
            foreach (GridViewItem tile in tiles)
            {
                if (tile.Tag is TaskbarSystemApp app &&
                    (query.Length == 0 || app.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
                {
                    grid.Items.Add(tile);
                    if (selected.Contains(app))
                    {
                        grid.SelectedItems.Add(tile);
                    }
                }
            }

            refiltering = false;
            status.Visibility = loaded && grid.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        search.TextChanged += (_, args) =>
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                ApplyFilter();
            }
        };

        dialog.Opened += async (_, _) =>
        {
            IReadOnlyList<TaskbarSystemApp> apps = await Task.Run(TaskbarFolderService.GetSystemApps);
            var present = new HashSet<string>(folder.Apps.Select(a => a.Name), StringComparer.CurrentCultureIgnoreCase);
            var images = new List<(Image Image, string ParsingName)>();
            foreach (TaskbarSystemApp app in apps.Where(a => !present.Contains(a.Name)))
            {
                var image = new Image { Width = 32, Height = 32 };
                var name = new TextBlock
                {
                    Text = app.Name,
                    FontSize = 12,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxLines = 2
                };
                var tileContent = new StackPanel { Width = 96, Height = 84, Spacing = 6, Padding = new Thickness(4, 8, 4, 4) };
                tileContent.Children.Add(image);
                tileContent.Children.Add(name);
                var tile = new GridViewItem { Content = tileContent, Tag = app };
                ToolTipService.SetToolTip(tile, app.Name);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(tile, app.Name);
                tiles.Add(tile);
                images.Add((image, app.ParsingName));
            }

            loaded = true;
            loading.IsActive = false;
            loading.Visibility = Visibility.Collapsed;
            ApplyFilter();
            search.Focus(FocusState.Programmatic);

            Microsoft.UI.Dispatching.DispatcherQueue? dispatcher = App.UiDispatcherQueue;
            foreach ((Image image, string parsingName) in images)
            {
                if (closed || dispatcher is null)
                {
                    break;
                }

                byte[]? bytes = await Task.Run(() => IconHelper.GetAppsFolderIconPngBytes(parsingName));
                if (bytes is not null)
                {
                    image.Source = await IconHelper.CreateBitmapImageAsync(dispatcher, bytes, 32);
                }
            }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary || selected.Count == 0)
        {
            return;
        }

        try
        {
            bool changed = false;
            foreach (TaskbarSystemApp app in selected.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                changed |= TaskbarFolderService.AddSystemApp(folder, app) is not null;
            }

            if (changed)
            {
                await SaveAsync(folder, regenerateIcon: false);
            }
        }
        catch (Exception ex)
        {
            App.Log($"[TaskbarFolders] Add apps failed: {ex}");
            ShowError(string.Format(T("TaskbarFolders.Error"), ex.Message));
        }
    }

    // ── Emoji picker: same catalog and categories as the widget icon picker ──

    private static TextBlock BuildEmojiButtonContent(string? emoji) => new()
    {
        Text = string.IsNullOrWhiteSpace(emoji) ? "\U0001F642" : emoji,
        FontFamily = new FontFamily("Segoe UI Emoji"),
        FontSize = 18
    };

    /// <summary>
    /// Category chips wrap to their own width (a GridView sizes every cell like
    /// the first one and clipped longer labels); emojis are plain buttons so a
    /// click always reaches the handler.
    /// </summary>
    private Flyout BuildEmojiFlyout(Func<string, Task> onPick)
    {
        var flyout = new Flyout();
        var chips = new DeskBox.Controls.CenteredWrapPanel { HorizontalSpacing = 6, VerticalSpacing = 6 };
        var grid = new VariableSizedWrapGrid
        {
            Orientation = Orientation.Horizontal,
            ItemWidth = 42,
            ItemHeight = 42,
            MaximumRowsOrColumns = 8
        };
        var chipButtons = new List<ToggleButton>();

        void ShowCategory(WidgetEmojiCategory category)
        {
            foreach (ToggleButton chip in chipButtons)
            {
                chip.IsChecked = ReferenceEquals(chip.Tag, category);
            }

            grid.Children.Clear();
            foreach (string value in category.Emojis)
            {
                var button = new Button
                {
                    Width = 40,
                    Height = 40,
                    Padding = new Thickness(0),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    Content = new TextBlock
                    {
                        Text = value,
                        FontFamily = new FontFamily("Segoe UI Emoji"),
                        FontSize = 20
                    }
                };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, value);
                button.Click += async (_, _) =>
                {
                    flyout.Hide();
                    await onPick(value);
                };
                grid.Children.Add(button);
            }
        }

        foreach (WidgetEmojiCategory category in WidgetTitleIconEmojiCatalog.Categories)
        {
            var chip = new ToggleButton
            {
                Tag = category,
                Content = T("Widget.CustomIcon.Category." + category.Id),
                Padding = new Thickness(10, 3, 10, 4),
                CornerRadius = new CornerRadius(12),
                FontSize = 12
            };
            chip.Click += (_, _) => ShowCategory(category);
            chipButtons.Add(chip);
            chips.Children.Add(chip);
        }

        if (WidgetTitleIconEmojiCatalog.Categories.Length > 0)
        {
            ShowCategory(WidgetTitleIconEmojiCatalog.Categories[0]);
        }

        var panel = new StackPanel { Width = 344, Spacing = 10 };
        panel.Children.Add(chips);
        panel.Children.Add(new ScrollViewer { Content = grid, MaxHeight = 264 });
        flyout.Content = panel;
        return flyout;
    }

    private async Task AddFileAsync(TaskbarFolder folder)
    {
        IReadOnlyList<string> paths = await FileOpenPickerService.PickFilesAsync(_hwnd);
        bool changed = false;
        foreach (string path in paths)
        {
            changed |= TaskbarFolderService.AddApp(folder, path) is not null;
        }

        if (changed)
        {
            await SaveAsync(folder, regenerateIcon: false);
        }
    }

    private async Task PickImageAsync(TaskbarFolder folder)
    {
        string? path = await FileOpenPickerService.PickSingleFileAsync(
            _hwnd,
            [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".ico"],
            PickerLocationId.PicturesLibrary);
        if (path is null)
        {
            return;
        }

        try
        {
            using IRandomAccessStream stream = await (await Windows.Storage.StorageFile.GetFileFromPathAsync(path)).OpenReadAsync();
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            if (decoder.PixelWidth < MinImageSize || decoder.PixelHeight < MinImageSize)
            {
                ShowError(T("TaskbarFolders.Icon.ImageTooSmall"));
                return;
            }

            TaskbarFolderService.ImportImage(folder, path);
            await SaveAsync(folder, regenerateIcon: true);
        }
        catch (Exception ex)
        {
            ShowError(string.Format(T("TaskbarFolders.Error"), ex.Message));
        }
    }

    private async Task DeleteFolderAsync(TaskbarFolder folder)
    {
        var dialog = new ContentDialog
        {
            Title = T("TaskbarFolders.Delete.Confirm.Title"),
            Content = T(TaskbarFolderService.IsPinned(folder) ? "TaskbarFolders.Delete.Confirm.PinnedBody" : "TaskbarFolders.Delete.Confirm.Body"),
            PrimaryButtonText = T("TaskbarFolders.Delete"),
            CloseButtonText = T("Common.Cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await Task.Run(() => TaskbarFolderService.DeleteFolder(_document, folder));
        TaskbarFolderService.Save(_document);
        Render();
    }

    private async Task SaveAsync(TaskbarFolder folder, bool regenerateIcon, string? previousShortcutPath = null)
    {
        ErrorInfoBar.IsOpen = false;
        try
        {
            if (regenerateIcon || folder.IconFile is null || !File.Exists(folder.IconFile))
            {
                TaskbarFolderService.SetIcon(folder, await RenderIconAsync(folder));
            }

            TaskbarFolderService.WriteShortcut(folder, previousShortcutPath);
            TaskbarFolderService.Save(_document);
        }
        catch (Exception ex)
        {
            App.Log($"[TaskbarFolders] Save failed: {ex}");
            ShowError(string.Format(T("TaskbarFolders.Error"), ex.Message));
            // Keep what is valid on disk even when the icon or shortcut failed.
            TaskbarFolderService.Save(_document);
        }

        RefreshFolder(folder);
    }

    private void ShowError(string message)
    {
        ErrorInfoBar.Message = message;
        ErrorInfoBar.IsOpen = true;
    }

    // ── Icon rendering ──

    /// <summary>
    /// Draws the folder icon on the off-screen surface and packs it into an
    /// .ico (256 down to 16 px) so the taskbar shows it crisp at every DPI.
    /// </summary>
    private async Task<byte[]> RenderIconAsync(TaskbarFolder folder)
    {
        IconRenderSurface.CornerRadius = new CornerRadius(28);
        IconRenderSurface.Child = null;
        switch (folder.IconKind)
        {
            case TaskbarFolderIconKind.Emoji:
                IconRenderSurface.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
                IconRenderSurface.Child = new Viewbox
                {
                    Margin = new Thickness(6),
                    Child = new TextBlock
                    {
                        Text = folder.Emoji ?? string.Empty,
                        FontFamily = new FontFamily("Segoe UI Emoji"),
                        IsColorFontEnabled = true
                    }
                };
                break;
            case TaskbarFolderIconKind.Image when folder.ImagePath is not null:
                var bitmap = new BitmapImage();
                using (IRandomAccessStream stream = await (await Windows.Storage.StorageFile.GetFileFromPathAsync(folder.ImagePath)).OpenReadAsync())
                {
                    await bitmap.SetSourceAsync(stream);
                }

                IconRenderSurface.Background = null;
                IconRenderSurface.Child = new Image { Source = bitmap, Stretch = Stretch.UniformToFill };
                break;
            default:
                IconRenderSurface.Background = new SolidColorBrush(new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent));
                IconRenderSurface.Child = new FontIcon
                {
                    Glyph = "",
                    FontSize = 72,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
                };
                break;
        }

        IconRenderSurface.UpdateLayout();
        await Task.Delay(60);

        var target = new RenderTargetBitmap();
        await target.RenderAsync(IconRenderSurface, 256, 256);
        IBuffer pixels = await target.GetPixelsAsync();
        byte[] bgra = pixels.ToArray();
        int width = target.PixelWidth;
        int height = target.PixelHeight;

        var images = new List<(int, byte[])>();
        foreach (int size in IconSizes)
        {
            using var output = new InMemoryRandomAccessStream();
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)width, (uint)height, 96, 96, bgra);
            encoder.BitmapTransform.ScaledWidth = (uint)size;
            encoder.BitmapTransform.ScaledHeight = (uint)size;
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
            await encoder.FlushAsync();
            var bytes = new byte[output.Size];
            output.Seek(0);
            await output.ReadAsync(bytes.AsBuffer(), (uint)bytes.Length, InputStreamOptions.None);
            images.Add((size, bytes));
        }

        IconRenderSurface.Child = null;
        return TaskbarFolderService.BuildIco(images);
    }

    private static async Task LoadAppIconAsync(Image image, string path)
    {
        try
        {
            image.Source = await IconHelper.GetIconAsync(path, hideShortcutArrowOverlay: true, decodePixelWidth: 40);
        }
        catch (Exception ex)
        {
            App.LogVerbose($"[TaskbarFolders] Icon load failed '{path}': {ex.Message}");
        }
    }
}
