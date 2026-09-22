namespace YtDlpGui.Application.Import;

/// <summary>
/// Runs the fully automatic "Import Apple Music Library" pipeline:
/// parse file → search + match every song → enqueue matches into the existing
/// download queue → report progress → write failed_songs.txt → final summary.
/// </summary>
public interface ILibraryImportService
{
    /// <summary>Only one import may run at a time.</summary>
    bool IsRunning { get; }

    /// <summary>Raised on background threads; UI subscribers must marshal to their dispatcher.</summary>
    event EventHandler<ImportProgress>? ProgressChanged;

    Task<LibraryImportSummary> ImportAsync(LibraryImportRequest request, CancellationToken cancellationToken);
}
