using System;
using Xunit;
using Stella.Utils.Logging;
using System.IO;

namespace Test.Genshin.Logging;

public class StellaLoggerTests
{
    [Fact]
    public void Initialize_CreatesLogDirectory()
    {
        var logDir = Path.Combine(Path.GetTempPath(), "StellaTestLogs", "LoggerInit");
        if (Directory.Exists(logDir)) Directory.Delete(logDir, true);
        StellaLogger.Initialize(logDir);
        Assert.True(Directory.Exists(logDir));
        StellaLogger.Flush();
        Directory.Delete(logDir, true);
    }

    [Fact]
    public void Log_Info_WritesToLogFile()
    {
        var logDir = Path.Combine(Path.GetTempPath(), "StellaTestLogs", "LogInfo");
        StellaLogger.Initialize(logDir);
        StellaLogger.Info("TestCategory", "Hello from test");
        StellaLogger.Flush();
        var files = Directory.GetFiles(logDir, "*.log");
        Assert.NotEmpty(files);
        var content = File.ReadAllText(files[0]);
        Assert.Contains("Hello from test", content);
        Assert.Contains("[INF]", content);
        Assert.Contains("TestCategory", content);
        Directory.Delete(logDir, true);
    }

    [Fact]
    public void Log_Error_IncludesExceptionStack()
    {
        var logDir = Path.Combine(Path.GetTempPath(), "StellaTestLogs", "LogError");
        StellaLogger.Initialize(logDir);
        try { throw new InvalidOperationException("Test error"); }
        catch (Exception ex) { StellaLogger.Error("TestCategory", "Something broke", ex); }
        StellaLogger.Flush();
        var files = Directory.GetFiles(logDir, "*.log");
        var content = File.ReadAllText(files[0]);
        Assert.Contains("[ERR]", content);
        Assert.Contains("Test error", content);
        Assert.Contains("InvalidOperationException", content);
        Directory.Delete(logDir, true);
    }
}
