using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace OpenWinBox;

public sealed partial class SettingsWindow : Window
{
    private readonly App _app = App.Current;
    private readonly Dictionary<string, StackPanel> _pages;
    private bool _loading = true; // los controles disparan eventos al cargar sus valores iniciales

    public SettingsWindow(string page)
    {
        InitializeComponent();
        Title = Lang.T("settings.title");
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        AppWindow.Resize(new SizeInt32(1000, 720));
        AppWindow.SetIcon(ShellHost.IconPath); // barra de tareas y Alt+Tab
        TitleIcon.Source = AboutIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new System.Uri(ShellHost.IconPath));
        App.ApplyTheme(this);

        _pages = new()
        {
            ["General"] = GeneralPage, ["Apariencia"] = AparienciaPage, ["Widgets"] = WidgetsPage,
            ["Funcionales"] = FuncionalesPage, ["Atajos"] = AtajosPage, ["Acerca"] = AcercaPage,
        };
        _featurePages = new()
        {
            ["Music"] = MusicOptionsPage, ["Glance"] = GlanceOptionsPage, ["Weather"] = WeatherOptionsPage,
            ["Spotify"] = SpotifyOptionsPage,
        };
        RefreshFeatures();
        UpdateWeatherLocationText();
        LoadFeatureOptions();

