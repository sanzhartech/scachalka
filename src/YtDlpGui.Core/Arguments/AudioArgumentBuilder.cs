using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Core.Arguments;

/// <summary>
/// Builds yt-dlp arguments for audio extraction (MP3, M4A, OPUS, FLAC, WAV):
/// best source audio, converted by ffmpeg at the highest quality setting.
/// </summary>
public sealed class AudioArgumentBuilder : ArgumentBuilderBase
{
    public override bool CanBuild(DownloadRequest request) => request.Format.IsAudio();

    protected override void AppendFormatArguments(List<string> args, DownloadRequest request)
    {
        args.Add("-f");
        args.Add("bestaudio/best");
        args.Add("--extract-audio");
        args.Add("--audio-format");
        args.Add(request.Format.AudioFormat());
        // 0 = best quality for lossy encoders; ignored for lossless (flac/wav).
        args.Add("--audio-quality");
        args.Add("0");
    }
}
