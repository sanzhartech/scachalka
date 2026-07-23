using YtDlpGui.Application.Jobs;

namespace YtDlpGui.Application.Execution;

/// <summary>Runs the full lifecycle of a single download job (resolve → download → merge/convert).</summary>
public interface IDownloadExecutor
{
    Task ExecuteAsync(DownloadJob job, CancellationToken cancellationToken);
}
