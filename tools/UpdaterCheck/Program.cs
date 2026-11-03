using System.Runtime.CompilerServices;
using OpenWinBox;

var failures = 0;
void Check(bool ok, [CallerArgumentExpression(nameof(ok))] string text = "")
{
    if (ok) return;
    failures++;
    Console.WriteLine($"FALLA: {text}");
}

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

Console.WriteLine(failures == 0 ? "OK" : $"{failures} fallas");
return failures == 0 ? 0 : 1;
