using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Localization;
using YtDlpGui.Abstractions.Models;
using YtDlpGui.Application.Import;
using YtDlpGui.Application.Jobs;
using YtDlpGui.Application.Queue;
using YtDlpGui.Application.Tools;
using YtDlpGui.UI.Services;
using YtDlpGui.UI.Theming;

namespace YtDlpGui.UI.ViewModels;

/// <summary>
/// Main window state: URL input, format/quality/folder options, tool status,
/// the download queue and the log feed. All collection mutations happen on the
/// UI dispatcher; background threads only touch scalar job properties.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private const int MaxLogItemsInView = 600;

    private readonly IUrlValidator _urlValidator;
    private readonly IDownloadCoordinator _coordinator;
    private readonly ISettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly IToolLocator _toolLocator;
    private readonly IToolUpdater _toolUpdater;
    private readonly ToolContext _toolContext;
    private readonly ILogSink _log;
    private readonly IFolderService _folderService;
    private readonly IClipboardService _clipboard;
    private readonly ILocalizer _localizer;
    private readonly ILibraryImportService _importService;
    private readonly IBulkImportService _bulkImportService;
    private readonly Dispatcher _dispatcher;
    private readonly bool _isInitialized;

    /// <summary>Remembered so the tool banner can be rebuilt when the language changes.</summary>
    private ToolLocation _lastToolLocation = ToolLocation.Empty;

    public ObservableCollection<DownloadJob> Jobs { get; } = [];

    public ObservableCollection<LogEntry> Logs { get; } = [];

    public IReadOnlyList<FormatOption> Formats => FormatOption.All;

    public IReadOnlyList<QualityOption> Qualities => SelectedFormat.Value.IsVideo()
        ? QualityOption.VideoQualities
        : QualityOption.AudioQualities;

    [ObservableProperty]
    private string _urlInput = string.Empty;

    public bool HasUrlInput => !string.IsNullOrWhiteSpace(UrlInput);

    public string UrlCountText => _localizer.Language == "ru"
        ? "Ctrl + V — вставить из буфера обмена"
        : "Ctrl + V — paste from clipboard";

    public string QueueTitle => _localizer.Language == "ru"
        ? $"Загрузки ({Jobs.Count})"
        : $"Downloads ({Jobs.Count})";

    [ObservableProperty]
    private string _outputFolder = string.Empty;

    [ObservableProperty]
    private FormatOption _selectedFormat = FormatOption.All[0];

    [ObservableProperty]
    private QualityOption _selectedQuality = QualityOption.All[0];

    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private bool _autoUpdateOnStartup = true;

    [ObservableProperty]
    private bool _isUpdatingTools;

    [ObservableProperty]
    private string _statusText = Loc.T(LocKeys.StatusReady);

    [ObservableProperty]
    private string _toolStatusText = Loc.T(LocKeys.ToolsChecking);

    [ObservableProperty]
    private bool _areToolsReady;

    [ObservableProperty]
    private bool _isCheckingTools = true;

    // Advanced yt-dlp options.

    [ObservableProperty]
    private bool _isAdvOptionsExpanded;

    [ObservableProperty]
    private bool _allowPlaylists;

    [ObservableProperty]
    private bool _embedMetadata;

    [ObservableProperty]
    private bool _embedThumbnail;

    [ObservableProperty]
    private bool _embedSubtitles;

    [ObservableProperty]
    private string _subtitleLanguages = string.Empty;

    [ObservableProperty]
    private string _selectedCookiesBrowser = CookiesDisabled;

    [ObservableProperty]
    private string _customArguments = string.Empty;

    [ObservableProperty]
    private LanguageOption _selectedLanguage = LanguageOption.All[0];

    public const string CookiesDisabled = "None";

    public IReadOnlyList<string> CookieBrowsers { get; } =
        [CookiesDisabled, "chrome", "edge", "firefox", "brave", "opera", "vivaldi"];

    public IReadOnlyList<LanguageOption> Languages => LanguageOption.All;

    public bool IsQualityEnabled => true;

    public bool IsSubtitleOptionEnabled => SelectedFormat.Value.IsVideo();

    public MainViewModel(
        IUrlValidator urlValidator,
        IDownloadCoordinator coordinator,
        ISettingsStore settingsStore,
        AppSettings settings,
        IToolLocator toolLocator,
        IToolUpdater toolUpdater,
        ToolContext toolContext,
        ILogSink log,
        IFolderService folderService,
        IClipboardService clipboard,
        ILocalizer localizer,
        ILibraryImportService importService,
        IBulkImportService bulkImportService)
    {
        _urlValidator = urlValidator;
        _coordinator = coordinator;
        _settingsStore = settingsStore;
        _settings = settings;
        _toolLocator = toolLocator;
        _toolUpdater = toolUpdater;
        _toolContext = toolContext;
        _log = log;
        _folderService = folderService;
        _clipboard = clipboard;
        _localizer = localizer;
        _importService = importService;
        _bulkImportService = bulkImportService;
        _dispatcher = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;

        ApplySettings(settings);
        _isInitialized = true;

        _coordinator.JobEnqueued += OnJobEnqueued;
        _importService.ProgressChanged += OnImportProgressChanged;
        _bulkImportService.ProgressChanged += OnBulkImportProgressChanged;
        _log.EntryAdded += OnLogEntryAdded;
        _localizer.LanguageChanged += OnLanguageChanged;
        Jobs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(QueueTitle));
        foreach (var entry in _log.GetSnapshot())
        {
            Logs.Add(entry);
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _dispatcher.BeginInvoke(() =>
        {
            // Recompute strings that were captured at their last event.
            RebuildToolStatus();
            UpdateStatus();
            OnPropertyChanged(nameof(UrlCountText));
            OnPropertyChanged(nameof(QueueTitle));
            foreach (var job in Jobs)
            {
                job.RaiseLocalizedTextChanged();
            }
        });
    }

    /// <summary>Called once after the window is shown: discovers external tools off the UI thread and optionally updates components.</summary>
    public async Task InitializeAsync()
    {
        await RefreshToolsAsync();

        if (AutoUpdateOnStartup && _lastToolLocation.HasYtDlp)
        {
            _ = UpdateToolsInternalAsync(isAutomatic: true);
        }
    }

    private void ApplySettings(AppSettings settings)
    {
        // _isInitialized is still false here, so the OnXChanged hooks skip
        // save-on-change and the redundant theme re-apply.
        OutputFolder = ResolveInitialFolder(settings.LastOutputFolder);
        SelectedFormat = Formats.FirstOrDefault(f => f.Value == settings.PreferredFormat) ?? Formats[0];
        SelectedQuality = Qualities.FirstOrDefault(q => q.Value == settings.PreferredQuality) ?? Qualities[0];
        IsDarkTheme = !string.Equals(settings.Theme, ThemeManager.Light, StringComparison.OrdinalIgnoreCase);
        AutoUpdateOnStartup = settings.AutoUpdateOnStartup;
        AllowPlaylists = settings.AllowPlaylists;
        EmbedMetadata = settings.EmbedMetadata;
        EmbedThumbnail = settings.EmbedThumbnail;
        EmbedSubtitles = settings.EmbedSubtitles;
        SubtitleLanguages = settings.SubtitleLanguages;
        SelectedCookiesBrowser = CookieBrowsers.Contains(settings.CookiesFromBrowser)
            ? settings.CookiesFromBrowser
            : CookiesDisabled;
        CustomArguments = settings.CustomArguments;
        SelectedLanguage = LanguageOption.ForCode(settings.Language);
    }

    private static string ResolveInitialFolder(string? saved)
    {
        if (!string.IsNullOrWhiteSpace(saved) && Directory.Exists(saved))
        {
            return saved!;
        }

        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        return Directory.Exists(downloads)
            ? downloads
            : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
    }

    private void OnJobEnqueued(object? sender, DownloadJob job)
    {
        void Add()
        {
            Jobs.Add(job);
            job.PropertyChanged += OnJobPropertyChanged;
            UpdateStatus();
        }

        if (_dispatcher.CheckAccess())
        {
            Add();
        }
        else
        {
            _dispatcher.BeginInvoke(Add);
        }
    }

    private void OnJobPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DownloadJob.Stage))
        {
            _dispatcher.BeginInvoke(UpdateStatus);
        }
    }

    private void OnLogEntryAdded(object? sender, LogEntry entry)
    {
        _dispatcher.BeginInvoke(() =>
        {
            Logs.Add(entry);
            while (Logs.Count > MaxLogItemsInView)
            {
                Logs.RemoveAt(0);
            }
        });
    }

    private void UpdateStatus()
    {
        var queued = 0;
        var active = 0;
        var completed = 0;
        var failed = 0;
        var canceled = 0;

        foreach (var job in Jobs)
        {
            switch (job.Stage)
            {
                case DownloadStage.Queued: queued++; break;
                case DownloadStage.Completed: completed++; break;
                case DownloadStage.Failed: failed++; break;
                case DownloadStage.Canceled: canceled++; break;
                case DownloadStage.Paused: break;
                default: active++; break;
            }
        }

        StatusText = Jobs.Count == 0
            ? Loc.T(LocKeys.StatusReady)
            : Loc.T(LocKeys.StatusSummaryFormat, active, queued, completed, failed, canceled);
    }

    /// <summary>Rebuilds the tool banner text from the last discovery result (used after a language switch).</summary>
    private void RebuildToolStatus()
    {
        if (IsCheckingTools)
        {
            ToolStatusText = Loc.T(LocKeys.ToolsChecking);
            return;
        }

        ToolStatusText = DescribeToolStatus(_lastToolLocation);
    }

    private string DescribeToolStatus(ToolLocation location)
    {
        if (location.IsReady)
        {
            return Loc.T(LocKeys.ToolsReadyFormat, location.YtDlpVersion, ShortFfmpeg(location.FfmpegVersion));
        }

        var missing = new List<string>(2);
        if (!location.HasYtDlp)
        {
            missing.Add("yt-dlp");
        }

        if (!location.HasFfmpeg)
        {
            missing.Add("FFmpeg");
        }

        return missing.Count == 0
            ? Loc.T(LocKeys.ToolsCheckFailed)
            : Loc.T(LocKeys.ToolsMissingFormat, string.Join(Loc.T(LocKeys.ToolsMissingJoin), missing));
    }

    private void SaveSettingsSafe()
    {
        if (!_isInitialized)
        {
            return; // Constructor is still applying loaded settings.
        }

        _settings.LastOutputFolder = OutputFolder;
        _settings.PreferredFormat = SelectedFormat.Value;
        _settings.PreferredQuality = SelectedQuality.Value;
        _settings.Theme = IsDarkTheme ? ThemeManager.Dark : ThemeManager.Light;
        _settings.AutoUpdateOnStartup = AutoUpdateOnStartup;
        _settings.AllowPlaylists = AllowPlaylists;
        _settings.EmbedMetadata = EmbedMetadata;
        _settings.EmbedThumbnail = EmbedThumbnail;
        _settings.EmbedSubtitles = EmbedSubtitles;
        _settings.SubtitleLanguages = SubtitleLanguages;
        _settings.CookiesFromBrowser =
            SelectedCookiesBrowser == CookiesDisabled ? string.Empty : SelectedCookiesBrowser;
        _settings.CustomArguments = CustomArguments;
        _settings.Language = SelectedLanguage.Code;

        _ = _settingsStore.SaveAsync(_settings);
    }

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (_isInitialized)
        {
            _localizer.SetLanguage(value.Code);
        }

        SaveSettingsSafe();
    }

    /// <summary>Snapshot of the advanced options attached to each enqueued download.</summary>
    private DownloadOptions BuildDownloadOptions() => new(
        AllowPlaylists,
        EmbedMetadata,
        EmbedThumbnail,
        EmbedSubtitles,
        SubtitleLanguages,
        SelectedCookiesBrowser == CookiesDisabled ? string.Empty : SelectedCookiesBrowser,
        CustomArguments);

    partial void OnAllowPlaylistsChanged(bool value) => SaveSettingsSafe();

    partial void OnEmbedMetadataChanged(bool value) => SaveSettingsSafe();

    partial void OnEmbedThumbnailChanged(bool value) => SaveSettingsSafe();

    partial void OnEmbedSubtitlesChanged(bool value) => SaveSettingsSafe();

    partial void OnSubtitleLanguagesChanged(string value) => SaveSettingsSafe();

    partial void OnSelectedCookiesBrowserChanged(string value) => SaveSettingsSafe();

    partial void OnCustomArgumentsChanged(string value) => SaveSettingsSafe();

    partial void OnAutoUpdateOnStartupChanged(bool value) => SaveSettingsSafe();

    partial void OnUrlInputChanged(string value)
    {
        OnPropertyChanged(nameof(HasUrlInput));
        OnPropertyChanged(nameof(UrlCountText));
    }

    partial void OnSelectedFormatChanged(FormatOption value)
    {
        OnPropertyChanged(nameof(Qualities));
        OnPropertyChanged(nameof(IsQualityEnabled));
        OnPropertyChanged(nameof(IsSubtitleOptionEnabled));

        if (!Qualities.Any(q => q.Value == SelectedQuality?.Value))
        {
            SelectedQuality = Qualities[0];
        }

        SaveSettingsSafe();
    }

    partial void OnSelectedQualityChanged(QualityOption value) => SaveSettingsSafe();

    partial void OnOutputFolderChanged(string value) => SaveSettingsSafe();

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (_isInitialized)
        {
            ThemeManager.Apply(value ? ThemeManager.Dark : ThemeManager.Light);
        }

        SaveSettingsSafe();
    }
}
