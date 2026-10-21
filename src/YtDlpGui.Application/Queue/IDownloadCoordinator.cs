using YtDlpGui.Abstractions.Models;
using YtDlpGui.Application.Jobs;

namespace YtDlpGui.Application.Queue;

/// <summary>Owns the download queue: accepts requests, runs workers, cancels and retries jobs.</summary>
public interface IDownloadCoordinator : IAsyncDisposable
{
    /// <summary>Raised on the enqueueing thread; the UI marshals to its dispatcher.</summary>
    event EventHandler<DownloadJob>? JobEnqueued;

    DownloadJob Enqueue(DownloadRequest request);

    void Cancel(DownloadJob job);

    void Pause(DownloadJob job);

    /// <returns>False when the job is not in a paused state.</returns>
    bool Resume(DownloadJob job);

    /// <returns>False when the job is not in a retryable state.</returns>
    bool Retry(DownloadJob job);
}
