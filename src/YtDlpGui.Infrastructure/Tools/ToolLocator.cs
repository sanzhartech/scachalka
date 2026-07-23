using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Infrastructure.Tools;

/// <summary>
/// Cascading search for yt-dlp.exe / ffmpeg.exe:
/// app folder → app folder\tools → every PATH entry → WinGet links.
/// A candidate only counts as found when a quick "--version" probe succeeds,
/// which filters out broken shims and zero-byte placeholders.
/// </summary>
public sealed class ToolLocator(IMediaToolRunner runner, ILogSink log) : IToolLocator
{
    private const string YtDlpExe = "yt-dlp.exe";
    private const string FfmpegExe = "ffmpeg.exe";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    public async Task<ToolLocation> LocateAsync(CancellationToken cancellationToken)
    {
        var ytDlpPath = FindExecutable(YtDlpExe);
        var ffmpegPath = FindExecutable(FfmpegExe);

        string? ytDlpVersion = null;
        if (ytDlpPath is not null)
        {
            ytDlpVersion = await ProbeVersionAsync(ytDlpPath, "--version", cancellationToken).ConfigureAwait(false);
            if (ytDlpVersion is null)
            {
                log.Write(LogLevel.Warning, $"Found {ytDlpPath} but it did not respond to --version; ignoring it.");
                ytDlpPath = null;
            }
        }

        string? ffmpegVersion = null;
        if (ffmpegPath is not null)
        {
            ffmpegVersion = await ProbeVersionAsync(ffmpegPath, "-version", cancellationToken).ConfigureAwait(false);
            if (ffmpegVersion is null)
            {
                log.Write(LogLevel.Warning, $"Found {ffmpegPath} but it did not respond to -version; ignoring it.");
                ffmpegPath = null;
            }
        }

        return new ToolLocation(ytDlpPath, ytDlpVersion, ffmpegPath, ffmpegVersion);
    }

    private static string? FindExecutable(string fileName)
    {
        foreach (var directory in EnumerateSearchDirectories())
        {
            try
            {
                var candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // PATH may contain malformed entries — skip them.
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSearchDirectories()
    {
        yield return AppContext.BaseDirectory;
        yield return Path.Combine(AppContext.BaseDirectory, "tools");

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var entry in path.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return entry;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            yield return Path.Combine(localAppData, "Microsoft", "WinGet", "Links");
        }
    }

    /// <returns>The first output line (the version), or null when the probe failed or timed out.</returns>
    private async Task<string?> ProbeVersionAsync(string exePath, string versionArgument, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);

        string? firstLine = null;
        try
        {
            var result = await runner.RunAsync(
                exePath,
                [versionArgument],
                line => firstLine ??= line.Trim(),
                onErrorLine: null,
                timeout.Token).ConfigureAwait(false);

            return result.IsSuccess && !string.IsNullOrWhiteSpace(firstLine) ? firstLine : null;
        }
        catch (Exception ex)
        {
            log.Write(LogLevel.Warning, $"Version probe for {exePath} failed: {ex.Message}");
            return null;
        }
    }
}
