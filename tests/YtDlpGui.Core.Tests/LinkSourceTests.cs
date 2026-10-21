using YtDlpGui.Core.Links;

namespace YtDlpGui.Core.Tests;

public sealed class LinkSourceTests
{
    private readonly CsvLinkSource _csv = new();
    private readonly JsonLinkSource _json = new();
    private readonly PlainTextLinkSource _txt = new();

    // ---- CSV ----

    [Fact]
    public void Csv_ExtractsUrlColumnByHeader_IgnoringOtherColumns()
    {
        // Arrange
        var content =
            "title,artist,album,youtube_music_url\n" +
            "FWU,Don Toliver,Hardstone Psycho,https://music.youtube.com/watch?v=AAAA\n" +
            "Timeless,The Weeknd,Hurry Up Tomorrow,https://music.youtube.com/watch?v=BBBB\n";

        // Act
        var links = _csv.Extract(content);

        // Assert
        Assert.Equal(2, links.Count);
        Assert.Equal("https://music.youtube.com/watch?v=AAAA", links[0].Url);
        Assert.Equal("https://music.youtube.com/watch?v=BBBB", links[1].Url);
    }

    [Fact]
    public void Csv_PrefersYoutubeMusicUrlColumn_OverGenericUrl()
    {
        // Arrange
        var content =
            "url,youtube_music_url\n" +
            "https://example.com/x,https://music.youtube.com/watch?v=AAAA\n";

        // Act
        var links = _csv.Extract(content);

        // Assert
        Assert.Equal("https://music.youtube.com/watch?v=AAAA", links[0].Url);
    }

    [Fact]
    public void Csv_HandlesQuotedFieldsWithCommas()
    {
        // Arrange
        var content =
            "title,url\n" +
            "\"Hello, World\",https://youtu.be/CCCC\n";

        // Act
        var links = _csv.Extract(content);

        // Assert
        Assert.Single(links);
        Assert.Equal("https://youtu.be/CCCC", links[0].Url);
    }

    [Fact]
    public void Csv_EmptyUrlCell_YieldsNullUrl()
    {
        // Arrange
        var content = "title,url\nNo link here,\n";

        // Act
        var links = _csv.Extract(content);

        // Assert
        Assert.Single(links);
        Assert.Null(links[0].Url);
    }

    [Fact]
    public void Csv_HeaderlessFile_ScansCellsForHttpValues()
    {
        // Arrange
        var content = "FWU;Don Toliver;https://youtu.be/DDDD\n";

        // Act
        var links = _csv.Extract(content);

        // Assert
        Assert.Single(links);
        Assert.Equal("https://youtu.be/DDDD", links[0].Url);
    }

    [Fact]
    public void Csv_CanExtract_ByExtensionOrHeaderSniffing()
    {
        Assert.True(_csv.CanExtract("songs.csv", "whatever"));
        Assert.True(_csv.CanExtract("data.txt", "title,url\nX,https://youtu.be/a\n"));
        Assert.False(_csv.CanExtract("plain.txt", "https://youtu.be/a\nhttps://youtu.be/b\n"));
    }

    // ---- JSON ----

    [Fact]
    public void Json_ExtractsFromArrayOfObjects_CaseInsensitiveKeys()
    {
        // Arrange
        var content = """
            [
              {"Title": "FWU", "youtube_music_url": "https://music.youtube.com/watch?v=AAAA"},
              {"Title": "Other", "URL": "https://youtu.be/BBBB"},
              {"Title": "No link"}
            ]
            """;

        // Act
        var links = _json.Extract(content);

        // Assert
        Assert.Equal(3, links.Count);
        Assert.Equal("https://music.youtube.com/watch?v=AAAA", links[0].Url);
        Assert.Equal("https://youtu.be/BBBB", links[1].Url);
        Assert.Null(links[2].Url);
    }

    [Fact]
    public void Json_ExtractsFromArrayOfStrings()
    {
        // Arrange
        var content = """["https://youtu.be/AAAA", "https://youtu.be/BBBB"]""";

        // Act
        var links = _json.Extract(content);

        // Assert
        Assert.Equal(2, links.Count);
        Assert.Equal("https://youtu.be/AAAA", links[0].Url);
    }

    [Fact]
    public void Json_ExtractsFromRootObjectWithArrayProperty()
    {
        // Arrange
        var content = """{"songs": [{"link": "https://youtu.be/AAAA"}]}""";

        // Act
        var links = _json.Extract(content);

        // Assert
        Assert.Single(links);
        Assert.Equal("https://youtu.be/AAAA", links[0].Url);
    }

    [Fact]
    public void Json_MalformedInput_ReturnsEmpty()
    {
        Assert.Empty(_json.Extract("{not valid json"));
    }

    [Fact]
    public void Json_CanExtract_ByExtensionOrContentSniffing()
    {
        Assert.True(_json.CanExtract("links.json", "whatever"));
        Assert.True(_json.CanExtract("links.txt", """[{"url": "x"}]"""));
        Assert.False(_json.CanExtract("links.txt", "https://youtu.be/a"));
    }

    // ---- TXT ----

    [Fact]
    public void Txt_OneUrlPerLine_SkipsBlankLines()
    {
        // Arrange
        var content = "https://youtu.be/AAAA\n\n  https://youtube.com/watch?v=BBBB  \n";

        // Act
        var links = _txt.Extract(content);

        // Assert
        Assert.Equal(2, links.Count);
        Assert.Equal("https://youtu.be/AAAA", links[0].Url);
        Assert.Equal("https://youtube.com/watch?v=BBBB", links[1].Url);
        Assert.Equal("line 3", links[1].Origin);
    }
}
