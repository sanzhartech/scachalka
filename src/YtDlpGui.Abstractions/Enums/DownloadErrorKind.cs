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

    /// <summary>The browser is running and locks its cookie database (--cookies-from-browser failed).</summary>
    CookieBrowserLocked,
    Unknown
}
