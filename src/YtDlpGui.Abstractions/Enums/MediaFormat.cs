namespace YtDlpGui.Abstractions.Enums;

/// <summary>Target container/codec family for a download.</summary>
public enum MediaFormat
{
    // Video containers (merged via ffmpeg).
    Mp4,
    Mkv,

    // Audio formats (extracted and converted via ffmpeg).
    Mp3,
    M4a,
    Opus,
    Flac,
    Wav
}
