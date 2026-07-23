using System.Globalization;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Core.Arguments;

/// <summary>
/// Builds yt-dlp arguments for video downloads (MP4, MKV) with an optional resolution cap.
/// MKV accepts any codec combination; MP4 remains the compatibility default.
/// </summary>
public sealed class VideoArgumentBuilder : ArgumentBuilderBase
{
    public override bool CanBuild(DownloadRequest request) => request.Format.IsVideo();

    protected override void AppendFormatArguments(List<string> args, DownloadRequest request)
    {
        args.Add("-f");
        args.Add(BuildFormatSelector(request.Quality));
        args.Add("--merge-output-format");
        args.Add(request.Format.MergeContainer());
    }

    private static string BuildFormatSelector(VideoQuality quality)
    {
        var height = quality.MaxHeight();
        if (height is null)
        {
            // Best video + best audio, single best file as fallback.
            return "bv*+ba/b";
        }

        var h = height.Value.ToString(CultureInfo.InvariantCulture);
        // "<=?" treats formats with unknown height as acceptable instead of failing.
        return $"bv*[height<=?{h}]+ba/b[height<=?{h}]";
    }
}
