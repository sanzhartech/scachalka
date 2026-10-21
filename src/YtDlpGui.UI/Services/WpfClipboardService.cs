using System.Runtime.InteropServices;
using System.Windows;

namespace YtDlpGui.UI.Services;

/// <summary>
/// Clipboard access with the failure modes of the real Windows clipboard handled:
/// it can be locked by another process (ExternalException) or contain non-text data.
/// </summary>
public sealed class WpfClipboardService : IClipboardService
{
    public string? GetText()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch (ExternalException)
        {
            // Another process holds the clipboard lock — treat as "nothing to paste".
            return null;
        }
    }

    public void SetText(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (ExternalException)
        {
            // Another process holds the clipboard lock
        }
    }
}
