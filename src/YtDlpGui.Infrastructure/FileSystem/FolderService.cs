using System.Diagnostics;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;

namespace YtDlpGui.Infrastructure.FileSystem;

/// <summary>Opens Explorer windows for the "open destination folder" features.</summary>
public sealed class FolderService(ILogSink log) : IFolderService
{
    public bool OpenFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            log.Write(LogLevel.Warning, $"Cannot open folder — it does not exist: {folderPath}");
            return false;
        }

        return StartExplorer($"\"{folderPath}\"");
    }

    public bool RevealFile(string filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            return StartExplorer($"/select,\"{filePath}\"");
        }

        // File may have been moved/renamed by post-processing — fall back to its folder.
        var folder = Path.GetDirectoryName(filePath);
        return folder is not null && OpenFolder(folder);
    }

    private bool StartExplorer(string arguments)
    {
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
            log.Write(LogLevel.Error, $"Failed to open Explorer: {ex.Message}");
            return false;
        }
    }
}
