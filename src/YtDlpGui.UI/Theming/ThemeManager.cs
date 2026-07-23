using System.Windows;

namespace YtDlpGui.UI.Theming;

/// <summary>
/// Swaps the active theme ResourceDictionary at runtime.
/// All styles reference brushes via DynamicResource, so the swap restyles the live UI instantly.
/// </summary>
public static class ThemeManager
{
    public const string Dark = "Dark";
    public const string Light = "Light";

    private const string ThemeFolderMarker = "/Themes/";

    /// <summary>Raised after a theme is applied; the window uses it to repaint the title bar.</summary>
    public static event Action<bool>? ThemeChanged;

    public static bool IsDark { get; private set; } = true;

    public static void Apply(string theme)
    {
        var isDark = !string.Equals(theme, Light, StringComparison.OrdinalIgnoreCase);
        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        var newSource = new Uri(
            $"pack://application:,,,/YtDlpGui.UI;component/Themes/{(isDark ? Dark : Light)}.xaml",
            UriKind.Absolute);

        var dictionaries = app.Resources.MergedDictionaries;
        for (var i = 0; i < dictionaries.Count; i++)
        {
            var source = dictionaries[i].Source;
            if (source is not null
                && source.OriginalString.Contains(ThemeFolderMarker, StringComparison.OrdinalIgnoreCase)
                && (source.OriginalString.EndsWith("Dark.xaml", StringComparison.OrdinalIgnoreCase)
                    || source.OriginalString.EndsWith("Light.xaml", StringComparison.OrdinalIgnoreCase)))
            {
                dictionaries[i] = new ResourceDictionary { Source = newSource };
                IsDark = isDark;
                ThemeChanged?.Invoke(isDark);
                return;
            }
        }

        // No theme dictionary yet (first call during startup) — add one.
        dictionaries.Insert(0, new ResourceDictionary { Source = newSource });
        IsDark = isDark;
        ThemeChanged?.Invoke(isDark);
    }
}
