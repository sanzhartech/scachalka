using System.Globalization;
using System.Text;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Core.Matching;

/// <summary>
/// Scores yt-dlp search candidates against a library song (0–100) and picks the best.
/// The score combines title similarity, artist evidence, duration proximity and
/// official-source signals, and heavily penalizes altered versions (nightcore,
/// slowed, remix, cover, live, …) unless the library title itself contains the term.
/// Accuracy over speed: pure in-memory logic, no I/O.
/// </summary>
public sealed class SongMatchScorer : ISongMatchScorer
{
    // Weights (sum of maxima = 100).
    private const int TitleWeight = 40;
    private const int ArtistWeight = 25;
    private const int DurationWeight = 20;
    private const int OfficialWeight = 15;

    /// <summary>Below this confidence a match is considered too risky to download.</summary>
    public int MinimumConfidence => 55;

    /// <summary>Altered/derived versions that must never be downloaded automatically.</summary>
    private static readonly string[] HardBannedTerms =
    [
        "nightcore", "slowed", "reverb", "sped up", "speed up", "spedup",
        "bass boosted", "bassboosted", "8d audio", "8d"
    ];

    /// <summary>Different works: remixes, covers, live takes, AI versions, karaoke…</summary>
    private static readonly string[] SoftBannedTerms =
    [
        "remix", "mashup", "mash up", "cover", "karaoke", "instrumental",
        "fan edit", "fanmade", "fan made", "ai cover", "ai version", "ai generated",
        "live", "concert", "reupload", "re upload", "reaction", "parody", "tribute"
    ];

    /// <summary>Lyric videos have correct audio but the spec prefers official sources.</summary>
    private static readonly string[] LyricsTerms = ["lyrics", "lyric video", "lyric"];

    public int Score(LibrarySong song, SearchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentNullException.ThrowIfNull(candidate);

        var songTitleNorm = Normalize(song.Title);
        var artistNorm = Normalize(song.Artist);
        var candidateFullNorm = Normalize(candidate.Title);
        var candidateCoreNorm = Normalize(StripBrackets(candidate.Title));
        var channelNorm = Normalize(candidate.Channel);

        var score = TitleScore(songTitleNorm, artistNorm, candidateCoreNorm)
                    + ArtistScore(artistNorm, channelNorm, candidateFullNorm)
                    + DurationScore(song.DurationSeconds, candidate.DurationSeconds)
                    + OfficialScore(candidate, candidateFullNorm, channelNorm);

        score += BannedPenalty(songTitleNorm, candidateFullNorm);

        return Math.Clamp(score, 0, 100);
    }

