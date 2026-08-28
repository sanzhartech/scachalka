using YtDlpGui.Abstractions.Enums;

namespace YtDlpGui.Abstractions.Models;

/// <summary>
/// Outcome of checking or updating an external tool (e.g. yt-dlp).
/// </summary>
public sealed record ToolUpdateResult(
    bool Success,
    ToolUpdateStatus Status,
    string? OldVersion,
    string? NewVersion,
    string Message,
    string? TechnicalDetails = null)
{
    public static ToolUpdateResult UpToDate(string? version, string message) =>
        new(true, ToolUpdateStatus.UpToDate, version, version, message);

    public static ToolUpdateResult Updated(string? oldVersion, string? newVersion, string message) =>
        new(true, ToolUpdateStatus.Updated, oldVersion, newVersion, message);

    public static ToolUpdateResult Failed(
        ToolUpdateStatus status,
        string message,
        string? technicalDetails = null,
        string? currentVersion = null) =>
        new(false, status, currentVersion, currentVersion, message, technicalDetails);
}
