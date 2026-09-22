namespace YtDlpGui.UI.Services;

/// <summary>Abstraction over the Windows clipboard so the view model stays testable.</summary>
public interface IClipboardService
{
    /// <returns>Clipboard text, or null when the clipboard is empty, non-text or locked by another app.</returns>
    string? GetText();

    /// <summary>Places text onto the clipboard.</summary>
    void SetText(string text);
}
