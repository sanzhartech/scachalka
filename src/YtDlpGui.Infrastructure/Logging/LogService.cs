using YtDlpGui.Abstractions;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Infrastructure.Logging;

/// <summary>
/// Thread-safe log sink with two outputs:
/// - a bounded in-memory ring buffer (feeds the log window; memory can never grow unbounded),
/// - a best-effort session file in %APPDATA%\YtDlpGui\logs (survives crashes for diagnostics).
/// File IO failures degrade silently to in-memory-only logging — logging must never crash the app.
/// </summary>
public sealed class LogService : ILogSink, IDisposable
{
    private const int Capacity = 2000;

    private readonly object _gate = new();
    private readonly Queue<LogEntry> _entries = new(Capacity);
    private readonly StreamWriter? _fileWriter;

    public event EventHandler<LogEntry>? EntryAdded;

    public LogService(IFileNameSanitizer sanitizer)
    {
        _fileWriter = TryCreateSessionFile(sanitizer);
    }

    public void Write(LogLevel level, string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        var entry = new LogEntry(DateTimeOffset.Now, level, message);

        lock (_gate)
        {
            if (_entries.Count >= Capacity)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(entry);

            try
            {
                _fileWriter?.WriteLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.LevelText}] {entry.Message}");
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // Disk problems must not affect the application.
            }
        }

        EntryAdded?.Invoke(this, entry);
    }

    public IReadOnlyList<LogEntry> GetSnapshot()
    {
        lock (_gate)
        {
            return [.. _entries];
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            try
            {
                _fileWriter?.Flush();
                _fileWriter?.Dispose();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // Nothing meaningful can be done during shutdown.
            }
        }
    }

    private static StreamWriter? TryCreateSessionFile(IFileNameSanitizer sanitizer)
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var logsFolder = Path.Combine(appData, AppInfo.DataFolder, "logs");
            Directory.CreateDirectory(logsFolder);

            var fileName = sanitizer.Sanitize($"session-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            var writer = new StreamWriter(Path.Combine(logsFolder, fileName), append: false)
            {
                AutoFlush = true
            };
            return writer;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
