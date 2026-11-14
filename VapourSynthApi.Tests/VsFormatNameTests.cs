using Xunit;

namespace HanumanInstitute.VapourSynthApi.Tests;

public class VsFormatNameTests
{
    [Theory]
    [InlineData(VsColorFamily.RGB, VsSampleType.Integer, 8, 0, 0, "RGB24", null)]
    [InlineData(VsColorFamily.RGB, VsSampleType.Float, 32, 0, 0, "RGBS", null)]
    [InlineData(VsColorFamily.Gray, VsSampleType.Integer, 8, 0, 0, "GRAY8", null)]
    [InlineData(VsColorFamily.Gray, VsSampleType.Integer, 32, 0, 0, "GRAY32", null)]
    [InlineData(VsColorFamily.Gray, VsSampleType.Float, 32, 0, 0, "GRAYS", null)]
    [InlineData(VsColorFamily.YUV, VsSampleType.Integer, 8, 1, 1, "YUV420P8", "4:2:0")]
    [InlineData(VsColorFamily.YUV, VsSampleType.Integer, 10, 1, 1, "YUV420P10", "4:2:0")]
    [InlineData(VsColorFamily.YUV, VsSampleType.Float, 32, 0, 0, "YUV444PS", "4:4:4")]
    public void From_PresetLayout_ReturnsName(
        VsColorFamily family, VsSampleType sample, int bits, int subW, int subH, string name, string? sub)
    {
        var format = new VsFormat
        {
            ColorFamily = family,
            SampleType = sample,
            BitsPerSample = bits,
            SubSamplingW = subW,
            SubSamplingH = subH,
            NumPlanes = family == VsColorFamily.Gray ? 1 : 3
        };

        var actual = VsFormatName.From(format);

        Assert.Equal(name, actual);
        Assert.Equal(sub, VsFormatName.Subsampling(format));
    }
}
