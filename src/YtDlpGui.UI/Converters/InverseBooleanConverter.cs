using System.Globalization;
using System.Windows.Data;

namespace YtDlpGui.UI.Converters;

/// <summary>Negates a boolean binding (e.g. disable a button while a check is running).</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : false;
}