        AutoStartToggle.IsOn = ShellHost.AutoStart;
        LanguageCombo.Items.Add(new ComboBoxItem { Content = Lang.T("settings.language.system"), Tag = Lang.FollowWindows });
        // Cada idioma con su propio nombre: se encuentra aunque la interfaz esté en uno que no se entiende
        foreach (var (code, name) in Lang.Available) LanguageCombo.Items.Add(new ComboBoxItem { Content = name, Tag = code });
        LanguageCombo.SelectedItem = LanguageCombo.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == _app.Config.Language)
                                     ?? LanguageCombo.Items[0];
        ThemeCombo.SelectedItem = ThemeCombo.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == _app.Config.Theme)
                                  ?? ThemeCombo.Items[0];
        LayerCombo.SelectedIndex = _app.Config.PinToDesktop ? 0 : 1;
        MergeDelaySlider.Value = _app.Config.MergeDelayMs / 1000.0;
        _mergeDelayReady = true;
        MergeDelayText.Text = $"{MergeDelaySlider.Value:0.0} s";
        HotkeyWarning.IsOpen = !_app.HotkeyRegistered;
        VersionText.Text = $"OpenWinBox {Updater.CurrentVersion}";
        UpdateChannelCombo.SelectedIndex = IncludeBetas ? 1 : 0;
        Closed += (_, _) =>
        {
            _closed = true;
            _updateCts?.Cancel();
        };
        RefreshWidgetList();
        _loading = false;

        ShowPage(page);
    }

    // tag: una sección del menú ("General", "Funcionales"…) o un widget funcional ("Music", "Glance", "Weather")
    public string CurrentPage { get; private set; } = "General"; // para volver a ella al cambiar de idioma

    public void ShowPage(string tag)
    {
        var section = _featurePages.ContainsKey(tag) ? "Funcionales" : tag;
        Nav.SelectedItem = Nav.MenuItems.Cast<NavigationViewItem>().First(i => (string)i.Tag == section);
        if (section != tag) OpenFeaturePage(tag);
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;
        ShowOnly(_pages[(string)item.Tag]);
        PageTitle.Text = (string)item.Content;
        CurrentPage = (string)item.Tag;
        SubPageBack.Visibility = Visibility.Collapsed;
    }

    private void ShowOnly(StackPanel page)
    {
        foreach (var panel in _pages.Values.Concat(_featurePages.Values))
            panel.Visibility = panel == page ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------- Configuración de cada widget funcional (en esta misma ventana) ----------

    private readonly Dictionary<string, StackPanel> _featurePages;

    private void OpenFeaturePage(string kind)
    {
        ShowOnly(_featurePages[kind]);
        PageTitle.Text = $"{Lang.T("settings.nav.featureWidgets")}  ›  {WidgetWindow.FeatureTitle(kind)}";
        CurrentPage = kind;
        SubPageBack.Visibility = Visibility.Visible;
    }

    private void SubPageBack_Click(object sender, RoutedEventArgs e)
    {
        ShowOnly(FuncionalesPage);
        PageTitle.Text = Lang.T("settings.nav.featureWidgets");
        CurrentPage = "Funcionales";
        SubPageBack.Visibility = Visibility.Collapsed;
    }

    private void OpenFeature_Click(object sender, RoutedEventArgs e) => OpenFeaturePage((string)((FrameworkElement)sender).Tag);

    // Clic en cualquier parte de la fila, salvo en su interruptor (que enciende/apaga el widget)
    private void FeatureRow_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        for (var d = e.OriginalSource as DependencyObject; d != null && d != sender; d = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(d))
            if (d is ToggleSwitch or Button) return;
        OpenFeaturePage((string)((FrameworkElement)sender).Tag);
    }

    // Interruptores con Tag "Sección.Propiedad" (p. ej. "Music.ShowArtist") enlazados a AppConfig.<Sección>
    private object Options(string section) => typeof(AppConfig).GetProperty(section)!.GetValue(_app.Config)!;

    private void LoadFeatureOptions()
    {
        // Se llama con _loading = true: poner IsOn no guarda nada
        foreach (var row in _featurePages.Values.SelectMany(p => p.Children.OfType<SettingRow>()))
            if (row.Action is ToggleSwitch { Tag: string tag } toggle && tag.Split('.') is [var section, var name])
                toggle.IsOn = (bool)Options(section).GetType().GetProperty(name)!.GetValue(Options(section))!;

        MusicStylePreviews.Content = StyleGallery(_app.Config.Music, spotify: false);
        SpotifyStylePreviews.Content = StyleGallery(_app.Config.Spotify, spotify: true);
        var glance = _app.Config.Glance;
        foreach (var zone in System.TimeZoneInfo.GetSystemTimeZones())
        {
            var item = new ComboBoxItem { Content = zone.DisplayName, Tag = zone.Id };
            TimeZoneCombo.Items.Add(item);
            if (zone.Id == glance.SecondTimeZone) TimeZoneCombo.SelectedItem = item;
        }
        SecondLabelBox.Text = glance.SecondClockLabel ?? "";
        LocalLabelBox.Text = glance.LocalClockLabel ?? "";
        LocalLabelBox.PlaceholderText = GlanceView.DefaultLabel(System.TimeZoneInfo.Local);
        UpdateSecondLabelPlaceholder();
    }

    private void OptionToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not ToggleSwitch { Tag: string tag } toggle || tag.Split('.') is not [var section, var name]) return;
        var options = Options(section);
        options.GetType().GetProperty(name)!.SetValue(options, toggle.IsOn);
        _app.SaveFeatureOptions();
    }

    // Tarjetas con el widget real en cada estilo (a escala, con lo que está sonando); elegir una cambia el estilo
    private GridView StyleGallery(MusicOptions options, bool spotify)
    {
        var styles = new[] { ("Standard", Lang.T("settings.style.standard")), ("Compact", Lang.T("settings.style.compact")), ("Backdrop", Lang.T("settings.style.backdrop")) };
        var gallery = new GridView { SelectionMode = ListViewSelectionMode.Single };
        foreach (var (id, name) in styles)
        {
            var item = new StackPanel
            {
                Tag = id, Spacing = 6, Padding = new Thickness(4),
                Children =
                {
                    new Border
                    {
                        Width = 190, Height = 110, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
                        Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SolidBackgroundFillColorTertiaryBrush"],
                        BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                        // Tamaño real del widget (378×218) reducido para que se vea igual que en el escritorio
                        Child = new Viewbox
                        {
                            Child = new Grid
                            {
                                Width = 378, Height = 218, IsHitTestVisible = false, // solo vista previa: sus botones no controlan nada
                                Children = { new MusicView { SpotifyOnly = spotify, StyleOverride = id } },
                            },
                        },
                    },
                    new TextBlock { Text = name, HorizontalAlignment = HorizontalAlignment.Center },
                },
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, Lang.T("settings.style.name", name));
            gallery.Items.Add(item);
        }
        gallery.SelectedIndex = System.Math.Max(0, System.Array.FindIndex(styles, s => s.Item1 == options.Style));
        gallery.SelectionChanged += (_, _) =>
        {
            if (gallery.SelectedItem is not StackPanel { Tag: string id }) return;
            options.Style = id;
            _app.SaveFeatureOptions();
        };
        return gallery;
    }

    private void TimeZoneCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || TimeZoneCombo.SelectedItem is not ComboBoxItem { Tag: string id }) return;
        _app.Config.Glance.SecondTimeZone = id;
        UpdateSecondLabelPlaceholder();
        _app.SaveFeatureOptions();
    }

    private void LocalLabelBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        _app.Config.Glance.LocalClockLabel = string.IsNullOrWhiteSpace(LocalLabelBox.Text) ? null : LocalLabelBox.Text.Trim();
        _app.SaveFeatureOptions();
    }

    private void SecondLabelBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        _app.Config.Glance.SecondClockLabel = string.IsNullOrWhiteSpace(SecondLabelBox.Text) ? null : SecondLabelBox.Text.Trim();
        _app.SaveFeatureOptions();
    }

    // Muestra el nombre que se usará si se deja vacío
    private void UpdateSecondLabelPlaceholder()
    {
        try { SecondLabelBox.PlaceholderText = GlanceView.DefaultLabel(System.TimeZoneInfo.FindSystemTimeZoneById(_app.Config.Glance.SecondTimeZone)); }
        catch (System.TimeZoneNotFoundException) { SecondLabelBox.PlaceholderText = Lang.T("settings.clockName"); }
    }

    private void RefreshWidgetList()
    {
        WidgetList.Children.Clear();
        foreach (var widget in _app.Config.Widgets.Where(w => w.Folders.Count > 0))
        {
            var add = new Button { Content = Lang.T("settings.addFolder") };
            ToolTipService.SetToolTip(add, Lang.T("settings.addFolder.tip"));
            add.Click += async (_, _) =>
            {
                if (await App.PickFolderAsync(this) is not { } folder) return;
                _app.AddFolder(widget, folder);
                RefreshWidgetList();
            };
            var remove = new Button { Content = Lang.T("settings.remove") };
            ToolTipService.SetToolTip(remove, Lang.T("settings.remove.tip"));
            remove.Click += (_, _) =>
            {
                _app.RemoveWidget(widget);
                RefreshWidgetList();
            };

            WidgetList.Children.Add(new SettingRow
            {
                Glyph = widget.Folders.Count > 1 ? "" : "",
                Header = widget.Name ?? WidgetWindow.TitleOf(widget, widget.Folders[0]),
                Description = string.Join("   ·   ", widget.Folders),
                Action = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { add, remove } },
            });
        }
        if (WidgetList.Children.Count == 0)
            WidgetList.Children.Add(new TextBlock
            {
                Text = Lang.T("settings.noWidgets"),
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            });
    }

    // También se llama cuando un widget se desactiva desde su propio menú
    public void RefreshFeatures()
    {
        var loading = _loading;
        _loading = true;
        MusicToggle.IsOn = !_app.Config.Feature("Music").IsDisabled;
        GlanceToggle.IsOn = !_app.Config.Feature("Glance").IsDisabled;
        WeatherToggle.IsOn = !_app.Config.Feature("Weather").IsDisabled;
        SpotifyToggle.IsOn = !_app.Config.Feature("Spotify").IsDisabled;
        AudioToggle.IsOn = !_app.Config.Feature("Audio").IsDisabled;
        _loading = loading;
    }

    // ---------- Ubicación del clima ----------

    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = System.TimeSpan.FromSeconds(10) };
    private List<WeatherPlace> _cityResults = new();

    private void UpdateWeatherLocationText()
    {
        var chosen = _app.Config.WeatherLocation;
        WeatherLocationRow.Description = chosen == null
            ? Lang.T("settings.location.auto")
            : Lang.T("settings.location.chosen", chosen.Name);
        AutoLocationButton.IsEnabled = chosen != null;
    }

    // Sugerencias mientras se escribe (geocodificación de Open-Meteo, gratis y sin cuenta)
    private async void CitySearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        var text = sender.Text.Trim();
        if (text.Length < 2) { sender.ItemsSource = null; return; }
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(await Http.GetStringAsync(
                $"https://geocoding-api.open-meteo.com/v1/search?name={System.Uri.EscapeDataString(text)}&count=6&language={Lang.Culture.TwoLetterISOLanguageName}"));
            if (sender.Text.Trim() != text) return; // ya escribió otra cosa
            var results = new List<WeatherPlace>();
            if (doc.RootElement.TryGetProperty("results", out var list))
                foreach (var r in list.EnumerateArray())
                {
                    var parts = new[] { "name", "admin1", "country" }
                        .Select(k => r.TryGetProperty(k, out var v) ? v.GetString() : null)
                        .Where(s => !string.IsNullOrEmpty(s)).Distinct();
                    results.Add(new WeatherPlace(string.Join(", ", parts),
                        r.GetProperty("latitude").GetDouble(), r.GetProperty("longitude").GetDouble()));
                }
            _cityResults = results;
            sender.ItemsSource = results.Count > 0 ? results.Select(p => p.Name).ToList() : new List<string> { Lang.T("settings.citySearch.none") };
        }
        catch (System.Exception e) when (e is System.Net.Http.HttpRequestException or System.Threading.Tasks.TaskCanceledException
                                             or System.Text.Json.JsonException or KeyNotFoundException)
        {
            sender.ItemsSource = new List<string> { Lang.T("common.noConnection") };
        }
    }

    private void CitySearch_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args) =>
        ChooseCity(args.SelectedItem as string);

    private void CitySearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) =>
        ChooseCity(args.ChosenSuggestion as string ?? _cityResults.FirstOrDefault()?.Name);

    private void ChooseCity(string? name)
    {
        if (_cityResults.FirstOrDefault(p => p.Name == name) is not { } place) return; // "Sin resultados" / "Sin conexión"
        _app.SetWeatherLocation(place);
        CitySearch.Text = "";
        CitySearch.ItemsSource = null;
        UpdateWeatherLocationText();
    }

    private void AutoLocation_Click(object sender, RoutedEventArgs e)
    {
        _app.SetWeatherLocation(null);
        UpdateWeatherLocationText();
    }

    private void FeatureToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loading && sender is ToggleSwitch { Tag: string kind } toggle) _app.SetFeatureEnabled(kind, toggle.IsOn);
    }

    private void AutoStartToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loading) ShellHost.AutoStart = AutoStartToggle.IsOn;
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading && ThemeCombo.SelectedItem is ComboBoxItem { Tag: string theme }) _app.SetTheme(theme);
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Esta ventana se recrea al cambiar de idioma: no cerrarla dentro de su propio evento
        if (!_loading && LanguageCombo.SelectedItem is ComboBoxItem { Tag: string language })
            DispatcherQueue.TryEnqueue(() => _app.SetLanguage(language));
    }

    private void LayerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) _app.SetPinned(LayerCombo.SelectedIndex == 0);
    }

    // ---------- Actualizaciones ----------

    private System.Threading.CancellationTokenSource? _updateCts;
    private UpdateRelease? _update;
    private bool _downloading;
    private bool _closed; // con la ventana cerrada ya no se tocan sus controles

    private bool IncludeBetas => _app.Config.UpdateBetas ?? Updater.IsBeta(Updater.CurrentVersion);

    private void UpdateChannelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _app.Config.UpdateBetas = UpdateChannelCombo.SelectedIndex == 1;
        _app.Config.Save();
        UpdateBar.IsOpen = false; // el resultado anterior era de otro canal
    }

    private void ShowUpdateStatus(InfoBarSeverity severity, string title, string message, string? action = null)
    {
        UpdateBar.Severity = severity;
        UpdateBar.Title = title;
        UpdateBar.Message = message;
        UpdateActionButton.Content = action;
        UpdateActionButton.Visibility = action != null ? Visibility.Visible : Visibility.Collapsed;
        UpdateNotesLink.Visibility = _update != null ? Visibility.Visible : Visibility.Collapsed;
        UpdateProgress.Visibility = _downloading ? Visibility.Visible : Visibility.Collapsed;
        UpdateBar.IsOpen = true;
    }

    private void SetUpdateControlsEnabled(bool enabled) =>
        CheckUpdatesButton.IsEnabled = UpdateChannelCombo.IsEnabled = enabled;

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        var token = (_updateCts = new System.Threading.CancellationTokenSource()).Token;
        var description = UpdateRow.Description;
        _update = null;
        UpdateBar.IsOpen = false;
        SetUpdateControlsEnabled(false);
        UpdateRow.Description = Lang.T("settings.updates.checking");
        try
        {
            _update = await Updater.FindUpdateAsync(IncludeBetas, token);
            if (_closed) return;
            if (_update == null)
                ShowUpdateStatus(InfoBarSeverity.Success, Lang.T("settings.updates.upToDate"),
                    Lang.T("settings.updates.upToDate.message", Updater.CurrentVersion, Lang.T(IncludeBetas ? "settings.updates.betas" : "settings.updates.official")));
            else
            {
                UpdateNotesLink.NavigateUri = new System.Uri(_update.PageUrl);
                ShowUpdateStatus(InfoBarSeverity.Informational, Lang.T("settings.updates.available", _update.Version),
                    Lang.T("settings.updates.available.message", Updater.CurrentVersion), Lang.T("settings.updates.update"));
            }
        }
        catch (System.Exception ex) when (ex is System.Net.Http.HttpRequestException or System.OperationCanceledException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            if (_closed) return;
            ShowUpdateStatus(InfoBarSeverity.Error, Lang.T("settings.updates.checkFailed"), Lang.T("settings.updates.checkFailed.message"));
        }
        SetUpdateControlsEnabled(true);
        UpdateRow.Description = description;
    }

    private async void UpdateAction_Click(object sender, RoutedEventArgs e)
    {
        if (_downloading)
        {
            _updateCts?.Cancel();
            return;
        }
        if (_update is not { } update) return;

        var token = (_updateCts = new System.Threading.CancellationTokenSource()).Token;
        _downloading = true;
        SetUpdateControlsEnabled(false);
        UpdateProgress.Value = 0;
        ShowUpdateStatus(InfoBarSeverity.Informational, Lang.T("settings.updates.downloading", update.Version), Lang.T("settings.updates.downloading.message"), Lang.T("settings.updates.cancel"));
        string? setup = null, error = null;
        try
        {
            setup = await Updater.DownloadAsync(update, new System.Progress<double>(p => { if (!_closed) UpdateProgress.Value = p; }), token);
            _app.InstallUpdate(setup);
            return;
        }
        catch (System.OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (InvalidDataException) { error = Lang.T("settings.updates.badHash"); }
        catch (System.ComponentModel.Win32Exception) { error = Lang.T("settings.updates.launchFailed"); }
        catch (System.Exception ex) when (ex is System.Net.Http.HttpRequestException or System.OperationCanceledException or IOException or System.UnauthorizedAccessException)
        {
            error = Lang.T("settings.updates.downloadFailed");
        }
        _downloading = false;
        if (_closed) return;
        SetUpdateControlsEnabled(true);
        if (error != null) ShowUpdateStatus(InfoBarSeverity.Error, Lang.T("settings.updates.failed"), error, Lang.T("settings.updates.retry"));
        else ShowUpdateStatus(InfoBarSeverity.Informational, Lang.T("settings.updates.available", update.Version), Lang.T("settings.updates.canceled"), Lang.T("settings.updates.update"));
    }

    private async void NewWidget_Click(object sender, RoutedEventArgs e)
    {
        if (await App.PickFolderAsync(this) is not { } folder) return;
        _app.AddWidget(folder);
        RefreshWidgetList();
    }

    private void Onboarding_Click(object sender, RoutedEventArgs e) => _app.OpenOnboarding();

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(Path.GetDirectoryName(AppConfig.FilePath)!) { UseShellExecute = true });

    private bool _mergeDelayReady; // el Slider dispara ValueChanged al cargarse con su mínimo: no guardar eso

    private void MergeDelaySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_mergeDelayReady) return;
        MergeDelayText.Text = $"{e.NewValue:0.0} s";
        var ms = (int)System.Math.Round(e.NewValue * 1000);
        if (ms == _app.Config.MergeDelayMs) return;
        _app.Config.MergeDelayMs = ms;
        _app.Config.Save();
    }
}
