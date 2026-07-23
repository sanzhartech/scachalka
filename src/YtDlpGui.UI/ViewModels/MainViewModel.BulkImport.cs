using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Localization;
using YtDlpGui.Application.Import;

namespace YtDlpGui.UI.ViewModels;

/// <summary>
/// "Bulk Import Links" feature: file picking and progress display. Shares the
/// import panel, the busy flag and the cancel button with the Apple Music
/// import — only one import of either kind runs at a time. The pipeline itself
/// (detect format → extract → validate → enqueue) lives in <see cref="IBulkImportService"/>.
/// </summary>
public sealed partial class MainViewModel
{
    [RelayCommand]
    private async Task BulkImportLinksAsync()
    {
        if (IsImporting)
        {
            StatusText = Loc.T(LocKeys.ImportAlreadyRunning);
            return;
        }

        if (!AreToolsReady)
        {
            StatusText = Loc.T(LocKeys.StatusToolsMissing);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = Loc.T(LocKeys.BulkFileDialogTitle),
            Filter = "Link files (*.csv;*.txt;*.json)|*.csv;*.txt;*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        // Bulk links use the same format/quality/folder the user picked for manual adds.
        var request = new BulkImportRequest(
            dialog.FileName, SelectedFormat.Value, SelectedQuality.Value, OutputFolder, BuildDownloadOptions());

        _importCts = new CancellationTokenSource();
        IsImporting = true;
        ImportStatusText = Loc.T(LocKeys.BulkReading);
        ImportDetailText = string.Empty;
        ImportProgressValue = 0;
        ImportProgressMax = 1;

        try
        {
            var summary = await _bulkImportService.ImportAsync(request, _importCts.Token);
            ShowBulkSummary(summary);
        }
        catch (Exception ex)
        {
            ImportStatusText = Loc.T(LocKeys.ImportFailed);
            ImportDetailText = string.Empty;
            _log.Write(LogLevel.Error, $"Bulk import failed: {ex}");
        }
        finally
        {
            IsImporting = false;
            _importCts.Dispose();
            _importCts = null;
        }
    }

    /// <summary>Marshals service progress (raised on worker threads) onto the UI thread.</summary>
    private void OnBulkImportProgressChanged(object? sender, BulkImportProgress progress)
    {
        _dispatcher.BeginInvoke(() => RenderBulkProgress(progress));
    }

    private void RenderBulkProgress(BulkImportProgress progress)
    {
        switch (progress.Phase)
        {
            case BulkImportPhase.Reading:
                ImportStatusText = Loc.T(LocKeys.BulkReading);
                return;

            case BulkImportPhase.Validating:
                ImportStatusText = Loc.T(LocKeys.BulkValidatingFormat, progress.RowsFound);
                return;

            case BulkImportPhase.Queueing:
                ImportStatusText = Loc.T(LocKeys.BulkQueueingFormat, progress.Queued, progress.Valid);
                ImportProgressMax = Math.Max(1, progress.Valid);
                ImportProgressValue = progress.Queued;
                break;

            case BulkImportPhase.Downloading:
                ImportStatusText = Loc.T(LocKeys.ImportDownloadingFormat,
                    progress.DownloadsCompleted + progress.DownloadsFailed, progress.Queued);
                ImportProgressMax = Math.Max(1, progress.Queued);
                ImportProgressValue = progress.DownloadsCompleted + progress.DownloadsFailed;
                break;

            case BulkImportPhase.Canceled:
                ImportStatusText = Loc.T(LocKeys.ImportCanceled);
                break;
        }

        ImportDetailText = Loc.T(LocKeys.BulkStatsFormat,
            progress.Valid, progress.Duplicates, progress.Invalid);
    }

    private void ShowBulkSummary(BulkImportSummary summary)
    {
        if (summary.WasCanceled)
        {
            ImportStatusText = Loc.T(LocKeys.ImportCanceled);
        }
        else if (summary.Imported == 0)
        {
            ImportStatusText = Loc.T(LocKeys.BulkNoLinks);
        }
        else
        {
            ImportStatusText = Loc.T(LocKeys.BulkDoneFormat,
                summary.Imported,
                summary.Queued,
                summary.Duplicates,
                summary.Invalid,
                summary.Downloaded,
                FormatElapsed(summary.Elapsed));
        }

        ImportDetailText = summary.FailedListPath is null
            ? string.Empty
            : Loc.T(LocKeys.ImportFailedSavedFormat, summary.FailedListPath);
        ImportProgressValue = ImportProgressMax;
    }
}
