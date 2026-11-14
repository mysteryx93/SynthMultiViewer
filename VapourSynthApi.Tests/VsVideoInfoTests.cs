using Xunit;

namespace HanumanInstitute.VapourSynthApi.Tests;

public class VsVideoInfoTests
{
    [Fact]
    public void IsConstantFormat_ZeroSize_ReturnsFalse()
    {
        var info = new VsVideoInfo();

        var isConstant = info.IsConstantFormat;

        Assert.False(isConstant);
    }

    [Fact]
    public void IsConstantFormat_FixedSizeAndPlanes_ReturnsTrue()
    {
        var info = new VsVideoInfo { Width = 640, Height = 480, Format = new() { NumPlanes = 3 } };

        var isConstant = info.IsConstantFormat;

        Assert.True(isConstant);
    }

    [Fact]
    public void IsSameFormat_MatchingClips_ReturnsTrue()
    {
        var format = new VsFormat { NumPlanes = 3, ColorFamily = VsColorFamily.RGB };
        var first = new VsVideoInfo { Width = 1920, Height = 1080, Format = format };
        var second = new VsVideoInfo { Width = 1920, Height = 1080, Format = format };

        var isSame = first.IsSameFormat(second);

        Assert.True(isSame);
    }

    [Fact]
    public void IsSameFormat_DifferentSize_ReturnsFalse()
    {
        var format = new VsFormat { NumPlanes = 3 };
        var first = new VsVideoInfo { Width = 1920, Height = 1080, Format = format };
        var second = new VsVideoInfo { Width = 1280, Height = 720, Format = format };

        var isSame = first.IsSameFormat(second);

        Assert.False(isSame);
    }
}
