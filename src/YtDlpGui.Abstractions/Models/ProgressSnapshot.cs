namespace YtDlpGui.Abstractions.Models;

/// <summary>
/// A single point-in-time progress reading parsed from yt-dlp output.
/// Any value may be null when yt-dlp reports "NA" (e.g. live streams without a known size).
/// </summary>
public sealed record ProgressSnapshot(
    double? Percent,
    double? SpeedBytesPerSecond,
    int? EtaSeconds,
    double? DownloadedBytes,
    double? TotalBytes)
{
    public bool IsFinished => Percent is >= 100;
}
