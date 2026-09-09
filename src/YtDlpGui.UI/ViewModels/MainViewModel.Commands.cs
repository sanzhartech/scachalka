using System.IO;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Localization;
using YtDlpGui.Abstractions.Models;
using YtDlpGui.Application.Jobs;

namespace YtDlpGui.UI.ViewModels;

/// <summary>Command handlers of the main window (add/paste/browse/cancel/retry/open/clear/re-check/update).</summary>
public sealed partial class MainViewModel
{
    [RelayCommand]
    private void AddUrls() => EnqueueFromText(UrlInput, clearInputOnSuccess: true);

    [RelayCommand]
    private void ClearUrlInput() => UrlInput = string.Empty;

    [RelayCommand]
    private void PasteAndDownload()
    {
        if (!string.IsNullOrWhiteSpace(UrlInput))
        {
            EnqueueFromText(UrlInput, clearInputOnSuccess: true);
            return;
        }

        var text = _clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text))
        {
            StatusText = Loc.T(LocKeys.StatusClipboardNoText);
            return;
        }

        EnqueueFromText(text, clearInputOnSuccess: false);
    }

    [RelayCommand]
    private void PasteFromClipboard()
    {
        var text = _clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text))
        {
            StatusText = Loc.T(LocKeys.StatusClipboardNoText);
            return;
        }

        EnqueueFromText(text, clearInputOnSuccess: false);
    }

    /// <summary>Entry point for window drag & drop (called from code-behind).</summary>
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

    /// <summary>Action to prompt user for output folder; defaults to OpenFolderDialog, replaceable in unit tests.</summary>
    public Func<string, string?>? PickFolderAction { get; set; }

    [RelayCommand]
    private void BrowseFolder()
    {
        try
        {
            string? selected = null;
            if (PickFolderAction is not null)
            {
                selected = PickFolderAction(OutputFolder);
            }
            else
            {
                var dialog = new OpenFolderDialog
                {
                    Title = Loc.T(LocKeys.LabelOutputFolder),
                    InitialDirectory = Directory.Exists(OutputFolder) ? OutputFolder : string.Empty
                };

                if (dialog.ShowDialog() == true)
                {
                    selected = dialog.FolderName;
                }
            }

            if (!string.IsNullOrWhiteSpace(selected))
            {
                OutputFolder = selected;
                _log.Write(LogLevel.Info, $"Output folder set to: {OutputFolder}");
            }
        }
        catch (Exception ex)
        {
            _log.Write(LogLevel.Warning, $"Browse folder failed: {ex.Message}");
            StatusText = $"Ошибка выбора папки: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            StatusText = "Папка сохранения не указана.";
            return;
        }

        try
        {
            if (!Directory.Exists(OutputFolder))
            {
                Directory.CreateDirectory(OutputFolder);
            }

            if (!_folderService.OpenFolder(OutputFolder))
            {
                StatusText = $"Не удалось открыть папку: {OutputFolder}";
            }
        }
        catch (Exception ex)
        {
            _log.Write(LogLevel.Error, $"Failed to open output folder '{OutputFolder}': {ex.Message}");
            StatusText = $"Ошибка при открытии папки: {ex.Message}";
        }
    }

    [RelayCommand]
    private void PauseJob(DownloadJob? job)
    {
        if (job is not null)
        {
            _coordinator.Pause(job);
            UpdateStatus();
        }
    }

    [RelayCommand]
    private void ResumeJob(DownloadJob? job)
    {
        if (job is not null)
        {
            _coordinator.Resume(job);
            UpdateStatus();
        }
    }

    [RelayCommand]
    private void StopJob(DownloadJob? job)
    {
        if (job is not null)
        {
            _coordinator.Cancel(job);
            UpdateStatus();
        }
    }

    [RelayCommand]
    private void CancelJob(DownloadJob? job)
    {
        StopJob(job);
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
        if (job is null) return;

        var filePath = job.DestinationFile;
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            var trimmed = filePath.Trim().Trim('"');
            if (trimmed.StartsWith(@"\\?\", StringComparison.Ordinal))
            {
                trimmed = trimmed[4..];
            }

            if (!Path.IsPathRooted(trimmed))
            {
                trimmed = Path.Combine(job.Request.OutputFolder, trimmed);
            }

            if (File.Exists(trimmed))
            {
                var fullPath = Path.GetFullPath(trimmed);
                job.DestinationFile = fullPath;
                if (!_folderService.RevealFile(fullPath))
                {
                    StatusText = "Не удалось открыть файл в проводнике.";
                }
                return;
            }
        }

        // Try to resolve in output folder if extension/name slightly changed during post-processing
        var resolved = TryResolveDownloadedFile(job);
        if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
        {
            var fullPath = Path.GetFullPath(resolved);
            job.DestinationFile = fullPath;
            if (!_folderService.RevealFile(fullPath))
            {
                StatusText = "Не удалось открыть файл в проводнике.";
            }
            return;
        }

        // File is missing — open destination folder and show notification
        var folder = !string.IsNullOrWhiteSpace(filePath)
            ? Path.GetDirectoryName(filePath)
            : job.Request.OutputFolder;

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            folder = job.Request.OutputFolder;
        }

        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            _folderService.OpenFolder(folder);
            StatusText = "Файл не найден. Открыта папка сохранения.";
        }
        else
        {
            StatusText = "Файл и папка назначения не найдены.";
            _log.Write(LogLevel.Warning, $"Destination folder not found: {folder}");
        }
    }

    private static string? TryResolveDownloadedFile(DownloadJob job)
    {
        try
        {
            var folder = job.Request.OutputFolder;
            if (!Directory.Exists(folder))
            {
                return null;
            }

            var candidate = job.DestinationFile;
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                var stripped = System.Text.RegularExpressions.Regex.Replace(
                    candidate, @"\.(?:f\d+|temp|part)(\.[a-zA-Z0-9]+)$", "$1", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (File.Exists(stripped))
                {
                    return stripped;
                }

                var baseName = Path.GetFileNameWithoutExtension(stripped);
                baseName = System.Text.RegularExpressions.Regex.Replace(
                    baseName, @"\.f\d+$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                var matches = Directory.GetFiles(folder, $"{baseName}.*", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (matches.Count > 0)
                {
                    var preferredExt = "." + job.Request.Format.ToString().ToLowerInvariant();
                    return matches.FirstOrDefault(f => f.EndsWith(preferredExt, StringComparison.OrdinalIgnoreCase)) ?? matches[0];
                }
            }

            if (!string.IsNullOrWhiteSpace(job.Title))
            {
                var matches = Directory.GetFiles(folder, $"*{job.Title}*", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (matches.Count > 0)
                {
                    return matches[0];
                }
            }
        }
        catch
        {
            // Ignore resolution errors and let caller fall back to opening folder
        }

        return null;
    }

    /// <summary>Delegate to launch a media file; replaceable in unit tests.</summary>
    public Action<string>? OpenFileAction { get; set; }

    [RelayCommand]
    private void PlayJobFile(DownloadJob? job)
    {
        if (job is null) return;

        var filePath = job.DestinationFile;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            StatusText = "Файл не найден на диске.";
            _log.Write(LogLevel.Warning, $"Cannot play file — it does not exist: {filePath}");
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(filePath);
            if (OpenFileAction is not null)
            {
                OpenFileAction(fullPath);
            }
            else
            {
                using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fullPath)
                {
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            _log.Write(LogLevel.Error, $"Failed to open file '{filePath}': {ex.Message}");
            StatusText = $"Не удалось открыть файл: {ex.Message}";
        }
    }

    [RelayCommand]
    private void MoveJobUp(DownloadJob? job)
    {
        if (job is null) return;
        var index = Jobs.IndexOf(job);
        if (index > 0)
        {
            Jobs.Move(index, index - 1);
        }
    }

    [RelayCommand]
    private void MoveJobDown(DownloadJob? job)
    {
        if (job is null) return;
        var index = Jobs.IndexOf(job);
        if (index >= 0 && index < Jobs.Count - 1)
        {
            Jobs.Move(index, index + 1);
        }
    }

    [RelayCommand]
    private void CopyJobUrl(DownloadJob? job)
    {
        if (job is not null && !string.IsNullOrWhiteSpace(job.Url))
        {
            _clipboard.SetText(job.Url);
            StatusText = "Ссылка скопирована в буфер обмена";
            _log.Write(LogLevel.Info, $"Copied URL to clipboard: {job.Url}");
        }
    }

    [RelayCommand]
    private void DeleteJob(DownloadJob? job)
    {
        if (job is null) return;
        CancelJob(job);
        job.PropertyChanged -= OnJobPropertyChanged;
        Jobs.Remove(job);
        UpdateStatus();
    }

    /// <summary>Action to prompt user confirmation before deleting file from disk.</summary>
    public Func<string, bool> ConfirmDeleteAction { get; set; } = msg => YtDlpGui.UI.Views.ConfirmDeleteDialog.Show(msg);

    [RelayCommand]
    private void DeleteJobFile(DownloadJob? job)
    {
        if (job is null) return;

        if (!ConfirmDeleteAction("Удалить файл с компьютера?"))
        {
            return;
        }

        if (!string.IsNullOrEmpty(job.DestinationFile))
        {
            try
            {
                if (File.Exists(job.DestinationFile))
                {
                    File.Delete(job.DestinationFile);
                }

                var partFile = job.DestinationFile + ".part";
                if (File.Exists(partFile))
                {
                    File.Delete(partFile);
                }
            }
            catch (Exception ex)
            {
                _log.Write(LogLevel.Warning, $"Failed to delete file '{job.DestinationFile}': {ex.Message}");
                StatusText = $"Не удалось удалить файл: {ex.Message}";
            }
        }

        DeleteJob(job);
    }

    [RelayCommand]
    private void ClearFinished()
    {
        for (var i = Jobs.Count - 1; i >= 0; i--)
        {
            var stage = Jobs[i].Stage;
            if (stage is DownloadStage.Completed or DownloadStage.Canceled)
            {
                Jobs[i].PropertyChanged -= OnJobPropertyChanged;
                Jobs.RemoveAt(i);
            }
        }

        UpdateStatus();
    }

    // Command aliases to match any naming variation used across the app / specifications
    public IRelayCommand<DownloadJob?> ShowJobFolderCommand => OpenJobLocationCommand;
    public IRelayCommand<DownloadJob?> RemoveJobCommand => DeleteJobCommand;
    public IRelayCommand ClearCompletedCommand => ClearFinishedCommand;
    public IRelayCommand OpenFolderCommand => OpenOutputFolderCommand;
    public IAsyncRelayCommand MassImportCommand => BulkImportLinksCommand;
    public IAsyncRelayCommand UpdateComponentsCommand => UpdateToolsCommand;

    [RelayCommand]
    private async Task RecheckToolsAsync() => await RefreshToolsAsync();

    [RelayCommand]
    private async Task UpdateToolsAsync() => await UpdateToolsInternalAsync(isAutomatic: false);

    public async Task UpdateToolsInternalAsync(bool isAutomatic)
    {
        if (IsUpdatingTools)
        {
            return;
        }

        if (!_lastToolLocation.HasYtDlp)
        {
            StatusText = Loc.T(LocKeys.ToolsNotFound);
            return;
        }

        IsUpdatingTools = true;
        StatusText = Loc.T(LocKeys.ToolsUpdating);

        try
        {
            var result = await _toolUpdater.UpdateAsync(_lastToolLocation, CancellationToken.None);

            if (result.Status == ToolUpdateStatus.Updated)
            {
                StatusText = result.Message;
                await RefreshToolsAsync();
            }
            else if (result.Status == ToolUpdateStatus.UpToDate)
            {
                StatusText = isAutomatic ? StatusText : result.Message;
            }
            else
            {
                StatusText = result.Message;
            }
        }
        catch (Exception ex)
        {
            _log.Write(LogLevel.Error, $"Update components failed: {ex.Message}");
            StatusText = Loc.T(LocKeys.ToolsUpdateFailedFormat, ex.Message);
        }
        finally
        {
            IsUpdatingTools = false;
        }
    }

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
