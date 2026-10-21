using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Abstractions.Interfaces;

/// <summary>
/// One import file format (CSV, JSON, plain text, …). The bulk importer picks the
/// first registered source whose <see cref="CanExtract"/> accepts the file, so new
/// formats (M3U, Spotify exports, …) are added by registering another implementation —
/// the import engine itself never changes.
/// </summary>
public interface ILinkSource
{
    /// <summary>Human-readable format name for logging ("CSV", "JSON", "TXT").</summary>
    string FormatName { get; }

    /// <summary>Format detection by file name and/or content sniffing. Must be cheap.</summary>
    bool CanExtract(string fileName, string content);

    /// <summary>Pulls every link entry out of the file content. Never throws on malformed input.</summary>
    IReadOnlyList<ExtractedLink> Extract(string content);
}
