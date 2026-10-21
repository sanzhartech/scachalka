namespace YtDlpGui.Abstractions.Models;

/// <summary>
/// One entry pulled out of an imported link file before validation.
/// <paramref name="Url"/> is null when the row/item had no URL at all;
/// <paramref name="Origin"/> points back to the source ("row 12", "line 3", "item 7").
/// </summary>
public sealed record ExtractedLink(string? Url, string Origin);

/// <summary>Why an extracted link was not queued.</summary>
public enum LinkRejectReason
{
    MissingUrl,
    InvalidUrl,
    UnsupportedDomain,
    Duplicate
}

/// <summary>An extracted link that failed validation, with the reason.</summary>
public sealed record RejectedLink(ExtractedLink Link, LinkRejectReason Reason);

/// <summary>
/// Outcome of validating and deduplicating a batch of extracted links.
/// <paramref name="ValidUrls"/> preserves the original file order.
/// <paramref name="InvalidTotal"/> counts everything rejected except duplicates.
/// </summary>
public sealed record BulkLinkAnalysis(
    IReadOnlyList<string> ValidUrls,
    IReadOnlyList<RejectedLink> Rejected,
    int Duplicates,
    int InvalidTotal);
