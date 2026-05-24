using Xunit;
using Stella.DeviceIdentifier;

namespace Test.Genshin.DeviceIdentifier;

public class DeviceIdentifierTests
{
    [Fact]
    public void Collect_ReturnsNonNullDeviceInfo()
    {
        var info = DeviceDetector.Collect();
        Assert.NotNull(info);
    }

    [Fact]
    public void Collect_GpuName_IsNotEmpty()
    {
        var info = DeviceDetector.Collect();
        Assert.False(string.IsNullOrWhiteSpace(info.GpuName));
    }

    [Fact]
    public void Collect_GpuMemory_IsPositive()
    {
        var info = DeviceDetector.Collect();
        Assert.True(info.GpuMemoryMb > 0, $"Expected GPU memory > 0, got {info.GpuMemoryMb} MB");
    }

    [Fact]
    public void Collect_CpuName_IsNotEmpty()
    {
        var info = DeviceDetector.Collect();
        Assert.False(string.IsNullOrWhiteSpace(info.CpuName));
    }

    [Fact]
    public void Collect_Resolution_IsReasonable()
    {
        var info = DeviceDetector.Collect();
        Assert.True(info.ScreenWidth >= 800, $"Width {info.ScreenWidth} too small");
        Assert.True(info.ScreenHeight >= 600, $"Height {info.ScreenHeight} too small");
    }

    [Theory]
    [InlineData("NVIDIA GeForce RTX 4060", "rtx4060")]
    [InlineData("NVIDIA GeForce GTX 1660", "gtx1660")]
    [InlineData("AMD Radeon RX 7900 XTX", "rx7900xtx")]
    public void GpuShortName_NormalizesCorrectly(string full, string expected)
    {
        var info = new DeviceInfo { GpuName = full };
        Assert.Equal(expected, info.GpuShortName);
    }
}
