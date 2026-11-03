using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media.Control;

namespace OpenWinBox;

// Música: lo que suena en Windows (Spotify, navegador, Media Player…) mediante los controles multimedia
// del sistema, igual que el original. No reproduce nada por sí mismo.
public sealed partial class MusicView : UserControl
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private GlobalSystemMediaTransportControlsSessionTimelineProperties? _timeline;
    private bool _playing;
    private readonly DispatcherQueueTimer _tick;

    // Widget de Spotify: sigue solo la sesión de Spotify (aunque suene otra cosa), con sus colores,
    // aleatorio/repetir y "Abrir Spotify" cuando está cerrado
    public bool SpotifyOnly { get; set; }
    // Vista previa en Configuración: fuerza un estilo sin cambiar la configuración guardada
    public string? StyleOverride { get; set; }

    private MusicOptions Options => SpotifyOnly ? App.Current.Config.Spotify : App.Current.Config.Music;
    private string CurrentStyle => StyleOverride ?? Options.Style;
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush SpotifyGreen = new(Windows.UI.Color.FromArgb(255, 0x1D, 0xB9, 0x54));

    public MusicView()
    {
        InitializeComponent();
        // La línea de tiempo del sistema solo avisa al cambiar de estado; el avance se calcula cada segundo
        _tick = DispatcherQueue.CreateTimer();
        _tick.Interval = TimeSpan.FromSeconds(1);
        _tick.Tick += (_, _) => UpdateProgress();

        Loaded += async (_, _) =>
        {
            ApplyOptions();
            App.FeatureOptionsChanged += ApplyOptions;
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += (_, _) => DispatcherQueue.TryEnqueue(Attach);
            _manager.SessionsChanged += (_, _) => DispatcherQueue.TryEnqueue(Attach); // Spotify se abrió o cerró
            Attach(); // arranca el avance si algo está sonando
        };
        Unloaded += (_, _) => { _tick.Stop(); App.FeatureOptionsChanged -= ApplyOptions; };
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    // Estilo y elementos visibles (Configuración › Widgets funcionales › Música)
    private void ApplyOptions()
    {
        var o = Options;
        var compact = CurrentStyle == "Compact";
        var backdrop = CurrentStyle == "Backdrop";
        ShuffleButton.Visibility = RepeatButton.Visibility = Show(SpotifyOnly && !compact);
        if (SpotifyOnly)
        {
            ProgressFill.Fill = SpotifyGreen;
            PlayIcon.Foreground = SpotifyGreen;
        }
        Backdrop.Visibility = Show(backdrop);
        Layout.RequestedTheme = backdrop ? ElementTheme.Dark : ElementTheme.Default; // texto claro sobre la carátula oscurecida
        ArtworkBox.Visibility = Show(o.ShowArtwork);
        ArtworkBox.Width = ArtworkBox.Height = compact ? 40 : 56;
        ArtistText.Visibility = Show(o.ShowArtist);
        StatusText.Visibility = Show(o.ShowStatus && !compact);
        Controls.Visibility = Show(o.ShowControls && !(SpotifyOnly && _session == null));
        // Compacto: los controles pasan a la misma fila que el título
        Panel target = compact ? Header : Layout;
        if (Controls.Parent != target)
        {
            ((Panel)Controls.Parent).Children.Remove(Controls);
            target.Children.Add(Controls);
        }
        Grid.SetRow(Controls, compact ? 0 : 2);
        Grid.SetColumn(Controls, compact ? 2 : 0);
        Controls.Spacing = compact ? 0 : 10;
        UpdateProgress();
    }

    private void Attach()
    {
        var session = SpotifyOnly
            ? _manager?.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
            : _manager?.GetCurrentSession();
        // Misma sesión: no volver a suscribirse (SessionsChanged llega seguido por otras apps)
        if (session != null && ReferenceEquals(session, _session)) return;
        if (_session != null)
        {
            _session.MediaPropertiesChanged -= OnMediaChanged;
            _session.PlaybackInfoChanged -= OnPlaybackChanged;
            _session.TimelinePropertiesChanged -= OnTimelineChanged;
        }
        _session = session;
        if (_session != null)
        {
            _session.MediaPropertiesChanged += OnMediaChanged;
            _session.PlaybackInfoChanged += OnPlaybackChanged;
            _session.TimelinePropertiesChanged += OnTimelineChanged;
        }
        _ = UpdateMediaAsync();
        UpdatePlayback();
        UpdateTimeline();
    }

    private void OnMediaChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = UpdateMediaAsync());
    private void OnPlaybackChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs e) =>
        DispatcherQueue.TryEnqueue(UpdatePlayback);
    private void OnTimelineChanged(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs e) =>
        DispatcherQueue.TryEnqueue(UpdateTimeline);

    private async Task UpdateMediaAsync()
    {
        var session = _session;
        OpenSpotifyButton.Visibility = Show(SpotifyOnly && session == null);
        Controls.Visibility = Show(Options.ShowControls && !(SpotifyOnly && session == null));
        if (session == null)
        {
            TitleText.Text = SpotifyOnly ? "Spotify no está abierto" : "No se está reproduciendo nada";
            ArtistText.Text = StatusText.Text = "";
            Artwork.Source = BackdropImage.Source = null;
            ArtworkPlaceholder.Visibility = Visibility.Visible;
            return;
        }
        GlobalSystemMediaTransportControlsSessionMediaProperties props;
        try { props = await session.TryGetMediaPropertiesAsync(); }
        catch (Exception) { return; } // la app de origen se cerró mientras preguntábamos
        if (session != _session) return;

        TitleText.Text = string.IsNullOrEmpty(props.Title) ? "Sin título" : props.Title;
        ArtistText.Text = props.Artist;
        if (props.Thumbnail != null)
        {
            try
            {
                using var stream = await props.Thumbnail.OpenReadAsync();
                var bitmap = new BitmapImage { DecodePixelWidth = 112 };
                await bitmap.SetSourceAsync(stream);
                Artwork.Source = BackdropImage.Source = bitmap; // reducida a 112 px: de fondo se ve difuminada
                ArtworkPlaceholder.Visibility = Visibility.Collapsed;
                return;
            }
            catch (Exception) { } // carátula ilegible: mostrar la nota musical
        }
        Artwork.Source = BackdropImage.Source = null;
        ArtworkPlaceholder.Visibility = Visibility.Visible;
    }

    private void UpdatePlayback()
    {
        var info = _session?.GetPlaybackInfo();
        _playing = info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        // En pausa la posición no cambia: no despertar cada segundo
        if (_playing && IsLoaded) _tick.Start(); else _tick.Stop();
        PlayIcon.Glyph = _playing ? "" : "";
        StatusText.Text = info == null ? "" : _playing ? "Reproduciendo" : "En pausa";
        PreviousButton.IsEnabled = info?.Controls.IsPreviousEnabled == true;
        NextButton.IsEnabled = info?.Controls.IsNextEnabled == true;
        PlayButton.IsEnabled = info != null;
        if (!SpotifyOnly) return;
        // Aleatorio y repetir: verde cuando están activos, como en Spotify
        ShuffleButton.IsEnabled = info?.Controls.IsShuffleEnabled == true;
        RepeatButton.IsEnabled = info?.Controls.IsRepeatEnabled == true;
        SetActive(ShuffleIcon, info?.IsShuffleActive == true);
        var repeat = info?.AutoRepeatMode ?? Windows.Media.MediaPlaybackAutoRepeatMode.None;
        RepeatIcon.Glyph = repeat == Windows.Media.MediaPlaybackAutoRepeatMode.Track ? "" : "";
        SetActive(RepeatIcon, repeat != Windows.Media.MediaPlaybackAutoRepeatMode.None);
    }

    // Verde si está activo; si no, el color normal del tema (null dejaría el ícono invisible)
    private static void SetActive(FontIcon icon, bool active)
    {
        if (active) icon.Foreground = SpotifyGreen;
        else icon.ClearValue(IconElement.ForegroundProperty);
    }

    private async void Shuffle_Click(object sender, RoutedEventArgs e)
    {
        if (_session?.GetPlaybackInfo() is { } info) await _session.TryChangeShuffleActiveAsync(info.IsShuffleActive != true);
    }

    // Repetir: apagado → lista → canción → apagado, como el botón de Spotify
    private async void Repeat_Click(object sender, RoutedEventArgs e)
    {
        if (_session?.GetPlaybackInfo() is not { } info) return;
        var next = info.AutoRepeatMode switch
        {
            Windows.Media.MediaPlaybackAutoRepeatMode.None => Windows.Media.MediaPlaybackAutoRepeatMode.List,
            Windows.Media.MediaPlaybackAutoRepeatMode.List => Windows.Media.MediaPlaybackAutoRepeatMode.Track,
            _ => Windows.Media.MediaPlaybackAutoRepeatMode.None,
        };
        await _session.TryChangeAutoRepeatModeAsync(next);
    }

    private void OpenSpotify_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("spotify:") { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception) { TitleText.Text = "Spotify no está instalado"; }
    }

    private void UpdateTimeline()
    {
        _timeline = _session?.GetTimelineProperties();
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        var t = _timeline;
        var duration = t == null ? TimeSpan.Zero : t.EndTime - t.StartTime;
        // Como el original: ocultar la barra si la app no informa duración (radio, algunos navegadores)
        Timeline.Visibility = Show(duration > TimeSpan.Zero && Options.ShowProgress && CurrentStyle != "Compact");
        if (t == null || duration <= TimeSpan.Zero) return;

        var position = t.Position - t.StartTime;
        if (_playing) position += DateTimeOffset.Now - t.LastUpdatedTime;
        if (position > duration) position = duration;
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        ProgressScale.ScaleX = position / duration;
        PositionText.Text = Format(position);
        DurationText.Text = Format(duration);
    }

    private static string Format(TimeSpan t) => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");

    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_session != null) await _session.TryTogglePlayPauseAsync();
    }

    private async void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (_session != null) await _session.TrySkipPreviousAsync();
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_session != null) await _session.TrySkipNextAsync();
    }
}
