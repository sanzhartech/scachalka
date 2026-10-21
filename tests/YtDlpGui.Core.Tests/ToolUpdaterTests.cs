using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Infrastructure.Tools;

namespace YtDlpGui.Core.Tests;

public sealed class ToolUpdaterTests
{
    [Fact]
    public void Parse_WhenAlreadyUpToDate_ReturnsUpToDateStatus()
    {
        // Arrange
        var lines = new[]
        {
            "Latest version: stable@2026.08.19 from yt-dlp/yt-dlp",
            "yt-dlp is up to date (stable@2026.08.19 from yt-dlp/yt-dlp)"
        };

        // Act
        var result = YtDlpUpdateParser.Parse(0, lines, "2026.08.19");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(ToolUpdateStatus.UpToDate, result.Status);
        Assert.Equal("2026.08.19", result.NewVersion);
        Assert.Equal("2026.08.19", result.OldVersion);
    }

    [Fact]
    public void Parse_WhenUpdatedSuccessfully_ReturnsUpdatedStatusWithNewVersion()
    {
        // Arrange
        var lines = new[]
        {
            "Current version: stable@2026.07.04 from yt-dlp/yt-dlp",
            "Latest version: stable@2026.08.19 from yt-dlp/yt-dlp",
            "Updating to stable@2026.08.19 from yt-dlp/yt-dlp ...",
            "Updated yt-dlp to stable@2026.08.19 from yt-dlp/yt-dlp"
        };

        // Act
        var result = YtDlpUpdateParser.Parse(0, lines, "2026.07.04");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(ToolUpdateStatus.Updated, result.Status);
        Assert.Equal("2026.07.04", result.OldVersion);
        Assert.Equal("2026.08.19", result.NewVersion);
    }

    [Fact]
    public void Parse_WhenPermissionDenied_ReturnsPermissionDeniedStatus()
    {
        // Arrange
        var lines = new[]
        {
            "Updating to stable@2026.08.19 from yt-dlp/yt-dlp ...",
            "ERROR: PermissionError: [WinError 5] Access is denied: 'C:\\Program Files\\yt-dlp\\yt-dlp.exe'"
        };

        // Act
        var result = YtDlpUpdateParser.Parse(1, lines, "2026.07.04");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ToolUpdateStatus.PermissionDenied, result.Status);
    }

    [Fact]
    public void Parse_WhenNetworkFails_ReturnsNetworkErrorStatus()
    {
        // Arrange
        var lines = new[]
        {
            "ERROR: urllib.error.URLError: <urlopen error [Errno 11001] getaddrinfo failed>"
        };

        // Act
        var result = YtDlpUpdateParser.Parse(1, lines, "2026.08.19");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ToolUpdateStatus.NetworkError, result.Status);
    }

    [Fact]
    public void Parse_WhenNonZeroExitCode_ReturnsFailedStatus()
    {
        // Arrange
        var lines = new[]
        {
            "ERROR: An unknown critical internal error occurred"
        };

        // Act
        var result = YtDlpUpdateParser.Parse(2, lines, "2026.08.19");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ToolUpdateStatus.Failed, result.Status);
    }

    [Fact]
    public void Parse_WhenEmptyOutputAndZeroExit_ReturnsUpToDate()
    {
        // Act
        var result = YtDlpUpdateParser.Parse(0, [], "2026.08.19");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(ToolUpdateStatus.UpToDate, result.Status);
    }

    [Fact]
    public void Parse_WhenEmptyOutputAndNonZeroExit_ReturnsFailed()
    {
        // Act
        var result = YtDlpUpdateParser.Parse(1, [], "2026.08.19");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ToolUpdateStatus.Failed, result.Status);
    }

    [Theory]
    [InlineData("Updated yt-dlp to 2026.08.19", "2026.08.19")]
    [InlineData("Updated yt-dlp to stable@2026.09.01 from yt-dlp", "2026.09.01")]
    [InlineData("Updating to version 2026.10.15", "2026.10.15")]
    public void Parse_ExtractsVersionVariations(string line, string expectedVersion)
    {
        var result = YtDlpUpdateParser.Parse(0, [line], "2026.07.01");

        Assert.Equal(ToolUpdateStatus.Updated, result.Status);
        Assert.Equal(expectedVersion, result.NewVersion);
    }
}
