using System.Collections.Concurrent;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Models;
using YtDlpGui.Application.Execution;
using YtDlpGui.Application.Import;
using YtDlpGui.Application.Jobs;
using YtDlpGui.Application.Queue;
using YtDlpGui.Application.Tools;
using YtDlpGui.Core.Validation;
using YtDlpGui.Infrastructure.Localization;
using YtDlpGui.UI.Services;
using YtDlpGui.UI.ViewModels;

namespace YtDlpGui.Core.Tests;

public sealed class ButtonCommandTests
{
    private static DownloadRequest CreateRequest(string url = "https://example.com/video") =>
        new(url, MediaFormat.Mp4, VideoQuality.Best, "D:\\Downloads");

    private sealed class MockClipboard : IClipboardService
    {
        public string? Content { get; set; }
        public string? GetText() => Content;
        public void SetText(string text) => Content = text;
    }

    private sealed class MockFolderService : IFolderService
    {
        public string? LastOpenedFolder { get; private set; }
        public string? LastRevealedFile { get; private set; }
        public bool OpenFolderResult { get; set; } = true;
        public bool RevealFileResult { get; set; } = true;

        public bool OpenFolder(string folderPath)
        {
            LastOpenedFolder = folderPath;
            return OpenFolderResult;
        }

        public bool RevealFile(string filePath)
        {
            LastRevealedFile = filePath;
            return RevealFileResult;
        }
    }

