using Xunit;
using Stella.Core;
using Stella.Core.Reshade;
using Stella.Core.FpsUnlock;
using Stella.Core.Migoto;

namespace Test.Genshin.Core;

public class AllEnginesIntegrationTests
{
    [Fact]
    public async Task AllThreeEngines_CanBeRegisteredAndLaunched()
    {
        var engines = new IEngineModule[]
        {
            new ReshadeEngine(),
            new FpsUnlockEngine { TargetFps = 144 },
            new MigotoEngine()
        };

        var manager = new EngineManager(engines);
        var tempDir = Path.Combine(Path.GetTempPath(), "StellaTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = await manager.LaunchAllAsync(tempDir);
            Assert.True(result);
            Assert.True(manager.IsRunning);
            Assert.Equal(3, manager.Engines.Count);

            await manager.ShutdownAllAsync();
            Assert.False(manager.IsRunning);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
