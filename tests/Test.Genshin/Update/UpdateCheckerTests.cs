using Xunit;
using Stella.Update;

namespace Test.Genshin.Update;

public class UpdateCheckerTests
{
    [Fact]
    public void ParseVersion_ValidString_ReturnsVersion()
    {
        var version = UpdateChecker.ParseVersion("v8.10.1.0");
        Assert.Equal(8, version.Major);
        Assert.Equal(10, version.Minor);
        Assert.Equal(1, version.Build);
    }

    [Fact]
    public void ParseVersion_NoVPrefix_StillWorks()
    {
        var version = UpdateChecker.ParseVersion("1.2.3");
        Assert.Equal(1, version.Major);
        Assert.Equal(2, version.Minor);
        Assert.Equal(3, version.Build);
    }

    [Fact]
    public void IsUpdateAvailable_NewerVersion_ReturnsTrue()
    {
        var current = new Version(8, 5, 0);
        var latest = new Version(8, 10, 1);
        Assert.True(UpdateChecker.IsUpdateAvailable(current, latest));
    }

    [Fact]
    public void IsUpdateAvailable_SameVersion_ReturnsFalse()
    {
        var current = new Version(8, 10, 0);
        var latest = new Version(8, 10, 0);
        Assert.False(UpdateChecker.IsUpdateAvailable(current, latest));
    }

    [Fact]
    public void IsUpdateAvailable_OlderVersion_ReturnsFalse()
    {
        var current = new Version(8, 10, 1);
        var latest = new Version(8, 5, 0);
        Assert.False(UpdateChecker.IsUpdateAvailable(current, latest));
    }

    [Fact]
    public void ParseVersion_InvalidString_ReturnsDefault()
    {
        var version = UpdateChecker.ParseVersion("not-a-version");
        Assert.Equal(0, version.Major);
        Assert.Equal(0, version.Minor);
    }
}
