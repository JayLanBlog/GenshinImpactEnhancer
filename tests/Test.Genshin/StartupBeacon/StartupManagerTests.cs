using Xunit;
using GIEnhancer.StartupBeacon;

namespace Test.Genshin.StartupBeacon;

public class StartupManagerTests
{
    [Fact]
    public void GetStartupRegistryPath_ReturnsCorrectRegistryPath()
    {
        var path = StartupManager.GetStartupRegistryPath();
        Assert.Contains("SOFTWARE", path);
        Assert.Contains("Run", path);
    }

    [Fact]
    public void IsStartupEnabled_ChecksRegistryWithoutThrowing()
    {
        // Should not throw even without registry access in test environment
        var exception = Record.Exception(() => StartupManager.IsStartupEnabled());
        Assert.Null(exception);
    }

    [Fact]
    public void EnableDisable_DoesNotThrow()
    {
        var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "test.exe";
        var e1 = Record.Exception(() => StartupManager.EnableStartup(exe));
        var e2 = Record.Exception(() => StartupManager.DisableStartup());
        Assert.Null(e1);
        Assert.Null(e2);
    }
}
