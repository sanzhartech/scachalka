namespace YtDlpGui.Abstractions.Models;

/// <summary>Resolved locations and versions of the external tools the app depends on.</summary>
public sealed record ToolLocation(
    string? YtDlpPath,
    string? YtDlpVersion,
    string? FfmpegPath,
    string? FfmpegVersion)
{
    public bool HasYtDlp => !string.IsNullOrEmpty(YtDlpPath);
    public bool HasFfmpeg => !string.IsNullOrEmpty(FfmpegPath);
    public bool IsReady => HasYtDlp && HasFfmpeg;

    public static ToolLocation Empty { get; } = new(null, null, null, null);
}
