using YtDlpGui.Abstractions.Interfaces;

namespace YtDlpGui.Core.Naming;

/// <summary>
/// Makes an arbitrary (possibly remote-supplied) string safe to use as a Windows file name.
/// Unicode letters are preserved; only characters Windows forbids are replaced.
/// </summary>
public sealed class FileNameSanitizer : IFileNameSanitizer
{
    private const int MaxLength = 200;
    private const string Fallback = "download";

    private static readonly HashSet<char> InvalidChars = [.. Path.GetInvalidFileNameChars()];

    // Names reserved by Windows regardless of extension (CON.mp4 is still invalid).
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public string Sanitize(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Fallback;
        }

        var builder = new System.Text.StringBuilder(fileName.Length);
        foreach (var ch in fileName)
        {
            // Control characters and Windows-invalid characters become underscores.
            builder.Append(InvalidChars.Contains(ch) || char.IsControl(ch) ? '_' : ch);
        }

        // Trailing dots and spaces are silently stripped by Windows — remove them explicitly.
        var result = builder.ToString().Trim().TrimEnd('.', ' ');

        if (result.Length == 0)
        {
            return Fallback;
        }

        if (result.Length > MaxLength)
        {
            result = result[..MaxLength].TrimEnd('.', ' ');
        }

        var stem = result.Split('.')[0];
        if (ReservedNames.Contains(stem))
        {
            result = "_" + result;
        }

        return result.Length == 0 ? Fallback : result;
    }
}
