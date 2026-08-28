using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Localization;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Infrastructure.Tools;

/// <summary>
/// Updates yt-dlp using "yt-dlp -U" via IMediaToolRunner with semaphore synchronization
/// to protect against concurrent execution.
/// </summary>
public sealed class ToolUpdater(IMediaToolRunner runner, ILogSink log) : IToolUpdater
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<ToolUpdateResult> UpdateAsync(ToolLocation location, CancellationToken cancellationToken = default)
    {
        if (!location.HasYtDlp || string.IsNullOrEmpty(location.YtDlpPath))
        {
            log.Write(LogLevel.Warning, "Cannot update yt-dlp: executable path is not found.");
            return ToolUpdateResult.Failed(
                ToolUpdateStatus.NotFound,
                Loc.T(LocKeys.ToolsNotFound),
                "Executable path is null or empty.",
                location.YtDlpVersion);
        }

        // Prevent parallel update runs
        var acquired = await _lock.WaitAsync(0, cancellationToken).ConfigureAwait(false);
        if (!acquired)
        {
            log.Write(LogLevel.Warning, "An update operation is already in progress.");
            return ToolUpdateResult.Failed(
                ToolUpdateStatus.Failed,
                "An update is already running.",
                currentVersion: location.YtDlpVersion);
        }

        try
        {
            log.Write(LogLevel.Info, $"Checking for yt-dlp updates: {location.YtDlpPath} (current: {location.YtDlpVersion ?? "unknown"})...");

            var lines = new List<string>();
            var toolResult = await runner.RunAsync(
                location.YtDlpPath,
                ["-U"],
                line =>
                {
                    lock (lines) lines.Add(line);
                    log.Write(LogLevel.Debug, $"[yt-dlp update] {line}");
                },
                errLine =>
                {
                    lock (lines) lines.Add(errLine);
                    log.Write(LogLevel.Warning, $"[yt-dlp update] {errLine}");
                },
                cancellationToken).ConfigureAwait(false);

            var result = YtDlpUpdateParser.Parse(toolResult.ExitCode, lines, location.YtDlpVersion);

            switch (result.Status)
            {
                case ToolUpdateStatus.Updated:
                    log.Write(LogLevel.Info, $"yt-dlp updated: {result.OldVersion ?? "?"} -> {result.NewVersion ?? "?"}");
                    break;
                case ToolUpdateStatus.UpToDate:
                    log.Write(LogLevel.Info, $"yt-dlp is up to date: {result.NewVersion ?? location.YtDlpVersion ?? "latest"}.");
                    break;
                case ToolUpdateStatus.PermissionDenied:
                    log.Write(LogLevel.Error, $"yt-dlp update failed (Permission Denied): {result.Message}");
                    break;
                case ToolUpdateStatus.NetworkError:
                    log.Write(LogLevel.Warning, $"yt-dlp update failed (Network Error): {result.Message}");
                    break;
                case ToolUpdateStatus.Cancelled:
                    log.Write(LogLevel.Warning, "yt-dlp update was cancelled.");
                    break;
                default:
                    log.Write(LogLevel.Error, $"yt-dlp update failed ({result.Status}): {result.Message}");
                    break;
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            log.Write(LogLevel.Warning, "yt-dlp update was cancelled.");
            return ToolUpdateResult.Failed(
                ToolUpdateStatus.Cancelled,
                Loc.T(LocKeys.ToolsUpdateCancelled),
                "Operation cancelled.",
                location.YtDlpVersion);
        }
        catch (Exception ex)
        {
            log.Write(LogLevel.Error, $"Unexpected error during tool update: {ex}");
            return ToolUpdateResult.Failed(
                ToolUpdateStatus.Failed,
                Loc.T(LocKeys.ToolsUpdateFailedFormat, ex.Message),
                ex.ToString(),
                location.YtDlpVersion);
        }
        finally
        {
            _lock.Release();
        }
    }
}
