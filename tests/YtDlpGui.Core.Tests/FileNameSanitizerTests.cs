using YtDlpGui.Core.Naming;

namespace YtDlpGui.Core.Tests;

public sealed class FileNameSanitizerTests
{
    private readonly FileNameSanitizer _sut = new();

    [Fact]
    public void Sanitize_ReplacesInvalidCharacters()
    {
        var result = _sut.Sanitize("video: \"the/best\" <ever>?");

        Assert.DoesNotContain(result, c => Path.GetInvalidFileNameChars().Contains(c));
    }

    [Fact]
    public void Sanitize_PreservesUnicode()
    {
        var result = _sut.Sanitize("Видео 動画 🎬 ê");

        Assert.Equal("Видео 動画 🎬 ê", result);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con.mp4")]
    [InlineData("LPT1.mp3")]
    public void Sanitize_EscapesReservedWindowsNames(string name)
    {
        var result = _sut.Sanitize(name);

        Assert.StartsWith("_", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    public void Sanitize_FallsBackForEmptyResults(string name)
    {
        Assert.Equal("download", _sut.Sanitize(name));
    }

    [Fact]
    public void Sanitize_TrimsTrailingDotsAndSpaces()
    {
        Assert.Equal("clip", _sut.Sanitize("clip... "));
    }

    [Fact]
    public void Sanitize_LimitsLength()
    {
        var result = _sut.Sanitize(new string('a', 500));

        Assert.True(result.Length <= 200);
    }
}
