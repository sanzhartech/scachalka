namespace YtDlpGui.Abstractions.Enums;

/// <summary>Capability and mapping helpers shared by the UI and the argument builders.</summary>
public static class MediaFormatExtensions
{
    public static bool IsVideo(this MediaFormat format) =>
        format is MediaFormat.Mp4 or MediaFormat.Mkv;

    public static bool IsAudio(this MediaFormat format) => !format.IsVideo();

    /// <summary>Value for yt-dlp's --merge-output-format (video formats only).</summary>
    public static string MergeContainer(this MediaFormat format) => format switch
    {
        MediaFormat.Mp4 => "mp4",
        MediaFormat.Mkv => "mkv",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Not a video container.")
    };

    /// <summary>Value for yt-dlp's --audio-format (audio formats only).</summary>
    public static string AudioFormat(this MediaFormat format) => format switch
    {
        MediaFormat.Mp3 => "mp3",
        MediaFormat.M4a => "m4a",
        MediaFormat.Opus => "opus",
        MediaFormat.Flac => "flac",
        MediaFormat.Wav => "wav",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Not an audio format.")
    };

    /// <summary>WAV (RIFF) has no standard cover-art support — skip --embed-thumbnail for it.</summary>
    public static bool SupportsThumbnail(this MediaFormat format) => format != MediaFormat.Wav;

    public static string ToDisplay(this MediaFormat format) => format switch
    {
        MediaFormat.Mp4 => "MP4 (video)",
        MediaFormat.Mkv => "MKV (video)",
        MediaFormat.Mp3 => "MP3 (audio)",
        MediaFormat.M4a => "M4A (audio)",
        MediaFormat.Opus => "OPUS (audio)",
        MediaFormat.Flac => "FLAC (audio)",
        MediaFormat.Wav => "WAV (audio)",
        _ => format.ToString()
    };

    public static string ToShortDisplay(this MediaFormat format) =>
        format.ToString().ToUpperInvariant();
}
