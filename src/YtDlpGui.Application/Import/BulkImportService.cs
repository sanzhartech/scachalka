using System.ComponentModel;
using System.Text;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Localization;
using YtDlpGui.Abstractions.Models;
using YtDlpGui.Application.Jobs;
using YtDlpGui.Application.Queue;
using YtDlpGui.Core.Links;

namespace YtDlpGui.Application.Import;

/// <summary>
/// Orchestrates one bulk link import end-to-end. All heavy stages (file read,
/// extraction, validation) run off the caller's thread, so the UI stays
/// responsive with 10k+ links. Valid links are enqueued into the existing
/// <see cref="IDownloadCoordinator"/> in file order; rejected ones are written
/// to failed_links.txt with a reason. A failure of one link never stops the run.
/// </summary>
public sealed class BulkImportService(
    IEnumerable<ILinkSource> linkSources,
    BulkLinkAnalyzer analyzer,
    IDownloadCoordinator coordinator,
    ILogSink log) : IBulkImportService
{
    private const string FailedListFileName = "failed_links.txt";

    /// <summary>Progress push frequency while queueing large batches.</summary>
    private const int QueueProgressStep = 200;

    private int _isRunning;

    public bool IsRunning => Volatile.Read(ref _isRunning) == 1;

    public event EventHandler<BulkImportProgress>? ProgressChanged;

    public async Task<BulkImportSummary> ImportAsync(
        BulkImportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            throw new InvalidOperationException("A bulk import is already running.");
        }

        var startedAt = DateTimeOffset.Now;
        try
        {
            return await RunAsync(request, startedAt, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _isRunning, 0);
        }
    }

    private async Task<BulkImportSummary> RunAsync(
        BulkImportRequest request, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        BulkRunState? state = null;
        string? failedListPath = null;

        try
        {
            // Phase 1: read the file and detect its format.
            Report(new BulkImportProgress(BulkImportPhase.Reading, 0, 0, 0, 0, 0, 0, 0));
            var content = await Task.Run(
                () => TextFileReader.ReadAllTextSmart(request.FilePath), cancellationToken).ConfigureAwait(false);

            var fileName = Path.GetFileName(request.FilePath);
            var source = linkSources.FirstOrDefault(s => s.CanExtract(fileName, content));
            var links = source is null
                ? []
                : await Task.Run(() => source.Extract(content), cancellationToken).ConfigureAwait(false);
            log.Write(LogLevel.Info,
                $"Bulk import: {fileName} detected as {source?.FormatName ?? "unknown"}, {links.Count} entr(ies).");

            // Phase 2: validate + dedupe.
            Report(new BulkImportProgress(BulkImportPhase.Validating, links.Count, 0, 0, 0, 0, 0, 0));
            var analysis = await Task.Run(() => analyzer.Analyze(links), cancellationToken).ConfigureAwait(false);
            log.Write(LogLevel.Info,
                $"Bulk import: {analysis.ValidUrls.Count} valid, {analysis.Duplicates} duplicate(s), " +
                $"{analysis.InvalidTotal} invalid.");

            state = new BulkRunState(links.Count, analysis, OnDownloadTick);
            failedListPath = WriteFailedList(request.OutputFolder, analysis);

            // Phase 3: enqueue everything valid, in file order.
            Report(Snapshot(BulkImportPhase.Queueing, state));
            await Task.Run(() => EnqueueAll(request, analysis, state, cancellationToken), cancellationToken)
                .ConfigureAwait(false);

            // Phase 4: the existing queue downloads; we only watch it finish.
            Report(Snapshot(BulkImportPhase.Downloading, state));
            var finished = await WaitForDownloadsAsync(state, cancellationToken).ConfigureAwait(false);

            Report(Snapshot(finished ? BulkImportPhase.Completed : BulkImportPhase.Canceled, state));
            var summary = BuildSummary(state, startedAt, failedListPath, wasCanceled: !finished);
            log.Write(LogLevel.Info,
                $"Bulk import finished: {summary.Imported} imported, {summary.Queued} queued, " +
                $"{summary.Downloaded} downloaded, {summary.DownloadFailed} failed, {summary.Elapsed:hh\\:mm\\:ss}.");
            return summary;
        }
        catch (OperationCanceledException)
        {
            log.Write(LogLevel.Warning, "Bulk import canceled.");
            if (state is not null)
            {
                Report(Snapshot(BulkImportPhase.Canceled, state));
                return BuildSummary(state, startedAt, failedListPath, wasCanceled: true);
            }

            Report(new BulkImportProgress(BulkImportPhase.Canceled, 0, 0, 0, 0, 0, 0, 0));
            return new BulkImportSummary(0, 0, 0, 0, 0, 0, Elapsed(startedAt), null, true);
        }
    }

    private void EnqueueAll(
        BulkImportRequest request, BulkLinkAnalysis analysis, BulkRunState state, CancellationToken cancellationToken)
    {
        foreach (var url in analysis.ValidUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var job = coordinator.Enqueue(new DownloadRequest(
                url, request.Format, request.Quality, request.OutputFolder, request.Options));
            state.TrackDownload(job);

            if (state.Queued % QueueProgressStep == 0)
            {
                Report(Snapshot(BulkImportPhase.Queueing, state));
            }
        }
    }

    /// <summary>Called (once per job) from worker threads when a tracked download turns terminal.</summary>
    private void OnDownloadTick(BulkRunState state)
    {
        var phase = state.IsQueueingDone ? BulkImportPhase.Downloading : BulkImportPhase.Queueing;
        Report(Snapshot(phase, state));
    }

    /// <summary>Waits for all enqueued jobs; returns false when canceled early.</summary>
    private static async Task<bool> WaitForDownloadsAsync(BulkRunState state, CancellationToken cancellationToken)
    {
        try
        {
            await state.WhenAllDownloads().WaitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Writes every rejected link with its reason; returns the path, or null when nothing was rejected.</summary>
    private string? WriteFailedList(string outputFolder, BulkLinkAnalysis analysis)
    {
        if (analysis.Rejected.Count == 0)
        {
            return null;
        }

        var path = Path.Combine(outputFolder, FailedListFileName);
        try
        {
            Directory.CreateDirectory(outputFolder);
            var lines = analysis.Rejected.Select(r =>
                $"{r.Link.Origin}: {r.Link.Url ?? "—"} — {ReasonText(r.Reason)}");
            File.WriteAllLines(path, lines, Encoding.UTF8);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Write(LogLevel.Error, $"Could not write {FailedListFileName}: {ex.Message}");
            return null;
        }
    }

    private static string ReasonText(LinkRejectReason reason) => reason switch
    {
        LinkRejectReason.MissingUrl => Loc.T(LocKeys.BulkReasonMissing),
        LinkRejectReason.InvalidUrl => Loc.T(LocKeys.BulkReasonInvalid),
        LinkRejectReason.UnsupportedDomain => Loc.T(LocKeys.BulkReasonUnsupportedDomain),
        _ => Loc.T(LocKeys.BulkReasonDuplicate)
    };

    private static BulkImportSummary BuildSummary(
        BulkRunState state, DateTimeOffset startedAt, string? failedListPath, bool wasCanceled) =>
        new(
            state.Rows,
            state.Queued,
            state.Duplicates,
            state.Invalid,
            state.DownloadsCompleted,
            state.DownloadsFailed,
            Elapsed(startedAt),
            failedListPath,
            wasCanceled);

    private static BulkImportProgress Snapshot(BulkImportPhase phase, BulkRunState state) =>
        new(
            phase,
            state.Rows,
            state.Valid,
            state.Invalid,
            state.Duplicates,
            state.Queued,
            state.DownloadsCompleted,
            state.DownloadsFailed);

    private void Report(BulkImportProgress progress) => ProgressChanged?.Invoke(this, progress);

    private static TimeSpan Elapsed(DateTimeOffset startedAt) => DateTimeOffset.Now - startedAt;

    /// <summary>Thread-safe mutable state of one bulk import run.</summary>
    private sealed class BulkRunState(int rows, BulkLinkAnalysis analysis, Action<BulkRunState> onDownloadTick)
    {
        private readonly object _gate = new();
        private readonly List<Task> _downloadTasks = [];
        private int _queued;
        private int _downloadsCompleted;
        private int _downloadsFailed;

        public int Rows { get; } = rows;

        public int Valid { get; } = analysis.ValidUrls.Count;

        public int Invalid { get; } = analysis.InvalidTotal;

        public int Duplicates { get; } = analysis.Duplicates;

        public int Queued => Volatile.Read(ref _queued);

        public int DownloadsCompleted => Volatile.Read(ref _downloadsCompleted);

        public int DownloadsFailed => Volatile.Read(ref _downloadsFailed);

        public bool IsQueueingDone => Queued >= Valid;

        /// <summary>Subscribes to the job's stage and counts its first terminal state exactly once.</summary>
        public void TrackDownload(DownloadJob job)
        {
            Interlocked.Increment(ref _queued);
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
            {
                _downloadTasks.Add(tcs.Task);
            }

            void Handler(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName != nameof(DownloadJob.Stage) || !job.IsTerminal)
                {
                    return;
                }

                job.PropertyChanged -= Handler;
                if (tcs.TrySetResult())
                {
                    OnTerminal(job);
                }
            }

            job.PropertyChanged += Handler;

            // The job may have finished between Enqueue and the subscription.
            if (job.IsTerminal)
            {
                job.PropertyChanged -= Handler;
                if (tcs.TrySetResult())
                {
                    OnTerminal(job);
                }
            }
        }

        private void OnTerminal(DownloadJob job)
        {
            if (job.Stage == DownloadStage.Completed)
            {
                Interlocked.Increment(ref _downloadsCompleted);
            }
            else
            {
                Interlocked.Increment(ref _downloadsFailed);
            }

            onDownloadTick(this);
        }

        public Task WhenAllDownloads()
        {
            lock (_gate)
            {
                return Task.WhenAll(_downloadTasks);
            }
        }
    }
}
