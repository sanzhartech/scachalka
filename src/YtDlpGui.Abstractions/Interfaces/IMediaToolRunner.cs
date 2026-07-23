using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Abstractions.Interfaces;

/// <summary>
/// The single gateway for executing external tool processes (yt-dlp, ffmpeg).
/// Implementations must never block the caller, must stream output line-by-line
/// and must kill the whole process tree on cancellation.
/// </summary>
public interface IMediaToolRunner
{
    Task<ToolResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        Action<string>? onOutputLine,
        Action<string>? onErrorLine,
        CancellationToken cancellationToken);
}
