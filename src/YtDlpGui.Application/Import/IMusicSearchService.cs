using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Application.Import;

/// <summary>
/// Finds download candidates for a library song by querying yt-dlp's search
/// (YouTube and YouTube Music). Returns raw candidates; matching is done by
/// <see cref="Abstractions.Interfaces.ISongMatchScorer"/>.
/// </summary>
public interface IMusicSearchService
{
    Task<IReadOnlyList<SearchCandidate>> SearchAsync(LibrarySong song, CancellationToken cancellationToken);
}
