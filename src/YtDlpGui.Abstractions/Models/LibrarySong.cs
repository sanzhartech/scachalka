namespace YtDlpGui.Abstractions.Models;

/// <summary>One track read from an imported music library file (e.g. an Apple Music export).</summary>
public sealed record LibrarySong(
    string Title,
    string Artist,
    string? Album = null,
    int? DurationSeconds = null)
{
    /// <summary>"Artist - Title" — the canonical search query and display form.</summary>
    public string DisplayName => string.IsNullOrEmpty(Artist) ? Title : $"{Artist} - {Title}";
}
