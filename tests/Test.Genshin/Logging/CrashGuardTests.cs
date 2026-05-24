using System.IO;
using Xunit;
using Stella.Utils.Logging;

namespace Test.Genshin.Logging;

public class CrashGuardTests
{
    [Fact]
    public void WasLastSessionCrashed_NoFile_ReturnsFalse()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CrashGuardTest", "NoFile");
        var guard = new CrashGuard(dir);
        Assert.False(guard.WasLastSessionCrashed());
    }

    [Fact]
    public void MarkRunning_Then_WasLastSessionCrashed_ReturnsTrue()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CrashGuardTest", "Running");
        Directory.CreateDirectory(dir);
        var guard = new CrashGuard(dir);
        guard.MarkRunning();
        var guard2 = new CrashGuard(dir);
        Assert.True(guard2.WasLastSessionCrashed());
        Directory.Delete(dir, true);
    }

    [Fact]
    public void MarkCleanExit_ClearsCrashState()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CrashGuardTest", "CleanExit");
        Directory.CreateDirectory(dir);
        var guard = new CrashGuard(dir);
        guard.MarkRunning();
        guard.MarkCleanExit();
        Assert.False(guard.WasLastSessionCrashed());
        Directory.Delete(dir, true);
    }
}
