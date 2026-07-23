using System.ComponentModel;
using System.Text;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Localization;
using YtDlpGui.Abstractions.Models;
using YtDlpGui.Application.Jobs;
using YtDlpGui.Application.Queue;

namespace YtDlpGui.Application.Import;

/// <summary>
/// Orchestrates a library import end-to-end. Searching runs with bounded
/// parallelism; every confident match is enqueued into the existing
/// <see cref="IDownloadCoordinator"/> immediately, so downloads overlap with
/// searching and the pipeline scales to libraries with thousands of songs.
/// Failures never stop the run: unmatched songs are collected and written to
/// failed_songs.txt with a reason.
/// </summary>
public sealed class LibraryImportService(
    ILibraryParser parser,
    IMusicSearchService searchService,
    ISongMatchScorer scorer,
    IDownloadCoordinator coordinator,
    ILogSink log) : ILibraryImportService
{
    /// <summary>Parallel song searches. Each search spawns two short yt-dlp processes.</summary>
    private const int SearchParallelism = 3;

    private const string FailedListFileName = "failed_songs.txt";

    private int _isRunning;

    public bool IsRunning => Volatile.Read(ref _isRunning) == 1;

    public event EventHandler<ImportProgress>? ProgressChanged;

    public async Task<LibraryImportSummary> ImportAsync(
        LibraryImportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            throw new InvalidOperationException("A library import is already running.");
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

    private async Task<LibraryImportSummary> RunAsync(
        LibraryImportRequest request, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        // Phase 1: read and parse the exported library.
        Report(new ImportProgress(ImportPhase.Reading, 0, 0, 0, 0, 0, 0, null, MatchKind.None, 0));

        var songs = await Task.Run(
            () => parser.Parse(TextFileReader.ReadAllTextSmart(request.FilePath)), cancellationToken).ConfigureAwait(false);
        log.Write(LogLevel.Info, $"Library import: found {songs.Count} song(s) in {request.FilePath}.");

        if (songs.Count == 0)
        {
            Report(new ImportProgress(ImportPhase.Completed, 0, 0, 0, 0, 0, 0, null, MatchKind.None, 0));
            return new LibraryImportSummary(0, 0, 0, 0, 0, Elapsed(startedAt), null, false);
        }

        // Phase 2: search + match + enqueue, with bounded parallelism.
        var state = new ImportRunState(songs.Count);
        Report(SnapshotProgress(ImportPhase.Searching, state, null));

        try
        {
            await Parallel.ForEachAsync(
                songs,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = SearchParallelism,
                    CancellationToken = cancellationToken
                },
                async (song, ct) => await ProcessSongAsync(song, request, state, ct).ConfigureAwait(false))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            log.Write(LogLevel.Warning, "Library import canceled while searching.");
            var canceledPath = WriteFailedList(request.OutputFolder, state);
            Report(SnapshotProgress(ImportPhase.Canceled, state, null));
            return BuildSummary(state, startedAt, canceledPath, wasCanceled: true);
        }

        // Phase 3: everything matched is queued — wait for the queue to finish them.
        Report(SnapshotProgress(ImportPhase.Downloading, state, null));
        var wasCanceled = !await WaitForDownloadsAsync(state, cancellationToken).ConfigureAwait(false);

        var failedListPath = WriteFailedList(request.OutputFolder, state);
        Report(SnapshotProgress(wasCanceled ? ImportPhase.Canceled : ImportPhase.Completed, state, null));

        var summary = BuildSummary(state, startedAt, failedListPath, wasCanceled);
        log.Write(LogLevel.Info,
            $"Library import finished: {summary.SongsFound} songs, {summary.Downloaded} downloaded, " +
            $"{summary.Skipped} skipped, {summary.DownloadFailed} failed, {summary.Elapsed:hh\\:mm\\:ss}.");
        return summary;
    }

    private async Task ProcessSongAsync(
        LibrarySong song, LibraryImportRequest request, ImportRunState state, CancellationToken cancellationToken)
    {
        Report(SnapshotProgress(ImportPhase.Searching, state, song.DisplayName));

        IReadOnlyList<SearchCandidate> candidates;
        try
        {
            candidates = await searchService.SearchAsync(song, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // One song must never abort the whole library.
            log.Write(LogLevel.Warning, $"Search crashed for \"{song.DisplayName}\": {ex.Message}");
            candidates = [];
        }

        var match = scorer.ChooseBest(song, candidates);
        if (!match.HasCandidate)
        {
            state.AddSkipped(song, Loc.T(LocKeys.ImportReasonNoMatch));
            log.Write(LogLevel.Warning, $"No match found: {song.DisplayName}");
        }
        else if (match.Confidence < scorer.MinimumConfidence)
        {
            state.AddSkipped(song, Loc.T(LocKeys.ImportReasonLowConfidenceFormat, match.Confidence));
            log.Write(LogLevel.Warning,
                $"Confidence too low ({match.Confidence}%): {song.DisplayName} → {match.Candidate!.Title}");
        }
        else
        {
            EnqueueMatch(match, request, state);
        }

        state.MarkProcessed();
        Report(SnapshotProgress(ImportPhase.Searching, state, song.DisplayName, match));
    }

    private void EnqueueMatch(SongMatch match, LibraryImportRequest request, ImportRunState state)
    {
        var candidate = match.Candidate!;
        log.Write(LogLevel.Info,
            $"Matched ({match.Confidence}%, {match.Kind}): {match.Song.DisplayName} → {candidate.Title} [{candidate.Url}]");

        // Search-result URLs are plain watch links — playlists must stay off so
        // YouTube "mix" auto-playlists can never explode a single song.
        var job = coordinator.Enqueue(new DownloadRequest(
            candidate.Url,
            request.Format,
            request.Quality,
            request.OutputFolder,
            request.Options with { AllowPlaylists = false }));

        state.TrackDownload(job, match.Song, OnJobTerminal);
    }

    /// <summary>Called (once per job) when a tracked download reaches a terminal stage.</summary>
    private void OnJobTerminal(ImportRunState state, DownloadJob job, LibrarySong song)
    {
        if (job.Stage == DownloadStage.Completed)
        {
            state.MarkDownloadCompleted();
        }
        else
        {
            state.MarkDownloadFailed(song, Loc.T(LocKeys.ImportReasonDownloadFailed));
        }

        var phase = state.IsSearchingDone ? ImportPhase.Downloading : ImportPhase.Searching;
        Report(SnapshotProgress(phase, state, null));
    }

    /// <summary>Waits for all enqueued jobs; returns false when canceled early.</summary>
    private static async Task<bool> WaitForDownloadsAsync(ImportRunState state, CancellationToken cancellationToken)
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

    /// <summary>Writes skipped + failed songs with reasons; returns the path, or null when nothing failed.</summary>
    private string? WriteFailedList(string outputFolder, ImportRunState state)
    {
        var failures = state.GetFailures();
        if (failures.Count == 0)
        {
            return null;
        }

        var path = Path.Combine(outputFolder, FailedListFileName);
        try
        {
            Directory.CreateDirectory(outputFolder);
            var lines = failures.Select(f => $"{f.Song.DisplayName} — {f.Reason}");
            File.WriteAllLines(path, lines, Encoding.UTF8);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Write(LogLevel.Error, $"Could not write {FailedListFileName}: {ex.Message}");
            return null;
        }
    }

    private static LibraryImportSummary BuildSummary(
        ImportRunState state, DateTimeOffset startedAt, string? failedListPath, bool wasCanceled) =>
        new(
            state.SongsTotal,
            state.Matched,
            state.DownloadsCompleted,
            state.DownloadsFailed,
            state.Skipped,
            Elapsed(startedAt),
            failedListPath,
            wasCanceled);

    private static ImportProgress SnapshotProgress(
        ImportPhase phase, ImportRunState state, string? currentSong, SongMatch? lastMatch = null) =>
        new(
            phase,
            state.SongsTotal,
            state.SongsProcessed,
            state.Matched,
            state.Skipped,
            state.DownloadsCompleted,
            state.DownloadsFailed,
            currentSong,
            lastMatch?.Kind ?? MatchKind.None,
            lastMatch?.Confidence ?? 0);

    private void Report(ImportProgress progress) => ProgressChanged?.Invoke(this, progress);

    private static TimeSpan Elapsed(DateTimeOffset startedAt) => DateTimeOffset.Now - startedAt;

    /// <summary>Thread-safe mutable state of one import run.</summary>
    private sealed class ImportRunState(int songsTotal)
    {
        private readonly object _gate = new();
        private readonly List<(LibrarySong Song, string Reason)> _failures = [];
        private readonly List<Task> _downloadTasks = [];
        private int _processed;
        private int _matched;
        private int _skipped;
        private int _downloadsCompleted;
        private int _downloadsFailed;

        public int SongsTotal { get; } = songsTotal;

        public int SongsProcessed => Volatile.Read(ref _processed);

        public int Matched => Volatile.Read(ref _matched);

        public int Skipped => Volatile.Read(ref _skipped);

        public int DownloadsCompleted => Volatile.Read(ref _downloadsCompleted);

        public int DownloadsFailed => Volatile.Read(ref _downloadsFailed);

        public bool IsSearchingDone => SongsProcessed >= SongsTotal;

        public void MarkProcessed() => Interlocked.Increment(ref _processed);

        public void MarkDownloadCompleted() => Interlocked.Increment(ref _downloadsCompleted);

        public void AddSkipped(LibrarySong song, string reason)
        {
            Interlocked.Increment(ref _skipped);
            lock (_gate)
            {
                _failures.Add((song, reason));
            }
        }

        public void MarkDownloadFailed(LibrarySong song, string reason)
        {
            Interlocked.Increment(ref _downloadsFailed);
            lock (_gate)
            {
                _failures.Add((song, reason));
            }
        }

        /// <summary>Subscribes to the job's stage and completes its tracking task on the first terminal state.</summary>
        public void TrackDownload(
            DownloadJob job, LibrarySong song, Action<ImportRunState, DownloadJob, LibrarySong> onTerminal)
        {
            Interlocked.Increment(ref _matched);
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
                    onTerminal(this, job, song);
                }
            }

            job.PropertyChanged += Handler;

            // The job may have finished between Enqueue and the subscription.
            if (job.IsTerminal)
            {
                job.PropertyChanged -= Handler;
                if (tcs.TrySetResult())
                {
                    onTerminal(this, job, song);
                }
            }
        }

        public Task WhenAllDownloads()
        {
            lock (_gate)
            {
                return Task.WhenAll(_downloadTasks);
            }
        }

        public IReadOnlyList<(LibrarySong Song, string Reason)> GetFailures()
        {
            lock (_gate)
            {
                return [.. _failures];
            }
        }
    }
}
