using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;

namespace OpenWinBox;

// Glance: hora y fecha sobre la foto del día de Bing, como el original (backgroundSource "Bing",
// rotación cada 30 min en orden aleatorio con fundido)
public sealed partial class GlanceView : UserControl
{
    private static readonly string ImagesFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenWinBox", "glance");
    private static readonly TimeSpan RotationInterval = TimeSpan.FromMinutes(30);
    private const int KeepImages = 30;

    private readonly DispatcherQueueTimer _clock;
    private readonly DispatcherQueueTimer _rotation;
    private bool _showingA;
    private string? _current;

    private static TimeSpan UntilNextMinute() =>
        TimeSpan.FromMilliseconds(60_000 - DateTime.Now.Second * 1000 - DateTime.Now.Millisecond + 50);

    public GlanceView()
    {
        InitializeComponent();

        _clock = DispatcherQueue.CreateTimer();
        // La hora no muestra segundos: despertar solo al cambiar de minuto (antes, cada segundo)
        _clock.Interval = UntilNextMinute();
        _clock.Tick += (_, _) => { UpdateClock(); _clock.Interval = UntilNextMinute(); };
        _rotation = DispatcherQueue.CreateTimer();
        _rotation.Interval = RotationInterval;
        _rotation.Tick += (_, _) => _ = ShowNextImageAsync();

        Loaded += async (_, _) =>
        {
            UpdateClock();
            _clock.Interval = UntilNextMinute();
            _clock.Start();
            App.FeatureOptionsChanged += UpdateClock;
            _rotation.Start();
            await ShowNextImageAsync();          // lo que ya está en caché, al instante
            if (await DownloadBingImagesAsync()) // y si hay fotos nuevas del día, mostrar una
                await ShowNextImageAsync();
        };
        Unloaded += (_, _) => { _clock.Stop(); _rotation.Stop(); App.FeatureOptionsChanged -= UpdateClock; };
    }

    private static string TimeFormat => App.Current.Config.Glance.Use24Hour ? "HH:mm" : "h:mm";
    private static string AmPm(DateTime t) => App.Current.Config.Glance.Use24Hour ? "" : t.Hour < 12 ? "a.m." : "p.m.";

    private static string Offset(TimeZoneInfo zone, DateTime now)
    {
        var o = zone.GetUtcOffset(now);
        return $"UTC{(o < TimeSpan.Zero ? "−" : "+")}{o.Duration().ToString(@"hh\:mm")}";
    }

    // Etiqueta por defecto del segundo reloj: la primera ciudad del nombre de la zona ("(UTC+01:00) Bruselas, …" → "Bruselas")
    public static string DefaultLabel(TimeZoneInfo zone)
    {
        var name = zone.DisplayName;
        var start = name.IndexOf(") ", StringComparison.Ordinal);
        if (start >= 0) name = name[(start + 2)..];
        var comma = name.IndexOf(',');
        return comma > 0 ? name[..comma] : name;
    }

