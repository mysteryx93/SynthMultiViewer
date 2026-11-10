using ReactiveUI.Builder;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class VideoPropertiesViewModelTests
{
    static VideoPropertiesViewModelTests() =>
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();

    [Fact]
    public void Rebuild_NoViewer_ShowsEmptyState()
    {
        var model = new VideoPropertiesViewModel();

        model.Rebuild(null);

        var row = Assert.Single(model.Items);
        Assert.True(row.IsHeader);
        Assert.Equal("No clip loaded", row.Name);
    }

    [Fact]
    public void Rebuild_Yuv420P10_ShowsSourceFormatNotRgb()
    {
        var viewer = new ViewerViewModel
        {
            ClipInfo = new()
            {
                Host = ScriptKind.AviSynth,
                Width = 32,
                Height = 16,
                FrameCount = 24,
                FpsNumerator = 24,
                FpsDenominator = 1,
                FormatName = "YUV420P10",
                ColorFamily = "YUV",
                BitDepth = 10,
                SampleType = "Integer",
                Subsampling = "4:2:0",
                Planes = 3
            },
            Position = TimeSpan.FromSeconds(3)
        };
        var model = new VideoPropertiesViewModel();

        model.Rebuild(viewer);

        Assert.Contains(model.Items, x => x is { IsHeader: true, Name: "Clip" });
        Assert.Contains(model.Items, x => x is { IsHeader: true, Name: "Frame" });
        Assert.Equal("AviSynth", Value(model, "Host"));
        Assert.Equal("32×16", Value(model, "Size"));
        Assert.Equal("YUV420P10", Value(model, "Format"));
        Assert.Equal("YUV", Value(model, "Color family"));
        Assert.Equal("10", Value(model, "Bit depth"));
        Assert.Equal("4:2:0", Value(model, "Subsampling"));
        Assert.Equal("4", Value(model, "Index"));
        Assert.Equal("None", Value(model, "Properties"));
        Assert.DoesNotContain(model.Items, x => x.Value is "RGB32" or "RGB24");
    }

    [Fact]
    public void Rebuild_EmptyAviSynthFrameProperties_KeepsFrameSection()
    {
        var viewer = new ViewerViewModel
        {
            ClipInfo = new()
            {
                Host = ScriptKind.AviSynth,
                Width = 8,
                Height = 8,
                FrameCount = 1,
                FpsNumerator = 24,
                FpsDenominator = 1,
                FormatName = "YV12",
                ColorFamily = "YUV",
                BitDepth = 8,
                SampleType = "Integer",
                Subsampling = "4:2:0",
                Planes = 3
            },
            FrameProperties = []
        };
        var model = new VideoPropertiesViewModel();

        model.Rebuild(viewer);

        Assert.Contains(model.Items, x => x is { IsHeader: true, Name: "Frame" });
        Assert.Equal("None", Value(model, "Properties"));
    }

    [Fact]
    public void Rebuild_FrameProperties_ListsKeys()
    {
        var viewer = new ViewerViewModel
        {
            ClipInfo = new()
            {
                Host = ScriptKind.VapourSynth,
                Width = 16,
                Height = 16,
                FrameCount = 2,
                FpsNumerator = 24000,
                FpsDenominator = 1001,
                FormatName = "YUV420P8",
                ColorFamily = "YUV",
                BitDepth = 8,
                SampleType = "Integer",
                Subsampling = "4:2:0",
                Planes = 3
            },
            FrameProperties =
            [
                new("_Matrix", "1"),
                new("_PictType", "I"),
                new("MyFilter", "on")
            ]
        };
        var model = new VideoPropertiesViewModel();

        model.Rebuild(viewer);

        Assert.Equal("VapourSynth", Value(model, "Host"));
        Assert.Equal("1 (BT.709)", Value(model, "_Matrix"));
        Assert.Equal("I (Intra)", Value(model, "_PictType"));
        Assert.Equal("on", Value(model, "MyFilter"));
        Assert.DoesNotContain(model.Items, x => x.Name == "Properties");
    }

    [Fact]
    public void ViewerChanged_UpdatesRows()
    {
        var model = new VideoPropertiesViewModel();
        var viewer = new ViewerViewModel
        {
            ClipInfo = new()
            {
                Host = ScriptKind.VapourSynth,
                Width = 8,
                Height = 8,
                FrameCount = 1,
                FpsNumerator = 24,
                FpsDenominator = 1,
                FormatName = "GRAY8",
                ColorFamily = "Gray",
                BitDepth = 8,
                SampleType = "Integer",
                Planes = 1
            }
        };

        model.Viewer = viewer;

        Assert.Equal("GRAY8", Value(model, "Format"));
    }

    [Fact]
    public void ViewerChanged_FrameProperties_UpdatesRows()
    {
        var model = new VideoPropertiesViewModel();
        var viewer = new ViewerViewModel
        {
            ClipInfo = new()
            {
                Host = ScriptKind.VapourSynth,
                Width = 8,
                Height = 8,
                FrameCount = 1,
                FpsNumerator = 24,
                FpsDenominator = 1,
                FormatName = "GRAY8",
                ColorFamily = "Gray",
                BitDepth = 8,
                SampleType = "Integer",
                Planes = 1
            }
        };
        model.Viewer = viewer;

        viewer.Position = TimeSpan.FromSeconds(0);
        viewer.FrameProperties = [new("_DurationNum", "1")];

        Assert.Equal("1", Value(model, "_DurationNum"));
    }

    [Theory]
    [InlineData(24, 1, "24 fps")]
    [InlineData(24000, 1001, "24000/1001 (23.976 fps)")]
    [InlineData(0, 1, "—")]
    public void FormatFrameRate_KnownValues_MatchesDisplay(long num, long den, string expected)
    {
        Assert.Equal(expected, VideoPropertiesViewModel.FormatFrameRate(num, den));
    }

    [Theory]
    [InlineData("_Matrix", "1", "1 (BT.709)")]
    [InlineData("_Matrix", "2", "2 (Unspecified)")]
    [InlineData("_ColorRange", "1", "1 (Limited)")]
    [InlineData("_Range", "1", "1 (Full)")]
    [InlineData("_FieldBased", "0", "0 (Progressive)")]
    [InlineData("_DurationNum", "1", "1")]
    [InlineData("MyFilter", "on", "on")]
    public void Format_ReservedAndCustom_DecodesKnownKeys(string name, string value, string expected)
    {
        Assert.Equal(expected, FramePropertyDisplay.Format(name, value));
    }

    private static string Value(VideoPropertiesViewModel model, string name) =>
        Assert.Single(model.Items, x => !x.IsHeader && x.Name == name).Value;
}
