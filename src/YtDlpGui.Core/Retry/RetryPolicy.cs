using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;

namespace YtDlpGui.Core.Retry;

/// <summary>
/// Manual (user-initiated) retry policy. Permissive by design: the user may have fixed
/// the underlying condition (freed disk space, restored network, installed tools).
/// Only inherently unfixable failures are excluded.
/// </summary>
public sealed class RetryPolicy : IRetryPolicy
{
    public bool CanRetry(DownloadErrorKind kind) => kind switch
    {
        DownloadErrorKind.InvalidUrl => false,
        DownloadErrorKind.None => false,
        _ => true
    };
}