    private void UpdateClock()
    {
        var o = App.Current.Config.Glance;
        var now = DateTime.Now;
        var time = now.ToString(TimeFormat);
        if (TimeText.Text != time) TimeText.Text = time;
        AmPmText.Text = AmPm(now);

        var date = o.ShowDate ? now.ToString("M", CultureInfo.CurrentCulture) + (o.ShowYear ? $" {now.Year}" : "") : "";
        var weekday = o.ShowWeekday ? now.ToString("dddd", CultureInfo.CurrentCulture) : "";
        DateText.Text = string.Join("   ", new[] { date, weekday }.Where(s => s.Length > 0));
        DateText.Visibility = DateText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        TimeZoneInfo? zone = null;
        if (o.ShowSecondClock)
        {
            try { zone = TimeZoneInfo.FindSystemTimeZoneById(o.SecondTimeZone); }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { } // zona borrada del sistema
        }
        var dual = zone != null ? Visibility.Visible : Visibility.Collapsed;
        SecondClock.Visibility = ClockDivider.Visibility = LocalLabelText.Visibility = LocalZoneText.Visibility = dual;
        SecondColumn.Width = zone != null ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        if (zone == null) return;
        LocalLabelText.Text = string.IsNullOrWhiteSpace(o.LocalClockLabel) ? DefaultLabel(TimeZoneInfo.Local) : o.LocalClockLabel;
        LocalZoneText.Text = Offset(TimeZoneInfo.Local, now);
        var there = TimeZoneInfo.ConvertTime(now, zone);
        SecondLabelText.Text = string.IsNullOrWhiteSpace(o.SecondClockLabel) ? DefaultLabel(zone) : o.SecondClockLabel;
        SecondTimeText.Text = there.ToString(TimeFormat);
        SecondAmPmText.Text = AmPm(there);
        SecondZoneText.Text = Offset(zone, now);
    }

    private async Task ShowNextImageAsync()
    {
        var images = Directory.Exists(ImagesFolder) ? Directory.GetFiles(ImagesFolder, "*.jpg") : Array.Empty<string>();
        var choices = images.Where(i => i != _current).ToArray();
        if (choices.Length == 0) return;
        var next = choices[Random.Shared.Next(choices.Length)];

        // Ancho necesario para cubrir el widget también a lo alto (las fotos de Bing son 16:9); decodificar solo
        // al ancho del widget dejaba la foto borrosa en widgets altos
        var coverWidth = Math.Max(ActualWidth, ActualHeight * 16 / 9) * XamlRoot.RasterizationScale;
        var bitmap = new BitmapImage { DecodePixelWidth = Math.Max(400, (int)coverWidth) };
        try
        {
            using var stream = File.OpenRead(next);
            await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return; }
        _current = next;

        // Fundido cruzado entre las dos capas de imagen
        var (incoming, outgoing) = _showingA ? (ImageB, ImageA) : (ImageA, ImageB);
        _showingA = !_showingA;
        incoming.Source = bitmap;
        var fade = new Storyboard();
        fade.Children.Add(Fade(incoming, 1));
        fade.Children.Add(Fade(outgoing, 0));
        fade.Begin();
    }

    private static DoubleAnimation Fade(UIElement target, double to)
    {
        var animation = new DoubleAnimation { To = to, Duration = TimeSpan.FromMilliseconds(800), EasingFunction = new QuadraticEase() };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Opacity");
        return animation;
    }

    // Descarga las fotos recientes de Bing que falten; true si bajó alguna nueva
    private static async Task<bool> DownloadBingImagesAsync()
    {
        try
        {
            Directory.CreateDirectory(ImagesFolder);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var market = CultureInfo.CurrentUICulture.Name;
            using var doc = JsonDocument.Parse(await http.GetStringAsync(
                $"https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=8&mkt={market}"));
            var added = false;
            foreach (var image in doc.RootElement.GetProperty("images").EnumerateArray())
            {
                var file = Path.Combine(ImagesFolder, image.GetProperty("hsh").GetString() + ".jpg");
                if (File.Exists(file)) continue;
                var bytes = await http.GetByteArrayAsync("https://www.bing.com" + image.GetProperty("url").GetString());
                await File.WriteAllBytesAsync(file + ".tmp", bytes); // .tmp: nunca dejar una foto a medias
                File.Move(file + ".tmp", file, overwrite: true);
                added = true;
            }
            // Conservar solo las más recientes
            foreach (var old in new DirectoryInfo(ImagesFolder).GetFiles("*.jpg").OrderByDescending(f => f.LastWriteTimeUtc).Skip(KeepImages))
                old.Delete();
            return added;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or JsonException
                                      or KeyNotFoundException or InvalidOperationException or UnauthorizedAccessException)
        {
            return false; // sin internet: seguir con las fotos en caché
        }
    }
}
