using System.Text.RegularExpressions;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Localization;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Infrastructure.Tools;

/// <summary>
/// Parses console output and exit code from "yt-dlp -U" into a structured ToolUpdateResult.
/// </summary>
public static partial class YtDlpUpdateParser
{
    [GeneratedRegex(@"(?:stable@|version\s+|to\s+)?(\d{4}\.\d{2}\.\d{2}(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionPattern();

    public static ToolUpdateResult Parse(int exitCode, IReadOnlyList<string> lines, string? currentVersion)
    {
        if (lines == null || lines.Count == 0)
        {
            if (exitCode == 0)
            {
                return ToolUpdateResult.UpToDate(currentVersion, Loc.T(LocKeys.ToolsUpToDate));
            }

            return ToolUpdateResult.Failed(
                ToolUpdateStatus.Failed,
                Loc.T(LocKeys.ToolsUpdateFailedFormat, $"exit code {exitCode}"),
                $"Process exited with code {exitCode} with no output.",
                currentVersion);
        }

        var fullText = string.Join(Environment.NewLine, lines);

        // 1. Check for up to date
        // e.g. "yt-dlp is up to date (stable@2026.08.19 from yt-dlp/yt-dlp)"
        // or "yt-dlp is up to date" or "is up-to-date" or "Latest version: stable@..."
        var isUpToDate = lines.Any(l =>
            l.Contains("is up to date", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("is up-to-date", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("already up to date", StringComparison.OrdinalIgnoreCase));

        // 2. Check for updated
        // e.g. "Updated yt-dlp to stable@2026.08.19 from yt-dlp/yt-dlp"
        // or "Updating to stable@2026.08.19..."
        var isUpdated = lines.Any(l =>
            l.Contains("Updated yt-dlp to", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("Updated to", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("Successfully updated", StringComparison.OrdinalIgnoreCase));

        // Check for specific version mentions
        string? detectedVersion = ExtractVersion(lines);

        if (isUpdated)
        {
            var newVersion = detectedVersion ?? currentVersion;
            var message = !string.IsNullOrEmpty(newVersion)
                ? Loc.T(LocKeys.ToolsUpdatedFormat, newVersion)
                : Loc.T(LocKeys.ToolsUpdatedFormat, "latest");
            return ToolUpdateResult.Updated(currentVersion, newVersion, message);
        }

        if (isUpToDate && exitCode == 0)
        {
            var ver = detectedVersion ?? currentVersion;
            return ToolUpdateResult.UpToDate(ver, Loc.T(LocKeys.ToolsUpToDate));
        }

        // Check for errors
        var isPermissionDenied = lines.Any(l =>
            l.Contains("PermissionError", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("Access is denied", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("WinError 5", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("permission denied", StringComparison.OrdinalIgnoreCase));

        if (isPermissionDenied)
        {
            return ToolUpdateResult.Failed(
                ToolUpdateStatus.PermissionDenied,
                Loc.T(LocKeys.ToolsUpdatePermissionDenied),
                fullText,
                currentVersion);
        }

        var isNetworkError = lines.Any(l =>
            l.Contains("URLError", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("getaddrinfo failed", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("ConnectionRefused", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("Network is unreachable", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("HTTP Error", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("Temporary failure in name resolution", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("Max retries exceeded", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("unable to download", StringComparison.OrdinalIgnoreCase));

        if (isNetworkError)
        {
            return ToolUpdateResult.Failed(
                ToolUpdateStatus.NetworkError,
                Loc.T(LocKeys.ToolsUpdateNetworkError),
                fullText,
                currentVersion);
        }

        if (exitCode == 0)
        {
            // If exit code is 0, but no explicit updated string found
            if (detectedVersion != null && !string.Equals(detectedVersion, currentVersion, StringComparison.OrdinalIgnoreCase))
            {
                return ToolUpdateResult.Updated(currentVersion, detectedVersion, Loc.T(LocKeys.ToolsUpdatedFormat, detectedVersion));
            }

            return ToolUpdateResult.UpToDate(detectedVersion ?? currentVersion, Loc.T(LocKeys.ToolsUpToDate));
        }

        var firstError = lines.FirstOrDefault(l =>
            l.Contains("ERROR:", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("Error:", StringComparison.OrdinalIgnoreCase)) ?? lines.LastOrDefault() ?? $"exit code {exitCode}";

        return ToolUpdateResult.Failed(
            ToolUpdateStatus.Failed,
            Loc.T(LocKeys.ToolsUpdateFailedFormat, firstError.Trim()),
            fullText,
            currentVersion);
    }

    private static string? ExtractVersion(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            if (line.Contains("Updated yt-dlp to", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Latest version:", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Updating to", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("is up to date", StringComparison.OrdinalIgnoreCase))
            {
                var match = VersionPattern().Match(line);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
        }

        return null;
    }
}
