namespace YtDlpGui.Abstractions.Models;

/// <summary>Outcome of running an external tool process.</summary>
public sealed record ToolResult(
    int ExitCode,
    string StdErrTail,
    bool WasCanceled)
{
    public bool IsSuccess => ExitCode == 0 && !WasCanceled;
}
