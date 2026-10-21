using YtDlpGui.Abstractions.Models;
using YtDlpGui.Core.Matching;

namespace YtDlpGui.Core.Tests;

public sealed class SongMatchScorerTests
{
    private readonly SongMatchScorer _sut = new();

    private static readonly LibrarySong Song = new("FWU", "Don Toliver", "Heaven or Hell", 196);

    private static SearchCandidate Candidate(
        string title,
        string channel = "Some Channel",
        double? duration = 196,
        bool verified = false,
        SearchSource source = SearchSource.YouTube,
        long? views = null,
        string id = "aaaaaaaaaaa") =>
        new(id, $"https://www.youtube.com/watch?v={id}", title, channel, duration, views, verified, source);

    [Fact]
    public void Score_TopicChannelExactDuration_BeatsRandomUpload()
    {
        // Arrange
        var official = Candidate("FWU", channel: "Don Toliver - Topic");
        var random = Candidate("FWU by don toliver full song", channel: "randomuser123", duration: 240);

        // Act & Assert
        Assert.True(_sut.Score(Song, official) > _sut.Score(Song, random));
        Assert.True(_sut.Score(Song, official) >= _sut.MinimumConfidence);
    }

    [Theory]
    [InlineData("FWU (Nightcore)")]
    [InlineData("FWU (slowed + reverb)")]
    [InlineData("FWU sped up")]
    [InlineData("FWU (Bass Boosted)")]
    [InlineData("FWU Remix")]
    [InlineData("FWU cover by me")]
    [InlineData("FWU karaoke version")]
    [InlineData("FWU (Live at Rolling Loud)")]
    [InlineData("FWU instrumental")]
    public void Score_BannedVariants_FallBelowThreshold(string title)
    {
        // Arrange: same channel/duration as a perfect match — only the title differs.
        var altered = Candidate(title, channel: "Don Toliver - Topic");

        // Act
        var score = _sut.Score(Song, altered);

        // Assert
        Assert.True(score < _sut.MinimumConfidence, $"\"{title}\" scored {score}");
    }

    [Fact]
    public void Score_BannedTermInLibraryTitle_IsNotPenalized()
    {
        // Arrange: the song itself is a remix — candidates carrying the term must stay valid.
        var remixSong = new LibrarySong("Wild Thoughts Remix", "DJ Khaled", null, 205);
        var candidate = Candidate("Wild Thoughts Remix", channel: "DJ Khaled - Topic", duration: 205);

        // Act
        var score = _sut.Score(remixSong, candidate);

        // Assert
        Assert.True(score >= _sut.MinimumConfidence, $"scored {score}");
    }

    [Fact]
    public void Score_WrongDuration_IsPenalized()
    {
        // Arrange
        var exact = Candidate("FWU", channel: "Don Toliver - Topic", duration: 196);
        var extended = Candidate("FWU", channel: "Don Toliver - Topic", duration: 400);

        // Act & Assert
        Assert.True(_sut.Score(Song, exact) - _sut.Score(Song, extended) >= 30);
    }

    [Fact]
    public void Score_OfficialAudio_OutranksLyricsVideo()
    {
        // Arrange
        var officialAudio = Candidate("Don Toliver - FWU (Official Audio)", channel: "Don Toliver", verified: true);
        var lyricsVideo = Candidate("Don Toliver - FWU (Lyrics)", channel: "Lyrics Paradise");

        // Act & Assert
        Assert.True(_sut.Score(Song, officialAudio) > _sut.Score(Song, lyricsVideo));
    }

    [Fact]
    public void Score_HandlesDiacriticsAndFeaturing()
    {
        // Arrange
        var song = new LibrarySong("Híghest in the Room", "Beyoncé", null, 176);
        var candidate = Candidate(
            "Beyonce - Highest in the Room (feat. Someone)", channel: "Beyonce - Topic", duration: 176);

        // Act
        var score = _sut.Score(song, candidate);

        // Assert
        Assert.True(score >= _sut.MinimumConfidence, $"scored {score}");
    }

    [Fact]
    public void ChooseBest_PicksHighestConfidenceCandidate()
    {
        // Arrange
        var official = Candidate("FWU", channel: "Don Toliver - Topic", id: "official0001");
        var nightcore = Candidate("FWU Nightcore", channel: "NightcoreNation", id: "nightcore001");
        var cover = Candidate("FWU (cover)", channel: "some girl sings", id: "cover0000001");

        // Act
        var match = _sut.ChooseBest(Song, [nightcore, official, cover]);

        // Assert
        Assert.Equal("official0001", match.Candidate?.Id);
        Assert.Equal(MatchKind.OfficialMusicTrack, match.Kind);
        Assert.True(match.Confidence >= _sut.MinimumConfidence);
    }

    [Fact]
    public void ChooseBest_EmptyCandidates_ReturnsNotFound()
    {
        // Act
        var match = _sut.ChooseBest(Song, []);

        // Assert
        Assert.False(match.HasCandidate);
        Assert.Equal(MatchKind.None, match.Kind);
        Assert.Equal(0, match.Confidence);
    }

    [Fact]
    public void ChooseBest_ClassifiesVevoUpload()
    {
        // Arrange
        var vevo = Candidate("Don Toliver - FWU", channel: "DonToliverVEVO");

        // Act
        var match = _sut.ChooseBest(Song, [vevo]);

        // Assert
        Assert.Equal(MatchKind.Vevo, match.Kind);
    }

    [Fact]
    public void ChooseBest_YouTubeMusicResult_ClassifiedAsOfficialTrack()
    {
        // Arrange
        var music = Candidate("FWU", channel: "Don Toliver", source: SearchSource.YouTubeMusic);

        // Act
        var match = _sut.ChooseBest(Song, [music]);

        // Assert
        Assert.Equal(MatchKind.OfficialMusicTrack, match.Kind);
    }

    [Fact]
    public void ChooseBest_TieBrokenByViewCount()
    {
        // Arrange: identical candidates except views.
        var lowViews = Candidate("FWU", channel: "Don Toliver - Topic", views: 1_000, id: "lowviews0001");
        var highViews = Candidate("FWU", channel: "Don Toliver - Topic", views: 9_000_000, id: "highviews001");

        // Act
        var match = _sut.ChooseBest(Song, [lowViews, highViews]);

        // Assert
        Assert.Equal("highviews001", match.Candidate?.Id);
    }

    [Fact]
    public void Score_CompletelyUnrelatedSong_ScoresLow()
    {
        // Arrange
        var unrelated = Candidate(
            "Baby Shark Dance", channel: "Pinkfong", duration: 137, verified: true);

        // Act
        var score = _sut.Score(Song, unrelated);

        // Assert
        Assert.True(score < _sut.MinimumConfidence, $"scored {score}");
    }
}