    public SongMatch ChooseBest(LibrarySong song, IReadOnlyList<SearchCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentNullException.ThrowIfNull(candidates);

        SearchCandidate? best = null;
        var bestScore = -1;
        foreach (var candidate in candidates)
        {
            var score = Score(song, candidate);
            if (score > bestScore
                || (score == bestScore && (candidate.ViewCount ?? 0) > (best?.ViewCount ?? 0)))
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best is null
            ? SongMatch.NotFound(song)
            : new SongMatch(song, best, bestScore, ClassifyKind(best));
    }

    /// <summary>Names the strongest official signal of the winning candidate (for UI display).</summary>
    private static MatchKind ClassifyKind(SearchCandidate candidate)
    {
        var title = Normalize(candidate.Title);
        var channel = Normalize(candidate.Channel);

        if (IsTopicChannel(candidate.Channel) || candidate.Source == SearchSource.YouTubeMusic)
        {
            return MatchKind.OfficialMusicTrack;
        }

        if (title.Contains("official audio", StringComparison.Ordinal))
        {
            return MatchKind.OfficialAudio;
        }

        if (title.Contains("official video", StringComparison.Ordinal)
            || title.Contains("official music video", StringComparison.Ordinal))
        {
            return MatchKind.OfficialMusicVideo;
        }

        if (channel.EndsWith("vevo", StringComparison.Ordinal))
        {
            return MatchKind.Vevo;
        }

        return candidate.IsVerifiedChannel ? MatchKind.VerifiedChannel : MatchKind.HighConfidence;
    }

    /// <summary>0–40: how well the candidate title covers the song title tokens.</summary>
    private static int TitleScore(string songTitle, string artist, string candidateTitle)
    {
        var songTokens = Tokenize(songTitle);
        if (songTokens.Count == 0)
        {
            return 0;
        }

        var candidateTokens = Tokenize(candidateTitle);

        // Candidate titles are often "Artist - Title"; artist tokens are legitimate noise.
        var artistTokens = Tokenize(artist);
        var matched = songTokens.Count(t => candidateTokens.Contains(t));
        var coverage = (double)matched / songTokens.Count;

        // Penalize heavy unexplained noise (compilations, "top 50" mixes, etc.).
        var noise = candidateTokens.Count(t => !songTokens.Contains(t) && !artistTokens.Contains(t));
        var noisePenalty = noise > songTokens.Count + 4 ? 6 : 0;

        return Math.Max(0, (int)Math.Round(TitleWeight * coverage) - noisePenalty);
    }

    /// <summary>0–25: evidence that the upload belongs to the right artist.</summary>
    private static int ArtistScore(string artist, string channel, string candidateTitle)
    {
        var artistTokens = Tokenize(artist);
        if (artistTokens.Count == 0)
        {
            return ArtistWeight / 2; // Title-only library entry — neutral.
        }

        var channelCore = channel.EndsWith(" topic", StringComparison.Ordinal)
            ? channel[..^6].TrimEnd()
            : channel;

        // Official artist channel (exact or " - Topic" auto-channel).
        if (channelCore == artist)
        {
            return ArtistWeight;
        }

        var channelTokens = Tokenize(channelCore);
        if (artistTokens.All(channelTokens.Contains))
        {
            return ArtistWeight - 3;
        }

        var titleTokens = Tokenize(candidateTitle);
        if (artistTokens.All(titleTokens.Contains))
        {
            return 15;
        }

        var partial = artistTokens.Count(t => channelTokens.Contains(t) || titleTokens.Contains(t));
        return partial * 2 >= artistTokens.Count ? 8 : 0;
    }

    /// <summary>−15…20: duration proximity is the strongest cheap signal against wrong versions.</summary>
    private static int DurationScore(int? songSeconds, double? candidateSeconds)
    {
        if (songSeconds is null || candidateSeconds is null)
        {
            return DurationWeight / 2; // Unknown — neutral.
        }

        var diff = Math.Abs(candidateSeconds.Value - songSeconds.Value);
        return diff switch
        {
            <= 2 => DurationWeight,
            <= 5 => 16,
            <= 10 => 10,
            <= 20 => 4,
            <= 30 => 0,
            _ => -15 // Extended/shortened edit — almost certainly the wrong version.
        };
    }

    /// <summary>0–15: official-source signals, ordered by the priority ladder of the spec.</summary>
    private static int OfficialScore(SearchCandidate candidate, string title, string channel)
    {
        var bonus = 0;

        if (IsTopicChannel(candidate.Channel))
        {
            bonus += 15; // Auto-generated artist channel = the official YouTube Music track.
        }

        if (candidate.Source == SearchSource.YouTubeMusic)
        {
            bonus += 8;
        }

        if (channel.EndsWith("vevo", StringComparison.Ordinal))
        {
            bonus += 13;
        }

        if (title.Contains("official audio", StringComparison.Ordinal))
        {
            bonus += 12;
        }
        else if (title.Contains("official music video", StringComparison.Ordinal)
                 || title.Contains("official video", StringComparison.Ordinal))
        {
            bonus += 10;
        }

        if (candidate.IsVerifiedChannel)
        {
            bonus += 8;
        }

        return Math.Min(bonus, OfficialWeight);
    }

    /// <summary>
    /// Large negative penalty for altered versions. A term is only penalized when the
    /// library title itself does not contain it (a song legitimately named "Remix"
    /// must still be findable).
    /// </summary>
    private static int BannedPenalty(string songTitle, string candidateTitle)
    {
        var penalty = 0;

        // Penalties outweigh a perfect score elsewhere: even an exact-duration,
        // official-looking "(Nightcore)" upload must land below the threshold.
        if (ContainsAnyTerm(candidateTitle, HardBannedTerms, songTitle))
        {
            penalty -= 60;
        }

        if (ContainsAnyTerm(candidateTitle, SoftBannedTerms, songTitle))
        {
            penalty -= 50;
        }

        if (ContainsAnyTerm(candidateTitle, LyricsTerms, songTitle))
        {
            penalty -= 25;
        }

        return penalty;
    }

    private static bool ContainsAnyTerm(string text, string[] terms, string allowedSource)
    {
        var padded = $" {text} ";
        foreach (var term in terms)
        {
            if (padded.Contains($" {term} ", StringComparison.Ordinal)
                && !$" {allowedSource} ".Contains($" {term} ", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTopicChannel(string channel) =>
        channel.TrimEnd().EndsWith("- Topic", StringComparison.OrdinalIgnoreCase);

    /// <summary>Removes "(…)" and "[…]" groups: decorations like "(Official Audio)".</summary>
    private static string StripBrackets(string text)
    {
        var sb = new StringBuilder(text.Length);
        var depth = 0;
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '(' or '[':
                    depth++;
                    break;
                case ')' or ']':
                    depth = Math.Max(0, depth - 1);
                    break;
                default:
                    if (depth == 0)
                    {
                        sb.Append(ch);
                    }
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>Lowercase, strip diacritics and "feat." clauses, collapse punctuation to spaces.</summary>
    private static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue; // Diacritic — "beyoncé" and "beyonce" must match.
            }

            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        var normalized = sb.ToString();

        // "feat"/"ft" credits differ wildly between platforms — drop the marker word.
        var tokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t is not ("feat" or "ft" or "featuring"));

        return string.Join(' ', tokens);
    }

    private static HashSet<string> Tokenize(string normalizedText) =>
        normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
}
