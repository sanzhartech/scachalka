using YtDlpGui.Abstractions.Models;
using YtDlpGui.Core.Links;
using YtDlpGui.Core.Validation;

namespace YtDlpGui.Core.Tests;

public sealed class BulkLinkAnalyzerTests
{
    private readonly BulkLinkAnalyzer _sut = new(new UrlValidator());

    private static ExtractedLink Link(string? url, string origin = "line 1") => new(url, origin);

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc123")]
    [InlineData("https://youtube.com/watch?v=abc123")]
    [InlineData("https://m.youtube.com/watch?v=abc123")]
    [InlineData("https://music.youtube.com/watch?v=abc123")]
    [InlineData("https://youtu.be/abc123")]
    public void Analyze_AcceptsAllYouTubeDomains(string url)
    {
        // Act
        var result = _sut.Analyze([Link(url)]);

        // Assert
        Assert.Single(result.ValidUrls);
        Assert.Empty(result.Rejected);
    }

    [Theory]
    [InlineData("https://vimeo.com/12345", LinkRejectReason.UnsupportedDomain)]
    [InlineData("https://soundcloud.com/track", LinkRejectReason.UnsupportedDomain)]
    [InlineData("not a url at all", LinkRejectReason.InvalidUrl)]
    [InlineData("ftp://youtube.com/x", LinkRejectReason.InvalidUrl)]
    [InlineData(null, LinkRejectReason.MissingUrl)]
    [InlineData("   ", LinkRejectReason.MissingUrl)]
    public void Analyze_RejectsWithCorrectReason(string? url, LinkRejectReason expected)
    {
        // Act
        var result = _sut.Analyze([Link(url)]);

        // Assert
        Assert.Empty(result.ValidUrls);
        Assert.Equal(expected, Assert.Single(result.Rejected).Reason);
    }

    [Fact]
    public void Analyze_DetectsSameVideoAcrossUrlForms_AsDuplicate()
    {
        // Arrange: the same video id via three different URL shapes.
        var links = new[]
        {
            Link("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "line 1"),
            Link("https://youtu.be/dQw4w9WgXcQ", "line 2"),
            Link("https://music.youtube.com/watch?v=dQw4w9WgXcQ&si=xyz", "line 3")
        };

        // Act
        var result = _sut.Analyze(links);

        // Assert
        Assert.Single(result.ValidUrls);
        Assert.Equal(2, result.Duplicates);
        Assert.Equal(0, result.InvalidTotal);
    }

    [Fact]
    public void Analyze_VideoIdsAreCaseSensitive_NoFalseDuplicates()
    {
        // Arrange
        var links = new[]
        {
            Link("https://youtu.be/AbCdEfGhIjK"),
            Link("https://youtu.be/abcdefghijk")
        };

        // Act
        var result = _sut.Analyze(links);

        // Assert
        Assert.Equal(2, result.ValidUrls.Count);
    }

    [Fact]
    public void Analyze_PreservesOriginalOrder()
    {
        // Arrange
        var links = new[]
        {
            Link("https://youtu.be/first0000001"),
            Link("https://vimeo.com/skip"),
            Link("https://youtu.be/second000002"),
            Link("https://youtu.be/third0000003")
        };

        // Act
        var result = _sut.Analyze(links);

        // Assert
        Assert.Equal(
            ["https://youtu.be/first0000001", "https://youtu.be/second000002", "https://youtu.be/third0000003"],
            result.ValidUrls);
    }

    [Fact]
    public void Analyze_MixedBatch_CountsEverything()
    {
        // Arrange
        var links = new[]
        {
            Link("https://youtu.be/AAAAAAAAAAA"),
            Link("https://youtu.be/AAAAAAAAAAA"),   // duplicate
            Link("https://example.com/x"),          // unsupported
            Link("garbage"),                        // invalid
            Link(null),                             // missing
            Link("https://youtu.be/BBBBBBBBBBB")
        };

        // Act
        var result = _sut.Analyze(links);

        // Assert
        Assert.Equal(2, result.ValidUrls.Count);
        Assert.Equal(1, result.Duplicates);
        Assert.Equal(3, result.InvalidTotal);
        Assert.Equal(4, result.Rejected.Count);
    }

    [Fact]
    public void Analyze_PlaylistUrls_AreValidAndDedupedByFullUrl()
    {
        // Arrange
        var links = new[]
        {
            Link("https://www.youtube.com/playlist?list=PL123"),
            Link("https://www.youtube.com/playlist?list=PL123"),
            Link("https://www.youtube.com/playlist?list=PL456")
        };

        // Act
        var result = _sut.Analyze(links);

        // Assert
        Assert.Equal(2, result.ValidUrls.Count);
        Assert.Equal(1, result.Duplicates);
    }
}
