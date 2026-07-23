using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Application.Tools;

/// <summary>
/// Shared, thread-safe holder of the last tool discovery result.
/// Written by the UI's "re-check tools" flow, read by the download executor.
/// </summary>
public sealed class ToolContext
{
    private volatile ToolLocation _current = ToolLocation.Empty;

    public ToolLocation Current
    {
        get => _current;
        set => _current = value ?? ToolLocation.Empty;
    }
}
