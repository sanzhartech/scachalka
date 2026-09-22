using YtDlpGui.Abstractions.Enums;

namespace YtDlpGui.Abstractions.Models;

/// <summary>User settings persisted between sessions as JSON.</summary>
public sealed class AppSettings
{
    public string? LastOutputFolder { get; set; }

    public MediaFormat PreferredFormat { get; set; } = MediaFormat.Mp4;

    public VideoQuality PreferredQuality { get; set; } = VideoQuality.Best;

    /// <summary>"Dark" or "Light".</summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>UI language code ("en", "ru"). Empty on first run → resolved from the OS.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>How many downloads may run at the same time (clamped to 1..4 at use site).</summary>
    public int MaxConcurrentDownloads { get; set; } = 2;

    /// <summary>Whether to check and update yt-dlp automatically on application startup.</summary>
    public bool AutoUpdateOnStartup { get; set; } = true;

    // Advanced yt-dlp options (persisted between sessions).

    public bool AllowPlaylists { get; set; }

    public bool EmbedMetadata { get; set; }

    public bool EmbedThumbnail { get; set; }

    public bool EmbedSubtitles { get; set; }

    /// <summary>Comma-separated subtitle languages for --sub-langs (empty = yt-dlp default).</summary>
    public string SubtitleLanguages { get; set; } = string.Empty;

    /// <summary>Browser name for --cookies-from-browser (empty = disabled).</summary>
    public string CookiesFromBrowser { get; set; } = string.Empty;

    /// <summary>Extra raw yt-dlp arguments appended to every download.</summary>
    public string CustomArguments { get; set; } = string.Empty;

    public static AppSettings CreateDefault() => new();
}
