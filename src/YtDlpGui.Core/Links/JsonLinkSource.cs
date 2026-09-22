using System.Text.Json;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Core.Links;

/// <summary>
/// Extracts links from JSON exports: a root array (of URL strings or of objects
/// with a url / youtube_music_url / youtube_url / link field, case-insensitive),
/// or a root object whose array properties contain such items.
/// Malformed JSON yields an empty result instead of throwing.
/// </summary>
public sealed class JsonLinkSource : ILinkSource
{
    /// <summary>Recognized URL property names, in priority order.</summary>
    private static readonly string[] UrlKeys = ["youtube_music_url", "youtube_url", "url", "link"];

    public string FormatName => "JSON";

    public bool CanExtract(string fileName, string content)
    {
        if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var trimmed = content.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return trimmed.StartsWith('[') || trimmed.StartsWith('{');
    }

    public IReadOnlyList<ExtractedLink> Extract(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => ExtractArray(doc.RootElement),
                JsonValueKind.Object => ExtractFromObjectArrays(doc.RootElement),
                _ => []
            };
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Root object form ({"songs": […]}): every array property is scanned.</summary>
    private static List<ExtractedLink> ExtractFromObjectArrays(JsonElement root)
    {
        var links = new List<ExtractedLink>();
        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                links.AddRange(ExtractArray(property.Value));
            }
        }

        return links;
    }

    private static List<ExtractedLink> ExtractArray(JsonElement array)
    {
        var links = new List<ExtractedLink>(array.GetArrayLength());
        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            index++;
            var origin = $"item {index}";
            var url = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Object => FindUrlProperty(element),
                _ => null
            };

            links.Add(new ExtractedLink(string.IsNullOrWhiteSpace(url) ? null : url!.Trim(), origin));
        }

        return links;
    }

    private static string? FindUrlProperty(JsonElement element)
    {
        // Case-insensitive lookup with the same priority order as the CSV source.
        foreach (var key in UrlKeys)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }
            }
        }

        return null;
    }
}
