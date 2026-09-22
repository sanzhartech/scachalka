using YtDlpGui.Core.Library;

namespace YtDlpGui.Core.Tests;

public sealed class AppleMusicLibraryParserTests
{
    private readonly AppleMusicLibraryParser _sut = new();

    [Fact]
    public void Parse_ReadsTabSeparatedWithHeader()
    {
        // Arrange: shape of a real Apple Music "Export as plain text" file.
        var content =
            "Name\tArtist\tComposer\tAlbum\tGrouping\tGenre\tSize\tTime\n" +
            "FWU\tDon Toliver\t\tHeaven or Hell\t\tHip-Hop\t9000000\t196\n" +
            "SICKO MODE\tTravis Scott\t\tASTROWORLD\t\tHip-Hop\t12000000\t312\n";

        // Act
        var songs = _sut.Parse(content);

        // Assert
        Assert.Equal(2, songs.Count);
        Assert.Equal("FWU", songs[0].Title);
        Assert.Equal("Don Toliver", songs[0].Artist);
        Assert.Equal("Heaven or Hell", songs[0].Album);
        Assert.Equal(196, songs[0].DurationSeconds);
        Assert.Equal("Travis Scott - SICKO MODE", songs[1].DisplayName);
    }

    [Fact]
    public void Parse_ReadsTabSeparatedWithoutHeader_UsingItunesColumnOrder()
    {
        // Arrange
        var content = "Timeless\tThe Weeknd\t\tHurry Up Tomorrow\t\tR&B\t1\t256\n";

        // Act
        var songs = _sut.Parse(content);

        // Assert
        Assert.Single(songs);
        Assert.Equal("Timeless", songs[0].Title);
        Assert.Equal("The Weeknd", songs[0].Artist);
        Assert.Equal(256, songs[0].DurationSeconds);
    }

    [Fact]
    public void Parse_ReadsArtistDashTitleFallback()
    {
        // Arrange
        var content = "Don Toliver - FWU\nTravis Scott - SICKO MODE\n";

        // Act
        var songs = _sut.Parse(content);

        // Assert
        Assert.Equal(2, songs.Count);
        Assert.Equal("FWU", songs[0].Title);
        Assert.Equal("Don Toliver", songs[0].Artist);
        Assert.Null(songs[0].DurationSeconds);
    }

    [Fact]
    public void Parse_ParsesClockNotationDurations()
    {
        // Arrange
        var content =
            "Name\tArtist\tTime\n" +
            "Song A\tArtist A\t4:01\n" +
            "Song B\tArtist B\t1:02:03\n";

        // Act
        var songs = _sut.Parse(content);

        // Assert
        Assert.Equal(241, songs[0].DurationSeconds);
        Assert.Equal(3723, songs[1].DurationSeconds);
    }

    [Fact]
    public void Parse_DeduplicatesCaseInsensitively()
    {
        // Arrange
        var content = "Don Toliver - FWU\nDON TOLIVER - fwu\n";

        // Act
        var songs = _sut.Parse(content);

        // Assert
        Assert.Single(songs);
    }

    [Fact]
    public void Parse_SkipsEmptyLinesAndRowsWithoutTitle()
    {
        // Arrange
        var content =
            "Name\tArtist\tTime\n" +
            "\n" +
            "\tGhost Artist\t100\n" +
            "Real Song\tReal Artist\t200\n";

        // Act
        var songs = _sut.Parse(content);

        // Assert
        Assert.Single(songs);
        Assert.Equal("Real Song", songs[0].Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  \n")]
    public void Parse_ReturnsEmptyForBlankInput(string content)
    {
        Assert.Empty(_sut.Parse(content));
    }

    [Fact]
    public void Parse_SupportsRussianHeaders()
    {
        // Arrange
        var content =
            "Имя\tИсполнитель\tАльбом\tВремя\n" +
            "Песня\tАртист\tАльбом X\t180\n";

        // Act
        var songs = _sut.Parse(content);

        // Assert
        Assert.Single(songs);
        Assert.Equal("Песня", songs[0].Title);
        Assert.Equal("Артист", songs[0].Artist);
        Assert.Equal(180, songs[0].DurationSeconds);
    }
}
