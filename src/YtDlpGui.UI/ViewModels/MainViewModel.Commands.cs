using System.IO;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Localization;
using YtDlpGui.Abstractions.Models;
using YtDlpGui.Application.Jobs;

namespace YtDlpGui.UI.ViewModels;

/// <summary>Command handlers of the main window (add/paste/browse/cancel/retry/open/clear/re-check).</summary>
public sealed partial class MainViewModel
{
    [RelayCommand]
    private void AddUrls() => EnqueueFromText(UrlInput, clearInputOnSuccess: true);

    [RelayCommand]
    private void PasteFromClipboard()
    {
        var text = _clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text))
        {
            StatusText = "Clipboard does not contain text.";
            return;
        }

        EnqueueFromText(text, clearInputOnSuccess: false);
    }

    /// <summary>Entry point for window drag &amp; drop (called from code-behind).</summary>
    public void AddDroppedText(string text) => EnqueueFromText(text, clearInputOnSuccess: false);

    private void EnqueueFromText(string text, bool clearInputOnSuccess)
    {
        var extraction = _urlValidator.Extract(text);

        if (extraction.Invalid.Count > 0)
        {
            _log.Write(LogLevel.Warning,
                $"Rejected {extraction.Invalid.Count} invalid URL(s): {string.Join(", ", extraction.Invalid.Take(5))}");
        }

        if (extraction.Valid.Count == 0)
        {
            StatusText = Loc.T(LocKeys.StatusNoValidUrls);
            return;
        }

        if (!AreToolsReady)
        {
            StatusText = Loc.T(LocKeys.StatusToolsMissing);
            return;
        }

        var options = BuildDownloadOptions();
        foreach (var url in extraction.Valid)
        {
            _coordinator.Enqueue(new DownloadRequest(
                url, SelectedFormat.Value, SelectedQuality.Value, OutputFolder, options));
        }

        _log.Write(LogLevel.Info, $"Queued {extraction.Valid.Count} download(s).");
        if (clearInputOnSuccess)
        {
            UrlInput = string.Empty;
        }
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose the output folder",
            InitialDirectory = Directory.Exists(OutputFolder) ? OutputFolder : string.Empty
        };

        if (dialog.ShowDialog() == true)
        {
            OutputFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void OpenOutputFolder() => _folderService.OpenFolder(OutputFolder);

    [RelayCommand]
    private void CancelJob(DownloadJob? job)
    {
        if (job is not null)
        {
            _coordinator.Cancel(job);
        }
    }

    [RelayCommand]
    private void RetryJob(DownloadJob? job)
    {
        if (job is null)
        {
            return;
        }

        if (!_coordinator.Retry(job))
        {
            StatusText = Loc.T(LocKeys.StatusCannotRetry);
        }
    }

    [RelayCommand]
    private void OpenJobLocation(DownloadJob? job)
    {
        if (job is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(job.DestinationFile))
        {
            _folderService.RevealFile(job.DestinationFile);
        }
        else
        {
            _folderService.OpenFolder(job.Request.OutputFolder);
        }
    }

    [RelayCommand]
    private void ClearFinished()
    {
        for (var i = Jobs.Count - 1; i >= 0; i--)
        {
            if (Jobs[i].IsTerminal)
            {
                Jobs[i].PropertyChanged -= OnJobPropertyChanged;
                Jobs.RemoveAt(i);
            }
        }

        UpdateStatus();
    }

    [RelayCommand]
    private async Task RecheckToolsAsync() => await RefreshToolsAsync();

    private async Task RefreshToolsAsync()
    {
        IsCheckingTools = true;
        ToolStatusText = Loc.T(LocKeys.ToolsChecking);

        try
        {
            var location = await _toolLocator.LocateAsync(CancellationToken.None);
            _toolContext.Current = location;
            _lastToolLocation = location;
            AreToolsReady = location.IsReady;
            ToolStatusText = DescribeToolStatus(location);

            if (location.IsReady)
            {
                _log.Write(LogLevel.Info,
                    $"Tools ready: yt-dlp {location.YtDlpVersion} ({location.YtDlpPath}), ffmpeg ({location.FfmpegPath}).");
            }
            else
            {
                _log.Write(LogLevel.Error, ToolStatusText);
            }
        }
        catch (Exception ex)
        {
            AreToolsReady = false;
            _lastToolLocation = ToolLocation.Empty;
            ToolStatusText = Loc.T(LocKeys.ToolsCheckFailed);
            _log.Write(LogLevel.Error, $"Tool discovery failed: {ex}");
        }
        finally
        {
            IsCheckingTools = false;
        }
    }

    private static string ShortFfmpeg(string? versionLine)
    {
        // "ffmpeg version 7.1-essentials_build-www.gyan.dev ..." → "ffmpeg 7.1"
        if (string.IsNullOrEmpty(versionLine))
        {
            return "ffmpeg";
        }

        var parts = versionLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 ? $"ffmpeg {parts[2].Split('-')[0]}" : "ffmpeg";
    }
}
