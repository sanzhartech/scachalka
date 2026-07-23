using YtDlpGui.Abstractions.Enums;

namespace YtDlpGui.Abstractions.Interfaces;

/// <summary>Decides whether a failed download may be retried by the user.</summary>
public interface IRetryPolicy
{
    bool CanRetry(DownloadErrorKind kind);
}
