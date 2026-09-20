using HanumanInstitute.ScriptAssist.VapourSynth;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests.Host;

public class VapourSynthImportsTests
{
    [Fact]
    public void Contains_ImportLine_ReturnsTrue()
    {
        const string text = "import vapoursynth as vs\nimport havsfunc\n";

        var found = VapourSynthImports.Contains(text, "havsfunc");

        Assert.True(found);
    }

    [Fact]
    public void Contains_FromImport_ReturnsFalse()
    {
        const string text = "from havsfunc import QTGMC\n";

        var found = VapourSynthImports.Contains(text, "havsfunc");

        Assert.False(found);
    }

    [Fact]
    public void Contains_IndentedImport_ReturnsFalse()
    {
        const string text = "def Foo():\n    import havsfunc\n";

        var found = VapourSynthImports.Contains(text, "havsfunc");

        Assert.False(found);
    }

    [Fact]
    public void InsertionOffset_AfterLastImport_IncludesNewline()
    {
        const string text = "import vapoursynth as vs\ncore = vs.core\n";

        var offset = VapourSynthImports.InsertionOffset(text);

        Assert.Equal("import vapoursynth as vs\n".Length, offset);
    }

    [Fact]
    public void Statement_MidLine_PrependsNewline()
    {
        const string text = "import vs";

        var line = VapourSynthImports.Statement(text, text.Length, "havsfunc");

        Assert.Equal("\nimport havsfunc\n", line);
    }
}
