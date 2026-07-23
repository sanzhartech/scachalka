using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Abstractions.Interfaces;

/// <summary>Finds yt-dlp.exe and ffmpeg.exe on the machine and probes their versions.</summary>
public interface IToolLocator
{
    Task<ToolLocation> LocateAsync(CancellationToken cancellationToken);
}
