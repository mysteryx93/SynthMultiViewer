using Avalonia.Media;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class TabColorsTests
{
    [Fact]
    public void For_DarkTheme_UsesConfiguredEditorAndScalesViewerBrightness()
    {
        var vs = TabColors.For(ScriptKind.VapourSynth, false, AppTheme.Dark);
        var avs = TabColors.For(ScriptKind.AviSynth, false, AppTheme.Dark);
        var vsViewer = TabColors.For(ScriptKind.VapourSynth, true, AppTheme.Dark);
        var avsViewer = TabColors.For(ScriptKind.AviSynth, true, AppTheme.Dark);

        Assert.Equal(TabColors.VapourSynth, vs);
        Assert.Equal(TabColors.AviSynth, avs);
        Assert.Equal(Scale(vs, TabColors.ViewerBrightness), vsViewer);
        Assert.Equal(Scale(avs, TabColors.ViewerBrightness), avsViewer);
    }

    [Fact]
    public void For_LightTheme_InvertsDarkFill()
    {
        foreach (var kind in new[] { ScriptKind.VapourSynth, ScriptKind.AviSynth })
        {
            foreach (var viewer in new[] { false, true })
            {
                var dark = TabColors.For(kind, viewer, AppTheme.Dark);
                var light = TabColors.For(kind, viewer, AppTheme.Light);

                Assert.Equal(Invert(dark), light);
            }
        }
    }

    [Fact]
    public void For_CustomHue_UsesColorUnmixed()
    {
        var fill = TabColors.For(ScriptKind.VapourSynth, false, AppTheme.Light, Colors.HotPink);

        Assert.Equal(Colors.HotPink, fill);
        Assert.NotEqual(TabColors.Mix(Colors.HotPink, false, false), fill);
    }

    private static Color Scale(Color color, double factor) =>
        Color.FromRgb(
            (byte)Math.Clamp(Math.Round(color.R * factor), 0, 255),
            (byte)Math.Clamp(Math.Round(color.G * factor), 0, 255),
            (byte)Math.Clamp(Math.Round(color.B * factor), 0, 255));

    private static Color Invert(Color color) =>
        Color.FromRgb((byte)(255 - color.R), (byte)(255 - color.G), (byte)(255 - color.B));
}
