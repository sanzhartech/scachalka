using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Localization;
using YtDlpGui.Abstractions.Models;
using YtDlpGui.Application.Jobs;
using YtDlpGui.Application.Tools;

namespace YtDlpGui.Application.Execution;

/// <summary>
/// Executes one download end-to-end: validates tool availability, builds the yt-dlp
/// command line via the matching strategy, streams progress into the job and
/// classifies any failure into a user-friendly message.
/// </summary>
public sealed class DownloadExecutor(
    IMediaToolRunner runner,
    IEnumerable<IArgumentBuilder> builders,
    IProgressParser parser,
    IErrorClassifier classifier,
    IPartialFileCleaner partialCleaner,
    ToolContext toolContext,
    ILogSink log) : IDownloadExecutor
{
    /// <summary>UI update throttle: at most ~7 progress pushes per second per job.</summary>
    private const long ProgressIntervalMs = 150;

    public async Task ExecuteAsync(DownloadJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        job.StartedAt = DateTimeOffset.Now;
        job.Stage = DownloadStage.Resolving;

        var tools = toolContext.Current;
        if (!ValidateTools(job, tools))
        {
            return;
        }

        var builder = builders.FirstOrDefault(b => b.CanBuild(job.Request));
        if (builder is null)
        {
            Fail(job, DownloadErrorKind.Unknown, $"No download strategy registered for format {job.Request.Format}.");
            return;
        }

        if (!EnsureOutputFolder(job))
        {
            return;
        }

        var arguments = builder.Build(job.Request, tools);
        log.Write(LogLevel.Info, $"[{Short(job)}] Starting: {job.Url} → {job.Request.Format} into {job.Request.OutputFolder}");
        log.Write(LogLevel.Debug, $"[{Short(job)}] yt-dlp {string.Join(' ', arguments)}");

        var alreadyDownloaded = false;
        long lastProgressTick = 0;

        void OnOutput(string line)
        {
            if (!parser.TryParse(line, out var evt))
            {
                log.Write(LogLevel.Debug, $"[{Short(job)}] {line}");
                return;
            }

            switch (evt.Kind)
            {
                case YtDlpEventKind.Progress when evt.Progress is not null:
                    ApplyProgress(job, evt.Progress, ref lastProgressTick);
                    break;

                case YtDlpEventKind.StageChanged when evt.Stage is not null:
                    ApplyStage(job, evt.Stage.Value);
                    break;

                case YtDlpEventKind.DestinationResolved when evt.Path is not null:
                    job.DestinationFile = evt.Path;
                    job.Title = Path.GetFileNameWithoutExtension(evt.Path);
                    break;

                case YtDlpEventKind.AlreadyDownloaded:
                    alreadyDownloaded = true;
                    if (evt.Path is not null)
                    {
                        job.DestinationFile = evt.Path;
                        job.Title = Path.GetFileNameWithoutExtension(evt.Path);
                    }
                    break;
            }
        }

        void OnError(string line) => log.Write(LogLevel.Warning, $"[{Short(job)}] {line}");

        ToolResult result;
        var usedCookieFallback = false;
        try
        {
            result = await runner.RunAsync(tools.YtDlpPath!, arguments, OnOutput, OnError, cancellationToken)
                .ConfigureAwait(false);

            // A running browser locks its cookie database (yt-dlp #7271). Instead of
            // failing the whole download, transparently retry once without cookies.
            if (NeedsCookieFallback(job, result))
            {
                log.Write(LogLevel.Warning,
                    $"[{Short(job)}] Browser cookie database is locked — retrying without cookies. " +
                    "Close the browser completely to use its cookies.");
                job.Stage = DownloadStage.Resolving;

                var fallbackRequest = job.Request with
                {
                    Options = job.Request.EffectiveOptions with { CookiesFromBrowser = string.Empty }
                };
                var fallbackArguments = builder.Build(fallbackRequest, tools);
                result = await runner.RunAsync(
                        tools.YtDlpPath!, fallbackArguments, OnOutput, OnError, cancellationToken)
                    .ConfigureAwait(false);
                usedCookieFallback = true;
            }
        }
        catch (Exception ex)
        {
            Fail(job, DownloadErrorKind.Unknown, ex.Message);
            log.Write(LogLevel.Error, $"[{Short(job)}] Unexpected executor failure: {ex}");
            return;
        }

        CompleteJob(job, result, alreadyDownloaded);

        if (usedCookieFallback && job.Stage == DownloadStage.Completed)
        {
            job.StatusNote = Loc.T(LocKeys.NoteCookiesFallback);
        }
    }

    /// <summary>True when the failure is the locked-browser-cookie-DB case and cookies were in use.</summary>
    private bool NeedsCookieFallback(DownloadJob job, ToolResult result) =>
        !result.WasCanceled
        && result.ExitCode != 0
        && !string.IsNullOrWhiteSpace(job.Request.EffectiveOptions.CookiesFromBrowser)
        && classifier.Classify(result.ExitCode, result.StdErrTail) == DownloadErrorKind.CookieBrowserLocked;

    private bool ValidateTools(DownloadJob job, ToolLocation tools)
    {
        if (!tools.HasYtDlp)
        {
            Fail(job, DownloadErrorKind.ToolMissing, "yt-dlp.exe was not found.");
            return false;
        }

        // ffmpeg is required for both paths: MP4 merging and MP3 conversion.
        if (!tools.HasFfmpeg)
        {
            Fail(job, DownloadErrorKind.ToolMissing, "ffmpeg.exe was not found.");
            return false;
        }

        return true;
    }

    private bool EnsureOutputFolder(DownloadJob job)
    {
        try
        {
            Directory.CreateDirectory(job.Request.OutputFolder);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Fail(job, DownloadErrorKind.PermissionDenied, ex.Message);
            return false;
        }
    }

    private static void ApplyProgress(DownloadJob job, ProgressSnapshot snapshot, ref long lastTick)
    {
        if (job.Stage is DownloadStage.Resolving)
        {
            job.Stage = DownloadStage.Downloading;
        }

        // Throttle high-frequency updates; always let the final 100% through.
        var now = Environment.TickCount64;
        if (!snapshot.IsFinished && now - lastTick < ProgressIntervalMs)
        {
            return;
        }

        lastTick = now;
        job.Percent = snapshot.Percent;
        job.SpeedBytesPerSecond = snapshot.SpeedBytesPerSecond;
        job.EtaSeconds = snapshot.EtaSeconds;
        job.DownloadedBytes = snapshot.DownloadedBytes;
        job.TotalBytes = snapshot.TotalBytes;
    }

    private void ApplyStage(DownloadJob job, DownloadStage stage)
    {
        // Never regress a stage (e.g. a late fallback marker after conversion started).
        if (stage > job.Stage && stage is DownloadStage.Merging or DownloadStage.Converting)
        {
            job.Stage = stage;
            log.Write(LogLevel.Info, $"[{Short(job)}] {job.StageText}");
        }
    }

    private void CompleteJob(DownloadJob job, ToolResult result, bool alreadyDownloaded)
    {
        if (result.WasCanceled)
        {
            job.Stage = DownloadStage.Canceled;
            job.StatusNote = Loc.T(LocKeys.NoteCanceled);
            log.Write(LogLevel.Info, $"[{Short(job)}] Canceled.");
            CleanUpPartials(job);
            return;
        }

        if (result.ExitCode == 0)
        {
            job.Percent = 100;
            job.SpeedBytesPerSecond = null;
            job.EtaSeconds = null;
            job.Stage = DownloadStage.Completed;
            job.StatusNote = alreadyDownloaded ? Loc.T(LocKeys.NoteAlreadyExisted) : null;
            log.Write(LogLevel.Info, $"[{Short(job)}] Completed: {job.DestinationFile ?? job.Url}");
            return;
        }

        var kind = classifier.Classify(result.ExitCode, result.StdErrTail);
        var lastErrorLine = result.StdErrTail
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault(l => l.Contains("ERROR", StringComparison.OrdinalIgnoreCase));
        Fail(job, kind, lastErrorLine);
        log.Write(LogLevel.Error, $"[{Short(job)}] Failed (exit {result.ExitCode}, {kind}).");
    }

    private void Fail(DownloadJob job, DownloadErrorKind kind, string? detail)
    {
        job.ErrorKind = kind;
        job.ErrorMessage = classifier.GetUserMessage(kind, detail);
        job.SpeedBytesPerSecond = null;
        job.EtaSeconds = null;
        job.Stage = DownloadStage.Failed;
    }

    private void CleanUpPartials(DownloadJob job)
    {
        if (job.StartedAt is { } startedAt)
        {
            partialCleaner.CleanUp(job.Request.OutputFolder, startedAt);
        }
    }

    private static string Short(DownloadJob job) => job.Id.ToString("N")[..8];
}
