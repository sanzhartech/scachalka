namespace YtDlpGui.Abstractions.Interfaces;

/// <summary>Produces Windows-safe file names (invalid characters, reserved names, length, Unicode preserved).</summary>
public interface IFileNameSanitizer
{
    string Sanitize(string fileName);
}
