using System.Diagnostics;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;

namespace YtDlpGui.Infrastructure.FileSystem;

/// <summary>Opens Explorer windows for the "open destination folder" features.</summary>
public sealed class FolderService(ILogSink log) : IFolderService
{
    /// <summary>Custom explorer launcher delegate for testing or mocking.</summary>
    public Func<string, bool>? ExplorerLauncher { get; set; }

    public bool OpenFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            log.Write(LogLevel.Warning, "Cannot open folder — path is empty.");
            return false;
        }

        try
        {
            var cleanPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath.Trim().Trim('"')));
            if (!Directory.Exists(cleanPath))
            {
                log.Write(LogLevel.Warning, $"Cannot open folder — it does not exist: {cleanPath}");
                return false;
            }

            return StartExplorer($"\"{cleanPath}\"");
        }
        catch (Exception ex)
        {
            log.Write(LogLevel.Error, $"OpenFolder failed for '{folderPath}': {ex.Message}");
            return false;
        }
    }

    public bool RevealFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            log.Write(LogLevel.Warning, "Cannot reveal file — path is empty.");
            return false;
        }

        try
        {
            var cleanPath = Path.GetFullPath(filePath.Trim().Trim('"'));
            if (cleanPath.StartsWith(@"\\?\", StringComparison.Ordinal))
            {
                cleanPath = cleanPath[4..];
            }

            if (File.Exists(cleanPath))
            {
                return StartExplorer($"/select,\"{cleanPath}\"");
            }

            // File may have been moved/renamed by post-processing — fall back to its folder.
            var folder = Path.GetDirectoryName(cleanPath);
            return !string.IsNullOrWhiteSpace(folder) && OpenFolder(folder);
        }
        catch (Exception ex)
        {
            log.Write(LogLevel.Error, $"RevealFile failed for '{filePath}': {ex.Message}");
            return false;
        }
    }

    private bool StartExplorer(string arguments)
    {
        if (ExplorerLauncher is not null)
        {
            return ExplorerLauncher(arguments);
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = arguments,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            log.Write(LogLevel.Error, $"Failed to open Explorer with args '{arguments}': {ex.Message}");
            return false;
        }
    }
}
