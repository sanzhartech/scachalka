using System.Text;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Core.Links;

/// <summary>
/// Extracts links from CSV exports. The URL column is found by header name
/// (youtube_music_url / youtube_url / url / link — first match wins); all other
/// columns are ignored. Quoted fields (RFC 4180 style, including "" escapes) are
/// handled, and the delimiter (comma / semicolon / tab) is auto-detected.
/// Headerless files fall back to scanning every cell for an http(s) value.
/// </summary>
public sealed class CsvLinkSource : ILinkSource
{
    /// <summary>Recognized URL column names, in priority order.</summary>
    private static readonly string[] UrlColumns = ["youtube_music_url", "youtube_url", "url", "link"];

    private static readonly char[] CandidateDelimiters = [',', ';', '\t'];

    public string FormatName => "CSV";

    public bool CanExtract(string fileName, string content)
    {
        if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Content sniff: a first line that parses into cells with a known URL header.
        var firstLine = FirstLine(content);
        var delimiter = DetectDelimiter(firstLine);
        return delimiter is not null && FindUrlColumn(ParseLine(firstLine, delimiter.Value)) >= 0;
    }

    public IReadOnlyList<ExtractedLink> Extract(string content)
    {
        var lines = SplitLines(content);
        if (lines.Count == 0)
        {
            return [];
        }

        var delimiter = DetectDelimiter(lines[0]) ?? ',';
        var urlColumn = FindUrlColumn(ParseLine(lines[0], delimiter));
        var firstDataRow = urlColumn >= 0 ? 1 : 0;

        var links = new List<ExtractedLink>(lines.Count);
        for (var i = firstDataRow; i < lines.Count; i++)
        {
            var cells = ParseLine(lines[i], delimiter);
            var origin = $"row {i + 1}";

            var url = urlColumn >= 0
                ? CellAt(cells, urlColumn)
                : cells.FirstOrDefault(IsHttpUrl); // Headerless fallback: any http(s) cell.

            links.Add(new ExtractedLink(string.IsNullOrWhiteSpace(url) ? null : url!.Trim(), origin));
        }

        return links;
    }

    /// <summary>Index of the first recognized URL column in the header, or -1.</summary>
    private static int FindUrlColumn(List<string> header)
    {
        foreach (var column in UrlColumns)
        {
            for (var i = 0; i < header.Count; i++)
            {
                if (string.Equals(header[i].Trim().Trim('\uFEFF'), column, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>Picks the delimiter that appears most often in the header line.</summary>
    private static char? DetectDelimiter(string line)
    {
        char? best = null;
        var bestCount = 0;
        foreach (var candidate in CandidateDelimiters)
        {
            var count = line.Count(c => c == candidate);
            if (count > bestCount)
            {
                best = candidate;
                bestCount = count;
            }
        }

        return best;
    }

    /// <summary>Splits one CSV line honoring double-quoted fields with "" escapes.</summary>
    private static List<string> ParseLine(string line, char delimiter)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        cell.Append('"'); // Escaped quote inside a quoted field.
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == delimiter)
            {
                cells.Add(cell.ToString());
                cell.Clear();
            }
            else
            {
                cell.Append(ch);
            }
        }

        cells.Add(cell.ToString());
        return cells;
    }

    private static string? CellAt(List<string> cells, int index) =>
        index >= 0 && index < cells.Count ? cells[index] : null;

    private static bool IsHttpUrl(string cell)
    {
        var trimmed = cell.Trim();
        return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstLine(string content)
    {
        var end = content.IndexOf('\n');
        return (end < 0 ? content : content[..end]).TrimEnd('\r');
    }

    private static List<string> SplitLines(string content) =>
        content
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
}
