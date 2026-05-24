using Xunit;
using Stella.DeviceIdentifier;
using Stella.Services;

namespace Test.Genshin.Services;

public class PresetRecommenderTests
{
    private static List<PresetPerformanceData> CreateSampleData() => new()
    {
        new PresetPerformanceData
        {
            Name = "cinematic", DisplayName = "电影级", Description = "HDR+景深+光晕",
            VisualQualityScore = 90, StabilityScore = 85,
            PerformanceByGpu = new()
            {
                ["rtx4060"] = new() { GpuUtilization = 68, EstimatedFpsMin = 120, EstimatedFpsMax = 144, MinVramMb = 6000 },
                ["gtx1660"] = new() { GpuUtilization = 95, EstimatedFpsMin = 50, EstimatedFpsMax = 65, MinVramMb = 5000 }
            }
        },
        new PresetPerformanceData
        {
            Name = "bright", DisplayName = "明亮", Description = "锐化+色彩增强",
            VisualQualityScore = 60, StabilityScore = 95,
            PerformanceByGpu = new()
            {
                ["rtx4060"] = new() { GpuUtilization = 40, EstimatedFpsMin = 160, EstimatedFpsMax = 200, MinVramMb = 4000 },
                ["gtx1660"] = new() { GpuUtilization = 70, EstimatedFpsMin = 80, EstimatedFpsMax = 100, MinVramMb = 4000 }
            }
        },
        new PresetPerformanceData
        {
            Name = "realistic", DisplayName = "写实", Description = "自然色彩",
            VisualQualityScore = 75, StabilityScore = 90,
            PerformanceByGpu = new()
            {
                ["rtx4060"] = new() { GpuUtilization = 55, EstimatedFpsMin = 100, EstimatedFpsMax = 130, MinVramMb = 5000 }
            }
        }
    };

    [Fact]
    public void Recommend_Rtx4060_ReturnsTopThree()
    {
        var recommender = new PresetRecommender(CreateSampleData());
        var device = new DeviceInfo
        {
            GpuName = "NVIDIA GeForce RTX 4060",
            GpuMemoryMb = 8000,
            CpuName = "Intel i5-13400",
            ScreenWidth = 2560, ScreenHeight = 1440
        };
        var results = recommender.Recommend(device, communityData: null);
        Assert.Equal(3, results.Count);
        Assert.Equal("cinematic", results[0].PresetName);
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public void Recommend_Offline_RanksWithoutCommunity()
    {
        var recommender = new PresetRecommender(CreateSampleData());
        var device = new DeviceInfo
        {
            GpuName = "NVIDIA GeForce RTX 4060",
            GpuMemoryMb = 8000,
            ScreenWidth = 1920, ScreenHeight = 1080
        };
        var results = recommender.Recommend(device, communityData: null);
        Assert.All(results, r => Assert.True(r.Score > 0));
    }

    [Fact]
    public void Recommend_VramInsufficient_ExcludesPreset()
    {
        var recommender = new PresetRecommender(CreateSampleData());
        var device = new DeviceInfo
        {
            GpuName = "NVIDIA GeForce RTX 4060",
            GpuMemoryMb = 3000,
            ScreenWidth = 1920, ScreenHeight = 1080
        };
        var results = recommender.Recommend(device, communityData: null);
        Assert.Empty(results);
    }

    [Fact]
    public void Recommend_WithCommunityData_AdjustsScores()
    {
        var recommender = new PresetRecommender(CreateSampleData());
        var device = new DeviceInfo
        {
            GpuName = "NVIDIA GeForce RTX 4060",
            GpuMemoryMb = 8000,
            ScreenWidth = 1920, ScreenHeight = 1080
        };
        var communityData = new Dictionary<string, double>
        {
            ["bright"] = 80, ["realistic"] = 15, ["cinematic"] = 5
        };
        var results = recommender.Recommend(device, communityData);
        Assert.Equal("bright", results[0].PresetName);
    }
}
