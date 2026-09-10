using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Application.Import;

/// <summary>What the user asked to import and how matched songs should be downloaded.</summary>
public sealed record LibraryImportRequest(
    string FilePath,
    MediaFormat Format,
    VideoQuality Quality,
    string OutputFolder,
    DownloadOptions Options);

/// <summary>Where the import pipeline currently is.</summary>
public enum ImportPhase
{
    Reading,
    Searching,
    Downloading,
    Completed,
    Canceled,
    Failed
}

/// <summary>Immutable progress snapshot raised after every import state change.</summary>
public sealed record ImportProgress(
    ImportPhase Phase,
    int SongsTotal,
    int SongsProcessed,
    int Matched,
    int Skipped,
    int DownloadsCompleted,
    int DownloadsFailed,
    string? CurrentSong,
    MatchKind LastMatchKind,
    int LastMatchConfidence);

/// <summary>Final report of one library import run.</summary>
public sealed record LibraryImportSummary(
    int SongsFound,
    int Matched,
    int Downloaded,
    int DownloadFailed,
    int Skipped,
    TimeSpan Elapsed,
    string? FailedListPath,
    bool WasCanceled);
