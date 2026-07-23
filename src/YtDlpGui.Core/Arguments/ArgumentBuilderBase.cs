using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Core.Arguments;

/// <summary>
/// Shared yt-dlp arguments for every download strategy, including the advanced
/// options (playlists, embedding, cookies, custom arguments).
/// URLs are always passed after "--" and via ArgumentList (never a concatenated string),
/// which makes argument injection impossible. Custom user arguments come last so the
/// user can override any default — the GUI never restricts yt-dlp.
/// </summary>
public abstract class ArgumentBuilderBase : IArgumentBuilder
{
    // yt-dlp's built-in per-request retries; queue-level retry is handled by the app.
    private const string ToolRetries = "3";

    private const string SingleTemplate = "%(title).200B [%(id)s].%(ext)s";

    // Playlist items land in "<playlist title>/NN - <title> [id].ext"; single videos
    // keep the flat template because the conditional parts render empty.
    private const string PlaylistTemplate =
        "%(playlist_title&{}/|)s%(playlist_index&{} - |)s" + SingleTemplate;

    public abstract bool CanBuild(DownloadRequest request);

    public IReadOnlyList<string> Build(DownloadRequest request, ToolLocation tools)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(tools);

        var options = request.EffectiveOptions;
        var args = new List<string>
        {
            // One event per line, no ANSI noise — required by the progress parser.
            "--newline",
            "--color", "never",
            // The user's local yt-dlp config must not silently change our behavior.
            "--ignore-config",
            "--retries", ToolRetries,
            "--fragment-retries", ToolRetries,
            // Windows-safe file names while preserving Unicode titles.
            "--windows-filenames",
            "--no-overwrites",
            "--progress-template", ProgressTemplates.Download,
            "--progress-template", ProgressTemplates.Postprocess
        };

        args.Add(options.AllowPlaylists ? "--yes-playlist" : "--no-playlist");
        args.Add("--output");
        args.Add(Path.Combine(
            request.OutputFolder,
            options.AllowPlaylists ? PlaylistTemplate : SingleTemplate));

        if (!string.IsNullOrEmpty(tools.FfmpegPath))
        {
            args.Add("--ffmpeg-location");
            args.Add(tools.FfmpegPath);
        }

        AppendFormatArguments(args, request);
        AppendOptionArguments(args, request.Format, options);

        // User's raw arguments come last: yt-dlp's "last one wins" lets them override anything.
        args.AddRange(CommandLineTokenizer.Tokenize(options.CustomArguments));

        // "--" terminates option parsing; the URL can never be mistaken for a flag.
        args.Add("--");
        args.Add(request.Url);
        return args;
    }

    /// <summary>Adds the format/quality-specific part of the command line.</summary>
    protected abstract void AppendFormatArguments(List<string> args, DownloadRequest request);

    private static void AppendOptionArguments(List<string> args, MediaFormat format, DownloadOptions options)
    {
        if (options.EmbedMetadata)
        {
            args.Add("--embed-metadata");
            args.Add("--embed-chapters");
        }

        if (options.EmbedThumbnail && format.SupportsThumbnail())
        {
            args.Add("--embed-thumbnail");
        }

        // Subtitles only make sense inside video containers.
        if (options.EmbedSubtitles && format.IsVideo())
        {
            args.Add("--embed-subs");
            if (!string.IsNullOrWhiteSpace(options.SubtitleLanguages))
            {
                args.Add("--sub-langs");
                args.Add(options.SubtitleLanguages.Trim());
            }
        }

        if (!string.IsNullOrWhiteSpace(options.CookiesFromBrowser))
        {
            args.Add("--cookies-from-browser");
            args.Add(options.CookiesFromBrowser.Trim());
        }
    }
}
