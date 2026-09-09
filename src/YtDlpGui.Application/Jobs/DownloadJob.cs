using CommunityToolkit.Mvvm.ComponentModel;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Models;
using YtDlpGui.Core.Stages;

namespace YtDlpGui.Application.Jobs;

/// <summary>
/// Observable state of one download in the queue. Scalar properties are updated from
/// worker threads (WPF marshals INotifyPropertyChanged for scalars automatically);
/// collection membership is managed exclusively on the UI thread by the view model.
/// </summary>
public sealed partial class DownloadJob : ObservableObject
{
    public DownloadJob(DownloadRequest request)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        ThumbnailUrl = ResolveInitialThumbnail(request.Url);
    }

    private static string? ResolveInitialThumbnail(string url)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            url, @"(?:youtu\.be\/|youtube\.com\/(?:embed\/|v\/|watch\?v=|watch\?.+&v=))([\w-]{11})");
        return match.Success ? $"https://img.youtube.com/vi/{match.Groups[1].Value}/mqdefault.jpg" : null;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public DownloadRequest Request { get; }

    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.Now;

    [ObservableProperty]
    private string? _thumbnailUrl;

    /// <summary>Set by the executor when the job leaves the queue; used for .part cleanup.</summary>
    public DateTimeOffset? StartedAt { get; internal set; }

    [ObservableProperty]
    private DownloadStage _stage = DownloadStage.Queued;

    [ObservableProperty]
    private double? _percent;

    [ObservableProperty]
    private double? _speedBytesPerSecond;

    [ObservableProperty]
    private int? _etaSeconds;

    [ObservableProperty]
    private double? _downloadedBytes;

    [ObservableProperty]
    private double? _totalBytes;

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private string? _destinationFile;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private DownloadErrorKind _errorKind = DownloadErrorKind.None;

    [ObservableProperty]
    private string? _statusNote;

    [ObservableProperty]
    private int _attempts;

    public string Url => Request.Url;

    public bool CanCancel => StageRules.CanCancel(Stage);

    public bool CanPause => StageRules.CanPause(Stage);

    public bool CanResume => StageRules.CanResume(Stage);

    public bool CanRetry => StageRules.CanRetry(Stage);

    public bool IsTerminal => StageRules.IsTerminal(Stage);

    public bool IsPaused => Stage == DownloadStage.Paused;

    /// <summary>Set when user clicked pause on a running job so executor marks it Paused instead of Canceled.</summary>
    public bool IsPauseRequested { get; set; }

    public bool HasError => Stage == DownloadStage.Failed && !string.IsNullOrEmpty(ErrorMessage);

    public bool IsIndeterminate =>
        Stage == DownloadStage.Resolving
        || (Stage == DownloadStage.Downloading && Percent is null)
        || Stage is DownloadStage.Merging or DownloadStage.Converting;

    /// <summary>Resets transient state so the job can be re-queued by the retry command.</summary>
    internal void ResetForRetry()
    {
        Attempts++;
        Percent = null;
        SpeedBytesPerSecond = null;
        EtaSeconds = null;
        DownloadedBytes = null;
        TotalBytes = null;
        ErrorMessage = null;
        ErrorKind = DownloadErrorKind.None;
        StatusNote = null;
        Stage = DownloadStage.Queued;
    }

    partial void OnStageChanged(DownloadStage value)
    {
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(CanRetry));
        OnPropertyChanged(nameof(IsTerminal));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsIndeterminate));
        OnPropertyChanged(nameof(StageText));
        OnPropertyChanged(nameof(SpeedText));
        OnPropertyChanged(nameof(EtaText));
    }

    partial void OnPercentChanged(double? value) => OnPropertyChanged(nameof(IsIndeterminate));

    partial void OnSpeedBytesPerSecondChanged(double? value) => OnPropertyChanged(nameof(SpeedText));

    partial void OnEtaSecondsChanged(int? value) => OnPropertyChanged(nameof(EtaText));

    partial void OnDownloadedBytesChanged(double? value) => OnPropertyChanged(nameof(SizeText));

    partial void OnTotalBytesChanged(double? value) => OnPropertyChanged(nameof(SizeText));

    partial void OnTitleChanged(string? value) => OnPropertyChanged(nameof(DisplayName));

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));
}
