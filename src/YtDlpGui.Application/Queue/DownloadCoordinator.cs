using System.Collections.Concurrent;
using System.Threading.Channels;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Localization;
using YtDlpGui.Abstractions.Models;
using YtDlpGui.Application.Execution;
using YtDlpGui.Application.Jobs;

namespace YtDlpGui.Application.Queue;

/// <summary>
/// Bounded-concurrency download queue built on a Channel:
/// N worker loops pull queued jobs, each job runs under its own linked CancellationTokenSource,
/// so one job's failure or cancellation can never affect the others.
/// Shutdown cancels everything and waits for workers to drain.
/// </summary>
public sealed class DownloadCoordinator : IDownloadCoordinator
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(12);

    private readonly IDownloadExecutor _executor;
    private readonly IRetryPolicy _retryPolicy;
    private readonly ILogSink _log;
    private readonly Channel<DownloadJob> _pending;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _running = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task[] _workers;
    private bool _disposed;

    public event EventHandler<DownloadJob>? JobEnqueued;

    public DownloadCoordinator(
        IDownloadExecutor executor,
        IRetryPolicy retryPolicy,
        AppSettings settings,
        ILogSink log)
    {
        _executor = executor;
        _retryPolicy = retryPolicy;
        _log = log;
        _pending = Channel.CreateUnbounded<DownloadJob>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        var workerCount = Math.Clamp(settings.MaxConcurrentDownloads, 1, 4);
        _workers = new Task[workerCount];
        for (var i = 0; i < workerCount; i++)
        {
            _workers[i] = Task.Run(WorkerLoopAsync);
        }

        _log.Write(LogLevel.Debug, $"Download queue started with {workerCount} parallel worker(s).");
    }

    public DownloadJob Enqueue(DownloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var job = new DownloadJob(request);
        JobEnqueued?.Invoke(this, job);

        if (!_pending.Writer.TryWrite(job))
        {
            // Only possible when the writer is completed during shutdown.
            job.Stage = DownloadStage.Canceled;
        }

        return job;
    }

    public void Cancel(DownloadJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (_running.TryGetValue(job.Id, out var cts))
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The job finished between the lookup and the cancel — nothing to do.
            }

            return;
        }

        // Still waiting in the channel: mark canceled, workers skip non-queued jobs.
        if (job.Stage == DownloadStage.Queued)
        {
            job.Stage = DownloadStage.Canceled;
            job.StatusNote = Loc.T(LocKeys.NoteCanceledBeforeStart);
        }
    }

    public bool Retry(DownloadJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!job.CanRetry)
        {
            return false;
        }

        if (job.Stage == DownloadStage.Failed && !_retryPolicy.CanRetry(job.ErrorKind))
        {
            _log.Write(LogLevel.Warning, $"Retry rejected for {job.Url}: {job.ErrorKind} is not retryable.");
            return false;
        }

        job.ResetForRetry();
        return _pending.Writer.TryWrite(job);
    }

    private async Task WorkerLoopAsync()
    {
        try
        {
            await foreach (var job in _pending.Reader.ReadAllAsync(_shutdown.Token).ConfigureAwait(false))
            {
                if (job.Stage != DownloadStage.Queued)
                {
                    continue; // Canceled while waiting in the queue.
                }

                using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                _running[job.Id] = jobCts;
                try
                {
                    await _executor.ExecuteAsync(job, jobCts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // The executor handles its own errors; this is a last-resort guard
                    // so a single job can never kill the worker loop.
                    job.Stage = DownloadStage.Failed;
                    job.ErrorMessage = "Internal error — see the log for details.";
                    _log.Write(LogLevel.Error, $"Worker caught unexpected error for {job.Url}: {ex}");
                }
                finally
                {
                    _running.TryRemove(job.Id, out CancellationTokenSource? _);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pending.Writer.TryComplete();

        foreach (var cts in _running.Values)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Job completed concurrently.
            }
        }

        _shutdown.Cancel();

        try
        {
            await Task.WhenAll(_workers).WaitAsync(ShutdownTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _log.Write(LogLevel.Warning, "Workers did not drain within the shutdown timeout.");
        }

        _shutdown.Dispose();
    }
}
