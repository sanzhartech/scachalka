using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Core.Links;

/// <summary>
/// Fallback format: one URL per line. Accepts anything (register it last),
/// blank lines are skipped; garbage lines surface as invalid URLs downstream.
/// </summary>
public sealed class PlainTextLinkSource : ILinkSource
{
    public string FormatName => "TXT";

    public bool CanExtract(string fileName, string content) => true;

    public IReadOnlyList<ExtractedLink> Extract(string content)
    {
        var lines = content.Split('\n');
        var links = new List<ExtractedLink>(lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim().Trim('\uFEFF');
            if (trimmed.Length == 0)
            {
                continue;
            }

            links.Add(new ExtractedLink(trimmed, $"line {i + 1}"));
        }

        return links;
    }
}
