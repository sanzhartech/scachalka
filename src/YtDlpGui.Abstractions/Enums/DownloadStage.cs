namespace YtDlpGui.Abstractions.Enums;

/// <summary>Lifecycle stage of a single download job.</summary>
public enum DownloadStage
{
    Queued,
    Resolving,
    Downloading,
    Merging,
    Converting,
    Completed,
    Failed,
    Canceled
}
