namespace YtDlpGui.Application.Import;

/// <summary>
/// Runs the fully automatic "Bulk Import Links" pipeline:
/// detect file format → extract URLs → validate + dedupe → enqueue everything
/// valid into the existing download queue → report progress → write
/// failed_links.txt → final summary.
/// </summary>
public interface IBulkImportService
{
    /// <summary>Only one bulk import may run at a time.</summary>
    bool IsRunning { get; }

    /// <summary>Raised on background threads; UI subscribers must marshal to their dispatcher.</summary>
    event EventHandler<BulkImportProgress>? ProgressChanged;

    Task<BulkImportSummary> ImportAsync(BulkImportRequest request, CancellationToken cancellationToken);
}
