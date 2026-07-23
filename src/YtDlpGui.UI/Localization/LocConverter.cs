using System.Globalization;
using System.Windows.Data;
using YtDlpGui.Abstractions.Localization;

namespace YtDlpGui.UI.Localization;

/// <summary>
/// Returns the localized string for the key passed as the converter parameter.
/// The bound value (the localizer's Language) is ignored — it exists only to make the
/// binding re-evaluate when the language changes.
/// </summary>
public sealed class LocConverter : IValueConverter
{
    public static LocConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Loc.T(parameter as string ?? string.Empty);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
