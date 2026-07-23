using System.Globalization;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Models;
using YtDlpGui.Core.Arguments;

namespace YtDlpGui.Core.Progress;

/// <summary>
/// Parses yt-dlp stdout lines into structured events.
/// Primary source is our own machine-readable progress templates (NDL|/NPP|);
/// well-known human-readable markers are handled as a redundant fallback so the app
/// keeps working even if a future yt-dlp version changes template rendering.
/// Unknown lines are never an error — the parser just returns false.
/// </summary>
public sealed class YtDlpProgressParser : IProgressParser
{
    private const string DestinationMarker = "[download] Destination: ";
    private const string ExtractAudioDestMarker = "[ExtractAudio] Destination: ";
    private const string AlreadyDownloadedMarker = " has already been downloaded";

    public bool TryParse(string line, out YtDlpEvent evt)
    {
        evt = null!;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        line = line.Trim();

        if (line.StartsWith(ProgressTemplates.DownloadMarker, StringComparison.Ordinal))
        {
            return TryParseDownloadProgress(line, ref evt);
        }

        if (line.StartsWith(ProgressTemplates.PostprocessMarker, StringComparison.Ordinal))
        {
            return TryParsePostprocess(line, ref evt);
        }

        if (line.StartsWith(DestinationMarker, StringComparison.Ordinal))
        {
            evt = YtDlpEvent.ForDestination(line[DestinationMarker.Length..].Trim());
            return true;
        }

        if (line.StartsWith(ExtractAudioDestMarker, StringComparison.Ordinal))
        {
            evt = YtDlpEvent.ForDestination(line[ExtractAudioDestMarker.Length..].Trim());
            return true;
        }

        // Fallback stage markers printed by yt-dlp postprocessors.
        if (line.StartsWith("[Merger]", StringComparison.Ordinal))
        {
            evt = YtDlpEvent.ForStage(DownloadStage.Merging);
            return true;
        }

        if (line.StartsWith("[ExtractAudio]", StringComparison.Ordinal)
            || line.StartsWith("[VideoConvertor]", StringComparison.Ordinal)
            || line.StartsWith("[VideoRemuxer]", StringComparison.Ordinal))
        {
            evt = YtDlpEvent.ForStage(DownloadStage.Converting);
            return true;
        }

        if (line.Contains(AlreadyDownloadedMarker, StringComparison.Ordinal))
        {
            evt = YtDlpEvent.ForAlreadyDownloaded(ExtractAlreadyDownloadedPath(line));
            return true;
        }

        return false;
    }

    private static bool TryParseDownloadProgress(string line, ref YtDlpEvent evt)
    {
        // NDL|status|downloaded|total|total_estimate|speed|eta
        var parts = line.Split('|');
        if (parts.Length < 7)
        {
            return false;
        }

        var status = parts[1];
        var downloaded = ParseDouble(parts[2]);
        var total = ParseDouble(parts[3]) ?? ParseDouble(parts[4]);
        var speed = ParseDouble(parts[5]);
        var eta = ParseInt(parts[6]);

        double? percent = null;
        if (string.Equals(status, "finished", StringComparison.OrdinalIgnoreCase))
        {
            percent = 100;
        }
        else if (downloaded is not null && total is > 0)
        {
            percent = Math.Clamp(downloaded.Value / total.Value * 100, 0, 100);
        }

        evt = YtDlpEvent.ForProgress(new ProgressSnapshot(percent, speed, eta, downloaded, total));
        return true;
    }

    private static bool TryParsePostprocess(string line, ref YtDlpEvent evt)
    {
        // NPP|status|postprocessor
        var parts = line.Split('|');
        if (parts.Length < 3 || !string.Equals(parts[1], "started", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        DownloadStage? stage = parts[2] switch
        {
            "Merger" => DownloadStage.Merging,
            "ExtractAudio" or "VideoConvertor" or "VideoRemuxer" => DownloadStage.Converting,
            _ => null
        };

        if (stage is null)
        {
            return false;
        }

        evt = YtDlpEvent.ForStage(stage.Value);
        return true;
    }

    private static string? ExtractAlreadyDownloadedPath(string line)
    {
        // "[download] C:\path\file.mp4 has already been downloaded"
        var start = line.IndexOf("] ", StringComparison.Ordinal);
        var end = line.IndexOf(AlreadyDownloadedMarker, StringComparison.Ordinal);
        if (start < 0 || end <= start + 2)
        {
            return null;
        }

        var path = line[(start + 2)..end].Trim();
        return path.Length > 0 ? path : null;
    }

    private static double? ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && !double.IsNaN(result)
            ? result
            : null;

    private static int? ParseInt(string value) =>
        ParseDouble(value) is { } d ? (int)Math.Round(d) : null;
}
