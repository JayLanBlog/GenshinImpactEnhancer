using Microsoft.Win32;
using Stella.Utils.Logging;

namespace Stella.StartupBeacon;

public static class StartupManager
{
    private const string AppName = "GenshinStellaMod";
    private const string RegistryRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static string GetStartupRegistryPath() => RegistryRunKey;

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, writable: false);
            return key?.GetValue(AppName) != null;
        }
        catch (Exception ex)
        {
            StellaLogger.Error("StartupManager", "Failed to check startup status", ex);
            return false;
        }
    }

    public static void EnableStartup(string executablePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RegistryRunKey);
            key?.SetValue(AppName, $"\"{executablePath}\" --minimized");
            StellaLogger.Info("StartupManager", "Startup enabled");
        }
        catch (Exception ex)
        {
            StellaLogger.Error("StartupManager", "Failed to enable startup", ex);
        }
    }

    public static void DisableStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, writable: true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);
            StellaLogger.Info("StartupManager", "Startup disabled");
        }
        catch (Exception ex)
        {
            StellaLogger.Error("StartupManager", "Failed to disable startup", ex);
        }
    }
}
