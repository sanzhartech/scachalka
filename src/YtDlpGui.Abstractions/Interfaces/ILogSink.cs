using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Abstractions.Interfaces;

/// <summary>
/// Thread-safe application log: bounded in-memory ring buffer surfaced in the log window,
/// optionally mirrored to a session file on disk.
/// </summary>
public interface ILogSink
{
    void Write(LogLevel level, string message);

    event EventHandler<LogEntry>? EntryAdded;

    IReadOnlyList<LogEntry> GetSnapshot();
}
