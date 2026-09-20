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

    [Fact]
    public void Contains_ImportInsideString_ReturnsFalse()
    {
        const string text = "\"\"\"\nimport helper\n\"\"\"\nclip = \n";

        var found = VapourSynthImports.Contains(text, "helper");

        Assert.False(found);
    }

    [Fact]
    public void Contains_AliasedImport_DoesNotBindModuleName()
    {
        const string text = "import helper as h\n";

        var found = VapourSynthImports.Contains(text, "helper");

        Assert.False(found);
        Assert.False(VapourSynthImports.Contains(text, "h"));
    }

    [Fact]
    public void Contains_AliasOfOtherModule_DoesNotCount()
    {
        const string text = "import other as helper\n";

        var found = VapourSynthImports.Contains(text, "helper");

        Assert.False(found);
    }

    [Fact]
    public void Contains_DottedImport_MatchesFullPath()
    {
        const string text = "import pkg.sub\n";

        var found = VapourSynthImports.Contains(text, "pkg.sub");

        Assert.True(found);
    }

    [Fact]
    public void Contains_LaterAssignment_ShadowsImport()
    {
        const string text = "import helper\nhelper = 1\n";

        var found = VapourSynthImports.Contains(text, "helper");

        Assert.False(found);
    }

    [Fact]
    public void Plan_AfterSemicolon_InsertsAfterPhysicalLine()
    {
        const string text = "import os; x=1\nclip = ";

        var plan = VapourSynthImports.Plan(text, "helper");

        Assert.True(plan.Needed);
        Assert.Equal("import os; x=1\n".Length, plan.Offset);
        Assert.Equal("import helper\n", plan.Text);
    }

    [Fact]
    public void Plan_AfterPrologue_SkipsShebangEncodingAndDocstring()
    {
        const string text = "#!/usr/bin/env python3\n# -*- coding: utf-8 -*-\n\"\"\"module docstring\"\"\"\nclip = \n";

        var plan = VapourSynthImports.Plan(text, "helper");

        Assert.True(plan.Needed);
        Assert.Equal(text.IndexOf("clip", StringComparison.Ordinal), plan.Offset);
    }

    [Fact]
    public void Contains_CommaImport_FindsLaterName()
    {
        const string text = "import os, helper\n";

        var found = VapourSynthImports.Contains(text, "helper");

        Assert.True(found);
    }

    [Fact]
    public void InsertionOffset_UnclosedFromImport_StaysBeforeStatement()
    {
        const string text = "from x import (\nclip = \n";

        var offset = VapourSynthImports.InsertionOffset(text);

        Assert.Equal(0, offset);
    }

    [Fact]
    public void InsertionOffset_ImportAfterCall_UsesHeader()
    {
        const string text = "import vapoursynth as vs\nclip = helper.Foo()\nimport os\n";

        var offset = VapourSynthImports.InsertionOffset(text);

        Assert.Equal("import vapoursynth as vs\n".Length, offset);
    }

    [Fact]
    public void Plan_ShadowedImport_IsNotNeeded()
    {
        const string text = "import helper; helper = 1\nclip = helper.Foo()\n";

        var plan = VapourSynthImports.Plan(text, "helper");

        Assert.False(plan.Needed);
        Assert.Equal("", plan.Text);
    }

    [Fact]
    public void Plan_AssignmentWithoutImport_IsNotNeeded()
    {
        const string text = "helper = 1\nclip = helper.Foo()\n";

        var plan = VapourSynthImports.Plan(text, "helper");

        Assert.False(plan.Needed);
    }

    [Fact]
    public void Plan_UnclosedPrologueString_KeepsOffsetAtStart()
    {
        const string text = "\"\"\"unclosed\nclip = helper.Foo()\n";

        var plan = VapourSynthImports.Plan(text, "helper");

        Assert.True(plan.Needed);
        Assert.Equal(0, plan.Offset);
        Assert.Equal("import helper\n", plan.Text);
    }

    [Fact]
    public void Contains_InlineComment_StillFindsImport()
    {
        const string text = "import helper # note\n";

        var found = VapourSynthImports.Contains(text, "helper");

        Assert.True(found);
        Assert.False(VapourSynthImports.Plan(text, "helper").Needed);
    }

    [Fact]
    public void Contains_AliasedImportWithComment_StillFindsAliasPath()
    {
        const string text = "import helper as helper # note\n";

        var found = VapourSynthImports.Contains(text, "helper");

        Assert.True(found);
    }

    [Fact]
    public void Contains_DottedImportAlias_DoesNotBindPath()
    {
        const string text = "import pkg.sub as other\n";

        var found = VapourSynthImports.Contains(text, "pkg.sub");

        Assert.False(found);
        Assert.True(VapourSynthImports.Plan(text, "pkg.sub").Needed);
    }

    [Fact]
    public void Contains_DottedImportRootShadowed_ReturnsFalse()
    {
        const string text = "import pkg.sub; pkg = 1\n";

        var found = VapourSynthImports.Contains(text, "pkg.sub");

        Assert.False(found);
        Assert.False(VapourSynthImports.Plan(text, "pkg.sub").Needed);
    }

    [Fact]
    public void Contains_DelAfterImport_Shadows()
    {
        const string text = "import helper\ndel helper\n";

        var found = VapourSynthImports.Contains(text, "helper");

        Assert.False(found);
        Assert.False(VapourSynthImports.Plan(text, "helper").Needed);
    }

    [Fact]
    public void Contains_ForTarget_Shadows()
    {
        const string text = "import helper\nfor helper in items:\n    pass\n";

        var found = VapourSynthImports.Contains(text, "helper");

        Assert.False(found);
    }

    [Fact]
    public void Contains_TupleUnpack_Shadows()
    {
        const string text = "import helper\nhelper, x = 1, 2\n";

        var found = VapourSynthImports.Contains(text, "helper");

        Assert.False(found);
    }

    [Fact]
    public void Plan_CrlfDocument_UsesCrlf()
    {
        const string text = "import vapoursynth as vs\r\nclip = \r\n";

        var plan = VapourSynthImports.Plan(text, "helper");

        Assert.True(plan.Needed);
        Assert.Equal("import helper\r\n", plan.Text);
    }
}
