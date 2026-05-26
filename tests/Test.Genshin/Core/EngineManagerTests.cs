using Xunit;
using GIEnhancer.Core;

namespace Test.Genshin.Core;

public class EngineManagerTests
{
    private class TestEngine : IEngineModule
    {
        public string Name { get; }
        public bool IsInitialized { get; private set; }
        public TestEngine(string name) => Name = name;
        public Task<bool> InitializeAsync(string dir, CancellationToken ct)
        {
            IsInitialized = true;
            return Task.FromResult(true);
        }
        public Task ShutdownAsync()
        {
            IsInitialized = false;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task LaunchAll_SuccessfullyInitializesEngines()
    {
        var engine1 = new TestEngine("ReShade");
        var engine2 = new TestEngine("FPSUnlock");
        var manager = new EngineManager(new[] { engine1, engine2 });
        var result = await manager.LaunchAllAsync("C:\\FakeGame");
        Assert.True(result);
        Assert.True(engine1.IsInitialized);
        Assert.True(engine2.IsInitialized);
    }

    [Fact]
    public async Task LaunchAll_PartialFailure_StillSucceeds()
    {
        var working = new TestEngine("Working");
        var failing = new FailingEngine();
        var manager = new EngineManager(new IEngineModule[] { working, failing });
        var result = await manager.LaunchAllAsync("C:\\FakeGame");
        Assert.True(result);
        Assert.True(working.IsInitialized);
        Assert.False(failing.IsInitialized);
    }

    [Fact]
    public async Task ShutdownAll_CleansUpAllEngines()
    {
        var engine1 = new TestEngine("E1");
        var engine2 = new TestEngine("E2");
        var manager = new EngineManager(new[] { engine1, engine2 });
        await manager.LaunchAllAsync("C:\\FakeGame");
        await manager.ShutdownAllAsync();
        Assert.False(engine1.IsInitialized);
        Assert.False(engine2.IsInitialized);
    }

    private class FailingEngine : IEngineModule
    {
        public string Name => "Failing";
        public bool IsInitialized => false;
        public Task<bool> InitializeAsync(string d, CancellationToken c) => Task.FromResult(false);
        public Task ShutdownAsync() => Task.CompletedTask;
    }
}
