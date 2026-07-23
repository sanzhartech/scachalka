using YtDlpGui.Abstractions.Enums;

namespace YtDlpGui.Abstractions.Models;

/// <summary>One line in the application log window.</summary>
public sealed record LogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Message)
{
    public string TimeText => Timestamp.ToString("HH:mm:ss");
    public string LevelText => Level.ToString().ToUpperInvariant();
}
