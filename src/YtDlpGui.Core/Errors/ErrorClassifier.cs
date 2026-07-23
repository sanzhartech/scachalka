using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Localization;

namespace YtDlpGui.Core.Errors;

/// <summary>
/// Maps raw yt-dlp/OS failure output onto typed error kinds and translates them
/// into messages a non-technical user can act on.
/// </summary>
public sealed class ErrorClassifier : IErrorClassifier
{
    // Ordered: the first matching category wins. Patterns cover yt-dlp messages,
    // Python errno text and localized-agnostic OS error fragments.
    private static readonly (DownloadErrorKind Kind, string[] Patterns)[] Rules =
    [
        // Must be first: a running browser locks its cookie DB (yt-dlp issue #7271);
        // the same stderr often also contains generic "Permission denied" fragments.
        (DownloadErrorKind.CookieBrowserLocked,
            ["cookie database", "Failed to decrypt with DPAPI", "Failed to read extraction keys"]),
        (DownloadErrorKind.InvalidUrl,
            ["Unsupported URL", "is not a valid URL", "Incomplete YouTube ID", "URL could be a direct video link"]),
        (DownloadErrorKind.DiskFull,
            ["No space left on device", "There is not enough space", "[Errno 28]", "disk full", "not enough space on the disk"]),
        (DownloadErrorKind.PermissionDenied,
            ["Permission denied", "[Errno 13]", "Access is denied", "being used by another process"]),
        (DownloadErrorKind.Network,
            ["Unable to download", "Connection re", "timed out", "Temporary failure in name resolution",
             "getaddrinfo failed", "HTTP Error 5", "HTTP Error 429", "Network is unreachable",
             "Unable to connect", "SSL:", "Remote end closed connection", "IncompleteRead"]),
        (DownloadErrorKind.FileExists,
            ["has already been downloaded"])
    ];

    public DownloadErrorKind Classify(int exitCode, string stderrTail)
    {
        if (exitCode == 0)
        {
            return DownloadErrorKind.None;
        }

        stderrTail ??= string.Empty;
        foreach (var (kind, patterns) in Rules)
        {
            foreach (var pattern in patterns)
            {
                if (stderrTail.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return kind;
                }
            }
        }

        return DownloadErrorKind.Unknown;
    }

    public string GetUserMessage(DownloadErrorKind kind, string? detail)
    {
        if (kind == DownloadErrorKind.None)
        {
            return string.Empty;
        }

        var key = kind switch
        {
            DownloadErrorKind.ToolMissing => LocKeys.ErrorToolMissing,
            DownloadErrorKind.InvalidUrl => LocKeys.ErrorInvalidUrl,
            DownloadErrorKind.Network => LocKeys.ErrorNetwork,
            DownloadErrorKind.DiskFull => LocKeys.ErrorDiskFull,
            DownloadErrorKind.PermissionDenied => LocKeys.ErrorPermission,
            DownloadErrorKind.FileExists => LocKeys.ErrorFileExists,
            DownloadErrorKind.Interrupted => LocKeys.ErrorInterrupted,
            DownloadErrorKind.CookieBrowserLocked => LocKeys.ErrorCookieLocked,
            _ => LocKeys.ErrorUnknown
        };

        var message = Loc.T(key);
        return string.IsNullOrWhiteSpace(detail) ? message : $"{message} ({Truncate(detail, 220)})";
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
