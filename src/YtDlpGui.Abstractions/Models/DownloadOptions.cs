namespace YtDlpGui.Abstractions.Models;

/// <summary>
/// Advanced yt-dlp options captured per download. The GUI never restricts yt-dlp:
/// anything not covered by a dedicated toggle can be passed via <see cref="CustomArguments"/>.
/// </summary>
public sealed record DownloadOptions(
    bool AllowPlaylists,
    bool EmbedMetadata,
    bool EmbedThumbnail,
    bool EmbedSubtitles,
    string SubtitleLanguages,
    string CookiesFromBrowser,
    string CustomArguments)
{
    public static DownloadOptions Default { get; } =
        new(false, false, false, false, string.Empty, string.Empty, string.Empty);
}
