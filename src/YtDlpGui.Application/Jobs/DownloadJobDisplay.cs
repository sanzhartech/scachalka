using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Localization;
using YtDlpGui.Core.Formatting;

namespace YtDlpGui.Application.Jobs;

/// <summary>Read-only presentation strings for <see cref="DownloadJob"/> (bound by the queue list).</summary>
public sealed partial class DownloadJob
{
    public string DisplayName => string.IsNullOrEmpty(Title) ? Url : Title;

    public string FormatText => Request.Format.IsVideo()
        ? $"{Request.Format.ToShortDisplay()} · {Request.Quality.ToDisplay()}"
        : Request.Format.ToShortDisplay();

    public string StageText => Stage switch
    {
        DownloadStage.Queued => Attempts > 0
            ? Loc.T(LocKeys.StageQueuedRetryFormat, Attempts)
            : Loc.T(LocKeys.StageQueued),
        DownloadStage.Resolving => Loc.T(LocKeys.StageResolving),
        DownloadStage.Downloading => Loc.T(LocKeys.StageDownloading),
        DownloadStage.Merging => Loc.T(LocKeys.StageMerging),
        DownloadStage.Converting => Loc.T(LocKeys.StageConverting),
        DownloadStage.Completed => Loc.T(LocKeys.StageCompleted),
        DownloadStage.Failed => Loc.T(LocKeys.StageFailed),
        DownloadStage.Canceled => Loc.T(LocKeys.StageCanceled),
        _ => Stage.ToString()
    };

    /// <summary>Re-raises localized display properties so the queue refreshes on a language switch.</summary>
    public void RaiseLocalizedTextChanged()
    {
        OnPropertyChanged(nameof(StageText));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(FormatText));
        OnPropertyChanged(nameof(SpeedText));
        OnPropertyChanged(nameof(EtaText));
        OnPropertyChanged(nameof(SizeText));
    }

    public string SpeedText => Stage == DownloadStage.Downloading
        ? FormatHelper.FormatSpeed(SpeedBytesPerSecond)
        : "—";

    public string EtaText => Stage == DownloadStage.Downloading
        ? FormatHelper.FormatEta(EtaSeconds)
        : "—";

    public string SizeText
    {
        get
        {
            if (DownloadedBytes is null && TotalBytes is null)
            {
                return "—";
            }

            var downloaded = FormatHelper.FormatBytes(DownloadedBytes);
            return TotalBytes is null
                ? downloaded
                : $"{downloaded} / {FormatHelper.FormatBytes(TotalBytes)}";
        }
    }
}
