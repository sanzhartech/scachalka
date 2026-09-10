using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using YtDlpGui.Abstractions;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Localization;
using YtDlpGui.Abstractions.Models;
using YtDlpGui.Application.Queue;
using YtDlpGui.Infrastructure.Localization;
using YtDlpGui.Infrastructure.Logging;
using YtDlpGui.UI.Theming;
using YtDlpGui.UI.ViewModels;
using YtDlpGui.UI.Views;
using LogLevel = YtDlpGui.Abstractions.Enums.LogLevel;

namespace YtDlpGui.App;

/// <summary>
/// Application lifecycle: builds the DI container, applies the persisted theme,
/// shows the main window, kicks off tool discovery, and on exit cancels all
/// downloads, drains workers and flushes the log — in that order.
/// </summary>
public partial class App
{
    private static readonly TimeSpan ShutdownWait = TimeSpan.FromSeconds(12);

    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = ServiceRegistration.BuildServiceProvider();

        // Global exception guards: log, tell the user, keep the process alive where possible.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var settings = _services.GetRequiredService<AppSettings>();

        // Localization must be live before any localized window/view model is created.
        var localizer = _services.GetRequiredService<Localizer>();
        var language = Localizer.ResolveInitialLanguage(settings.Language);
        localizer.SetLanguage(language);
        settings.Language = language;
        Loc.Current = localizer;

        ThemeManager.Apply(settings.Theme);

        var log = _services.GetRequiredService<ILogSink>();
        log.Write(LogLevel.Info, $"{AppInfo.Name} started (v{typeof(App).Assembly.GetName().Version}).");

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();

        // Tool discovery runs after the window is visible so startup feels instant.
        var viewModel = _services.GetRequiredService<MainViewModel>();
        _ = RunInitializationAsync(viewModel, log);
    }

    private static async Task RunInitializationAsync(MainViewModel viewModel, ILogSink log)
    {
        try
        {
            await viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            log.Write(LogLevel.Error, $"Startup initialization failed: {ex}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_services is not null)
        {
            // Graceful shutdown: ServiceProvider.DisposeAsync disposes all IAsyncDisposable
            // services (such as DownloadCoordinator) and IDisposable services (such as LogService)
            // in correct dependency order without throwing InvalidOperationException.
            // Synchronous bounded wait: OnExit cannot be async, and we must not hang shutdown.
            _services.DisposeAsync().AsTask().Wait(ShutdownWait);
            _services = null;
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        TryLog($"Unhandled UI exception: {e.Exception}");
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe application will keep running; see the log for details.",
            AppInfo.Name,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        TryLog($"Unobserved task exception: {e.Exception}");
        e.SetObserved();
    }

    private void TryLog(string message)
    {
        try
        {
            _services?.GetRequiredService<ILogSink>().Write(LogLevel.Error, message);
        }
        catch (ObjectDisposedException)
        {
            // Shutdown race — nowhere left to log.
        }
    }
}
