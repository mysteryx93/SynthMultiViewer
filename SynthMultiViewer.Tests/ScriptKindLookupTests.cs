
namespace HanumanInstitute.SynthMultiViewer.Tests;

public class ScriptKindLookupTests
{
    [Theory]
    [InlineData("script.avs")]
    [InlineData("script.AVS")]
    [InlineData("include.avsi")]
    public void FromPath_AviSynthExtension_ReturnsAviSynth(string path)
    {
        var kind = ScriptKindLookup.FromPath(path);

        Assert.Equal(ScriptKind.AviSynth, kind);
    }

    [Theory]
    [InlineData("script.vpy")]
    [InlineData("script.VPY")]
    public void FromPath_VapourSynthExtension_ReturnsVapourSynth(string path)
    {
        var kind = ScriptKindLookup.FromPath(path);

        Assert.Equal(ScriptKind.VapourSynth, kind);
    }

    [Theory]
    [InlineData("image.png")]
    [InlineData("script.py")]
    [InlineData("script")]
    public void FromPath_UnknownExtension_ReturnsNull(string path)
    {
        var kind = ScriptKindLookup.FromPath(path);

        Assert.Null(kind);
    }

    [Fact]
    public void FromPath_EmptyPath_ThrowsArgumentException()
    {
        const string path = "";

        void Act()
        {
            ScriptKindLookup.FromPath(path);
        }

        Assert.Throws<ArgumentException>(Act);
    }

    [Fact]
    public void DefaultExtension_AviSynth_ReturnsAvs()
    {
        var extension = ScriptKindLookup.DefaultExtension(ScriptKind.AviSynth);

        Assert.Equal(".avs", extension);
    }

    [Fact]
    public void FileFilterExtensions_AviSynth_OmitsLeadingDot()
    {
        var extensions = ScriptKindLookup.FileFilterExtensions(ScriptKind.AviSynth);

        Assert.Equal(["avs", "avsi"], extensions);
    }
}
