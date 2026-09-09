namespace YtDlpGui.Abstractions.Enums;

/// <summary>Display and mapping helpers shared by the UI and the argument builders.</summary>
public static class VideoQualityExtensions
{
    /// <summary>Maximum pixel height for the quality, or null for "Best" (no cap).</summary>
    public static int? MaxHeight(this VideoQuality quality) => quality switch
    {
        VideoQuality.Q2160 => 2160,
        VideoQuality.Q1440 => 1440,
        VideoQuality.Q1080 => 1080,
        VideoQuality.Q720 => 720,
        VideoQuality.Q480 => 480,
        VideoQuality.Q360 => 360,
        _ => null
    };

    public static string ToDisplay(this VideoQuality quality) => quality switch
    {
        VideoQuality.Best => "Best available",
        VideoQuality.Q2160 => "4K (2160p)",
        VideoQuality.Q1440 => "1440p",
        VideoQuality.Q1080 => "1080p",
        VideoQuality.Q720 => "720p",
        VideoQuality.Q480 => "480p",
        VideoQuality.Q360 => "360p",
        _ => quality.ToString()
    };

    public static string ToAudioDisplay(this VideoQuality quality) => quality switch
    {
        VideoQuality.Best => "320 kbps",
        VideoQuality.Q1440 => "256 kbps",
        VideoQuality.Q1080 => "192 kbps",
        VideoQuality.Q720 => "128 kbps",
        _ => "320 kbps"
    };

    public static string ToShortDisplay(this VideoQuality quality) => quality switch
    {
        VideoQuality.Best => "Best",
        VideoQuality.Q2160 => "4K",
        VideoQuality.Q1440 => "1440p",
        VideoQuality.Q1080 => "1080p",
        VideoQuality.Q720 => "720p",
        VideoQuality.Q480 => "480p",
        VideoQuality.Q360 => "360p",
        _ => quality.ToString()
    };
}
