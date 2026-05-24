namespace Stella.Core;

public interface IEngineModule
{
    string Name { get; }
    Task<bool> InitializeAsync(string gameDirectory, CancellationToken ct = default);
    bool IsInitialized { get; }
    Task ShutdownAsync();
}
