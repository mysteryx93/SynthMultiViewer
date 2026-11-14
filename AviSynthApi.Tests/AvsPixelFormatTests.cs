using Xunit;

namespace HanumanInstitute.AviSynthApi.Tests;

public class AvsPixelFormatTests
{
    private const int Planar = unchecked((int)0x80000000);
    private const int Interleaved = 1 << 30;
    private const int Yuv = 1 << 29;
    private const int Bgr = 1 << 28;
    private const int VPlaneFirst = 1 << 3;
    private const int SampleBits10 = 5 << 16;
    private const int SampleBits32 = 2 << 16;
    private const int GenericYuv420 = Planar | Yuv | VPlaneFirst;
    private const int GenericY = Planar | Interleaved | Yuv;

    [Theory]
    [InlineData(1 | Bgr | Interleaved, "RGB24", "RGB", 8, "Integer", null, 1)]
    [InlineData(2 | Bgr | Interleaved, "RGB32", "RGB", 8, "Integer", null, 1)]
    [InlineData(1 << 2 | Yuv | Interleaved, "YUY2", "YUV", 8, "Integer", "4:2:2", 1)]
    [InlineData(GenericYuv420, "YV12", "YUV", 8, "Integer", "4:2:0", 3)]
    [InlineData(GenericYuv420 | SampleBits10, "YUV420P10", "YUV", 10, "Integer", "4:2:0", 3)]
    [InlineData(GenericYuv420 | SampleBits32, "YUV420PS", "YUV", 32, "Float", "4:2:0", 3)]
    [InlineData(GenericY, "Y8", "Gray", 8, "Integer", null, 1)]
    public void GetName_KnownPixelType_ReturnsLayout(
        int pixelType, string name, string family, int depth, string sample, string? sub, int planes)
    {
        var actualName = AvsPixelFormat.GetName(pixelType);

        Assert.Equal(name, actualName);
        Assert.Equal(family, AvsPixelFormat.GetColorFamily(pixelType));
        Assert.Equal(depth, AvsPixelFormat.GetBitDepth(pixelType));
        Assert.Equal(sample, AvsPixelFormat.GetSampleType(pixelType));
        Assert.Equal(sub, AvsPixelFormat.GetSubsampling(pixelType));
        Assert.Equal(planes, AvsPixelFormat.GetPlaneCount(pixelType));
    }

    [Fact]
    public void GetName_Unknown_ReturnsUnknown()
    {
        const int pixelType = 0;

        var name = AvsPixelFormat.GetName(pixelType);

        Assert.Equal("Unknown", name);
    }
}
