using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using YtDlpGui.Abstractions.Enums;

namespace YtDlpGui.UI.Converters;

/// <summary>Maps a download stage onto a theme brush for the stage chip in the queue list.</summary>
public sealed class StageToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is DownloadStage stage
            ? stage switch
            {
                DownloadStage.Completed => "Brush.Success",
                DownloadStage.Failed => "Brush.Danger",
                DownloadStage.Canceled => "Brush.Warning",
                DownloadStage.Paused => "Brush.Warning",
                DownloadStage.Queued => "Brush.Text.Secondary",
                _ => "Brush.Accent"
            }
            : "Brush.Text.Secondary";

        return System.Windows.Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
