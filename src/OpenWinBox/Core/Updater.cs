using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenWinBox;

internal sealed record UpdateRelease(string Version, string PageUrl, string SetupUrl, string SumsUrl, string SetupName);

// Busca versiones nuevas en las releases de GitHub y descarga el instalador verificando su SHA-256.
// shortcut: el hash no protege si alguien controla la cuenta de GitHub, hasta firmar el instalador
internal static class Updater
{
    private const string ReleasesUrl = "https://github.com/yosefhdev/Open-WinBox/releases";
    // /releases/latest ignora las betas: hay que pedir la lista
    private const string ApiUrl = "https://api.github.com/repos/yosefhdev/Open-WinBox/releases?per_page=30";
    private const string SumsName = "SHA256SUMS.txt";

    // Limita lo que viene de GitHub: esta versión forma parte de rutas y URLs
    private static readonly Regex VersionFormat = new(@"^\d{1,9}\.\d{1,9}\.\d{1,9}(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?$");

    private static readonly HttpClient Http = CreateClient();

    private static readonly string DownloadDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenWinBox", "updates");

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OpenWinBox"); // la API de GitHub rechaza peticiones sin User-Agent
        return client;
    }

    // InformationalVersion trae la etiqueta beta ("1.0.0-beta.1+<commit>"); el commit no se usa
    public static string CurrentVersion { get; } =
        (typeof(Updater).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

    public static bool IsBeta(string version) => version.Contains('-');

    // Precedencia de SemVer sin metadatos: 1.0.0-beta.2 < 1.0.0-beta.10 < 1.0.0-rc.1 < 1.0.0
    public static int Compare(string a, string b)
    {
        var (partsA, partsB) = (a.Split('-', 2), b.Split('-', 2));
        var core = partsA[0].Split('.').Select(int.Parse).Zip(partsB[0].Split('.').Select(int.Parse), (x, y) => x.CompareTo(y))
            .FirstOrDefault(c => c != 0);
        if (core != 0) return core;
        if (partsA.Length == 1 || partsB.Length == 1) return partsB.Length.CompareTo(partsA.Length); // la final va después de sus betas

        var (idsA, idsB) = (partsA[1].Split('.'), partsB[1].Split('.'));
        foreach (var (x, y) in idsA.Zip(idsB))
        {
            var (isNumX, isNumY) = (int.TryParse(x, out var numX), int.TryParse(y, out var numY));
            var c = isNumX && isNumY ? numX.CompareTo(numY)
                : isNumX != isNumY ? (isNumX ? -1 : 1)
                : string.CompareOrdinal(x, y);
            if (c != 0) return c;
        }
        return idsA.Length.CompareTo(idsB.Length);
    }

    // La versión más alta del canal que sea más nueva que la actual; null si ya está al día
    public static UpdateRelease? PickUpdate(string releasesJson, bool includeBetas, string current)
    {
        using var doc = JsonDocument.Parse(releasesJson);
        UpdateRelease? best = null;
        foreach (var release in doc.RootElement.EnumerateArray())
        {
            var tag = release.GetProperty("tag_name").GetString() ?? "";
            var version = tag.TrimStart('v');
            if (release.GetProperty("draft").GetBoolean() || !VersionFormat.IsMatch(version)) continue;
            if (!includeBetas && (release.GetProperty("prerelease").GetBoolean() || IsBeta(version))) continue;

            var setupName = $"OpenWinBox-{version}-Setup.exe";
            var assets = release.GetProperty("assets").EnumerateArray().Select(a => a.GetProperty("name").GetString()).ToList();
            if (!assets.Contains(setupName) || !assets.Contains(SumsName)) continue;

            if (best == null || Compare(version, best.Version) > 0)
                best = new UpdateRelease(version, $"{ReleasesUrl}/tag/{tag}", $"{ReleasesUrl}/download/{tag}/{setupName}",
                    $"{ReleasesUrl}/download/{tag}/{SumsName}", setupName);
        }
        return best != null && Compare(best.Version, current) > 0 ? best : null;
    }

    // Formato de sha256sum: "<hash>  <archivo>" por línea
    public static string? ExpectedHash(string sums, string fileName) =>
        sums.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .FirstOrDefault(p => p.Length == 2 && p[1].TrimStart('*') == fileName)?[0];

    public static async Task<UpdateRelease?> FindUpdateAsync(bool includeBetas, CancellationToken token) =>
        PickUpdate(await Http.GetStringAsync(ApiUrl, token), includeBetas, CurrentVersion);

    // Devuelve la ruta del instalador ya verificado. progress: de 0 a 1
    public static async Task<string> DownloadAsync(UpdateRelease update, IProgress<double> progress, CancellationToken token)
    {
        var expected = ExpectedHash(await Http.GetStringAsync(update.SumsUrl, token), update.SetupName)
                       ?? throw new InvalidDataException($"{SumsName} no incluye {update.SetupName}");

        CleanDownloads();
        Directory.CreateDirectory(DownloadDir);
        var path = Path.Combine(DownloadDir, update.SetupName);

        // ResponseHeadersRead: sin él, HttpClient guarda todo en memoria y aplica su Timeout a la descarga completa
        using (var response = await Http.GetAsync(update.SetupUrl, HttpCompletionOption.ResponseHeadersRead, token))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(token);
            await using var file = File.Create(path);
            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, token)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), token);
                received += read;
                if (total > 0) progress.Report((double)received / total.Value);
            }
        }

        await using (var file = File.OpenRead(path))
        {
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(file, token));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"El hash de {update.SetupName} no coincide");
        }
        return path;
    }

    // El instalador ya no hace falta tras usarlo (si sigue en uso, se borra en el próximo arranque)
    public static void CleanDownloads()
    {
        try { if (Directory.Exists(DownloadDir)) Directory.Delete(DownloadDir, recursive: true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
