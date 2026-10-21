using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Abstractions.Interfaces;

/// <summary>
/// Parses an exported music library text file (one song per entry) into
/// <see cref="LibrarySong"/> records. Implementations must tolerate unknown
/// columns, mixed encodings and duplicate entries.
/// </summary>
public interface ILibraryParser
{
    IReadOnlyList<LibrarySong> Parse(string fileContent);
}
