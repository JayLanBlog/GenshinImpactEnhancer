using System.Management;
using GIEnhancer.Utils.Logging;

namespace GIEnhancer.DeviceIdentifier;

public static class DeviceDetector
{
    public static DeviceInfo Collect()
    {
        try
        {
            var gpuName = "Unknown";
            long gpuMemoryMb = 0;
            int screenWidth = 1920;
            int screenHeight = 1080;

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    if (gpuName == "Unknown")
                        gpuName = obj["Name"]?.ToString() ?? "Unknown";

                    if (gpuMemoryMb == 0)
                    {
                        var ramObj = obj["AdapterRAM"];
                        if (ramObj != null && long.TryParse(ramObj.ToString(), out var bytes))
                            gpuMemoryMb = bytes / (1024 * 1024);
                    }

                    if (screenWidth == 1920)
                    {
                        var wObj = obj["CurrentHorizontalResolution"];
                        if (wObj != null && int.TryParse(wObj.ToString(), out var w))
                            screenWidth = w;
                    }

                    if (screenHeight == 1080)
                    {
                        var hObj = obj["CurrentVerticalResolution"];
                        if (hObj != null && int.TryParse(hObj.ToString(), out var h))
                            screenHeight = h;
                    }
                }
            }
            catch (Exception ex)
            {
                StellaLogger.Warn("DeviceDetector", $"Win32_VideoController query failed: {ex.Message}");
            }

            var cpuName = "Unknown";
            int logicalCores = 0;
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    if (cpuName == "Unknown")
                        cpuName = obj["Name"]?.ToString() ?? "Unknown";
                }
            }
            catch (Exception ex)
            {
                StellaLogger.Warn("DeviceDetector", $"Win32_Processor query failed: {ex.Message}");
            }

            long totalMemoryMb = 0;
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
                foreach (var obj in searcher.Get())
                {
                    if (logicalCores == 0)
                    {
                        var coresObj = obj["NumberOfLogicalProcessors"];
                        if (coresObj != null && int.TryParse(coresObj.ToString(), out var cores))
                            logicalCores = cores;
                    }

                    if (totalMemoryMb == 0)
                    {
                        var memObj = obj["TotalPhysicalMemory"];
                        if (memObj != null && long.TryParse(memObj.ToString(), out var totalBytes))
                            totalMemoryMb = totalBytes / (1024 * 1024);
                    }
                }
            }
            catch (Exception ex)
            {
                StellaLogger.Warn("DeviceDetector", $"Win32_ComputerSystem query failed: {ex.Message}");
            }

            var info = new DeviceInfo
            {
                GpuName = gpuName,
                GpuMemoryMb = gpuMemoryMb,
                CpuName = cpuName,
                LogicalCores = logicalCores,
                TotalMemoryMb = totalMemoryMb,
                ScreenWidth = screenWidth,
                ScreenHeight = screenHeight
            };

            StellaLogger.Info("DeviceDetector", $"Collected: GPU={info.GpuShortName} ({info.GpuMemoryMb}MB), " +
                $"CPU={info.CpuName} ({info.LogicalCores} cores), RAM={info.TotalMemoryMb}MB, " +
                $"Screen={info.ScreenWidth}x{info.ScreenHeight}");

            return info;
        }
        catch (Exception ex)
        {
            StellaLogger.Error("DeviceDetector", "Failed to collect device info", ex);
            return new DeviceInfo();
        }
    }
}
