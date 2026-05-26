using GIEnhancer.Utils.Logging;

namespace GIEnhancer.Core;

public class EngineManager
{
    private readonly IReadOnlyList<IEngineModule> _engines;
    private bool _running;

    public EngineManager(IEnumerable<IEngineModule> engines)
    {
        _engines = engines.ToList().AsReadOnly();
    }

    public async Task<bool> LaunchAllAsync(string gameDirectory, CancellationToken ct = default)
    {
        StellaLogger.Info("EngineManager", $"Launching {_engines.Count} engine modules...");
        bool anySucceeded = false;
        foreach (var engine in _engines)
        {
            try
            {
                StellaLogger.Info("EngineManager", $"Initializing: {engine.Name}");
                bool ok = await engine.InitializeAsync(gameDirectory, ct);
                if (ok)
                {
                    StellaLogger.Info("EngineManager", $"{engine.Name} initialized successfully");
                    anySucceeded = true;
                }
                else
                {
                    StellaLogger.Warn("EngineManager", $"{engine.Name} failed to initialize");
                }
            }
            catch (Exception ex)
            {
                StellaLogger.Error("EngineManager", $"{engine.Name} threw exception during init", ex);
            }
        }
        _running = anySucceeded;
        if (anySucceeded)
            StellaLogger.Info("EngineManager", "Launch complete");
        return anySucceeded;
    }

    public async Task ShutdownAllAsync()
    {
        StellaLogger.Info("EngineManager", "Shutting down all engines...");
        _running = false;
        foreach (var engine in _engines)
        {
            try { await engine.ShutdownAsync(); }
            catch (Exception ex) { StellaLogger.Error("EngineManager", $"{engine.Name} shutdown error", ex); }
        }
    }

    public IReadOnlyList<IEngineModule> Engines => _engines;
    public bool IsRunning => _running;
}
