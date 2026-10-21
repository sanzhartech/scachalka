namespace YtDlpGui.Abstractions.Enums;

/// <summary>
/// Status of an external tool update operation.
/// </summary>
public enum ToolUpdateStatus
{
    UpToDate,
    Updated,
    Failed,
    NotFound,
    PermissionDenied,
    NetworkError,
    Cancelled,
    Unknown
}
