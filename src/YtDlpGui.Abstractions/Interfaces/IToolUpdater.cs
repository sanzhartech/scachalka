using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Abstractions.Interfaces;

/// <summary>
/// Service for checking and performing updates of external backend tools (such as yt-dlp).
/// </summary>
public interface IToolUpdater
{
    Task<ToolUpdateResult> UpdateAsync(
        ToolLocation location,
        CancellationToken cancellationToken = default);
}
