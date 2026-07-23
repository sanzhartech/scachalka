using YtDlpGui.Core.Validation;

namespace YtDlpGui.Core.Tests;

public sealed class UrlValidatorTests
{
    private readonly UrlValidator _sut = new();

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("http://vimeo.com/12345")]
    [InlineData("https://youtu.be/abc123")]
    [InlineData("https://www.youtube.com/watch?v=x&list=PL123")]
    public void IsValid_AcceptsHttpMediaUrls(string url)
    {
        Assert.True(_sut.IsValid(url));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/file")]
    [InlineData("javascript:alert(1)")]
    [InlineData("C:\\Videos\\file.mp4")]
    [InlineData("--output=evil")]
    [InlineData("https://")]
    public void IsValid_RejectsInvalidInput(string url)
    {
        Assert.False(_sut.IsValid(url));
    }

    [Fact]
    public void Extract_SplitsMixedTextAndDeduplicates()
    {
        // Arrange
        var text = "https://youtu.be/a garbage https://youtu.be/b\nhttps://youtu.be/a";

        // Act
        var result = _sut.Extract(text);

        // Assert
        Assert.Equal(2, result.Valid.Count);
        Assert.Single(result.Invalid);
        Assert.Equal("https://youtu.be/a", result.Valid[0]);
        Assert.Equal("https://youtu.be/b", result.Valid[1]);
    }

    [Fact]
    public void Extract_EmptyText_ReturnsEmpty()
    {
        var result = _sut.Extract("   \n  ");

        Assert.Empty(result.Valid);
        Assert.Empty(result.Invalid);
    }
}
