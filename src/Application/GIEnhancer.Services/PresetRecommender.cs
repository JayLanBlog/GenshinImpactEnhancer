using GIEnhancer.DeviceIdentifier;
using GIEnhancer.Utils.Logging;

namespace GIEnhancer.Services;

public class PresetRecommender
{
    private readonly List<PresetPerformanceData> _presets;

    public PresetRecommender(List<PresetPerformanceData> presets)
    {
        _presets = presets;
    }

    /// <summary>
    /// 获取内置的预设性能数据（硬编码基准数据，后续可改为在线更新）。
    /// 覆盖主流 GPU：RTX 4060, RTX 3060, RTX 4090, GTX 1660, RX 7900 XTX, Arc A770 等。
    /// </summary>
    public static List<PresetPerformanceData> GetBuiltInPresets() => new()
    {
        new PresetPerformanceData
        {
            Name = "cinematic", DisplayName = "电影级", Description = "HDR+景深+光晕+锐化",
            VisualQualityScore = 90, StabilityScore = 85,
            PerformanceByGpu = new()
            {
                ["rtx4060"] = new() { GpuUtilization = 68, EstimatedFpsMin = 120, EstimatedFpsMax = 144, MinVramMb = 6000 },
                ["rtx4090"] = new() { GpuUtilization = 35, EstimatedFpsMin = 200, EstimatedFpsMax = 240, MinVramMb = 6000 },
                ["rtx3060"] = new() { GpuUtilization = 75, EstimatedFpsMin = 90, EstimatedFpsMax = 120, MinVramMb = 6000 },
                ["gtx1660"] = new() { GpuUtilization = 95, EstimatedFpsMin = 50, EstimatedFpsMax = 65, MinVramMb = 5000 },
                ["rx7900xtx"] = new() { GpuUtilization = 40, EstimatedFpsMin = 180, EstimatedFpsMax = 220, MinVramMb = 6000 },
                ["arca770"] = new() { GpuUtilization = 70, EstimatedFpsMin = 75, EstimatedFpsMax = 95, MinVramMb = 5000 }
            }
        },
        new PresetPerformanceData
        {
            Name = "bright", DisplayName = "明亮", Description = "锐化+色彩增强+亮度提升",
            VisualQualityScore = 60, StabilityScore = 95,
            PerformanceByGpu = new()
            {
                ["rtx4060"] = new() { GpuUtilization = 40, EstimatedFpsMin = 160, EstimatedFpsMax = 200, MinVramMb = 4000 },
                ["rtx4090"] = new() { GpuUtilization = 20, EstimatedFpsMin = 240, EstimatedFpsMax = 300, MinVramMb = 4000 },
                ["rtx3060"] = new() { GpuUtilization = 50, EstimatedFpsMin = 130, EstimatedFpsMax = 160, MinVramMb = 4000 },
                ["gtx1660"] = new() { GpuUtilization = 70, EstimatedFpsMin = 80, EstimatedFpsMax = 100, MinVramMb = 4000 },
                ["rx7900xtx"] = new() { GpuUtilization = 25, EstimatedFpsMin = 220, EstimatedFpsMax = 280, MinVramMb = 4000 },
                ["arca770"] = new() { GpuUtilization = 45, EstimatedFpsMin = 110, EstimatedFpsMax = 140, MinVramMb = 4000 }
            }
        },
        new PresetPerformanceData
        {
            Name = "realistic", DisplayName = "写实", Description = "自然色彩+细微锐化+轻度景深",
            VisualQualityScore = 75, StabilityScore = 90,
            PerformanceByGpu = new()
            {
                ["rtx4060"] = new() { GpuUtilization = 55, EstimatedFpsMin = 100, EstimatedFpsMax = 130, MinVramMb = 5000 },
                ["rtx4090"] = new() { GpuUtilization = 30, EstimatedFpsMin = 200, EstimatedFpsMax = 260, MinVramMb = 5000 },
                ["rtx3060"] = new() { GpuUtilization = 65, EstimatedFpsMin = 80, EstimatedFpsMax = 105, MinVramMb = 5000 },
                ["gtx1660"] = new() { GpuUtilization = 82, EstimatedFpsMin = 60, EstimatedFpsMax = 80, MinVramMb = 4000 },
                ["rx7900xtx"] = new() { GpuUtilization = 35, EstimatedFpsMin = 180, EstimatedFpsMax = 240, MinVramMb = 5000 },
                ["arca770"] = new() { GpuUtilization = 55, EstimatedFpsMin = 80, EstimatedFpsMax = 105, MinVramMb = 4000 }
            }
        },
        new PresetPerformanceData
        {
            Name = "anime", DisplayName = "动漫风", Description = "色彩增强+轮廓线+去噪",
            VisualQualityScore = 70, StabilityScore = 80,
            PerformanceByGpu = new()
            {
                ["rtx4060"] = new() { GpuUtilization = 35, EstimatedFpsMin = 170, EstimatedFpsMax = 210, MinVramMb = 4000 },
                ["rtx4090"] = new() { GpuUtilization = 18, EstimatedFpsMin = 260, EstimatedFpsMax = 320, MinVramMb = 4000 },
                ["rtx3060"] = new() { GpuUtilization = 45, EstimatedFpsMin = 140, EstimatedFpsMax = 170, MinVramMb = 4000 },
                ["gtx1660"] = new() { GpuUtilization = 60, EstimatedFpsMin = 90, EstimatedFpsMax = 115, MinVramMb = 4000 },
                ["rx7900xtx"] = new() { GpuUtilization = 20, EstimatedFpsMin = 240, EstimatedFpsMax = 300, MinVramMb = 4000 },
                ["arca770"] = new() { GpuUtilization = 40, EstimatedFpsMin = 120, EstimatedFpsMax = 150, MinVramMb = 4000 }
            }
        }
    };

    public List<RecommendationResult> Recommend(DeviceInfo device, Dictionary<string, double>? communityData)
    {
        var online = communityData != null;
        double w1 = online ? 0.40 : 0.55;
        double w2 = online ? 0.30 : 0.35;
        double w3 = online ? 0.20 : 0.0;
        double w4 = online ? 0.10 : 0.10;

        var gpuKey = device.GpuShortName;
        var results = new List<RecommendationResult>();

        foreach (var preset in _presets)
        {
            if (!preset.PerformanceByGpu.TryGetValue(gpuKey, out var perf))
                continue;

            if (device.GpuMemoryMb < perf.MinVramMb)
                continue;

            if (perf.EstimatedFpsMin < 30)
                continue;

            double avgFps = (perf.EstimatedFpsMin + perf.EstimatedFpsMax) / 2.0;
            double perfScore = Math.Clamp((avgFps - 60) / (144 - 60) * 100, 0, 100);

            double communityScore = 0;
            if (online && communityData!.TryGetValue(preset.Name, out var cp))
                communityScore = cp;

            double total = w1 * perfScore + w2 * preset.VisualQualityScore
                         + w3 * communityScore + w4 * preset.StabilityScore;

            results.Add(new RecommendationResult
            {
                PresetName = preset.Name,
                DisplayName = preset.DisplayName,
                Score = Math.Round(total, 1),
                EstimatedFpsMin = perf.EstimatedFpsMin,
                EstimatedFpsMax = perf.EstimatedFpsMax,
                GpuUtilization = perf.GpuUtilization
            });
        }

        results.Sort((a, b) => b.Score.CompareTo(a.Score));
        StellaLogger.Info("PresetRecommender", $"Recommended {results.Count} presets for {gpuKey}" +
            $" ({(online ? "online" : "offline")})");
        return results;
    }
}
