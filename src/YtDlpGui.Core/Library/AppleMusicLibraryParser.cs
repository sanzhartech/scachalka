using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Core.Library;

/// <summary>
/// Parses Apple Music / iTunes "Export as plain text" playlist files.
/// Supported shapes, detected automatically:
///  - tab-separated with a header row (columns mapped by name, EN + RU headers),
///  - tab-separated without a header (classic iTunes column order),
///  - plain "Artist - Title" lines as a last-resort fallback.
/// Unknown columns are ignored; duplicates are collapsed case-insensitively.
/// </summary>
public sealed class AppleMusicLibraryParser : ILibraryParser
{
    // Classic iTunes plain-text export column order (used when there is no header row):
    // Name, Artist, Composer, Album, Grouping, Genre, Size, Time, ...
    private const int DefaultTitleColumn = 0;
    private const int DefaultArtistColumn = 1;
    private const int DefaultAlbumColumn = 3;
    private const int DefaultTimeColumn = 7;

    private static readonly string[] TitleHeaders = ["name", "title", "song", "имя", "название"];
    private static readonly string[] ArtistHeaders = ["artist", "артист", "исполнитель"];
    private static readonly string[] AlbumHeaders = ["album", "альбом"];
    private static readonly string[] TimeHeaders = ["time", "duration", "время"];

    public IReadOnlyList<LibrarySong> Parse(string fileContent)
    {
        if (string.IsNullOrWhiteSpace(fileContent))
        {
            return [];
        }

        var lines = fileContent
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
        {
            return [];
        }

        var songs = lines[0].Contains('\t')
            ? ParseTabSeparated(lines)
            : ParseArtistDashTitle(lines);

        return Deduplicate(songs);
    }

    private static List<LibrarySong> ParseTabSeparated(List<string> lines)
    {
        var firstCells = SplitCells(lines[0]);
        var map = TryMapHeader(firstCells);
        var startIndex = map is null ? 0 : 1;
        map ??= new ColumnMap(DefaultTitleColumn, DefaultArtistColumn, DefaultAlbumColumn, DefaultTimeColumn);

        var songs = new List<LibrarySong>(lines.Count);
        for (var i = startIndex; i < lines.Count; i++)
        {
            var cells = SplitCells(lines[i]);
            var title = CellAt(cells, map.Title);
            if (string.IsNullOrEmpty(title))
            {
                continue;
            }

            songs.Add(new LibrarySong(
                title,
                CellAt(cells, map.Artist) ?? string.Empty,
                CellAt(cells, map.Album),
                ParseDuration(CellAt(cells, map.Time))));
        }

        return songs;
    }

    /// <summary>Fallback for hand-made lists: one "Artist - Title" per line.</summary>
    private static List<LibrarySong> ParseArtistDashTitle(List<string> lines)
    {
        var songs = new List<LibrarySong>(lines.Count);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var separator = trimmed.IndexOf(" - ", StringComparison.Ordinal);
            if (separator > 0 && separator + 3 < trimmed.Length)
            {
                songs.Add(new LibrarySong(
                    trimmed[(separator + 3)..].Trim(),
                    trimmed[..separator].Trim()));
            }
            else
            {
                // No artist part — search by title alone.
                songs.Add(new LibrarySong(trimmed, string.Empty));
            }
        }

        return songs;
    }

    /// <summary>Returns a column map when the row looks like a header, otherwise null.</summary>
    private static ColumnMap? TryMapHeader(string[] cells)
    {
        int title = -1, artist = -1, album = -1, time = -1;
        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i].Trim().ToLowerInvariant();
            if (title < 0 && TitleHeaders.Contains(cell))
            {
                title = i;
            }
            else if (artist < 0 && ArtistHeaders.Contains(cell))
            {
                artist = i;
            }
            else if (album < 0 && AlbumHeaders.Contains(cell))
            {
                album = i;
            }
            else if (time < 0 && TimeHeaders.Contains(cell))
            {
                time = i;
            }
        }

        // A real header names at least the two mandatory columns.
        return title >= 0 && artist >= 0
            ? new ColumnMap(title, artist, album, time)
            : null;
    }

    private static string[] SplitCells(string line) => line.Split('\t');

    private static string? CellAt(string[] cells, int index)
    {
        if (index < 0 || index >= cells.Length)
        {
            return null;
        }

        var value = cells[index].Trim();
        return value.Length == 0 ? null : value;
    }

    /// <summary>Accepts plain seconds ("241") or clock notation ("4:01", "1:02:03").</summary>
    private static int? ParseDuration(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        if (int.TryParse(raw, out var seconds))
        {
            return seconds > 0 ? seconds : null;
        }

        var parts = raw.Split(':');
        if (parts.Length is < 2 or > 3)
        {
            return null;
        }

        var total = 0;
        foreach (var part in parts)
        {
            if (!int.TryParse(part.Trim(), out var value) || value < 0)
            {
                return null;
            }

            total = total * 60 + value;
        }

        return total > 0 ? total : null;
    }

    private static List<LibrarySong> Deduplicate(List<LibrarySong> songs)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new List<LibrarySong>(songs.Count);
        foreach (var song in songs)
        {
            if (seen.Add($"{song.Artist}|{song.Title}"))
            {
                unique.Add(song);
            }
        }

        return unique;
    }

    private sealed record ColumnMap(int Title, int Artist, int Album, int Time);
}
