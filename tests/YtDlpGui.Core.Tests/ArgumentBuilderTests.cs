using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Models;
using YtDlpGui.Core.Arguments;

namespace YtDlpGui.Core.Tests;

public sealed class ArgumentBuilderTests
{
    private static readonly ToolLocation Tools = new(
        @"C:\tools\yt-dlp.exe", "2025.01.01", @"C:\tools\ffmpeg.exe", "ffmpeg version 7.1");

    private static DownloadRequest Request(
        MediaFormat format,
        VideoQuality quality = VideoQuality.Best,
        DownloadOptions? options = null) =>
        new("https://youtu.be/abc", format, quality, @"C:\Downloads", options);

    [Fact]
    public void VideoBuilder_HandlesVideoFormatsOnly()
    {
        var sut = new VideoArgumentBuilder();

        Assert.True(sut.CanBuild(Request(MediaFormat.Mp4)));
        Assert.True(sut.CanBuild(Request(MediaFormat.Mkv)));
        Assert.False(sut.CanBuild(Request(MediaFormat.Mp3)));
        Assert.False(sut.CanBuild(Request(MediaFormat.Flac)));
    }

    [Fact]
    public void AudioBuilder_HandlesAudioFormatsOnly()
    {
        var sut = new AudioArgumentBuilder();

        Assert.True(sut.CanBuild(Request(MediaFormat.Mp3)));
        Assert.True(sut.CanBuild(Request(MediaFormat.Opus)));
        Assert.True(sut.CanBuild(Request(MediaFormat.Wav)));
        Assert.False(sut.CanBuild(Request(MediaFormat.Mp4)));
    }

    [Fact]
    public void VideoBuilder_QualityCapAppearsInFormatSelector()
    {
        var sut = new VideoArgumentBuilder();

        var args = sut.Build(Request(MediaFormat.Mp4, VideoQuality.Q1080), Tools);

        var list = args.ToList();
        Assert.Contains("height<=?1080", list[list.IndexOf("-f") + 1]);
        Assert.Equal("mp4", list[list.IndexOf("--merge-output-format") + 1]);
    }

    [Fact]
    public void VideoBuilder_MkvUsesMkvContainer()
    {
        var sut = new VideoArgumentBuilder();

        var args = sut.Build(Request(MediaFormat.Mkv), Tools).ToList();

        Assert.Equal("mkv", args[args.IndexOf("--merge-output-format") + 1]);
    }

    [Theory]
    [InlineData(MediaFormat.Mp3, "mp3")]
    [InlineData(MediaFormat.M4a, "m4a")]
    [InlineData(MediaFormat.Opus, "opus")]
    [InlineData(MediaFormat.Flac, "flac")]
    [InlineData(MediaFormat.Wav, "wav")]
    public void AudioBuilder_MapsAudioFormat(MediaFormat format, string expected)
    {
        var sut = new AudioArgumentBuilder();

        var args = sut.Build(Request(format), Tools).ToList();

        Assert.Contains("--extract-audio", args);
        Assert.Equal(expected, args[args.IndexOf("--audio-format") + 1]);
    }

    [Fact]
    public void Build_UrlIsLastAfterOptionTerminator()
    {
        var sut = new VideoArgumentBuilder();
        var request = Request(MediaFormat.Mp4);

        var args = sut.Build(request, Tools);

        Assert.Equal(request.Url, args[^1]);
        Assert.Equal("--", args[^2]);
    }

    [Fact]
    public void Build_DefaultOptions_NoPlaylistAndNoEmbeds()
    {
        var sut = new VideoArgumentBuilder();

        var args = sut.Build(Request(MediaFormat.Mp4), Tools);

        Assert.Contains("--no-playlist", args);
        Assert.DoesNotContain("--embed-metadata", args);
        Assert.DoesNotContain("--embed-thumbnail", args);
        Assert.DoesNotContain("--cookies-from-browser", args);
    }

    [Fact]
    public void Build_PlaylistOption_SwitchesTemplateAndFlag()
    {
        var sut = new VideoArgumentBuilder();
        var options = DownloadOptions.Default with { AllowPlaylists = true };

        var args = sut.Build(Request(MediaFormat.Mp4, options: options), Tools).ToList();

        Assert.Contains("--yes-playlist", args);
        Assert.DoesNotContain("--no-playlist", args);
        Assert.Contains("playlist_title", args[args.IndexOf("--output") + 1]);
    }

    [Fact]
    public void Build_EmbedOptions_AppearForVideo()
    {
        var sut = new VideoArgumentBuilder();
        var options = DownloadOptions.Default with
        {
            EmbedMetadata = true,
            EmbedThumbnail = true,
            EmbedSubtitles = true,
            SubtitleLanguages = "en,ru"
        };

        var args = sut.Build(Request(MediaFormat.Mp4, options: options), Tools).ToList();

        Assert.Contains("--embed-metadata", args);
        Assert.Contains("--embed-chapters", args);
        Assert.Contains("--embed-thumbnail", args);
        Assert.Contains("--embed-subs", args);
        Assert.Equal("en,ru", args[args.IndexOf("--sub-langs") + 1]);
    }

    [Fact]
    public void Build_SubtitlesIgnoredForAudio_ThumbnailIgnoredForWav()
    {
        var sut = new AudioArgumentBuilder();
        var options = DownloadOptions.Default with { EmbedSubtitles = true, EmbedThumbnail = true };

        var wavArgs = sut.Build(Request(MediaFormat.Wav, options: options), Tools);
        var mp3Args = sut.Build(Request(MediaFormat.Mp3, options: options), Tools);

        Assert.DoesNotContain("--embed-subs", wavArgs);
        Assert.DoesNotContain("--embed-thumbnail", wavArgs);
        Assert.Contains("--embed-thumbnail", mp3Args);
    }

    [Fact]
    public void Build_CookiesAndCustomArguments_ArePassedThrough()
    {
        var sut = new VideoArgumentBuilder();
        var options = DownloadOptions.Default with
        {
            CookiesFromBrowser = "firefox",
            CustomArguments = "--limit-rate 2M --proxy \"socks5://127.0.0.1:1080\""
        };

        var args = sut.Build(Request(MediaFormat.Mp4, options: options), Tools).ToList();

        Assert.Equal("firefox", args[args.IndexOf("--cookies-from-browser") + 1]);
        Assert.Equal("2M", args[args.IndexOf("--limit-rate") + 1]);
        Assert.Equal("socks5://127.0.0.1:1080", args[args.IndexOf("--proxy") + 1]);
        // Custom arguments must stay before the "--" terminator.
        Assert.True(args.IndexOf("--limit-rate") < args.IndexOf("--"));
    }

    [Fact]
    public void Build_IncludesSafetyAndProgressFlags()
    {
        var sut = new VideoArgumentBuilder();

        var args = sut.Build(Request(MediaFormat.Mp4), Tools);

        Assert.Contains("--newline", args);
        Assert.Contains("--ignore-config", args);
        Assert.Contains("--windows-filenames", args);
        Assert.Contains("--no-overwrites", args);
        Assert.Contains(ProgressTemplates.Download, args);
        Assert.Contains(ProgressTemplates.Postprocess, args);
        Assert.Contains(Tools.FfmpegPath!, args);
    }
}
