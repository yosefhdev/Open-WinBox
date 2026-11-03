using System;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;

namespace OpenWinBox;

// "Personalizar icono" como en el original: pestañas Emoji / Imagen, categorías, Aleatorio y Restaurar
internal static class IconPicker
{
    private static readonly (string Name, string Emojis)[] Categories =
    {
        ("Caras", "😀😃😄😁😆😅😂🤣😊😇🙂😉😍🥰😘😎🤓🥳🤩😏😴🤔🤯😱😡🥶🤠👻"),
        ("Personas", "👋👍👎👏🙌🙏💪🧠👀👤👥🧑‍💻👨‍🎨👩‍🚀🧙🦸🥷👑🎓💼🧳🎒👓🕶️"),
        ("Animales", "🐶🐱🐭🐹🐰🦊🐻🐼🐨🐯🦁🐮🐷🐸🐵🐔🐧🐦🦄🐝🦋🐢🐍🐙🐬🐳🌵🌸🌻🍀"),
        ("Comida", "🍎🍊🍋🍉🍇🍓🍒🍑🥑🥕🌽🍞🧀🍔🍟🍕🌮🍣🍩🍪🎂🍫🍿☕🍵🍺🍷🥤"),
        ("Viajes", "🚗🚕🚌🏎️🚓🚑🚒✈️🚀🛸🚁⛵🚢🚲🛴🏠🏢🏰🗽🗼⛰️🏖️🏝️🌋🌍🌙⭐☀️🌈"),
        ("Objetos", "💻🖥️⌨️🖱️🎮🕹️📱📷🎥🎧🎤🎸🎹📚📖📝✏️📌📎📁📂🗂️🗃️🔧🔨🛠️⚙️🔑💡🎨🧩🎲"),
        ("Símbolos", "❤️🧡💛💚💙💜🖤🤍⭐✨🔥💯✅❌⚠️❓❗💬🔔🎵➕➖🔴🟠🟡🟢🔵🟣⚫⚪"),
    };

    public static readonly string ImagesFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenWinBox", "icons");

    // onPick recibe "emoji:x", "image:<ruta>" o null (restaurar el predeterminado); el que llama hace ShowAt
    public static Flyout Create(Window owner, Action<string?> onPick)
    {
        var flyout = new Flyout { ShouldConstrainToRootBounds = false }; // los widgets chicos no lo contienen
        void Pick(string? spec) { flyout.Hide(); onPick(spec); }

        var grid = new GridView { SelectionMode = ListViewSelectionMode.None, IsItemClickEnabled = true, Height = 220 };
        grid.ItemClick += (_, e) => Pick("emoji:" + e.ClickedItem);
        var categories = new SelectorBar();
        foreach (var (name, _) in Categories) categories.Items.Add(new SelectorBarItem { Text = name });
        categories.SelectionChanged += (s, _) =>
        {
            var index = s.Items.IndexOf(s.SelectedItem);
            if (index >= 0) grid.ItemsSource = SplitEmojis(Categories[index].Emojis);
        };
        categories.SelectedItem = categories.Items[0];
        grid.ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" +
            "<TextBlock Text='{Binding}' FontFamily='Segoe UI Emoji' FontSize='20' Width='32' Height='32' TextAlignment='Center' /></DataTemplate>");
        var emojiPanel = new StackPanel { Spacing = 4, Children = { new ScrollViewer
        {
            Content = categories, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Disabled,
        }, grid } };

