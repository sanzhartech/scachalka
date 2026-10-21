using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Abstractions.Interfaces;

/// <summary>
/// Chooses the best download candidate for a library song from a set of
/// search results, producing a 0–100 confidence score. Pure logic — no I/O.
/// </summary>
public interface ISongMatchScorer
{
    /// <summary>Minimum confidence required before a match is worth downloading.</summary>
    int MinimumConfidence { get; }

    /// <summary>Scores one candidate against the song (0–100).</summary>
    int Score(LibrarySong song, SearchCandidate candidate);

    /// <summary>Picks the highest-confidence candidate, or a no-match result when the list is empty.</summary>
    SongMatch ChooseBest(LibrarySong song, IReadOnlyList<SearchCandidate> candidates);
}
