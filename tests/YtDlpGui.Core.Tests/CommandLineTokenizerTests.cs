using YtDlpGui.Core.Arguments;

namespace YtDlpGui.Core.Tests;

public sealed class CommandLineTokenizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Tokenize_EmptyInput_ReturnsNothing(string? input)
    {
        Assert.Empty(CommandLineTokenizer.Tokenize(input));
    }

    [Fact]
    public void Tokenize_SplitsOnWhitespace()
    {
        var tokens = CommandLineTokenizer.Tokenize("--limit-rate 2M  --no-mtime");

        Assert.Equal(["--limit-rate", "2M", "--no-mtime"], tokens);
    }

    [Fact]
    public void Tokenize_HonorsQuotesForPathsWithSpaces()
    {
        var tokens = CommandLineTokenizer.Tokenize("--download-archive \"C:\\My Files\\archive.txt\"");

        Assert.Equal(["--download-archive", @"C:\My Files\archive.txt"], tokens);
    }

    [Fact]
    public void Tokenize_UnclosedQuote_StillReturnsToken()
    {
        var tokens = CommandLineTokenizer.Tokenize("--proxy \"socks5://host");

        Assert.Equal(["--proxy", "socks5://host"], tokens);
    }
}
