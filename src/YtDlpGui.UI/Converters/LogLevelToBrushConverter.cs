using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using YtDlpGui.Abstractions.Enums;

namespace YtDlpGui.UI.Converters;

/// <summary>Colors log lines by severity in the log window.</summary>
public sealed class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is LogLevel level
            ? level switch
            {
                LogLevel.Error => "Brush.Danger",
                LogLevel.Warning => "Brush.Warning",
                LogLevel.Debug => "Brush.Text.Secondary",
                _ => "Brush.Text.Primary"
            }
            : "Brush.Text.Primary";

        return System.Windows.Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