    private sealed class MockSettingsStore : ISettingsStore
    {
        public AppSettings SavedSettings { get; private set; } = new();
        public AppSettings Load() => SavedSettings;
        public Task SaveAsync(AppSettings settings)
        {
            SavedSettings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class MockLog : ILogSink
    {
        public void Write(LogLevel level, string message) { }
#pragma warning disable CS0067
        public event EventHandler<LogEntry>? EntryAdded;
#pragma warning restore CS0067
        public IReadOnlyList<LogEntry> GetSnapshot() => [];
    }

    private sealed class MockToolLocator : IToolLocator
    {
        public Task<ToolLocation> LocateAsync(CancellationToken ct) =>
            Task.FromResult(new ToolLocation("yt-dlp.exe", "2026.01.01", "ffmpeg.exe", "7.1"));
    }

    private sealed class MockToolUpdater : IToolUpdater
    {
        public Task<ToolUpdateResult> UpdateAsync(ToolLocation current, CancellationToken ct) =>
            Task.FromResult(ToolUpdateResult.UpToDate("2026.01.01", "Up to date"));
    }

    private sealed class MockImportService : ILibraryImportService
    {
        public bool IsRunning => false;
#pragma warning disable CS0067
        public event EventHandler<ImportProgress>? ProgressChanged;
#pragma warning restore CS0067
        public Task<LibraryImportSummary> ImportAsync(LibraryImportRequest request, CancellationToken ct) =>
            Task.FromResult(new LibraryImportSummary(0, 0, 0, 0, 0, TimeSpan.Zero, null, false));
    }

    private sealed class MockBulkImportService : IBulkImportService
    {
        public bool IsRunning => false;
#pragma warning disable CS0067
        public event EventHandler<BulkImportProgress>? ProgressChanged;
#pragma warning restore CS0067
        public Task<BulkImportSummary> ImportAsync(BulkImportRequest request, CancellationToken ct) =>
            Task.FromResult(new BulkImportSummary(0, 0, 0, 0, 0, 0, TimeSpan.Zero, null, false));
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

    private static (MainViewModel vm, MockFolderService folderService, MockClipboard clipboard, DownloadCoordinator coordinator) CreateContext()
    {
        var log = new MockLog();
        var coordinator = new DownloadCoordinator(
            new MockExecutor(),
            new MockRetryPolicy(),
            new AppSettings { MaxConcurrentDownloads = 1 },
            log);

        var folderService = new MockFolderService();
        var clipboard = new MockClipboard();
        var settingsStore = new MockSettingsStore();
        var localizer = new Localizer();

        var vm = new MainViewModel(
            new UrlValidator(),
            coordinator,
            settingsStore,
            new AppSettings(),
            new MockToolLocator(),
            new MockToolUpdater(),
            new ToolContext(),
            log,
            folderService,
            clipboard,
            localizer,
            new MockImportService(),
            new MockBulkImportService());

        return (vm, folderService, clipboard, coordinator);
    }

    [Fact]
    public void CopyJobUrlCommand_CopiesUrlToClipboard()
    {
        var (vm, _, clipboard, coordinator) = CreateContext();
        var job = coordinator.Enqueue(CreateRequest("https://www.youtube.com/watch?v=dQw4w9WgXcQ"));

        vm.CopyJobUrlCommand.Execute(job);

        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", clipboard.Content);
        Assert.Contains("буфер", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RemoveJobCommand_RemovesJobFromList_DoesNotDeleteFile()
    {
        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            var (vm, _, _, coordinator) = CreateContext();
            var job = coordinator.Enqueue(CreateRequest());
            job.DestinationFile = tempFile;

            Assert.Single(vm.Jobs);

            vm.RemoveJobCommand.Execute(job);

            Assert.Empty(vm.Jobs);
            Assert.True(File.Exists(tempFile), "File should not be deleted when using RemoveJob / DeleteJob");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void DeleteJobFileCommand_WhenConfirmed_DeletesFileAndRemovesJob()
    {
        var tempFile = System.IO.Path.GetTempFileName();
        var tempPart = tempFile + ".part";
        File.WriteAllText(tempPart, "partial");

        try
        {
            var (vm, _, _, coordinator) = CreateContext();
            var job = coordinator.Enqueue(CreateRequest());
            job.DestinationFile = tempFile;

            // Simulate user confirming deletion
            vm.ConfirmDeleteAction = _ => true;

            vm.DeleteJobFileCommand.Execute(job);

            Assert.Empty(vm.Jobs);
            Assert.False(File.Exists(tempFile), "File should be deleted on confirmed DeleteJobFile");
            Assert.False(File.Exists(tempPart), "Part file should also be deleted");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(tempPart)) File.Delete(tempPart);
        }
    }

    [Fact]
    public void DeleteJobFileCommand_WhenCancelled_PreservesFileAndJob()
    {
        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            var (vm, _, _, coordinator) = CreateContext();
            var job = coordinator.Enqueue(CreateRequest());
            job.DestinationFile = tempFile;

            // Simulate user clicking Cancel on dialog
            vm.ConfirmDeleteAction = _ => false;

            vm.DeleteJobFileCommand.Execute(job);

            Assert.Single(vm.Jobs);
            Assert.True(File.Exists(tempFile), "File must be preserved when dialog is cancelled");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void DeleteJobFileCommand_WhenFileAlreadyMissing_GracefullyRemovesJob()
    {
        var missingPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".mp4");
        var (vm, _, _, coordinator) = CreateContext();
        var job = coordinator.Enqueue(CreateRequest());
        job.DestinationFile = missingPath;

        vm.ConfirmDeleteAction = _ => true;

        // Should not throw
        vm.DeleteJobFileCommand.Execute(job);

        Assert.Empty(vm.Jobs);
    }

    [Fact]
    public void ClearCompletedCommand_RemovesCompletedJobs_PreservesUnfinished()
    {
        var (vm, _, _, coordinator) = CreateContext();
        var jobCompleted = coordinator.Enqueue(CreateRequest("https://example.com/1"));
        jobCompleted.Stage = DownloadStage.Completed;

        var jobCanceled = coordinator.Enqueue(CreateRequest("https://example.com/2"));
        jobCanceled.Stage = DownloadStage.Canceled;

        var jobQueued = coordinator.Enqueue(CreateRequest("https://example.com/3"));
        jobQueued.Stage = DownloadStage.Queued;

        var jobDownloading = coordinator.Enqueue(CreateRequest("https://example.com/4"));
        jobDownloading.Stage = DownloadStage.Downloading;

        var jobPaused = coordinator.Enqueue(CreateRequest("https://example.com/5"));
        jobPaused.Stage = DownloadStage.Paused;

        Assert.Equal(5, vm.Jobs.Count);

        vm.ClearCompletedCommand.Execute(null);

        Assert.Equal(3, vm.Jobs.Count);
        Assert.Contains(jobQueued, vm.Jobs);
        Assert.Contains(jobDownloading, vm.Jobs);
        Assert.Contains(jobPaused, vm.Jobs);
        Assert.DoesNotContain(jobCompleted, vm.Jobs);
        Assert.DoesNotContain(jobCanceled, vm.Jobs);
    }

    [Fact]
    public void PlayJobFileCommand_ExistingFile_OpensFile()
    {
        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            var (vm, _, _, coordinator) = CreateContext();
            var job = coordinator.Enqueue(CreateRequest());
            job.DestinationFile = tempFile;

            string? openedPath = null;
            vm.OpenFileAction = path => openedPath = path;

            vm.PlayJobFileCommand.Execute(job);

            Assert.NotNull(openedPath);
            Assert.Equal(System.IO.Path.GetFullPath(tempFile), openedPath);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void PlayJobFileCommand_MissingFile_ShowsError_DoesNotOpenFolder()
    {
        var missingPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".mp4");
        var (vm, folderService, _, coordinator) = CreateContext();
        var job = coordinator.Enqueue(CreateRequest());
        job.DestinationFile = missingPath;

        string? openedPath = null;
        vm.OpenFileAction = path => openedPath = path;

        vm.PlayJobFileCommand.Execute(job);

        Assert.Null(openedPath);
        Assert.Null(folderService.LastOpenedFolder);
        Assert.Null(folderService.LastRevealedFile);
        Assert.Contains("не найден", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShowJobFolderCommand_ExistingFile_CallsRevealFile()
    {
        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            var (vm, folderService, _, coordinator) = CreateContext();
            var job = coordinator.Enqueue(CreateRequest());
            job.DestinationFile = tempFile;

            vm.ShowJobFolderCommand.Execute(job);

            Assert.NotNull(folderService.LastRevealedFile);
            Assert.Equal(System.IO.Path.GetFullPath(tempFile), folderService.LastRevealedFile);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ShowJobFolderCommand_MissingFile_FallsBackToFolderAndNotifies()
    {
        var tempDir = System.IO.Path.GetTempPath();
        var missingPath = System.IO.Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".mp4");

        var (vm, folderService, _, coordinator) = CreateContext();
        var job = coordinator.Enqueue(CreateRequest());
        job.DestinationFile = missingPath;

        vm.ShowJobFolderCommand.Execute(job);

        Assert.Null(folderService.LastRevealedFile);
        Assert.NotNull(folderService.LastOpenedFolder);
        Assert.Contains("не найден", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BrowseFolderCommand_UpdatesOutputFolder()
    {
        var (vm, _, _, _) = CreateContext();
        var targetFolder = "D:\\Desktop\\MySelectedFolder";

        vm.PickFolderAction = _ => targetFolder;

        vm.BrowseFolderCommand.Execute(null);

        Assert.Equal(targetFolder, vm.OutputFolder);
    }

    [Fact]
    public void OpenFolderCommand_CreatesDirectoryIfMissingAndOpens()
    {
        var tempTarget = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Scachalka_OpenFolder_" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, folderService, _, _) = CreateContext();
            vm.OutputFolder = tempTarget;

            Assert.False(Directory.Exists(tempTarget));

            vm.OpenFolderCommand.Execute(null);

            Assert.True(Directory.Exists(tempTarget));
            Assert.Equal(tempTarget, folderService.LastOpenedFolder);
        }
        finally
        {
            if (Directory.Exists(tempTarget)) Directory.Delete(tempTarget);
        }
    }

    [Fact]
    public void Pause_Resume_Stop_Commands_WorkProperly()
    {
        var (vm, _, _, coordinator) = CreateContext();
        var job = coordinator.Enqueue(CreateRequest());

        // Pause
        vm.PauseJobCommand.Execute(job);
        Assert.Equal(DownloadStage.Paused, job.Stage);

        // Resume
        vm.ResumeJobCommand.Execute(job);
        Assert.Equal(DownloadStage.Queued, job.Stage);

        // Stop
        vm.StopJobCommand.Execute(job);
        Assert.Equal(DownloadStage.Canceled, job.Stage);
    }
}
