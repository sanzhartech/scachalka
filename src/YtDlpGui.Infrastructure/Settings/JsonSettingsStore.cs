using System.Text.Json;
using System.Text.Json.Serialization;
using YtDlpGui.Abstractions;
using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Infrastructure.Settings;

/// <summary>
/// JSON settings persistence in %APPDATA%\YtDlpGui\settings.json.
/// Saves are atomic (temp file + move) so a crash mid-write can never corrupt settings;
/// a corrupt or missing file silently falls back to defaults.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly ILogSink _log;
    private readonly string _settingsPath;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public JsonSettingsStore(ILogSink log)
    {
        _log = log;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _settingsPath = Path.Combine(appData, AppInfo.DataFolder, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return AppSettings.CreateDefault();
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            if (settings is null)
            {
                _log.Write(LogLevel.Warning, "Settings file was empty — using defaults.");
                return AppSettings.CreateDefault();
            }

            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _log.Write(LogLevel.Warning, $"Could not read settings ({ex.Message}) — using defaults.");
            return AppSettings.CreateDefault();
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            var tempPath = _settingsPath + ".tmp";

            await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
            File.Move(tempPath, _settingsPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Write(LogLevel.Warning, $"Could not save settings: {ex.Message}");
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
