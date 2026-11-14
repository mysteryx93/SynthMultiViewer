using HanumanInstitute.AviSynthApi;
using HanumanInstitute.VapourSynthApi;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class ClipInfoTests
{
    [Fact]
    public void FromAviSynth_Yuv420P10_KeepsSourceLayout()
    {
        var info = new AvsVideoInfo
        {
            Width = 32,
            Height = 16,
            FrameCount = 10,
            FpsNumerator = 24,
            FpsDenominator = 1,
            PixelType = unchecked((int)0x80000000) | (1 << 29) | (1 << 3) | (5 << 16)
        };

        var clip = ClipInfo.FromAviSynth(info);

        Assert.Equal(ScriptKind.AviSynth, clip.Host);
        Assert.Equal("YUV420P10", clip.FormatName);
        Assert.Equal("YUV", clip.ColorFamily);
        Assert.Equal(10, clip.BitDepth);
        Assert.Equal("Integer", clip.SampleType);
        Assert.Equal("4:2:0", clip.Subsampling);
        Assert.Equal(3, clip.Planes);
        Assert.Equal(32, clip.Width);
        Assert.Equal(16, clip.Height);
        Assert.Equal(10, clip.FrameCount);
    }

    [Fact]
    public void FromVapourSynth_Yuv420P10_KeepsSourceLayout()
    {
        var info = new VsVideoInfo
        {
            Width = 32,
            Height = 16,
            NumFrames = 10,
            FpsNum = 24000,
            FpsDen = 1001,
            Format = new()
            {
                ColorFamily = VsColorFamily.YUV,
                SampleType = VsSampleType.Integer,
                BitsPerSample = 10,
                SubSamplingW = 1,
                SubSamplingH = 1,
                NumPlanes = 3
            }
        };

        var clip = ClipInfo.FromVapourSynth(info);

        Assert.Equal(ScriptKind.VapourSynth, clip.Host);
        Assert.Equal("YUV420P10", clip.FormatName);
        Assert.Equal("YUV", clip.ColorFamily);
        Assert.Equal(10, clip.BitDepth);
        Assert.Equal("4:2:0", clip.Subsampling);
        Assert.Equal(3, clip.Planes);
    }
}
