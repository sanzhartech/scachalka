using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Localization;
using YtDlpGui.Abstractions.Models;
using YtDlpGui.Application.Import;

namespace YtDlpGui.UI.ViewModels;

/// <summary>
/// "Import Apple Music Library" feature: file picking, progress display and
/// cancellation. The heavy lifting (parse → search → match → enqueue) lives in
/// <see cref="ILibraryImportService"/>; this partial only renders its progress.
/// </summary>
public sealed partial class MainViewModel
{
    private CancellationTokenSource? _importCts;

    [ObservableProperty]
    private bool _isImporting;

    /// <summary>Main line of the import panel ("Searching 12 / 508: …").</summary>
    [ObservableProperty]
    private string _importStatusText = string.Empty;

    /// <summary>Secondary line ("Matched: Official Audio · Confidence: 98%").</summary>
    [ObservableProperty]
    private string _importDetailText = string.Empty;

    [ObservableProperty]
    private double _importProgressValue;

    [ObservableProperty]
    private double _importProgressMax = 1;

    public bool IsImportPanelVisible => IsImporting || !string.IsNullOrEmpty(ImportStatusText);

    partial void OnIsImportingChanged(bool value) => OnPropertyChanged(nameof(IsImportPanelVisible));

    partial void OnImportStatusTextChanged(string value) => OnPropertyChanged(nameof(IsImportPanelVisible));

    [RelayCommand]
    private async Task ImportLibraryAsync()
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
            Title = Loc.T(LocKeys.ImportFileDialogTitle),
            Filter = "Apple Music export (*.txt)|*.txt|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        // Library import means music: keep the chosen audio format, otherwise default to MP3.
        var format = SelectedFormat.Value.IsAudio() ? SelectedFormat.Value : MediaFormat.Mp3;
        var request = new LibraryImportRequest(
            dialog.FileName, format, SelectedQuality.Value, OutputFolder, BuildDownloadOptions());

        _importCts = new CancellationTokenSource();
        IsImporting = true;
        ImportStatusText = Loc.T(LocKeys.ImportReading);
        ImportDetailText = string.Empty;
        ImportProgressValue = 0;
        ImportProgressMax = 1;

        try
        {
            var summary = await _importService.ImportAsync(request, _importCts.Token);
            ShowImportSummary(summary);
        }
        catch (Exception ex)
        {
            ImportStatusText = Loc.T(LocKeys.ImportFailed);
            ImportDetailText = string.Empty;
            _log.Write(LogLevel.Error, $"Library import failed: {ex}");
        }
        finally
        {
            IsImporting = false;
            _importCts.Dispose();
            _importCts = null;
        }
    }

    [RelayCommand]
    private void CancelImport() => _importCts?.Cancel();

    /// <summary>Marshals service progress (raised on worker threads) onto the UI thread.</summary>
    private void OnImportProgressChanged(object? sender, ImportProgress progress)
    {
        _dispatcher.BeginInvoke(() => RenderImportProgress(progress));
    }

    private void RenderImportProgress(ImportProgress progress)
    {
        switch (progress.Phase)
        {
            case ImportPhase.Reading:
                ImportStatusText = Loc.T(LocKeys.ImportReading);
                return;

            case ImportPhase.Searching:
                ImportStatusText =
                    $"{Loc.T(LocKeys.ImportFoundFormat, progress.SongsTotal)} " +
                    Loc.T(LocKeys.ImportSearchingFormat,
                        progress.SongsProcessed, progress.SongsTotal, progress.CurrentSong ?? "…");
                ImportProgressMax = Math.Max(1, progress.SongsTotal);
                ImportProgressValue = progress.SongsProcessed;
                break;

            case ImportPhase.Downloading:
                ImportStatusText = progress.Matched > 0
                    ? Loc.T(LocKeys.ImportDownloadingFormat,
                        progress.DownloadsCompleted + progress.DownloadsFailed, progress.Matched)
                    : Loc.T(LocKeys.ImportWaitingDownloads);
                ImportProgressMax = Math.Max(1, progress.Matched);
                ImportProgressValue = progress.DownloadsCompleted + progress.DownloadsFailed;
                break;

            case ImportPhase.Canceled:
                ImportStatusText = Loc.T(LocKeys.ImportCanceled);
                break;

            case ImportPhase.Completed when progress.SongsTotal == 0:
                ImportStatusText = Loc.T(LocKeys.ImportNoSongs);
                return;
        }

        ImportDetailText = BuildImportDetail(progress);
    }

    private static string BuildImportDetail(ImportProgress progress)
    {
        var parts = new List<string>(3);
        if (progress.LastMatchKind != MatchKind.None)
        {
            parts.Add(Loc.T(LocKeys.ImportMatchedFormat,
                MatchKindText(progress.LastMatchKind), progress.LastMatchConfidence));
        }

        if (progress.Skipped > 0)
        {
            parts.Add(Loc.T(LocKeys.ImportSkippedFormat, progress.Skipped));
        }

        if (progress.Matched > 0 && progress.Phase == ImportPhase.Searching)
        {
            parts.Add(Loc.T(LocKeys.ImportDownloadingFormat,
                progress.DownloadsCompleted + progress.DownloadsFailed, progress.Matched));
        }

        return string.Join(" · ", parts);
    }

    private void ShowImportSummary(LibraryImportSummary summary)
    {
        if (summary.WasCanceled)
        {
            ImportStatusText = Loc.T(LocKeys.ImportCanceled);
        }
        else if (summary.SongsFound == 0)
        {
            ImportStatusText = Loc.T(LocKeys.ImportNoSongs);
        }
        else
        {
            ImportStatusText = Loc.T(LocKeys.ImportDoneFormat,
                summary.SongsFound,
                summary.Downloaded,
                summary.Skipped,
                summary.DownloadFailed,
                FormatElapsed(summary.Elapsed));
        }

        ImportDetailText = summary.FailedListPath is null
            ? string.Empty
            : Loc.T(LocKeys.ImportFailedSavedFormat, summary.FailedListPath);
        ImportProgressValue = ImportProgressMax;
    }

    private static string MatchKindText(MatchKind kind) => kind switch
    {
        MatchKind.OfficialMusicTrack => Loc.T(LocKeys.MatchOfficialMusicTrack),
        MatchKind.OfficialAudio => Loc.T(LocKeys.MatchOfficialAudio),
        MatchKind.OfficialMusicVideo => Loc.T(LocKeys.MatchOfficialMusicVideo),
        MatchKind.Vevo => Loc.T(LocKeys.MatchVevo),
        MatchKind.VerifiedChannel => Loc.T(LocKeys.MatchVerifiedChannel),
        _ => Loc.T(LocKeys.MatchHighConfidence)
    };

    /// <summary>"18m 42s" / "1h 03m 12s".</summary>
    private static string FormatElapsed(TimeSpan elapsed) => elapsed.TotalHours >= 1
        ? $"{(int)elapsed.TotalHours}h {elapsed.Minutes:00}m {elapsed.Seconds:00}s"
        : $"{elapsed.Minutes}m {elapsed.Seconds:00}s";
}
