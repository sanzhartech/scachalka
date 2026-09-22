using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Core.Errors;
using YtDlpGui.Core.Retry;

namespace YtDlpGui.Core.Tests;

public sealed class ErrorClassifierTests
{
    private readonly ErrorClassifier _sut = new();

    [Fact]
    public void Classify_ExitZero_IsNone()
    {
        Assert.Equal(DownloadErrorKind.None, _sut.Classify(0, "whatever"));
    }

    [Theory]
    [InlineData("ERROR: Unsupported URL: https://example.com", DownloadErrorKind.InvalidUrl)]
    [InlineData("OSError: [Errno 28] No space left on device", DownloadErrorKind.DiskFull)]
    [InlineData("PermissionError: [Errno 13] Permission denied", DownloadErrorKind.PermissionDenied)]
    [InlineData("ERROR: Unable to download webpage: <urlopen error timed out>", DownloadErrorKind.Network)]
    [InlineData("urllib.error.URLError: getaddrinfo failed", DownloadErrorKind.Network)]
    [InlineData("ERROR: Could not copy Chrome cookie database. See  https://github.com/yt-dlp/yt-dlp/issues/7271  for more info",
        DownloadErrorKind.CookieBrowserLocked)]
    [InlineData("ERROR: Could not copy Edge cookie database.", DownloadErrorKind.CookieBrowserLocked)]
    [InlineData("WARNING: Failed to decrypt with DPAPI", DownloadErrorKind.CookieBrowserLocked)]
    [InlineData("something completely new", DownloadErrorKind.Unknown)]
    public void Classify_MapsStderrPatterns(string stderr, DownloadErrorKind expected)
    {
        Assert.Equal(expected, _sut.Classify(1, stderr));
    }

    [Fact]
    public void GetUserMessage_IncludesTruncatedDetail()
    {
        var message = _sut.GetUserMessage(DownloadErrorKind.Network, new string('x', 500));

        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.Contains("xxxx", message);
        Assert.True(message.Length < 400);
    }

    [Fact]
    public void GetUserMessage_None_IsEmpty()
    {
        Assert.Equal(string.Empty, _sut.GetUserMessage(DownloadErrorKind.None, "detail"));
    }

    [Fact]
    public void GetUserMessage_EveryKindHasText()
    {
        foreach (var kind in Enum.GetValues<DownloadErrorKind>())
        {
            if (kind == DownloadErrorKind.None)
            {
                continue;
            }

            Assert.False(string.IsNullOrWhiteSpace(_sut.GetUserMessage(kind, null)));
        }
    }

    [Fact]
    public void RetryPolicy_InvalidUrlIsNotRetryable_NetworkIs()
    {
        var policy = new RetryPolicy();

        Assert.False(policy.CanRetry(DownloadErrorKind.InvalidUrl));
        Assert.True(policy.CanRetry(DownloadErrorKind.Network));
        Assert.True(policy.CanRetry(DownloadErrorKind.DiskFull));
        Assert.True(policy.CanRetry(DownloadErrorKind.ToolMissing));
        Assert.True(policy.CanRetry(DownloadErrorKind.CookieBrowserLocked));
    }
}
