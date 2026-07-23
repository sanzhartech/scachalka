using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Core.Validation;

/// <summary>
/// Accepts absolute http/https URLs with a plausible host.
/// Everything else (garbage, file paths, javascript:, ftp:) is rejected up-front
/// so invalid input never reaches the process layer.
/// </summary>
public sealed class UrlValidator : IUrlValidator
{
    private static readonly char[] Separators = [' ', '\t', '\r', '\n', ';', ','];

    public bool IsValid(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        // A real media host always has a dot (youtube.com) or is an explicit local host.
        return !string.IsNullOrEmpty(uri.Host)
               && (uri.Host.Contains('.') || uri.HostNameType == UriHostNameType.IPv6 || uri.IsLoopback);
    }

    public UrlExtraction Extract(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return UrlExtraction.Empty;
        }

        var valid = new List<string>();
        var invalid = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IsValid(raw))
            {
                if (seen.Add(raw))
                {
                    valid.Add(raw);
                }
            }
            else
            {
                invalid.Add(raw);
            }
        }

        return new UrlExtraction(valid, invalid);
    }
}
