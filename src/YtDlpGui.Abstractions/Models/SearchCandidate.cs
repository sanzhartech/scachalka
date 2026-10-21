namespace YtDlpGui.Abstractions.Models;

/// <summary>Where a search candidate came from.</summary>
public enum SearchSource
{
    YouTube,
    YouTubeMusic
}

/// <summary>One search result returned by yt-dlp for a library song.</summary>
public sealed record SearchCandidate(
    string Id,
    string Url,
    string Title,
    string Channel,
    double? DurationSeconds,
    long? ViewCount,
    bool IsVerifiedChannel,
    SearchSource Source);
