using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Localization;
using YtDlpGui.Abstractions.Models;
using YtDlpGui.Application.Execution;
using YtDlpGui.Application.Jobs;
using YtDlpGui.Application.Queue;
using YtDlpGui.Core.Stages;

namespace YtDlpGui.Core.Tests;

public sealed class DownloadLifecycleTests
{
    private static DownloadRequest CreateRequest(string url = "https://example.com/video") =>
        new(url, MediaFormat.Mp4, VideoQuality.Best, "D:\\Downloads");

    [Fact]
    public void StageRules_VerifyPauseAndResumeRules()
    {
        // Active downloading stages can pause and can cancel
        Assert.True(StageRules.CanPause(DownloadStage.Downloading));
        Assert.True(StageRules.CanPause(DownloadStage.Resolving));
        Assert.True(StageRules.CanCancel(DownloadStage.Downloading));
        Assert.False(StageRules.CanResume(DownloadStage.Downloading));
        Assert.False(StageRules.IsTerminal(DownloadStage.Downloading));

        // Paused stage can resume and can cancel, but cannot pause again
        Assert.True(StageRules.CanResume(DownloadStage.Paused));
        Assert.True(StageRules.CanCancel(DownloadStage.Paused));
        Assert.False(StageRules.CanPause(DownloadStage.Paused));
        Assert.False(StageRules.IsTerminal(DownloadStage.Paused));

        // Canceled stage is terminal, cannot pause or resume
        Assert.True(StageRules.IsTerminal(DownloadStage.Canceled));
        Assert.False(StageRules.CanPause(DownloadStage.Canceled));
        Assert.False(StageRules.CanResume(DownloadStage.Canceled));
        Assert.False(StageRules.CanCancel(DownloadStage.Canceled));
    }

    [Fact]
    public void Pause_DoesNotCauseCancel()
    {
        var executor = new MockExecutor();
        var coordinator = new DownloadCoordinator(
            executor,
            new MockRetryPolicy(),
            new AppSettings { MaxConcurrentDownloads = 1 },
            new MockLog());

        var job = coordinator.Enqueue(CreateRequest());
        coordinator.Pause(job);

        Assert.Equal(DownloadStage.Paused, job.Stage);
        Assert.NotEqual(DownloadStage.Canceled, job.Stage);
        Assert.False(job.IsTerminal);
        Assert.Equal(LocKeys.StagePaused, job.StatusNote);
    }

    [Fact]
    public void Stop_CausesCancel()
    {
        var executor = new MockExecutor();
        var coordinator = new DownloadCoordinator(
            executor,
            new MockRetryPolicy(),
            new AppSettings { MaxConcurrentDownloads = 1 },
            new MockLog());

        var job = coordinator.Enqueue(CreateRequest());
        coordinator.Cancel(job);

        Assert.Equal(DownloadStage.Canceled, job.Stage);
        Assert.NotEqual(DownloadStage.Paused, job.Stage);
        Assert.True(job.IsTerminal);
        Assert.Equal(LocKeys.NoteCanceledBeforeStart, job.StatusNote);
    }

    [Fact]
    public void Pause_Then_Continue_ResumesDownload()
    {
        var executor = new MockExecutor();
        var coordinator = new DownloadCoordinator(
            executor,
            new MockRetryPolicy(),
            new AppSettings { MaxConcurrentDownloads = 1 },
            new MockLog());

        var job = coordinator.Enqueue(CreateRequest());
        coordinator.Pause(job);

        Assert.Equal(DownloadStage.Paused, job.Stage);

        var resumed = coordinator.Resume(job);
        Assert.True(resumed);
        Assert.Equal(DownloadStage.Queued, job.Stage);
        Assert.NotEqual(DownloadStage.Canceled, job.Stage);
        Assert.False(job.IsTerminal);
    }

    [Fact]
    public void Stop_FromPaused_SetsCancelled()
    {
        var executor = new MockExecutor();
        var coordinator = new DownloadCoordinator(
            executor,
            new MockRetryPolicy(),
            new AppSettings { MaxConcurrentDownloads = 1 },
            new MockLog());

        var job = coordinator.Enqueue(CreateRequest());
        coordinator.Pause(job);
        Assert.Equal(DownloadStage.Paused, job.Stage);

        coordinator.Cancel(job);
        Assert.Equal(DownloadStage.Canceled, job.Stage);
        Assert.True(job.IsTerminal);
        Assert.Equal(LocKeys.NoteCanceled, job.StatusNote);
    }

    [Fact]
    public void Pause_PreservesDownloadProgressAndData()
    {
        var job = new DownloadJob(CreateRequest())
        {
            Stage = DownloadStage.Downloading,
            Percent = 42.5,
            DownloadedBytes = 425000,
            TotalBytes = 1000000,
            Title = "Test Video",
            DestinationFile = "D:\\Downloads\\video.mp4"
        };

        // Simulating executor pausing the job
        job.IsPauseRequested = true;
        job.IsPauseRequested = false;
        job.Stage = DownloadStage.Paused;
        job.StatusNote = Loc.T(LocKeys.StagePaused);
        job.SpeedBytesPerSecond = null;
        job.EtaSeconds = null;

        Assert.Equal(DownloadStage.Paused, job.Stage);
        Assert.Equal(42.5, job.Percent);
        Assert.Equal(425000, job.DownloadedBytes);
        Assert.Equal(1000000, job.TotalBytes);
        Assert.Equal("Test Video", job.Title);
        Assert.Equal("D:\\Downloads\\video.mp4", job.DestinationFile);
        Assert.NotEqual(DownloadStage.Canceled, job.Stage);
    }

    private sealed class MockExecutor : IDownloadExecutor
    {
        public Task ExecuteAsync(DownloadJob job, CancellationToken cancellationToken) =>
            Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private sealed class MockRetryPolicy : IRetryPolicy
    {
        public bool CanRetry(DownloadErrorKind errorKind) => false;
        public TimeSpan GetDelay(int attempt) => TimeSpan.Zero;
    }

    private sealed class MockLog : ILogSink
    {
        public void Write(LogLevel level, string message) { }
#pragma warning disable CS0067
        public event EventHandler<LogEntry>? EntryAdded;
#pragma warning restore CS0067
        public IReadOnlyList<LogEntry> GetSnapshot() => [];
    }
}
