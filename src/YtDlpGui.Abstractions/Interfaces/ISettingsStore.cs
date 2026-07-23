using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Abstractions.Interfaces;

/// <summary>Persists <see cref="AppSettings"/> as JSON. Load never throws — corrupt files fall back to defaults.</summary>
public interface ISettingsStore
{
    AppSettings Load();

    Task SaveAsync(AppSettings settings);
}
