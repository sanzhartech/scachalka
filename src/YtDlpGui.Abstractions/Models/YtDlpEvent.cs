using YtDlpGui.Abstractions.Enums;

namespace YtDlpGui.Abstractions.Models;

public enum YtDlpEventKind
{
    Progress,
    StageChanged,
    DestinationResolved,
    AlreadyDownloaded
}

/// <summary>
/// A structured event parsed from one line of yt-dlp console output.
/// Exactly one payload is populated depending on <see cref="Kind"/>.
/// </summary>
public sealed record YtDlpEvent(
    YtDlpEventKind Kind,
    ProgressSnapshot? Progress = null,
    DownloadStage? Stage = null,
    string? Path = null)
{
    public static YtDlpEvent ForProgress(ProgressSnapshot snapshot) =>
        new(YtDlpEventKind.Progress, Progress: snapshot);

    public static YtDlpEvent ForStage(DownloadStage stage, string? path = null) =>
        new(YtDlpEventKind.StageChanged, Stage: stage, Path: path);

    public static YtDlpEvent ForDestination(string path) =>
        new(YtDlpEventKind.DestinationResolved, Path: path);

    public static YtDlpEvent ForAlreadyDownloaded(string? path) =>
        new(YtDlpEventKind.AlreadyDownloaded, Path: path);
}
