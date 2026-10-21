using System.Text;
using System.Text.Json;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Models;
using YtDlpGui.Application.Tools;

namespace YtDlpGui.Application.Import;

/// <summary>
/// Searches YouTube ("ytsearchN:") and YouTube Music (music.youtube.com search URL)
/// through yt-dlp with --flat-playlist -J: one fast metadata-only process per source,
/// no media download. Results from both sources are merged and deduplicated by video id.
/// A failed source degrades gracefully to the other one.
/// </summary>
public sealed class YtDlpMusicSearchService(
    IMediaToolRunner runner,
    ToolContext toolContext,
    ILogSink log) : IMusicSearchService
{
    /// <summary>How many candidates to retrieve per source; accuracy needs choice, not just the first hit.</summary>
    private const int ResultsPerSource = 10;

    public async Task<IReadOnlyList<SearchCandidate>> SearchAsync(
        LibrarySong song, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(song);

        var tools = toolContext.Current;
        if (!tools.HasYtDlp)
        {
            return [];
        }

        var query = song.DisplayName;
        var youTubeTask = SearchOneSourceAsync(
            tools.YtDlpPath!, $"ytsearch{ResultsPerSource}:{query}", SearchSource.YouTube, cancellationToken);
        var musicTask = SearchOneSourceAsync(
            tools.YtDlpPath!,
            $"https://music.youtube.com/search?q={Uri.EscapeDataString(query)}",
            SearchSource.YouTubeMusic,
            cancellationToken);

        await Task.WhenAll(youTubeTask, musicTask).ConfigureAwait(false);

        return Merge(youTubeTask.Result, musicTask.Result);
    }

    private async Task<List<SearchCandidate>> SearchOneSourceAsync(
        string ytDlpPath, string target, SearchSource source, CancellationToken cancellationToken)
    {
        var args = new List<string>
        {
            "-J",
            "--flat-playlist",
            "--no-warnings",
            "--ignore-config",
            "--color", "never",
            // Music search URLs can return long shelves — cap them like ytsearchN.
            "-I", $"1:{ResultsPerSource}",
            "--",
            target
        };

        var stdout = new StringBuilder();
        try
        {
            var result = await runner.RunAsync(
                    ytDlpPath, args, line => stdout.AppendLine(line), null, cancellationToken)
                .ConfigureAwait(false);

            if (result.WasCanceled)
            {
                return [];
            }

            if (result.ExitCode != 0)
            {
                log.Write(LogLevel.Warning, $"Search failed ({source}) for \"{target}\": exit {result.ExitCode}.");
                return [];
            }

            return ParseCandidates(stdout.ToString(), source);
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception ex)
        {
            log.Write(LogLevel.Warning, $"Search error ({source}) for \"{target}\": {ex.Message}");
            return [];
        }
    }

    /// <summary>Parses the -J document: {"entries": [{id,title,channel,duration,…}, …]}.</summary>
    private List<SearchCandidate> ParseCandidates(string json, SearchSource source)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var candidates = new List<SearchCandidate>(ResultsPerSource);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("entries", out var entries)
                || entries.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            foreach (var entry in entries.EnumerateArray())
            {
                var candidate = ParseEntry(entry, source);
                if (candidate is not null)
                {
                    candidates.Add(candidate);
                }
            }
        }
        catch (JsonException ex)
        {
            log.Write(LogLevel.Warning, $"Could not parse search results ({source}): {ex.Message}");
        }

        return candidates;
    }

    private static SearchCandidate? ParseEntry(JsonElement entry, SearchSource source)
    {
        var id = GetString(entry, "id");
        var title = GetString(entry, "title");
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(title))
        {
            return null;
        }

        // Music search shelves also contain albums/playlists/channels — videos only.
        var url = GetString(entry, "url");
        var isVideo = id.Length == 11 || (url?.Contains("watch?v=", StringComparison.Ordinal) ?? false);
        if (!isVideo)
        {
            return null;
        }

        var channel = GetString(entry, "channel") ?? GetString(entry, "uploader") ?? string.Empty;

        return new SearchCandidate(
            id,
            $"https://www.youtube.com/watch?v={id}",
            title,
            channel,
            GetDouble(entry, "duration"),
            GetLong(entry, "view_count"),
            GetBool(entry, "channel_is_verified"),
            source);
    }

    /// <summary>Merges both sources by video id; a hit in YouTube Music upgrades the source tag.</summary>
    private static List<SearchCandidate> Merge(List<SearchCandidate> youTube, List<SearchCandidate> music)
    {
        var byId = new Dictionary<string, SearchCandidate>(StringComparer.Ordinal);
        foreach (var candidate in youTube.Concat(music))
        {
            if (!byId.TryGetValue(candidate.Id, out var existing))
            {
                byId[candidate.Id] = candidate;
                continue;
            }

            // Keep the richer record, but remember it also surfaced on YouTube Music.
            var richer = existing.DurationSeconds is null && candidate.DurationSeconds is not null
                ? candidate
                : existing;
            if (existing.Source == SearchSource.YouTubeMusic || candidate.Source == SearchSource.YouTubeMusic)
            {
                richer = richer with { Source = SearchSource.YouTubeMusic };
            }

            byId[candidate.Id] = richer;
        }

        return [.. byId.Values];
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? GetDouble(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    private static long? GetLong(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt64()
            : null;

    private static bool GetBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
