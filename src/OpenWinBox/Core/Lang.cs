using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace OpenWinBox;

// Textos de la interfaz: un JSON por idioma en Strings/, incrustado en el .exe.
// Sin .resw/ResourceLoader: x:Uid se resuelve con el idioma del proceso y cambiarlo exige reiniciar la app.
public static class Lang
{
    public const string FollowWindows = "System";

    // Para agregar un idioma: Strings/<código>.json y una línea aquí (ver docs/development.md)
    public static readonly (string Code, string Name)[] Available = { ("es", "Español"), ("en", "English") };

    // Si Windows está en un idioma sin traducción, y para claves que falten en una traducción
    private const string Fallback = "en";

    // Se leen antes del primer Apply, que cambia la cultura de toda la app
    private static readonly CultureInfo SystemLanguage = CultureInfo.CurrentUICulture;
    private static readonly CultureInfo SystemFormat = CultureInfo.CurrentCulture;

    private static readonly Dictionary<string, string> FallbackTexts = Load(Fallback);
    private static Dictionary<string, string> _texts = FallbackTexts;

    public static string Current { get; private set; } = Fallback; // código del idioma en uso
    public static CultureInfo Culture { get; private set; } = SystemFormat;

    // choice: FollowWindows o un código de Available; cualquier otro valor (config editada a mano) sigue a Windows
    public static void Apply(string? choice)
    {
        Current = Available.Any(a => a.Code == choice) ? choice! : Resolve(SystemLanguage);
        _texts = Current == Fallback ? FallbackTexts : Load(Current);
        // Con el mismo idioma se conserva el formato regional de Windows (es-MX, en-GB); si no, el del idioma elegido
        var language = new CultureInfo(Current);
        Culture = language.TwoLetterISOLanguageName == SystemFormat.TwoLetterISOLanguageName ? SystemFormat : language;
        CultureInfo.DefaultThreadCurrentCulture = Culture;
    }

    public static string Resolve(CultureInfo system) =>
        Available.Select(a => a.Code).FirstOrDefault(c => c == system.Name)
        ?? Available.Select(a => a.Code).FirstOrDefault(c => c == system.TwoLetterISOLanguageName)
        ?? Fallback;

    public static string T(string key) => _texts.GetValueOrDefault(key) ?? FallbackTexts.GetValueOrDefault(key) ?? key;

    public static string T(string key, params object[] args) => string.Format(Culture, T(key), args);

    public static Dictionary<string, string> Load(string code)
    {
        using var stream = typeof(Lang).Assembly.GetManifestResourceStream($"Strings.{code}.json")
                           ?? throw new InvalidOperationException($"Falta Strings/{code}.json en el proyecto");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}
