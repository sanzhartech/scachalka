using System.Globalization;

namespace YtDlpGui.Core.Formatting;

/// <summary>Human-readable formatting for sizes, speeds and ETAs shown in the UI.</summary>
public static class FormatHelper
{
    private static readonly string[] Units = ["B", "KiB", "MiB", "GiB", "TiB"];

    public static string FormatBytes(double? bytes)
    {
        if (bytes is null or < 0)
        {
            return "—";
        }

        var value = bytes.Value;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value:0.#} {Units[unit]}");
    }

    public static string FormatSpeed(double? bytesPerSecond) =>
        bytesPerSecond is null ? "—" : $"{FormatBytes(bytesPerSecond)}/s";

    public static string FormatEta(int? seconds)
    {
        if (seconds is null or < 0)
        {
            return "—";
        }

        var span = TimeSpan.FromSeconds(seconds.Value);
        return span.TotalHours >= 1
            ? span.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : span.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }
}
