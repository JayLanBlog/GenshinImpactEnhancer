using GIEnhancer.Utils.Logging;

namespace GIEnhancer.Core.Migoto;

public class MigotoEngine : IEngineModule
{
    public string Name => "3DMigoto";
    public bool IsInitialized { get; private set; }
    public int LoadedModCount { get; private set; }

    public Task<bool> InitializeAsync(string gameDirectory, CancellationToken ct = default)
    {
        var modDir = Path.Combine(gameDirectory, "Mods");
        StellaLogger.Info(Name, $"Mod directory: {modDir}");
        IsInitialized = true;
        LoadedModCount = 0;
        StellaLogger.Info(Name, "Initialized (stub) - 0 mods loaded");
        return Task.FromResult(true);
    }

    public Task ShutdownAsync()
    {
        IsInitialized = false;
        LoadedModCount = 0;
        StellaLogger.Info(Name, "Shut down");
        return Task.CompletedTask;
    }
}
