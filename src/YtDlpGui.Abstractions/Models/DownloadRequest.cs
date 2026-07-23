using YtDlpGui.Abstractions.Enums;

namespace YtDlpGui.Abstractions.Models;

/// <summary>Immutable description of what the user asked to download.</summary>
public sealed record DownloadRequest(
    string Url,
    MediaFormat Format,
    VideoQuality Quality,
    string OutputFolder,
    DownloadOptions? Options = null)
{
    /// <summary>Advanced options; never null at use sites.</summary>
    public DownloadOptions EffectiveOptions => Options ?? DownloadOptions.Default;
}
