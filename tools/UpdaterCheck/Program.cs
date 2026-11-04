using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using OpenWinBox;

var failures = 0;
void Fail(string text)
{
    failures++;
    Console.WriteLine($"FALLA: {text}");
}
void Check(bool ok, [CallerArgumentExpression(nameof(ok))] string text = "")
{
    if (!ok) Fail(text);
}
static string SourcePath([CallerFilePath] string path = "") => path;

// Orden de versiones
string[] ordered = { "1.0.0-beta.1", "1.0.0-beta.2", "1.0.0-beta.10", "1.0.0-rc.1", "1.0.0", "1.0.1-beta.1", "1.0.1", "1.2.0", "2.0.0" };
for (var i = 0; i < ordered.Length - 1; i++)
{
    Check(Updater.Compare(ordered[i], ordered[i + 1]) < 0);
    Check(Updater.Compare(ordered[i + 1], ordered[i]) > 0);
}
Check(Updater.Compare("1.0.0-beta.2", "1.0.0-beta.2") == 0);

static string Release(string tag, bool prerelease = false, bool draft = false, bool assets = true)
{
    var version = tag.TrimStart('v');
    var list = assets ? $$"""[{"name":"OpenWinBox-{{version}}-Setup.exe"},{"name":"SHA256SUMS.txt"}]""" : "[]";
    return $$"""{"tag_name":"{{tag}}","prerelease":{{prerelease.ToString().ToLower()}},"draft":{{draft.ToString().ToLower()}},"assets":{{list}}}""";
}
static string Json(params string[] releases) => "[" + string.Join(",", releases) + "]";

// Canales
var mixed = Json(Release("v1.0.0-beta.10", prerelease: true), Release("v1.0.0-beta.3", prerelease: true), Release("v0.9.0"));
Check(Updater.PickUpdate(mixed, true, "1.0.0-beta.2")?.Version == "1.0.0-beta.10");
Check(Updater.PickUpdate(mixed, false, "0.8.0")?.Version == "0.9.0");
Check(Updater.PickUpdate(mixed, false, "1.0.0-beta.2") == null); // oficiales: la 0.9.0 sería retroceder
Check(Updater.PickUpdate(Json(Release("v1.0.0"), Release("v1.0.0-beta.9", prerelease: true)), false, "1.0.0-beta.2")?.Version == "1.0.0");
Check(Updater.PickUpdate(Json(Release("v1.0.0-beta.9")), false, "0.1.0") == null); // beta aunque GitHub no la marque

// Lo que no se debe ofrecer
Check(Updater.PickUpdate(Json(Release("v9.0.0", draft: true)), true, "1.0.0") == null);
Check(Updater.PickUpdate(Json(Release("v9.0.0", assets: false)), true, "1.0.0") == null);
Check(Updater.PickUpdate(Json(Release("v9.0.0/../../x")), true, "1.0.0") == null);
Check(Updater.PickUpdate(Json(Release("no-es-version")), true, "1.0.0") == null);
Check(Updater.PickUpdate("[]", true, "1.0.0") == null);
Check(Updater.PickUpdate(Json(Release("v1.0.0")), true, "1.0.0") == null);

// URLs
var found = Updater.PickUpdate(Json(Release("v1.0.0")), false, "0.1.0")!;
Check(found.SetupUrl == "https://github.com/yosefhdev/Open-WinBox/releases/download/v1.0.0/OpenWinBox-1.0.0-Setup.exe");
Check(found.SumsUrl == "https://github.com/yosefhdev/Open-WinBox/releases/download/v1.0.0/SHA256SUMS.txt");

// SHA256SUMS.txt, con LF, CRLF y el asterisco del modo binario
Check(Updater.ExpectedHash("abc  a.exe\ndef  b.exe\n", "b.exe") == "def");
Check(Updater.ExpectedHash("abc  a.exe\r\n", "a.exe") == "abc");
Check(Updater.ExpectedHash("abc *a.exe\n", "a.exe") == "abc");
Check(Updater.ExpectedHash("abc  a.exe\n", "b.exe") == null);

// Idiomas: cada traducción tiene las mismas claves y los mismos {0} que la de referencia
static string[] Holes(string text) => Regex.Matches(text, @"\{\d+\}").Select(m => m.Value).Order().ToArray();
var reference = Lang.Load("en");
foreach (var (code, _) in Lang.Available)
{
    var texts = Lang.Load(code);
    foreach (var key in reference.Keys.Except(texts.Keys)) Fail($"{code}: falta {key}");
    foreach (var key in texts.Keys.Except(reference.Keys)) Fail($"{code}: sobra {key}");
    foreach (var key in texts.Keys.Intersect(reference.Keys))
        if (!Holes(texts[key]).SequenceEqual(Holes(reference[key]))) Fail($"{code}: {key} no tiene los mismos marcadores que en");
}

// Toda clave usada en el código existe
var src = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!, "..", "..", "src", "OpenWinBox"));
foreach (var file in Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
             .Where(f => f.EndsWith(".cs") || f.EndsWith(".xaml"))
             .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
    foreach (Match m in Regex.Matches(File.ReadAllText(file), @"Lang\.T\((?:[^""\r\n]*\? )?""(?<k>[\w.]+)""|Key=(?<k>[\w.]+)\}"))
        if (!reference.ContainsKey(m.Groups["k"].Value)) Fail($"{Path.GetFileName(file)}: no existe la clave {m.Groups["k"].Value}");

// Elección de idioma
Check(Lang.Resolve(new CultureInfo("es-MX")) == "es");
Check(Lang.Resolve(new CultureInfo("en-GB")) == "en");
Check(Lang.Resolve(new CultureInfo("de-DE")) == "en"); // sin traducción
Lang.Apply("es");
Check(Lang.T("settings.language") == "Idioma");
Check(new DateTime(2026, 11, 3).ToString("M", Lang.Culture).Contains("noviembre"));
Lang.Apply("en");
Check(Lang.T("settings.language") == "Language");
Check(Lang.T("widget.tabCount", "A", 2) == "A (2 tabs)");
Check(new DateTime(2026, 11, 3).ToString("M", Lang.Culture).Contains("November"));
Check(Lang.T("clave.inexistente") == "clave.inexistente");
Lang.Apply(null); // config sin el campo
Check(Lang.Available.Any(a => a.Code == Lang.Current));
Lang.Apply("xx"); // config editada a mano
Check(Lang.Available.Any(a => a.Code == Lang.Current));

Console.WriteLine(failures == 0 ? "OK" : $"{failures} fallas");
return failures == 0 ? 0 : 1;
