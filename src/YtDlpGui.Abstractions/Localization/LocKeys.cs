namespace YtDlpGui.Abstractions.Localization;

/// <summary>Central catalog of localization keys, referenced by both C# and XAML.</summary>
public static class LocKeys
{
    // Tools banner.
    public const string ToolsChecking = "Tools.Checking";
    public const string ToolsRecheck = "Tools.Recheck";
    public const string ToolsReadyFormat = "Tools.ReadyFormat";
    public const string ToolsMissingFormat = "Tools.MissingFormat";
    public const string ToolsMissingJoin = "Tools.MissingJoin";
    public const string ToolsCheckFailed = "Tools.CheckFailed";

    // URL input.
    public const string UrlHint = "Url.Hint";
    public const string BtnAddToQueue = "Btn.AddToQueue";
    public const string BtnAddTooltip = "Btn.AddTooltip";
    public const string BtnPasteAdd = "Btn.PasteAdd";
    public const string BtnPasteTooltip = "Btn.PasteTooltip";

    // Options row.
    public const string LabelFormat = "Label.Format";
    public const string LabelQuality = "Label.Quality";
    public const string LabelOutputFolder = "Label.OutputFolder";
    public const string LabelLanguage = "Label.Language";
    public const string BtnBrowse = "Btn.Browse";
    public const string BtnOpenFolder = "Btn.OpenFolder";
    public const string CheckDarkTheme = "Check.DarkTheme";

    // Advanced options.
    public const string AdvHeader = "Adv.Header";
    public const string AdvPlaylists = "Adv.Playlists";
    public const string AdvPlaylistsTip = "Adv.PlaylistsTip";
    public const string AdvMetadata = "Adv.Metadata";
    public const string AdvThumbnail = "Adv.Thumbnail";
    public const string AdvThumbnailTip = "Adv.ThumbnailTip";
    public const string AdvSubtitles = "Adv.Subtitles";
    public const string AdvSubtitlesTip = "Adv.SubtitlesTip";
    public const string AdvSubLangsTip = "Adv.SubLangsTip";
    public const string AdvCookies = "Adv.Cookies";
    public const string AdvCookiesTip = "Adv.CookiesTip";
    public const string AdvExtraArgs = "Adv.ExtraArgs";
    public const string AdvExtraArgsTip = "Adv.ExtraArgsTip";
    public const string CookieNone = "Cookie.None";

    // Queue + job.
    public const string QueueTitle = "Queue.Title";
    public const string QueueClearFinished = "Queue.ClearFinished";
    public const string JobCancel = "Job.Cancel";
    public const string JobRetry = "Job.Retry";
    public const string JobShowInFolder = "Job.ShowInFolder";
    public const string JobEtaPrefix = "Job.EtaPrefix";

    public const string LogHeader = "Log.Header";

    // Stages.
    public const string StageQueued = "Stage.Queued";
    public const string StageQueuedRetryFormat = "Stage.QueuedRetryFormat";
    public const string StageResolving = "Stage.Resolving";
    public const string StageDownloading = "Stage.Downloading";
    public const string StageMerging = "Stage.Merging";
    public const string StageConverting = "Stage.Converting";
    public const string StageCompleted = "Stage.Completed";
    public const string StageFailed = "Stage.Failed";
    public const string StageCanceled = "Stage.Canceled";

    // Status bar messages.
    public const string StatusReady = "Status.Ready";
    public const string StatusNoValidUrls = "Status.NoValidUrls";
    public const string StatusToolsMissing = "Status.ToolsMissing";
    public const string StatusClipboardNoText = "Status.ClipboardNoText";
    public const string StatusCannotRetry = "Status.CannotRetry";
    public const string StatusSummaryFormat = "Status.SummaryFormat";

    // Job notes.
    public const string NoteCanceled = "Note.Canceled";
    public const string NoteCanceledBeforeStart = "Note.CanceledBeforeStart";
    public const string NoteAlreadyExisted = "Note.AlreadyExisted";

    // Error messages.
    public const string ErrorToolMissing = "Error.ToolMissing";
    public const string ErrorInvalidUrl = "Error.InvalidUrl";
    public const string ErrorNetwork = "Error.Network";
    public const string ErrorDiskFull = "Error.DiskFull";
    public const string ErrorPermission = "Error.Permission";
    public const string ErrorFileExists = "Error.FileExists";
    public const string ErrorInterrupted = "Error.Interrupted";
    public const string ErrorUnknown = "Error.Unknown";
}
