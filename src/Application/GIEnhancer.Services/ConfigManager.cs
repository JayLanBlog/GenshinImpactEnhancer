using System.Text.Json;
using GIEnhancer.Utils.Logging;

namespace GIEnhancer.Services;

public class ConfigManager<T> where T : class, new()
{
    private readonly string _configPath;

    public ConfigManager(string configPath)
    {
        _configPath = configPath;
    }

    public T Load()
    {
        if (!File.Exists(_configPath))
        {
            StellaLogger.Info("ConfigManager", $"Config not found at {_configPath}, using defaults");
            return new T();
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<T>(json) ?? new T();
            StellaLogger.Info("ConfigManager", $"Config loaded from {_configPath}");
            return config;
        }
        catch (Exception ex)
        {
            StellaLogger.Error("ConfigManager", $"Failed to load config, using defaults", ex);
            return new T();
        }
    }

    public void Save(T config)
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (dir != null) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
            StellaLogger.Info("ConfigManager", $"Config saved to {_configPath}");
        }
        catch (Exception ex)
        {
            StellaLogger.Error("ConfigManager", "Failed to save config", ex);
        }
    }
}
