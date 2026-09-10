using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Application.Import;

/// <summary>What the user asked to bulk-import and how the links should be downloaded.</summary>
public sealed record BulkImportRequest(
    string FilePath,
    MediaFormat Format,
    VideoQuality Quality,
    string OutputFolder,
    DownloadOptions Options);

/// <summary>Where the bulk import pipeline currently is.</summary>
public enum BulkImportPhase
{
    Reading,
    Validating,
    Queueing,
    Downloading,
    Completed,
    Canceled
}

/// <summary>Immutable progress snapshot raised after every bulk import state change.</summary>
public sealed record BulkImportProgress(
    BulkImportPhase Phase,
    int RowsFound,
    int Valid,
    int Invalid,
    int Duplicates,
    int Queued,
    int DownloadsCompleted,
    int DownloadsFailed);

/// <summary>Final report of one bulk import run.</summary>
public sealed record BulkImportSummary(
    int Imported,
    int Queued,
    int Duplicates,
    int Invalid,
    int Downloaded,
    int DownloadFailed,
    TimeSpan Elapsed,
    string? FailedListPath,
    bool WasCanceled);
