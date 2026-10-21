using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Models;
using YtDlpGui.Core.Progress;

namespace YtDlpGui.Core.Tests;

public sealed class ProgressParserTests
{
    private readonly YtDlpProgressParser _sut = new();

    [Fact]
    public void TryParse_DownloadProgress_ComputesPercentSpeedEta()
    {
        var ok = _sut.TryParse("NDL|downloading|52428800|104857600|NA|1048576.5|50", out var evt);

        Assert.True(ok);
        Assert.Equal(YtDlpEventKind.Progress, evt.Kind);
        Assert.NotNull(evt.Progress);
        Assert.Equal(50, evt.Progress!.Percent!.Value, 1);
        Assert.Equal(1048576.5, evt.Progress.SpeedBytesPerSecond);
        Assert.Equal(50, evt.Progress.EtaSeconds);
    }

    [Fact]
    public void TryParse_NaFields_BecomeNull()
    {
        var ok = _sut.TryParse("NDL|downloading|1000|NA|NA|NA|NA", out var evt);

        Assert.True(ok);
        Assert.Null(evt.Progress!.Percent);
        Assert.Null(evt.Progress.SpeedBytesPerSecond);
        Assert.Null(evt.Progress.EtaSeconds);
    }

    [Fact]
    public void TryParse_FinishedStatus_Reports100Percent()
    {
        var ok = _sut.TryParse("NDL|finished|1000|1000|NA|NA|NA", out var evt);

        Assert.True(ok);
        Assert.Equal(100, evt.Progress!.Percent);
    }

    [Fact]
    public void TryParse_PostprocessMerger_SignalsMergingStage()
    {
        var ok = _sut.TryParse("NPP|started|Merger", out var evt);

        Assert.True(ok);
        Assert.Equal(DownloadStage.Merging, evt.Stage);
    }

    [Fact]
    public void TryParse_PostprocessExtractAudio_SignalsConvertingStage()
    {
        var ok = _sut.TryParse("NPP|started|ExtractAudio", out var evt);

        Assert.True(ok);
        Assert.Equal(DownloadStage.Converting, evt.Stage);
    }

    [Fact]
    public void TryParse_DestinationLine_ExtractsPath()
    {
        var ok = _sut.TryParse(@"[download] Destination: C:\Downloads\Видео [abc].mp4", out var evt);

        Assert.True(ok);
        Assert.Equal(YtDlpEventKind.DestinationResolved, evt.Kind);
        Assert.Equal(@"C:\Downloads\Видео [abc].mp4", evt.Path);
    }

    [Fact]
    public void TryParse_AlreadyDownloaded_ExtractsPath()
    {
        var ok = _sut.TryParse(@"[download] C:\Downloads\clip.mp4 has already been downloaded", out var evt);

        Assert.True(ok);
        Assert.Equal(YtDlpEventKind.AlreadyDownloaded, evt.Kind);
        Assert.Equal(@"C:\Downloads\clip.mp4", evt.Path);
    }

    [Fact]
    public void TryParse_FallbackMergerMarker_SignalsMergingAndExtractsPath()
    {
        var ok = _sut.TryParse("[Merger] Merging formats into \"C:\\x.mp4\"", out var evt);

        Assert.True(ok);
        Assert.Equal(DownloadStage.Merging, evt.Stage);
        Assert.Equal(@"C:\x.mp4", evt.Path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[youtube] Extracting URL: https://youtu.be/x")]
    [InlineData("random noise")]
    [InlineData("NDL|broken")]
    public void TryParse_UnknownLines_ReturnFalseWithoutThrowing(string line)
    {
        Assert.False(_sut.TryParse(line, out _));
    }
}
