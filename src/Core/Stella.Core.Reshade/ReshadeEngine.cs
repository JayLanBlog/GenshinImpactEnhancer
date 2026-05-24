using Stella.Utils.Logging;

namespace Stella.Core.Reshade;

public class ReshadeEngine : IEngineModule
{
    public string Name => "ReShade";
    public bool IsInitialized { get; private set; }
    public string? PresetDirectory { get; private set; }
    public string? ActivePreset { get; private set; }

    public Task<bool> InitializeAsync(string gameDirectory, CancellationToken ct = default)
    {
        PresetDirectory = Path.Combine(gameDirectory, "reshade-shaders");
        StellaLogger.Info(Name, $"Preset directory: {PresetDirectory}");
        IsInitialized = true;
        StellaLogger.Info(Name, "Initialized (stub mode)");
        return Task.FromResult(true);
    }

    public Task ShutdownAsync()
    {
        IsInitialized = false;
        StellaLogger.Info(Name, "Shut down");
        return Task.CompletedTask;
    }

    public bool LoadPreset(string presetFilePath)
    {
        if (!File.Exists(presetFilePath))
        {
            StellaLogger.Warn(Name, $"Preset not found: {presetFilePath}");
            return false;
        }
        ActivePreset = Path.GetFileNameWithoutExtension(presetFilePath);
        StellaLogger.Info(Name, $"Preset loaded: {ActivePreset}");
        return true;
    }
}
