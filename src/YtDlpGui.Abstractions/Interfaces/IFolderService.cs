namespace YtDlpGui.Abstractions.Interfaces;

/// <summary>Windows Explorer integration for the "open destination" features.</summary>
public interface IFolderService
{
    /// <summary>Opens the folder in Explorer. Returns false when the folder does not exist or Explorer failed to start.</summary>
    bool OpenFolder(string folderPath);

    /// <summary>Opens Explorer with the given file pre-selected. Falls back to opening its folder.</summary>
    bool RevealFile(string filePath);
}
