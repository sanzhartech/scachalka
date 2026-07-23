namespace YtDlpGui.Core.Arguments;

/// <summary>
/// Machine-readable progress templates passed to yt-dlp.
/// Field order is a contract shared with <see cref="Progress.YtDlpProgressParser"/> —
/// keep both in sync when changing.
/// </summary>
public static class ProgressTemplates
{
    /// <summary>Marker prefix for download progress lines.</summary>
    public const string DownloadMarker = "NDL|";

    /// <summary>Marker prefix for post-processing (merge/convert) lines.</summary>
    public const string PostprocessMarker = "NPP|";

    public const string Download =
        "download:" + DownloadMarker +
        "%(progress.status)s|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|" +
        "%(progress.total_bytes_estimate)s|%(progress.speed)s|%(progress.eta)s";

    public const string Postprocess =
        "postprocess:" + PostprocessMarker +
        "%(progress.status)s|%(progress.postprocessor)s";
}
