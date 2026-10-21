using YtDlpGui.Abstractions.Enums;

namespace YtDlpGui.Core.Stages;

/// <summary>Single source of truth for what is allowed in each download stage.</summary>
public static class StageRules
{
    public static bool IsTerminal(DownloadStage stage) =>
        stage is DownloadStage.Completed or DownloadStage.Failed or DownloadStage.Canceled;

    public static bool IsActive(DownloadStage stage) =>
        stage is DownloadStage.Resolving or DownloadStage.Downloading
            or DownloadStage.Merging or DownloadStage.Converting;

    public static bool CanCancel(DownloadStage stage) =>
        stage is DownloadStage.Queued or DownloadStage.Paused || IsActive(stage);

    public static bool CanPause(DownloadStage stage) => IsActive(stage);

    public static bool CanResume(DownloadStage stage) => stage is DownloadStage.Paused;

    public static bool CanRetry(DownloadStage stage) =>
        stage is DownloadStage.Failed or DownloadStage.Canceled;
}
