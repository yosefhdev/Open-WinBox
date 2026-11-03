using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Devices.Geolocation;

namespace OpenWinBox;

// Tiempo: clima actual con Open-Meteo (gratis, sin cuenta). Ubicación de Windows si el usuario la permite;
// si no, aproximada por IP, como la "ubicación automática" del original. Se actualiza cada 60 min.
public sealed partial class WeatherView : UserControl
{
    private record Snapshot(double Temp, double Feels, int Humidity, double Wind, int Rain, int Code, bool IsDay,
        string? Place, DateTimeOffset At);

    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenWinBox", "weather.json");
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly DispatcherQueueTimer _refresh;
    private bool _loading;

    public WeatherView()
    {
        InitializeComponent();
        _refresh = DispatcherQueue.CreateTimer();
        _refresh.Interval = TimeSpan.FromMinutes(60); // weatherRefreshIntervalMinutes del original
        _refresh.Tick += (_, _) => _ = RefreshAsync();

        Action onLocationChanged = () => _ = RefreshAsync();
        Loaded += (_, _) =>
        {
            ApplyOptions();
            if (ReadCache() is { } cached) Show(cached); // lo último conocido, al instante
            _refresh.Start();
            LocationChanged += onLocationChanged;
            App.FeatureOptionsChanged += ApplyOptions;
            _ = RefreshAsync();
        };
        Unloaded += (_, _) => { _refresh.Stop(); LocationChanged -= onLocationChanged; App.FeatureOptionsChanged -= ApplyOptions; };
    }

