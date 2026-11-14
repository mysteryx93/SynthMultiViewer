using Xunit;

namespace HanumanInstitute.VapourSynthApi.Tests;

public class NativeLibraryLocatorTests
{
    [Fact]
    public void Matches_ImportName_ReturnsTrue()
    {
        var profile = NativeScriptHosts.VapourSynthScript;

        var matches = NativeLibraryLocator.Matches(profile, profile.ImportName);

        Assert.True(matches);
    }

    [Fact]
    public void Matches_KnownFileName_ReturnsTrue()
    {
        var profile = NativeScriptHosts.VapourSynthScript;

        var matches = NativeLibraryLocator.Matches(profile, profile.FileNames[0]);

        Assert.True(matches);
    }

    [Fact]
    public void Matches_UnknownName_ReturnsFalse()
    {
        var profile = NativeScriptHosts.VapourSynthScript;

        var matches = NativeLibraryLocator.Matches(profile, "avisynth");

        Assert.False(matches);
    }
}
