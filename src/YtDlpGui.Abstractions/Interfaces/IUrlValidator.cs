using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Abstractions.Interfaces;

/// <summary>Validates URLs and extracts them from free-form pasted or dropped text.</summary>
public interface IUrlValidator
{
    bool IsValid(string url);

    /// <summary>Splits arbitrary text into valid, de-duplicated URLs and rejected tokens.</summary>
    UrlExtraction Extract(string text);
}
