using Stella.Utils.Logging;

namespace Stella.Core.FpsUnlock;

public class FpsUnlockEngine : IEngineModule
{
    public string Name => "FPS Unlock";
    public bool IsInitialized { get; private set; }
    public int TargetFps { get; set; } = 144;
    public string? OffsetConfigPath { get; private set; }

    public Task<bool> InitializeAsync(string gameDirectory, CancellationToken ct = default)
    {
        OffsetConfigPath = Path.Combine(gameDirectory, "stella-fps-offsets.json");
        StellaLogger.Info(Name, $"Offset config: {OffsetConfigPath}");
        IsInitialized = true;
        StellaLogger.Info(Name, $"Initialized (stub) - target FPS: {TargetFps}");
        return Task.FromResult(true);
    }

    public Task ShutdownAsync()
    {
        IsInitialized = false;
        StellaLogger.Info(Name, "Shut down");
        return Task.CompletedTask;
    }
}
