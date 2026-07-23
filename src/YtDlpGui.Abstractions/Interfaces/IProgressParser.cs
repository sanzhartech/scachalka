using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Abstractions.Interfaces;

/// <summary>Parses a single line of yt-dlp console output into a structured event.</summary>
public interface IProgressParser
{
    /// <returns>True when the line carried a recognizable event; false for informational noise.</returns>
    bool TryParse(string line, out YtDlpEvent evt);
}
