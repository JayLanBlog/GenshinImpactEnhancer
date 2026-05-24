using Serilog;

namespace Stella.Utils.Logging;

public static class StellaLogger
{
    private static ILogger? _logger;
    private static string? _logDirectory;

    public static void Initialize(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(logDirectory);
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(logDirectory, "stella-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();
        _logger.Information("[{Source}] Logger initialized. Directory: {Dir}", "StellaLogger", logDirectory);
    }

    public static void Info(string source, string message)
        => _logger?.Information("[{Source}] {Message}", source, message);

    public static void Warn(string source, string message)
        => _logger?.Warning("[{Source}] {Message}", source, message);

    public static void Error(string source, string message, Exception? ex = null)
    {
        if (ex != null)
            _logger?.Error(ex, "[{Source}] {Message}", source, message);
        else
            _logger?.Error("[{Source}] {Message}", source, message);
    }

    public static void Debug(string source, string message)
        => _logger?.Debug("[{Source}] {Message}", source, message);

    public static void Flush()
    {
        if (_logger is IDisposable disposable)
            disposable.Dispose();
    }
}
