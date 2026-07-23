namespace YtDlpGui.Abstractions.Interfaces;

/// <summary>Removes leftover partial-download artifacts (*.part, *.ytdl) after a canceled job.</summary>
public interface IPartialFileCleaner
{
    /// <returns>Number of files removed (best effort — IO errors are swallowed and logged by the caller).</returns>
    int CleanUp(string folderPath, DateTimeOffset modifiedSince);
}
