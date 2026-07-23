using YtDlpGui.Abstractions.Enums;

namespace YtDlpGui.Abstractions.Interfaces;

/// <summary>Turns raw tool exit codes / stderr into a typed error and a user-friendly message.</summary>
public interface IErrorClassifier
{
    DownloadErrorKind Classify(int exitCode, string stderrTail);

    string GetUserMessage(DownloadErrorKind kind, string? detail);
}
