using System.Runtime.InteropServices;
using Xunit;

namespace HanumanInstitute.ApiAviSynth.Tests.Integration;

[CollectionDefinition("AviSynthNative", DisableParallelization = true)]
public class AviSynthNativeCollection;

[Collection("AviSynthNative")]
public class AvsScriptIntegrationTests
{
    private static readonly Lazy<bool> NativeAvailable = new(static () =>
    {
        foreach (var candidate in AvsPathResolver.LibraryFileNames)
        {
            if (NativeLibrary.TryLoad(candidate, out var library))
            {
                NativeLibrary.Free(library);
                return true;
            }
        }

        return false;
    });

    [Fact]
    public void Read_PlaybackFunction_OmitsBufferFunctions()
    {
        SkipIfNativeUnavailable();
        using var script = AvsScript.LoadScript(
            "function CatalogBufferOnly(clip c) { return c }\nBlankClip(length=1, width=16, height=16)");

        var functions = AvsCatalog.Read();

        Assert.DoesNotContain(functions, x => x.Name == "CatalogBufferOnly");
    }

    [Fact]
    public void Read_BlankClip_IncludesWidthArgument()
    {
        SkipIfNativeUnavailable();

        var functions = AvsCatalog.Read();

        Assert.Contains(functions, x => x.Name == "BlankClip" && x.Arguments != null &&
            x.Arguments.Contains("[width]i", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadScript_BlankClip_ReturnsVideoInfo()
    {
        SkipIfNativeUnavailable();
        const string source = "BlankClip(length=12, width=160, height=120)\n";

        using var script = AvsScript.LoadScript(source);
        var info = script.VideoInfo;
        using var frame = script.GetFrame(0);
        var plane = frame.GetPlane(0);

        Assert.Equal(160, info.Width);
        Assert.Equal(120, info.Height);
        Assert.Equal(12, info.FrameCount);
        Assert.NotEqual(IntPtr.Zero, plane.Pointer);
        Assert.Equal(120, plane.Height);
        Assert.True(plane.RowSize >= 160 * 4);
    }

    [Fact]
    public void TryEvaluate_NativeLibrary_ReturnsTrue()
    {
        SkipIfNativeUnavailable();

        var usable = AvsScript.TryEvaluate(out var error);

        Assert.True(usable);
        Assert.Null(error);
    }

    [Fact]
    public void TryReadVersion_NativeLibrary_ReturnsAviSynth()
    {
        SkipIfNativeUnavailable();

        var read = AvsScript.TryReadVersion(out var version, out var detail);

        Assert.True(read);
        Assert.Contains("AviSynth", version, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(detail));
    }

    [Fact]
    public void LoadFile_BlankClip_ReturnsVideoInfo()
    {
        SkipIfNativeUnavailable();
        var path = WriteTempScript("BlankClip(length=10, width=320, height=240)\n");
        try
        {
            using var script = AvsScript.LoadFile(path);
            var info = script.VideoInfo;

            Assert.Equal(320, info.Width);
            Assert.Equal(240, info.Height);
            Assert.Equal(10, info.FrameCount);
            Assert.True(info.FpsNumerator > 0);
            Assert.True(info.FpsDenominator > 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFile_MissingFile_ThrowsFileNotFoundException()
    {
        SkipIfNativeUnavailable();
        var path = Path.Combine(Path.GetTempPath(), $"SynthMultiViewer-missing-{Guid.NewGuid():N}.avs");

        var act = () => AvsScript.LoadFile(path);

        Assert.Throws<FileNotFoundException>(act);
    }

    [Fact]
    public void LoadFile_InvalidScript_ThrowsAvsException()
    {
        SkipIfNativeUnavailable();
        var path = WriteTempScript("this is not a valid avisynth script\n");
        try
        {
            var act = () => AvsScript.LoadFile(path);

            Assert.Throws<AvsException>(act);
        }
        finally
        {
            File.Delete(path);
        }
    }

    public static TheoryData<string> DisplayPixelTypes =>
    [
        "RGB24",
        "RGB32",
        "RGB48",
        "RGB64",
        "RGBP",
        "RGBP10",
        "RGBP12",
        "RGBP14",
        "RGBP16",
        "RGBPS",
        "RGBAP",
        "RGBAP16",
        "YV12",
        "YUY2",
        "YV16",
        "YV24",
        "YV411",
        "Y8",
        "Y10",
        "Y12",
        "Y14",
        "Y16",
        "Y32",
        "YUV420P10",
        "YUV420P12",
        "YUV420P14",
        "YUV420P16",
        "YUV420PS",
        "YUV422P10",
        "YUV422P16",
        "YUV444P10",
        "YUV444P16",
        "YUV444PS",
        "YUVA420",
        "YUVA420P10",
        "YUVA444P16"
    ];

    [Theory]
    [MemberData(nameof(DisplayPixelTypes))]
    public void LoadScript_PixelType_ConvertsToPackedRgb32(string pixelType)
    {
        SkipIfNativeUnavailable();
        var source = $"BlankClip(length=2, width=32, height=16, pixel_type=\"{pixelType}\")\n";

        using var script = AvsScript.LoadScript(source);
        var info = script.VideoInfo;
        using var frame = script.GetFrame(0);
        var plane = frame.GetPlane(0);

        Assert.Equal(AvsCsBgr32, info.PixelType);
        Assert.Equal(32, info.Width);
        Assert.Equal(16, info.Height);
        Assert.Equal(16, plane.Height);
        Assert.True(plane.RowSize >= 32 * 4);
        Assert.NotEqual(IntPtr.Zero, plane.Pointer);
    }

    [Fact]
    public void LoadScript_ReturnedClip_ConvertsToPackedRgb32()
    {
        SkipIfNativeUnavailable();
        const string source = """
            clip = BlankClip(length=1, width=32, height=16, pixel_type="YUV420P10", color=$FF0000)
            return clip
            """;

        using var script = AvsScript.LoadScript(source);
        var info = script.VideoInfo;
        using var frame = script.GetFrame(0);
        var plane = frame.GetPlane(0);

        Assert.Equal(AvsCsBgr32, info.PixelType);
        Assert.True(plane.RowSize >= info.Width * 4);
        AssertRedBgra(ReadBgra(plane));
        Assert.Equal("YUV420P10", script.SourceVideoInfo.FormatName);
        Assert.NotEqual(AvsCsBgr32, script.SourceVideoInfo.PixelType);
    }

    [Fact]
    public void GetSourceFrameProperties_PropSet_ReturnsSourceKeys()
    {
        SkipIfNativeUnavailable();
        const string source = """
            BlankClip(length=2, width=16, height=16, pixel_type="YV12")
            propSet("_Matrix", 1)
            propSet("_PictType", "I")
            """;

        using var script = AvsScript.LoadScript(source);
        var properties = script.GetSourceFrameProperties(0);
        var map = properties.ToDictionary(x => x.Name, x => x.Value, StringComparer.Ordinal);

        Assert.Equal("1", map["_Matrix"]);
        Assert.Equal("I", map["_PictType"]);
    }

    [Fact]
    public void LoadScript_NoClip_ThrowsAvsException()
    {
        SkipIfNativeUnavailable();
        const string source = "x = 1\n";

        var error = Assert.Throws<AvsException>(() => AvsScript.LoadScript(source));

        Assert.Contains("did not return a video clip", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadScript_Ffms2Yuv420File_ConvertsToRedRgb32()
    {
        SkipIfNativeUnavailable();
        var path = Path.Combine(Path.GetTempPath(), $"SynthMultiViewer-{Guid.NewGuid():N}.mp4");
        try
        {
            var ffmpeg = Run("ffmpeg", $"-y -f lavfi -i color=c=red:s=32x16:d=1 -pix_fmt yuv420p \"{path}\"");
            if (ffmpeg != 0 || !File.Exists(path))
            {
                Assert.Skip("ffmpeg is not available to create a YUV420 sample.");
            }

            using var script = AvsScript.LoadScript($"FFVideoSource(\"{path.Replace("\\", "\\\\")}\")\n");

            Assert.Equal(AvsCsBgr32, script.VideoInfo.PixelType);
            AssertRedBgra(ReadBgra(script));
        }
        catch (AvsException ex) when (ex.Message.Contains("FFVideoSource", StringComparison.OrdinalIgnoreCase) ||
                                      ex.Message.Contains("I don't know what", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Skip("AviSynth FFMS2 plugin was not autoloaded.");
        }
        finally
        {
            foreach (var leftover in new[] { path, path + ".ffindex" })
            {
                if (File.Exists(leftover))
                {
                    File.Delete(leftover);
                }
            }
        }
    }

    [Fact]
    public void LoadScript_Yv12IdentityMatrix_StillConvertsToRedRgb32()
    {
        SkipIfNativeUnavailable();
        const string source = """
            BlankClip(length=1, width=16, height=16, pixel_type="YV12", color=$FF0000)
            propSet("_Matrix", 0)
            """;

        using var script = AvsScript.LoadScript(source);

        AssertRedBgra(ReadBgra(script));
    }

    private const int AvsCsBgr32 = (1 << 1) | (1 << 28) | (1 << 30);

    private static byte[] ReadBgra(AvsScript script)
    {
        using var frame = script.GetFrame(0);
        return ReadBgra(frame.GetPlane(0));
    }

    private static byte[] ReadBgra(AvsPlane plane)
    {
        var pixel = new byte[4];
        Marshal.Copy(plane.Pointer, pixel, 0, pixel.Length);
        return pixel;
    }

    private static void AssertRedBgra(byte[] pixel)
    {
        Assert.True(pixel[2] > 200, $"red={pixel[2]} green={pixel[1]} blue={pixel[0]}");
        Assert.True(pixel[2] > pixel[1] + 80);
        Assert.True(pixel[2] > pixel[0] + 80);
    }

    private static void SkipIfNativeUnavailable() =>
        Assert.SkipUnless(NativeAvailable.Value, "AviSynth+ native library was not found.");

    private static int Run(string fileName, string arguments)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            if (process == null)
            {
                return -1;
            }

            process.WaitForExit(15000);
            return process.HasExited ? process.ExitCode : -1;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    private static string WriteTempScript(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"SynthMultiViewer-{Guid.NewGuid():N}.avs");
        File.WriteAllText(path, contents);
        return path;
    }
}
