using System.IO;
using System.Text.Json;
using SGuardLimiterMax.Models;

namespace SGuardLimiterMax.Services;

/// <summary>
/// Manages portable Config.json stored alongside the executable.
/// No AppData, no Registry — fully portable.
/// </summary>
public static class ConfigManager
{
    private static readonly string ConfigPath = Path.Combine(
        AppContext.BaseDirectory, "Config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Loads config from disk. Creates a default file if none exists.
    /// </summary>
    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var defaults = new AppConfig();
            Save(defaults);
            return defaults;
        }

        try
        {
            string json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        catch
        {
            // Corrupt or unreadable file — reset to defaults.
            var defaults = new AppConfig();
            Save(defaults);
            return defaults;
        }
    }

    /// <summary>
    /// Persists the current config state to disk.
    /// </summary>
    public static void Save(AppConfig config)
    {
        try
        {
            string json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Write failure is non-fatal; silently ignore.
        }
    }
}