    // Datos visibles (Configuración › Widgets funcionales › Tiempo)
    private void ApplyOptions()
    {
        var o = App.Current.Config.Weather;
        FeelsText.Visibility = o.ShowFeelsLike ? Visibility.Visible : Visibility.Collapsed;
        HumidityColumn.Width = o.ShowHumidity ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        WindColumn.Width = o.ShowWind ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        RainColumn.Width = o.ShowRain ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Stats.Visibility = o.ShowHumidity || o.ShowWind || o.ShowRain ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_loading) return;
        _loading = true;
        RefreshButton.IsEnabled = false;
        try
        {
            var (lat, lon, place) = await LocateAsync();
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(FormattableString.Invariant(
                $"https://api.open-meteo.com/v1/forecast?latitude={lat:0.####}&longitude={lon:0.####}") +
                "&current=temperature_2m,apparent_temperature,relative_humidity_2m,wind_speed_10m,weather_code,is_day" +
                "&daily=precipitation_probability_max&forecast_days=1&timezone=auto"));
            var current = doc.RootElement.GetProperty("current");
            var rain = doc.RootElement.GetProperty("daily").GetProperty("precipitation_probability_max")[0];
            var snapshot = new Snapshot(
                current.GetProperty("temperature_2m").GetDouble(),
                current.GetProperty("apparent_temperature").GetDouble(),
                (int)current.GetProperty("relative_humidity_2m").GetDouble(),
                current.GetProperty("wind_speed_10m").GetDouble(),
                rain.ValueKind == JsonValueKind.Number ? rain.GetInt32() : 0,
                current.GetProperty("weather_code").GetInt32(),
                current.GetProperty("is_day").GetInt32() == 1,
                place, DateTimeOffset.Now);
            Show(snapshot);
            WriteCache(snapshot);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException
                                      or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            // Sin internet: dejar lo último conocido y avisar de cuándo es
            if (ReadCache() is { } cached) Show(cached, stale: true);
            else ConditionText.Text = "Sin conexión";
        }
        finally
        {
            _loading = false;
            RefreshButton.IsEnabled = true;
        }
    }

    // Avisa a los widgets abiertos que cambió la ubicación elegida en Configuración
    public static event Action? LocationChanged;
    public static void NotifyLocationChanged() => LocationChanged?.Invoke();

    private static async Task<(double Lat, double Lon, string? Place)> LocateAsync()
    {
        if (App.Current.Config.WeatherLocation is { } chosen) return (chosen.Latitude, chosen.Longitude, chosen.Name);
        try
        {
            // Solo responde si "Permitir que las aplicaciones de escritorio accedan a tu ubicación" está activado
            if (await Geolocator.RequestAccessAsync() == GeolocationAccessStatus.Allowed)
            {
                var position = await new Geolocator { DesiredAccuracy = PositionAccuracy.Default }
                    .GetGeopositionAsync(TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(10));
                var (lat, lon) = (position.Coordinate.Point.Position.Latitude, position.Coordinate.Point.Position.Longitude);
                return (lat, lon, await CityNameAsync(lat, lon));
            }
        }
        catch (Exception) { } // ubicación apagada o sin respuesta: usar la aproximada por IP
        using var doc = JsonDocument.Parse(await Http.GetStringAsync("https://ipapi.co/json/"));
        var root = doc.RootElement;
        return (root.GetProperty("latitude").GetDouble(), root.GetProperty("longitude").GetDouble(),
            root.TryGetProperty("city", out var city) ? city.GetString() : null);
    }

    // Nombre de la ciudad para unas coordenadas (servicio gratuito sin cuenta); sin nombre si falla
    private static async Task<string?> CityNameAsync(double lat, double lon)
    {
        try
        {
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(FormattableString.Invariant(
                $"https://api.bigdatacloud.net/data/reverse-geocode-client?latitude={lat:0.####}&longitude={lon:0.####}&localityLanguage=es")));
            var root = doc.RootElement;
            foreach (var key in new[] { "city", "locality", "principalSubdivision" })
                if (root.TryGetProperty(key, out var v) && v.GetString() is { Length: > 0 } name) return name;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException) { }
        return null;
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => App.Current.OpenSettings("Weather");

    private void Show(Snapshot s, bool stale = false)
    {
        var (icon, text) = Describe(s.Code, s.IsDay);
        IconText.Text = icon;
        TempText.Text = $"{Math.Round(s.Temp)}°C";
        ConditionText.Text = text;
        FeelsText.Text = $"Se siente {Math.Round(s.Feels)}°C";
        HumidityText.Text = $"{s.Humidity}%";
        WindText.Text = $"{Math.Round(s.Wind)} km/h";
        RainText.Text = $"{s.Rain}%";
        PlaceText.Text = (s.Place ?? "Ubicación actual") + (stale ? $"  ·  sin conexión, datos de las {s.At:HH:mm}" : "");
        ToolTipService.SetToolTip(PlaceText, App.Current.Config.WeatherLocation == null
            ? "Ubicación automática. Cámbiala con ⚙" : "Ubicación elegida en Configuración");
    }

    // Códigos del tiempo WMO que usa Open-Meteo
    private static (string Icon, string Text) Describe(int code, bool day) => code switch
    {
        0 => (day ? "☀️" : "🌙", "Despejado"),
        1 => (day ? "🌤️" : "🌙", "Mayormente despejado"),
        2 => (day ? "⛅" : "☁️", "Parcialmente nublado"),
        3 => ("☁️", "Cubierto"),
        45 or 48 => ("🌫️", "Niebla"),
        51 or 53 or 55 => ("🌦️", "Llovizna"),
        56 or 57 => ("🌧️", "Llovizna helada"),
        61 or 63 or 65 => ("🌧️", "Lluvia"),
        66 or 67 => ("🌧️", "Lluvia helada"),
        71 or 73 or 75 or 77 => ("🌨️", "Nieve"),
        80 or 81 or 82 => ("🌦️", "Chubascos"),
        85 or 86 => ("🌨️", "Chubascos de nieve"),
        95 => ("⛈️", "Tormenta"),
        96 or 99 => ("⛈️", "Tormenta con granizo"),
        _ => ("🌡️", "—"),
    };

    private static Snapshot? ReadCache()
    {
        try { return File.Exists(CachePath) ? JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(CachePath)) : null; }
        catch (Exception e) when (e is JsonException or IOException) { return null; }
    }

    private static void WriteCache(Snapshot s)
    {
        try { File.WriteAllText(CachePath, JsonSerializer.Serialize(s)); }
        catch (IOException) { } // la caché es opcional
    }
}
