namespace YtDlpGui.Abstractions.Enums;

/// <summary>Classified cause of a failed download, used for user-friendly messages and retry decisions.</summary>
public enum DownloadErrorKind
{
    None,
    ToolMissing,
    InvalidUrl,
    Network,
    DiskFull,
    PermissionDenied,
    FileExists,
    Interrupted,
    Unknown
}
