namespace YtDlpGui.Abstractions.Models;

/// <summary>How the winning candidate was classified (best category it satisfies).</summary>
public enum MatchKind
{
    None,
    OfficialMusicTrack,
    OfficialAudio,
    OfficialMusicVideo,
    Vevo,
    VerifiedChannel,
    HighConfidence
}

/// <summary>
/// The outcome of matching one library song against its search candidates:
/// the chosen candidate (if any), a 0–100 confidence score and the match category.
/// </summary>
public sealed record SongMatch(
    LibrarySong Song,
    SearchCandidate? Candidate,
    int Confidence,
    MatchKind Kind)
{
    public bool HasCandidate => Candidate is not null;

    public static SongMatch NotFound(LibrarySong song) => new(song, null, 0, MatchKind.None);
}
