using System.Text.Json;

namespace GIEnhancer.Utils.Logging;

public class CrashGuard
{
    private readonly string _guardFilePath;

    public CrashGuard(string appDataDir)
    {
        _guardFilePath = Path.Combine(appDataDir, ".crash_guard.json");
    }

    public void MarkRunning()
    {
        var record = new CrashRecord
        {
            Running = true,
            LastStartTime = DateTime.UtcNow,
            Pid = Environment.ProcessId
        };
        File.WriteAllText(_guardFilePath, JsonSerializer.Serialize(record));
    }

    public void MarkCleanExit()
    {
        if (File.Exists(_guardFilePath))
            File.Delete(_guardFilePath);
    }

    public bool WasLastSessionCrashed()
    {
        if (!File.Exists(_guardFilePath)) return false;
        try
        {
            var json = File.ReadAllText(_guardFilePath);
            var record = JsonSerializer.Deserialize<CrashRecord>(json);
            return record?.Running == true;
        }
        catch { return false; }
    }

    public CrashRecord? GetLastRecord()
    {
        if (!File.Exists(_guardFilePath)) return null;
        try
        {
            var json = File.ReadAllText(_guardFilePath);
            return JsonSerializer.Deserialize<CrashRecord>(json);
        }
        catch { return null; }
    }

    public class CrashRecord
    {
        public bool Running { get; set; }
        public DateTime LastStartTime { get; set; }
        public int Pid { get; set; }
    }
}
