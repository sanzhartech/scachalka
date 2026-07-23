using Microsoft.Extensions.DependencyInjection;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Localization;
using YtDlpGui.Application.Execution;
using YtDlpGui.Application.Queue;
using YtDlpGui.Application.Tools;
using YtDlpGui.Core.Arguments;
using YtDlpGui.Core.Errors;
using YtDlpGui.Core.Naming;
using YtDlpGui.Core.Progress;
using YtDlpGui.Core.Retry;
using YtDlpGui.Core.Validation;
using YtDlpGui.Infrastructure.FileSystem;
using YtDlpGui.Infrastructure.Localization;
using YtDlpGui.Infrastructure.Logging;
using YtDlpGui.Infrastructure.Processes;
using YtDlpGui.Infrastructure.Settings;
using YtDlpGui.Infrastructure.Tools;
using YtDlpGui.UI.Services;
using YtDlpGui.UI.ViewModels;
using YtDlpGui.UI.Views;

namespace YtDlpGui.App;

/// <summary>
/// Composition root: the only place in the application where concrete types
/// from every layer are wired together.
/// </summary>
public static class ServiceRegistration
{
    public static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // Core (pure domain logic).
        services.AddSingleton<IUrlValidator, UrlValidator>();
        services.AddSingleton<IFileNameSanitizer, FileNameSanitizer>();
        services.AddSingleton<IProgressParser, YtDlpProgressParser>();
        services.AddSingleton<IErrorClassifier, ErrorClassifier>();
        services.AddSingleton<IRetryPolicy, RetryPolicy>();
        // Argument builder strategies: add new formats here without touching existing code.
        services.AddSingleton<IArgumentBuilder, VideoArgumentBuilder>();
        services.AddSingleton<IArgumentBuilder, AudioArgumentBuilder>();

        // Infrastructure (OS integration).
        services.AddSingleton<Localizer>();
        services.AddSingleton<ILocalizer>(sp => sp.GetRequiredService<Localizer>());
        services.AddSingleton<LogService>();
        services.AddSingleton<ILogSink>(sp => sp.GetRequiredService<LogService>());
        services.AddSingleton<IMediaToolRunner, ProcessMediaToolRunner>();
        services.AddSingleton<IToolLocator, ToolLocator>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IFolderService, FolderService>();
        services.AddSingleton<IPartialFileCleaner, PartialFileCleaner>();

        // Settings are loaded once and shared as a singleton instance.
        services.AddSingleton(sp => sp.GetRequiredService<ISettingsStore>().Load());

        // Application (orchestration).
        services.AddSingleton<ToolContext>();
        services.AddSingleton<IDownloadExecutor, DownloadExecutor>();
        services.AddSingleton<IDownloadCoordinator, DownloadCoordinator>();

        // UI.
        services.AddSingleton<IClipboardService, WpfClipboardService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
