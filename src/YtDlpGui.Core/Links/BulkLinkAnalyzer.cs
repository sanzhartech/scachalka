using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Core.Links;

/// <summary>
/// Validates and deduplicates a batch of extracted links for the bulk importer.
/// Rules, applied in order per link: missing URL → malformed URL → non-YouTube
/// domain → duplicate (the same video via youtu.be / watch?v= / music.youtube.com
/// counts as one). Valid URLs keep their original file order.
/// </summary>
public sealed class BulkLinkAnalyzer(IUrlValidator urlValidator)
{
    /// <summary>Accepted hosts after stripping a leading "www.".</summary>
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube.com",
        "m.youtube.com",
        "music.youtube.com",
        "youtu.be"
    };

    public BulkLinkAnalysis Analyze(IReadOnlyList<ExtractedLink> links)
    {
        ArgumentNullException.ThrowIfNull(links);

        var valid = new List<string>(links.Count);
        var rejected = new List<RejectedLink>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = 0;

        foreach (var link in links)
        {
            var reason = Inspect(link, seen);
            if (reason is null)
            {
                valid.Add(link.Url!.Trim());
            }
            else
            {
                rejected.Add(new RejectedLink(link, reason.Value));
                if (reason == LinkRejectReason.Duplicate)
                {
                    duplicates++;
                }
            }
        }

        return new BulkLinkAnalysis(valid, rejected, duplicates, rejected.Count - duplicates);
    }

    /// <summary>Null when the link is valid and new; otherwise the reject reason.</summary>
    private LinkRejectReason? Inspect(ExtractedLink link, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(link.Url))
        {
            return LinkRejectReason.MissingUrl;
        }

        var url = link.Url.Trim();
        if (!urlValidator.IsValid(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return LinkRejectReason.InvalidUrl;
        }

        if (!AllowedHosts.Contains(StripWww(uri.Host)))
        {
            return LinkRejectReason.UnsupportedDomain;
        }

        return seen.Add(DedupeKey(uri)) ? null : LinkRejectReason.Duplicate;
    }

    /// <summary>
    /// Same video, one key: the video id when extractable (ids are case-sensitive),
    /// otherwise the normalized host + path + query.
    /// </summary>
    private static string DedupeKey(Uri uri)
    {
        var videoId = TryGetVideoId(uri);
        return videoId is not null
            ? $"v:{videoId}"
            : $"{StripWww(uri.Host).ToLowerInvariant()}{uri.AbsolutePath}{uri.Query}";
    }

    private static string? TryGetVideoId(Uri uri)
    {
        var host = StripWww(uri.Host);

        if (host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            return FirstSegment(uri.AbsolutePath);
        }

        if (!host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // /watch?v=ID (also music.youtube.com/watch?v=ID).
        var fromQuery = QueryValue(uri.Query, "v");
        if (fromQuery is not null)
        {
            return fromQuery;
        }

        // /shorts/ID, /live/ID, /embed/ID.
        var path = uri.AbsolutePath;
        foreach (var prefix in (string[])["/shorts/", "/live/", "/embed/"])
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return FirstSegment(path[prefix.Length..]);
            }
        }

        return null;
    }

    private static string? QueryValue(string query, string name)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq > 0 && pair[..eq].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                var value = pair[(eq + 1)..];
                return value.Length > 0 ? Uri.UnescapeDataString(value) : null;
            }
        }

        return null;
    }

    private static string? FirstSegment(string path)
    {
        var segment = path.Trim('/').Split('/')[0];
        return segment.Length > 0 ? segment : null;
    }

    private static string StripWww(string host) =>
        host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
}
