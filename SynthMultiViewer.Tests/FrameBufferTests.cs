using System.Runtime.InteropServices;
using HanumanInstitute.AviSynthApi;
using HanumanInstitute.VapourSynthApi;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class FrameBufferTests
{
    [Fact]
    public void CopyTo_NativeFrameReleased_PreservesPixelsAndRowPadding()
    {
        byte[] nativeRows = [1, 2, 3, 4, 99, 99, 5, 6, 7, 8, 99, 99];
        using var destination = new NativeBuffer(16);
        using var pixels = CopyNativeFrame(nativeRows);
        var actual = new byte[16];

        pixels.CopyTo(destination.Address, 8);
        Marshal.Copy(destination.Address, actual, 0, actual.Length);

        Assert.Equal([1, 2, 3, 4, 0, 0, 0, 0, 5, 6, 7, 8, 0, 0, 0, 0], actual);
    }

    [Fact]
    public void CopyFrom_NegativeStride_FlipsRowsToTopDown()
    {
        byte[] nativeRows = [10, 11, 12, 13, 99, 99, 20, 21, 22, 23, 99, 99];
        using var source = new NativeBuffer(nativeRows.Length);
        Marshal.Copy(nativeRows, 0, source.Address, nativeRows.Length);
        using var destination = new NativeBuffer(8);
        var actual = new byte[8];

        using var pixels = FrameBuffer.CopyFrom(source.Address + 6, -6, 4, 2);
        pixels.CopyTo(destination.Address, 4);
        Marshal.Copy(destination.Address, actual, 0, actual.Length);

        Assert.Equal([10, 11, 12, 13, 20, 21, 22, 23], actual);
    }

    [Fact]
    public void CopyFrom_FlipVertical_PositiveStride_ReversesRowsToTopDown()
    {
        byte[] nativeRows = [10, 11, 12, 13, 99, 99, 20, 21, 22, 23, 99, 99];
        using var source = new NativeBuffer(nativeRows.Length);
        Marshal.Copy(nativeRows, 0, source.Address, nativeRows.Length);
        using var destination = new NativeBuffer(8);
        var actual = new byte[8];

        using var pixels = FrameBuffer.CopyFrom(source.Address, 6, 4, 2, flipVertical: true);
        pixels.CopyTo(destination.Address, 4);
        Marshal.Copy(destination.Address, actual, 0, actual.Length);

        Assert.Equal([20, 21, 22, 23, 10, 11, 12, 13], actual);
    }

    [Fact]
    public void CopyFrom_FlipVertical_ThreeRows_ReversesAllRows()
    {
        byte[] nativeRows =
        [
            1, 1, 1, 1, 99, 99,
            2, 2, 2, 2, 99, 99,
            3, 3, 3, 3, 99, 99
        ];
        using var source = new NativeBuffer(nativeRows.Length);
        Marshal.Copy(nativeRows, 0, source.Address, nativeRows.Length);
        using var destination = new NativeBuffer(12);
        var actual = new byte[12];

        using var pixels = FrameBuffer.CopyFrom(source.Address, 6, 4, 3, flipVertical: true);
        pixels.CopyTo(destination.Address, 4);
        Marshal.Copy(destination.Address, actual, 0, actual.Length);

        Assert.Equal([3, 3, 3, 3, 2, 2, 2, 2, 1, 1, 1, 1], actual);
    }

    [Fact]
    public void CopyRgbToBgra_NegativeStride_FlipsRowsToTopDown()
    {
        byte[] redRows = [1, 2, 3, 4];
        byte[] greenRows = [10, 20, 30, 40];
        byte[] blueRows = [50, 60, 70, 80];
        using var red = new NativeBuffer(redRows.Length);
        using var green = new NativeBuffer(greenRows.Length);
        using var blue = new NativeBuffer(blueRows.Length);
        Marshal.Copy(redRows, 0, red.Address, redRows.Length);
        Marshal.Copy(greenRows, 0, green.Address, greenRows.Length);
        Marshal.Copy(blueRows, 0, blue.Address, blueRows.Length);
        using var destination = new NativeBuffer(16);
        var actual = new byte[16];

        using var pixels = FrameBuffer.CopyRgbToBgra(
            red.Address + 2, -2, green.Address + 2, -2, blue.Address + 2, -2, 2, 2);
        pixels.CopyTo(destination.Address, 8);
        Marshal.Copy(destination.Address, actual, 0, actual.Length);

        Assert.Equal(
            [50, 10, 1, 255, 60, 20, 2, 255, 70, 30, 3, 255, 80, 40, 4, 255],
            actual);
    }

    [Fact]
    public void CopyFrom_FlipVertical_NegativeStride_KeepsTopDown()
    {
        byte[] nativeRows = [10, 11, 12, 13, 99, 99, 20, 21, 22, 23, 99, 99];
        using var source = new NativeBuffer(nativeRows.Length);
        Marshal.Copy(nativeRows, 0, source.Address, nativeRows.Length);
        using var destination = new NativeBuffer(8);
        var actual = new byte[8];

        using var pixels = FrameBuffer.CopyFrom(source.Address + 6, -6, 4, 2, flipVertical: true);
        pixels.CopyTo(destination.Address, 4);
        Marshal.Copy(destination.Address, actual, 0, actual.Length);

        Assert.Equal([20, 21, 22, 23, 10, 11, 12, 13], actual);
    }

    [Fact]
    public void CopyFrom_OpaqueBgra_FillsUnusedAlpha()
    {
        byte[] nativeRows = [10, 20, 30, 0, 40, 50, 60, 0];
        using var source = new NativeBuffer(nativeRows.Length);
        Marshal.Copy(nativeRows, 0, source.Address, nativeRows.Length);
        using var destination = new NativeBuffer(8);
        var actual = new byte[8];

        using var pixels = FrameBuffer.CopyFrom(source.Address, 8, 8, 1, opaqueBgra: true);
        pixels.CopyTo(destination.Address, 8);
        Marshal.Copy(destination.Address, actual, 0, actual.Length);

        Assert.Equal([10, 20, 30, 255, 40, 50, 60, 255], actual);
    }

    [Fact]
    public void CopyRgbToBgra_PlanarRgb_ProducesOpaqueBgraPixels()
    {
        using var red = new NativeBuffer(2);
        using var green = new NativeBuffer(2);
        using var blue = new NativeBuffer(2);
        using var destination = new NativeBuffer(8);
        Marshal.Copy(new byte[] { 10, 20 }, 0, red.Address, 2);
        Marshal.Copy(new byte[] { 30, 40 }, 0, green.Address, 2);
        Marshal.Copy(new byte[] { 50, 60 }, 0, blue.Address, 2);

        using var pixels = FrameBuffer.CopyRgbToBgra(red.Address, 2, green.Address, 2, blue.Address, 2, 2, 1);
        var actual = new byte[8];
        pixels.CopyTo(destination.Address, 8);
        Marshal.Copy(destination.Address, actual, 0, actual.Length);

        Assert.Equal([50, 30, 10, 255, 60, 40, 20, 255], actual);
    }

    public static TheoryData<string> AviSynthPixelTypes =>
    [
        "RGB24",
        "RGB32",
        "RGB48",
        "RGB64",
        "RGBP",
        "RGBP10",
        "RGBP16",
        "RGBPS",
        "YV12",
        "YUY2",
        "YV16",
        "YV24",
        "YV411",
        "Y8",
        "Y16",
        "Y32",
        "YUV420P10",
        "YUV420P12",
        "YUV420P16",
        "YUV420PS",
        "YUV422P10",
        "YUV444P16",
        "YUVA420",
        "YUVA420P10"
    ];

    [Theory]
    [MemberData(nameof(AviSynthPixelTypes))]
    public void CopyFrom_AviSynthStackedClip_KeepsRedOnTopAfterFlip(string pixelType)
    {
        Assert.SkipUnless(AvsScript.TryFindLibrary(out _), "AviSynth+ native library was not found.");
        using var script = AvsScript.LoadScript($"""
            top = BlankClip(length=1, width=32, height=8, pixel_type="{pixelType}", color=$FF0000)
            bot = BlankClip(length=1, width=32, height=8, pixel_type="{pixelType}", color=$0000FF)
            stacked = StackVertical(top, bot)
            return stacked
            """);
        using var frame = script.GetFrame(0);
        var plane = frame.GetPlane(0);
        using var destination = new NativeBuffer(plane.RowSize * plane.Height);
        var top = new byte[4];
        var bottom = new byte[4];

        using var pixels = FrameBuffer.CopyFrom(
            plane.Pointer, plane.Stride, plane.RowSize, plane.Height, flipVertical: true, opaqueBgra: true);
        pixels.CopyTo(destination.Address, plane.RowSize);
        Marshal.Copy(destination.Address, top, 0, top.Length);
        Marshal.Copy(IntPtr.Add(destination.Address, plane.RowSize * (plane.Height - 1)), bottom, 0, bottom.Length);

        Assert.True(plane.RowSize >= 32 * 4, $"rowSize={plane.RowSize} for {pixelType}");
        AssertTopBottom(pixelType is "Y8" or "Y16" or "Y32", top, bottom);
    }

    public static TheoryData<string> VapourSynthFormats =>
    [
        "RGB24",
        "RGB27",
        "RGB30",
        "RGB48",
        "RGBH",
        "RGBS",
        "GRAY8",
        "GRAY10",
        "GRAY16",
        "GRAY32",
        "GRAYH",
        "GRAYS",
        "YUV420P8",
        "YUV420P9",
        "YUV420P10",
        "YUV420P12",
        "YUV420P14",
        "YUV420P16",
        "YUV420PH",
        "YUV420PS",
        "YUV422P8",
        "YUV422P16",
        "YUV444P8",
        "YUV444PS",
        "YUV410P8",
        "YUV411P8",
        "YUV440P8"
    ];

    [Theory]
    [MemberData(nameof(VapourSynthFormats))]
    public void CopyRgbToBgra_VapourSynthStackedClip_KeepsRedOnTop(string format)
    {
        Assert.SkipUnless(VsHelper.TryFindLibrary(out _), "VapourSynth native library was not found.");
        using var script = VsScript.LoadScript(StackedVapourSynthClip(format));
        using var output = script.GetOutput();
        using var frame = output.GetFrame(0);
        var red = frame.GetPlane(0);
        var green = frame.GetPlane(1);
        var blue = frame.GetPlane(2);
        using var destination = new NativeBuffer(red.Width * red.Height * 4);
        var top = new byte[4];
        var bottom = new byte[4];

        using var pixels = FrameBuffer.CopyRgbToBgra(
            red.Ptr, red.Stride, green.Ptr, green.Stride, blue.Ptr, blue.Stride, red.Width, red.Height);
        pixels.CopyTo(destination.Address, red.Width * 4);
        Marshal.Copy(destination.Address, top, 0, top.Length);
        Marshal.Copy(IntPtr.Add(destination.Address, red.Width * 4 * (red.Height - 1)), bottom, 0, bottom.Length);

        AssertTopBottom(format.StartsWith("GRAY", StringComparison.Ordinal), top, bottom);
    }

    [Fact]
    public void CopyFrom_AviSynthYv12Frame_ProducesOpaqueRedBgra()
    {
        Assert.SkipUnless(AvsScript.TryFindLibrary(out _), "AviSynth+ native library was not found.");
        using var script = AvsScript.LoadScript(
            "BlankClip(length=1, width=16, height=16, pixel_type=\"YV12\", color=$FF0000)\n");
        using var frame = script.GetFrame(0);
        var plane = frame.GetPlane(0);
        using var destination = new NativeBuffer(plane.RowSize * plane.Height);
        var actual = new byte[4];

        using var pixels = FrameBuffer.CopyFrom(
            plane.Pointer, plane.Stride, plane.RowSize, plane.Height, flipVertical: true, opaqueBgra: true);
        pixels.CopyTo(destination.Address, plane.RowSize);
        Marshal.Copy(destination.Address, actual, 0, actual.Length);

        Assert.Equal(byte.MaxValue, actual[3]);
        Assert.True(actual[2] > 200, $"red={actual[2]} green={actual[1]} blue={actual[0]}");
        Assert.True(actual[2] > actual[1] + 80);
        Assert.True(actual[2] > actual[0] + 80);
    }

    [Fact]
    public void CopyRgbToBgra_VapourSynthYuv420Frame_ProducesOpaqueRedBgra()
    {
        Assert.SkipUnless(VsHelper.TryFindLibrary(out _), "VapourSynth native library was not found.");
        using var script = VsScript.LoadScript("""
            import vapoursynth as vs
            core = vs.core
            clip = core.std.BlankClip(width=16, height=16, length=1, format=vs.YUV420P8, color=[81, 90, 240])
            clip.set_output()
            """);
        using var output = script.GetOutput();
        using var frame = output.GetFrame(0);
        var red = frame.GetPlane(0);
        var green = frame.GetPlane(1);
        var blue = frame.GetPlane(2);
        using var destination = new NativeBuffer(red.Width * red.Height * 4);
        var actual = new byte[4];

        using var pixels = FrameBuffer.CopyRgbToBgra(
            red.Ptr, red.Stride, green.Ptr, green.Stride, blue.Ptr, blue.Stride, red.Width, red.Height);
        pixels.CopyTo(destination.Address, red.Width * 4);
        Marshal.Copy(destination.Address, actual, 0, actual.Length);

        Assert.Equal(byte.MaxValue, actual[3]);
        Assert.True(actual[2] > 200, $"red={actual[2]} green={actual[1]} blue={actual[0]}");
        Assert.True(actual[2] > actual[1] + 80);
        Assert.True(actual[2] > actual[0] + 80);
    }

    [Fact]
    public void CopyTo_BufferDisposed_RejectsAccessToReturnedPixels()
    {
        using var pixels = CopyNativeFrame(new byte[12]);
        using var destination = new NativeBuffer(16);

        pixels.Dispose();
        var error = Record.Exception(() => pixels.CopyTo(destination.Address, 8));

        Assert.IsType<ObjectDisposedException>(error);
    }

    private static string StackedVapourSynthClip(string format) => format == "GRAY32"
        ? """
            import vapoursynth as vs
            core = vs.core
            top = core.std.BlankClip(width=32, height=8, length=1, format=vs.GRAY32, color=[4294967295])
            bot = core.std.BlankClip(width=32, height=8, length=1, format=vs.GRAY32, color=[0])
            clip = core.std.StackVertical([top, bot])
            clip.set_output()
            """
        : $$"""
            import vapoursynth as vs
            core = vs.core
            top = core.std.BlankClip(width=32, height=8, length=1, format=vs.RGB24, color=[255, 0, 0])
            bot = core.std.BlankClip(width=32, height=8, length=1, format=vs.RGB24, color=[0, 0, 255])
            clip = core.std.StackVertical([top, bot])
            fmt = vs.{{format}}
            if clip.format.id != fmt:
                args = {"format": fmt}
                if "{{format}}".startswith(("YUV", "GRAY")):
                    args["matrix_s"] = "170m"
                clip = clip.resize.Bicubic(**args)
            clip.set_output()
            """;

    private static void AssertTopBottom(bool gray, byte[] top, byte[] bottom)
    {
        Assert.Equal(byte.MaxValue, top[3]);
        Assert.Equal(byte.MaxValue, bottom[3]);
        if (gray)
        {
            Assert.True(top[2] > bottom[2] + 40, $"top={FormatPixel(top)} bottom={FormatPixel(bottom)}");
            return;
        }

        Assert.True(top[2] > 180 && top[2] > top[0] + 60, $"top={FormatPixel(top)}");
        Assert.True(bottom[0] > 180 && bottom[0] > bottom[2] + 60, $"bottom={FormatPixel(bottom)}");
    }

    private static string FormatPixel(byte[] pixel) => $"B={pixel[0]} G={pixel[1]} R={pixel[2]} A={pixel[3]}";

    private static FrameBuffer CopyNativeFrame(byte[] rows)
    {
        using var source = new NativeBuffer(rows.Length);
        Marshal.Copy(rows, 0, source.Address, rows.Length);
        return FrameBuffer.CopyFrom(source.Address, 6, 4, 2);
    }

    private sealed class NativeBuffer : IDisposable
    {
        public NativeBuffer(int size)
        {
            Address = Marshal.AllocHGlobal(size);
            Marshal.Copy(new byte[size], 0, Address, size);
        }

        public IntPtr Address { get; }
        public void Dispose() => Marshal.FreeHGlobal(Address);
    }
}
