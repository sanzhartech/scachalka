namespace YtDlpGui.Abstractions.Models;

/// <summary>Result of splitting free-form pasted text into valid URLs and rejected tokens.</summary>
public sealed record UrlExtraction(
    IReadOnlyList<string> Valid,
    IReadOnlyList<string> Invalid)
{
    public static UrlExtraction Empty { get; } = new([], []);
}
