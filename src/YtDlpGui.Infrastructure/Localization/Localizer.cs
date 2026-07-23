using System.ComponentModel;
using System.Globalization;
using YtDlpGui.Abstractions.Localization;

namespace YtDlpGui.Infrastructure.Localization;

/// <summary>
/// In-memory string catalog with runtime language switching.
/// Raising <c>Language</c> and <c>Item[]</c> change notifications lets every WPF
/// binding built through the localization markup extension re-evaluate instantly.
/// Unknown keys return the key itself, so a missing translation is visible but harmless.
/// </summary>
public sealed class Localizer : ILocalizer
{
    public const string DefaultLanguage = "en";

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Catalogs =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = StringCatalog.English,
            ["ru"] = StringCatalog.Russian
        };

    private IReadOnlyDictionary<string, string> _current = StringCatalog.English;
    private string _language = DefaultLanguage;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    public string this[string key] =>
        _current.TryGetValue(key, out var value) ? value : key;

    public string Language => _language;

    public IReadOnlyList<string> AvailableLanguages { get; } = ["en", "ru"];

    public void SetLanguage(string language)
    {
        var code = Normalize(language);
        if (string.Equals(code, _language, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _language = code;
        _current = Catalogs[code];

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Resolves an explicit code, or falls back to the OS UI language when empty.</summary>
    public static string ResolveInitialLanguage(string? preferred)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return Normalize(preferred);
        }

        var osLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Normalize(osLang);
    }

    private static string Normalize(string language)
    {
        var code = language.Trim().ToLowerInvariant();
        return Catalogs.ContainsKey(code) ? code : DefaultLanguage;
    }
}
