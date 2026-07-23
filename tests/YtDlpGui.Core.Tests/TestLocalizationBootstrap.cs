using System.ComponentModel;
using System.Runtime.CompilerServices;
using YtDlpGui.Abstractions.Localization;

namespace YtDlpGui.Core.Tests;

/// <summary>
/// Ensures <see cref="Loc.Current"/> is populated before any test runs, so code under test
/// (e.g. ErrorClassifier) produces stable, non-empty strings. Returns the key as the value,
/// which keeps assertions independent of the production catalog wording.
/// </summary>
internal static class TestLocalizationBootstrap
{
    [ModuleInitializer]
    internal static void Initialize() => Loc.Current ??= new KeyEchoLocalizer();

    private sealed class KeyEchoLocalizer : ILocalizer
    {
        public string this[string key] => key;
        public string Language => "en";
        public IReadOnlyList<string> AvailableLanguages { get; } = ["en"];
        public void SetLanguage(string language) { }
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? LanguageChanged;

        // Keep the compiler from warning about the never-raised events.
        private void Suppress()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
