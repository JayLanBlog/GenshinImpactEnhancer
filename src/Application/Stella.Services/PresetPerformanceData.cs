namespace Stella.Services;

public class PresetPerformanceData
{
    public const string DataFileName = "presets-performance.json";
    public string Name { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Description { get; init; } = "";
    public int VisualQualityScore { get; init; }
    public int StabilityScore { get; init; }
    public Dictionary<string, GpuPerformanceEntry> PerformanceByGpu { get; init; } = new();
}

public class GpuPerformanceEntry
{
    public int GpuUtilization { get; init; }
    public int EstimatedFpsMin { get; init; }
    public int EstimatedFpsMax { get; init; }
    public int MinVramMb { get; init; }
}

public class RecommendationResult
{
    public string PresetName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public double Score { get; init; }
    public int EstimatedFpsMin { get; init; }
    public int EstimatedFpsMax { get; init; }
    public int GpuUtilization { get; init; }
}
