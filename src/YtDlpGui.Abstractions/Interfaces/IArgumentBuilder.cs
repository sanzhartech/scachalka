using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Abstractions.Interfaces;

/// <summary>
/// Strategy that translates a <see cref="DownloadRequest"/> into a yt-dlp argument list.
/// New output formats are added by registering new implementations — existing code stays untouched.
/// </summary>
public interface IArgumentBuilder
{
    bool CanBuild(DownloadRequest request);

    IReadOnlyList<string> Build(DownloadRequest request, ToolLocation tools);
}
