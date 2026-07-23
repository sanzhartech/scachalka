using System.Globalization;

namespace YtDlpGui.Abstractions.Localization;

/// <summary>
/// Ambient access to the current <see cref="ILocalizer"/> for layers whose objects are
/// not created through DI (domain models, jobs). The composition root assigns
/// <see cref="Current"/> at startup. Before assignment, lookups return the key,
/// so the app degrades gracefully (and unit tests can run without wiring).
/// </summary>
public static class Loc
{
    public static ILocalizer? Current { get; set; }

    public static string T(string key) =>
        Current is null ? key : Current[key];

    public static string T(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key), args);
}