        var chooseImage = new Button { Content = "Elegir imagen de la PC…", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        chooseImage.Click += async (_, _) =>
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary };
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".ico", ".bmp", ".gif", ".webp" }) picker.FileTypeFilter.Add(ext);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(owner));
            if (await picker.PickSingleFileAsync() is not { } file) return;
            // Copia propia: si el usuario mueve o borra la imagen original, el ícono no se pierde
            Directory.CreateDirectory(ImagesFolder);
            var copy = Path.Combine(ImagesFolder, Guid.NewGuid().ToString("N") + Path.GetExtension(file.Path));
            File.Copy(file.Path, copy);
            Pick("image:" + copy);
        };
        var imagePanel = new StackPanel
        {
            Spacing = 10, Visibility = Visibility.Collapsed, Padding = new Thickness(0, 8, 0, 8),
            Children =
            {
                new TextBlock { Text = "Usa una imagen PNG, JPG o ICO. Se guarda una copia dentro de OpenWinBox.", TextWrapping = TextWrapping.Wrap },
                chooseImage,
            },
        };

        var tabs = new SelectorBar { Items = { new SelectorBarItem { Text = "Emoji" }, new SelectorBarItem { Text = "Imagen" } } };
        tabs.SelectionChanged += (s, _) =>
        {
            var isEmoji = s.Items.IndexOf(s.SelectedItem) == 0;
            emojiPanel.Visibility = isEmoji ? Visibility.Visible : Visibility.Collapsed;
            imagePanel.Visibility = isEmoji ? Visibility.Collapsed : Visibility.Visible;
        };
        tabs.SelectedItem = tabs.Items[0];

        var random = new Button { Content = "Aleatorio" };
        random.Click += (_, _) =>
        {
            var all = Categories.SelectMany(c => SplitEmojis(c.Emojis)).ToArray();
            Pick("emoji:" + all[Random.Shared.Next(all.Length)]);
        };
        var reset = new Button { Content = "Restaurar valor predeterminado" };
        reset.Click += (_, _) => Pick(null);

        flyout.Content = new StackPanel
        {
            Width = 330, Spacing = 8,
            Children =
            {
                new StackPanel { Spacing = 2, Children =
                {
                    new TextBlock { Text = "Personalizar icono", Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] },
                    new TextBlock { Text = "Elige un emoji o una imagen para este widget", FontSize = 12,
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] },
                } },
                tabs, emojiPanel, imagePanel,
                new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } },
                    Children = { random, SetColumn(reset, 1) } },
            },
        };
        return flyout;
    }

    // Ícono de título para una carpeta según su configuración
    public static FrameworkElement Render(string? spec, double size)
    {
        if (spec?.StartsWith("emoji:") == true)
            return new TextBlock
            {
                Text = spec[6..], FontSize = size, FontFamily = new FontFamily("Segoe UI Emoji"),
                VerticalAlignment = VerticalAlignment.Center,
            };
        if (spec?.StartsWith("image:") == true && File.Exists(spec[6..]))
        {
            var bitmap = new BitmapImage();
            _ = LoadImageAsync(bitmap, spec[6..]);
            return new Image { Source = bitmap, Width = size + 3, Height = size + 3, VerticalAlignment = VerticalAlignment.Center };
        }
        return new FontIcon
        {
            Glyph = "", FontSize = size,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x4A, 0x9F, 0xEA)),
        };
    }

    // El mismo ícono en tamaño de menú
    public static IconElement MenuIcon(string? spec)
    {
        if (spec?.StartsWith("emoji:") == true)
            return new FontIcon { Glyph = spec[6..], FontFamily = new FontFamily("Segoe UI Emoji") };
        if (spec?.StartsWith("image:") == true && File.Exists(spec[6..]))
            return new BitmapIcon { UriSource = new Uri(spec[6..]), ShowAsMonochrome = false };
        return new FontIcon { Glyph = "\uE8B7", Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x4A, 0x9F, 0xEA)) };
    }

    private static async System.Threading.Tasks.Task LoadImageAsync(BitmapImage bitmap, string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { } // imagen rota: sin ícono
    }

    // Separa por grafema: algunos emojis son varios caracteres (👨‍🎨, ⚙️)
    private static string[] SplitEmojis(string s)
    {
        var e = System.Globalization.StringInfo.GetTextElementEnumerator(s);
        var list = new System.Collections.Generic.List<string>();
        while (e.MoveNext()) list.Add((string)e.Current);
        return list.ToArray();
    }

    private static FrameworkElement SetColumn(FrameworkElement element, int column)
    {
        Grid.SetColumn(element, column);
        return element;
    }
}
