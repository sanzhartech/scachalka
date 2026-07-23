using System.ComponentModel;

namespace YtDlpGui.Abstractions.Localization;

/// <summary>
/// Application-wide string catalog with runtime language switching.
/// Implements <see cref="INotifyPropertyChanged"/> so WPF bindings refresh when the
/// language changes (the <c>Language</c> property is raised on every switch).
/// </summary>
public interface ILocalizer : INotifyPropertyChanged
{
    /// <summary>Localized string for the key, or the key itself when it is unknown.</summary>
    string this[string key] { get; }

    /// <summary>Current two-letter language code (e.g. "en", "ru").</summary>
    string Language { get; }

    IReadOnlyList<string> AvailableLanguages { get; }

    void SetLanguage(string language);

    event EventHandler? LanguageChanged;
}
