namespace Stella.DeviceIdentifier;

public class DeviceInfo
{
    public string GpuName { get; init; } = "Unknown";
    public long GpuMemoryMb { get; init; }
    public string CpuName { get; init; } = "Unknown";
    public int LogicalCores { get; init; }
    public long TotalMemoryMb { get; init; }
    public int ScreenWidth { get; init; }
    public int ScreenHeight { get; init; }
    public string GpuShortName => NormalizeGpuName(GpuName);

    private static string NormalizeGpuName(string fullName)
    {
        var lower = fullName.ToLowerInvariant()
            .Replace("nvidia ", "").Replace("geforce ", "").Replace("amd ", "")
            .Replace("radeon ", "").Replace("rx ", "rx").Replace("intel ", "")
            .Replace("arc ", "arc").Replace("(r)", "").Replace(" graphics", "")
            .Replace(" ", "").Replace("-", "").Replace("_", "");
        return lower;
    }
}
