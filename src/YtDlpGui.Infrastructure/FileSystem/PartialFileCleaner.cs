using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;

namespace YtDlpGui.Infrastructure.FileSystem;

/// <summary>
/// Deletes leftover yt-dlp artifacts after a canceled download.
/// Only touches known temp patterns AND only files modified during the job's lifetime,
/// so user files in the same folder are never at risk.
/// </summary>
public sealed class PartialFileCleaner(ILogSink log) : IPartialFileCleaner
{
    private static readonly string[] TempPatterns = ["*.part", "*.part-Frag*", "*.ytdl"];

    public int CleanUp(string folderPath, DateTimeOffset modifiedSince)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return 0;
        }

        var removed = 0;
        foreach (var pattern in TempPatterns)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(folderPath, pattern, SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log.Write(LogLevel.Warning, $"Could not scan for partial files: {ex.Message}");
                continue;
            }

            foreach (var file in files)
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) >= modifiedSince.UtcDateTime.AddSeconds(-5))
                    {
                        File.Delete(file);
                        removed++;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    log.Write(LogLevel.Warning, $"Could not delete partial file {file}: {ex.Message}");
                }
            }
        }

        if (removed > 0)
        {
            log.Write(LogLevel.Info, $"Removed {removed} partial file(s) after cancellation.");
        }

        return removed;
    }
}
