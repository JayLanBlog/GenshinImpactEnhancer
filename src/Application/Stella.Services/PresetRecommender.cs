using Stella.DeviceIdentifier;
using Stella.Utils.Logging;

namespace Stella.Services;

public class PresetRecommender
{
    private readonly List<PresetPerformanceData> _presets;

    public PresetRecommender(List<PresetPerformanceData> presets)
    {
        _presets = presets;
    }

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
