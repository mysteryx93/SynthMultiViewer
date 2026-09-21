using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests.VapourSynth;

using static AssistHarness;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class VapourSynthLanguageTests
{
    [Theory]
    [InlineData("core.", "std")]
    [InlineData("core.std.", "Crop")]
    [InlineData("core.rife.", "RIFE")]
    [InlineData("vs.core.std.Cr", "Crop")]
    [InlineData("vs.", "core")]
    [InlineData("co", "core")]
    [InlineData("c = vs.core\nc.", "std")]
    [InlineData("c = vs.core\nc.std.", "Crop")]
    [InlineData("c = vs.get_core()\nc.rife.", "RIFE")]
    [InlineData("c = vs.core  # alias\nc.std.Cr", "Crop")]
    [InlineData("import vapoursynth as vpy\nvpy.", "core")]
    [InlineData("import vapoursynth as vpy\nvpy.core.std.", "Crop")]
    [InlineData("from vapoursynth import core as c\nc.std.", "Crop")]
    [InlineData("from vapoursynth import core\nc = core\nc.std.", "Crop")]
    public void Analyze_KnownPath_OffersExpectedMember(string text, string expected)
    {
        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == expected);
    }

    [Fact]
    public void Analyze_ClipAssignment_OffersBoundPlugins()
    {
        const string text = "clip = vs.core.std.BlankClip()\nclip.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Complete_ClipMembers_SortAlphabetically()
    {
        const string text = "clip = core.std.BlankClip()\nclip.";

        var names = VsService().Analyze(text, text.Length, Vs).Items.Select(x => x.InsertionText).ToArray();

        Assert.Equal(names.Order(StringComparer.Ordinal), names);
        Assert.True(Array.IndexOf(names, "std") < Array.IndexOf(names, "width"));
    }

    [Fact]
    public void Analyze_StatementStart_DoesNotLeakCatalogFunctions()
    {
        var reply = VsService().Analyze("Cr", 2, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_ChainAfterCall_OffersBoundPlugin()
    {
        const string text = "clip = core.std.BlankClip()\nclip.std.Crop(0, 0, 2, 2).std.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_Slice_KeepsNodeType()
    {
        const string text = "clip = core.std.BlankClip()\nclip[0:10].";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_Add_KeepsNodeType()
    {
        const string text = "a = core.std.BlankClip()\nb = a + a\nb.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_Annotation_TypesParameter()
    {
        const string text = "def f(clip: vs.VideoNode):\n    clip.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_ClipParameter_OffersBoundPlugins()
    {
        const string text = "def f(clip):\n    clip.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Hover_RadiusDefault_ShowsInt()
    {
        const string text = "def f(radius=1):\n    radius";

        var hover = VsService().Analyze(text, text.Length, Vs).Hover;

        Assert.NotNull(hover);
        Assert.Equal("int", hover.Text);
    }

    [Fact]
    public void Hover_BooleanDefault_ShowsBool()
    {
        const string text = "def f(flag=True):\n    flag";

        var hover = VsService().Analyze(text, text.Length, Vs).Hover;

        Assert.NotNull(hover);
        Assert.Equal("bool", hover.Text);
    }

    [Fact]
    public void Hover_StringDefault_ShowsStr()
    {
        const string text = "def f(name=\"x\"):\n    name";

        var hover = VsService().Analyze(text, text.Length, Vs).Hover;

        Assert.NotNull(hover);
        Assert.Equal("str", hover.Text);
    }

    [Fact]
    public void Hover_IntegerLiteral_ShowsInt()
    {
        const string text = "n = 4\nn";

        var hover = VsService().Analyze(text, text.Length, Vs).Hover;

        Assert.NotNull(hover);
        Assert.Equal("int", hover.Text);
    }

    [Fact]
    public void Hover_FloatLiteral_ShowsFloat()
    {
        const string text = "n = 1.5\nn";

        var hover = VsService().Analyze(text, text.Length, Vs).Hover;

        Assert.NotNull(hover);
        Assert.Equal("float", hover.Text);
    }

    [Fact]
    public void Analyze_FunctionParameter_DoesNotLeak()
    {
        const string text = "def f(clip):\n    return clip\nclip.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Hover_HeaderClipAndType_AreSilent()
    {
        const string text = "def f(clip: vs.VideoNode):";

        var clipHover = VsService().Analyze(text, text.IndexOf("clip", StringComparison.Ordinal) + 1, Vs).Hover;
        var typeHover = VsService().Analyze(text, text.IndexOf("VideoNode", StringComparison.Ordinal) + 1, Vs).Hover;
        var module = VsService().Analyze(text, text.IndexOf("vs.", StringComparison.Ordinal) + 1, Vs).Hover;

        Assert.Null(clipHover);
        Assert.Null(typeHover);
        Assert.NotNull(module);
        Assert.Equal("vapoursynth", module.Text);
    }

    [Fact]
    public void Hover_AnnotatedClipInBody_ShowsVideoNode()
    {
        const string text = "def f(clip: vs.VideoNode):\n    clip";

        var hover = VsService().Analyze(text, text.Length, Vs).Hover;

        Assert.NotNull(hover);
        Assert.Equal("VideoNode", hover.Text);
    }

    [Fact]
    public void Complete_ClipParameter_ShowsLocalSignature()
    {
        const string text = "def f(clip):\n    cl";

        var reply = VsService().Analyze(text, text.Length, Vs);

        var item = Assert.Single(reply.Items, x => x.InsertionText == "clip");
        Assert.Equal(SymbolKind.Local, item.Kind);
        Assert.Equal("clip: VideoNode", item.Signature);
    }

    [Fact]
    public void Complete_FloatConstant_ShowsIntProperty()
    {
        const string text = "vs.FL";
        const string hoverText = "vs.FLOAT";

        var reply = VsService().Analyze(text, text.Length, Vs);
        var hover = VsService().Analyze(hoverText, hoverText.Length, Vs).Hover;

        var flt = Assert.Single(reply.Items, x => x.InsertionText == "FLOAT");
        Assert.Equal(SymbolKind.Property, flt.Kind);
        Assert.Equal("FLOAT: int", flt.Signature);
        Assert.Equal("int", hover?.Text);
    }

    [Fact]
    public void Analyze_IndentedBody_KeepsClipScope()
    {
        const string text = """
            def f(clip):
                if True:
                    clip.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_ClassMethod_KeepsClipScope()
    {
        const string text = """
            class C:
                def g(self, clip):
                    clip.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_AfterClassMethod_DoesNotLeakClip()
    {
        const string text = """
            class C:
                def g(self, clip):
                    return clip
            clip.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_MultiKeyReturn_StaysUnknown()
    {
        const string text = "super = core.svp1.Super()\nsuper.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Hover_TupleAssignment_IsNotRoot()
    {
        const string text = "_MV_BLK = (4, 8, 16, 32, 64, 128)\n_MV_BLK";

        var hover = VsService().Analyze(text, text.Length, Vs).Hover;

        Assert.Null(hover);
    }

    [Fact]
    public void Hover_ParenthesizedClip_ShowsVideoNode()
    {
        const string text = "clip = (core.std.BlankClip())\nclip";

        var hover = VsService().Analyze(text, text.Length, Vs).Hover;

        Assert.NotNull(hover);
        Assert.Contains("VideoNode", hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Hover_NativeFunction_ShowsUnqualifiedSignature()
    {
        const string text = "core.std.BlankClip()";

        var hover = VsService().Analyze(text, text.IndexOf("BlankClip", StringComparison.Ordinal) + 1, Vs).Hover;

        Assert.NotNull(hover);
        Assert.StartsWith("BlankClip(", hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Hover_AssignedClip_ShowsVideoNode()
    {
        const string text = "clip = core.std.BlankClip()\nclip";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.NotNull(reply.Hover);
        Assert.Contains("VideoNode", reply.Hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Hover_Core_ShowsCoreType()
    {
        const string text = "core.std";

        var hover = VsService().Analyze(text, 2, Vs).Hover;

        Assert.NotNull(hover);
        Assert.Contains("Core", hover.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("plugin", hover.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Hover_PluginNamespace_ShowsPlugin()
    {
        const string text = "core.std";

        var hover = VsService().Analyze(text, text.Length, Vs).Hover;

        Assert.NotNull(hover);
        Assert.Contains("plugin", hover.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(": Core", hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_NodeProperty_ShowsTypedSignature()
    {
        const string text = "clip = core.std.BlankClip()\nclip.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        var width = Assert.Single(reply.Items, x => x.InsertionText == "width");
        Assert.Equal("width: int", width.Signature);
        Assert.DoesNotContain("parameters unknown", width.Signature, StringComparison.Ordinal);
    }

    [Fact]
    public void Hover_NodeProperty_ShowsInt()
    {
        const string text = "clip = core.std.BlankClip()\nclip.width";

        var hover = VsService().Analyze(text, text.Length, Vs).Hover;

        Assert.NotNull(hover);
        Assert.Equal("int", hover.Text);
    }

    [Fact]
    public void Hover_FormatProperty_ShowsVideoFormat()
    {
        const string text = "clip = core.std.BlankClip()\nclip.format";

        var hover = VsService().Analyze(text, text.Length, Vs).Hover;

        Assert.NotNull(hover);
        Assert.Equal("VideoFormat", hover.Text);
    }

    [Fact]
    public void Hover_FpsProperty_ShowsFraction()
    {
        const string text = "clip = core.std.BlankClip()\nclip.fps";

        var hover = VsService().Analyze(text, text.Length, Vs).Hover;

        Assert.NotNull(hover);
        Assert.Equal("Fraction", hover.Text);
    }

    [Fact]
    public void Complete_FpsProperty_ShowsFractionSignature()
    {
        const string text = "clip = core.std.BlankClip()\nclip.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        var fps = Assert.Single(reply.Items, x => x.InsertionText == "fps");
        Assert.Equal("fps: Fraction", fps.Signature);
    }

    [Theory]
    [InlineData("clip:vnode;")]
    [InlineData("clip:vnode")]
    public void Analyze_NativeReturnTerminator_TypesClip(string returnType)
    {
        var catalog = new[] { new Symbol("core.std.BlankClip", ["width:int:opt"], ReturnType: returnType) };
        const string text = "clip = core.std.BlankClip()\nclip";

        var reply = VsService().Analyze(text + ".", text.Length + 1, catalog);
        var hover = VsService().Analyze(text, text.Length, catalog).Hover;

        Assert.Contains(reply.Items, x => x.InsertionText == "width");
        Assert.NotNull(hover);
        Assert.Contains("VideoNode", hover.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("clip = core.std.BlankClip()\ncl", "clip")]
    [InlineData("src, dst = clip, clip\nds", "dst")]
    [InlineData("import vapoursynth as vpy\nvp", "vpy")]
    [InlineData("from vapoursynth import core as c\ncore.std.Crop(c", "c")]
    [InlineData("clip = core.std.BlankClip(width=320)\nw", "while")]
    public void Analyze_BufferName_CompletesAsLocal(string text, string expected)
    {
        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == expected);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Theory]
    [InlineData("# core.std.")]
    [InlineData("\"core.std.")]
    [InlineData("'''core.std.\n")]
    public void Analyze_CommentOrString_SuppressesCompletion(string text)
    {
        var result = VsService().Analyze(text, text.Length, Vs);

        Assert.Empty(result.Items);
        Assert.Null(result.Insight);
    }

    [Fact]
    public void Analyze_OpenIndex_DoesNotDumpRootMembers()
    {
        const string text = "clip = core.std.BlankClip()\nclip[";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "import");
        Assert.Null(reply.Insight);
    }

    [Fact]
    public void Analyze_MultilineCall_DoesNotTreatKeywordAsLocal()
    {
        const string text = """
            clip = core.std.BlankClip(
            width=640
            )
            clip.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_MultilineCallKeyword_IsNotALocal()
    {
        const string text = "clip = core.std.BlankClip(\nwidth=640\n)\nw";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Hover_StringAssignment_ShowsStr()
    {
        const string text = "name = \"hello\"";

        var hover = VsService().Analyze(text, text.IndexOf("name", StringComparison.Ordinal) + 1, Vs).Hover;

        Assert.NotNull(hover);
        Assert.Equal("str", hover.Text);
    }

    [Fact]
    public void Analyze_AnnotationOnlyVariable_IsTyped()
    {
        const string text = "clip: vs.VideoNode\nclip.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_ChainedCallAndSlice_OffersNodeMembers()
    {
        const string text = "core.std.BlankClip()[0:10].";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "import");
    }

    [Fact]
    public void Analyze_VideoNodeBound_OmitsAudioOnlyFilters()
    {
        const string text = "clip = core.std.BlankClip()\nclip.std.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "AudioTrim");
    }

    [Fact]
    public void Analyze_UnterminatedString_RecoversOnNewline()
    {
        const string text = "name = \"hello\nclip = core.std.BlankClip()\nclip.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_MultilineCallInsideFunction_KeepsScope()
    {
        const string text = """
            def f(clip):
                result = core.std.BlankClip(
            width=640
                )
                clip.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_ScopedImportAssignment_InfersReturnType()
    {
        const string helper = "def Filter(clip) -> vs.VideoNode:\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = """
            def f(clip):
                from helper import Filter
                result = Filter(clip)
                result.
            """;
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_NestedInnerAssignment_InfersClipType()
    {
        const string text = """
            def outer(clip):
                def inner():
                    result = clip
                    result.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_ScopedCoreAlias_DoesNotLeak()
    {
        const string text = """
            def f():
                from vapoursynth import core as local
                local.std.BlankClip()
            local.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_ScopedModuleAlias_DoesNotLeak()
    {
        const string text = """
            def f():
                import vapoursynth as local
            local.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "core");
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Analyze_PythonContinuation_KeepsTypes(string newline)
    {
        var text = "clip = \\" + newline + "core.std.BlankClip()" + newline + "clip.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Analyze_DottedContinuation_KeepsTypes(string newline)
    {
        var text = "clip = core.std.\\" + newline + "BlankClip()" + newline + "clip.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Analyze_ImportedContinuation_CompletesName(string newline)
    {
        const string helper = "def Filter(clip):\n    return clip\n";
        var service = VsService(Includes(Read));
        var text = "from helper import \\" + newline + "Filter" + newline + "Fil";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void Insight_Assignment_ShadowsFunctionSymbol()
    {
        const string text = """
            def Filter(clip) -> vs.VideoNode:
                return clip
            Filter = 2
            Filter(
            """;

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.True(insight == null || insight.Overloads.All(x => x.Name != "Filter"));
    }

    [Fact]
    public void Insight_ImportedAssignment_ShadowsFunctionSymbol()
    {
        const string helper = "def Filter(clip) -> vs.VideoNode:\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = "from helper import Filter\nFilter = 2\nFilter(";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var insight = service.Analyze(text, text.Length, Vs).Insight;

        Assert.True(insight == null || insight.Overloads.All(x => x.Name != "Filter"));
    }

    [Fact]
    public void Insight_LaterImport_ReplacesEarlierValue()
    {
        const string helper = "def Filter(clip) -> vs.VideoNode:\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = "Filter = 1\nfrom helper import Filter\nFilter(";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var insight = service.Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("Filter", insight.Overloads[0].Name);
    }

    [Fact]
    public void Insight_LaterDef_ReplacesEarlierValue()
    {
        const string text = """
            Filter = 1
            def Filter(clip) -> vs.VideoNode:
                return clip
            Filter(
            """;

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("Filter", insight.Overloads[0].Name);
    }

    [Fact]
    public void Analyze_OneLineFunctionLocal_DoesNotLeak()
    {
        const string text = """
            def f(clip): value = 1; leak = core.std.BlankClip()
            leak.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_OneLineFunctionBody_StaysInScope()
    {
        const string text = "def f(clip): value = core.std.BlankClip(); value.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_Unpacking_OverwritesExistingTypes()
    {
        const string text = """
            clip = core.std.BlankClip()
            clip, value = other
            clip.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_ParenthesizedKnownClip_KeepsMembers()
    {
        const string text = "clip = core.std.BlankClip()\n(clip).";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_ParameterDefault_InfersFromAssignedName()
    {
        const string text = """
            base = core.std.BlankClip()
            def f(source=base):
                source.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_EscapedQuoteInDefault_KeepsReturnType()
    {
        const string text = """
            def f(text="a\"b") -> vs.VideoNode:
                return clip
            clip = f()
            clip.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_ImportedEscapedQuoteDefault_KeepsReturnType()
    {
        const string helper = "def f(text=\"a\\\"b\") -> vs.VideoNode:\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = "from helper import f\nclip = f()\nclip.";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_ClassMethod_IsNotModuleFunction()
    {
        const string text = """
            class C:
                def Apply(self, clip):
                    return clip
            App
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Apply");
    }

    [Fact]
    public void Analyze_ClassMethodBody_KeepsClipParameter()
    {
        const string text = """
            class C:
                def Apply(self, clip):
                    clip.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_LaterNestedFunction_ReplacesEarlierValue()
    {
        const string text = """
            def f():
                make = 1
                def make() -> vs.VideoNode:
                    return clip
                result = make()
                result.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_LaterNestedFunction_ShadowsImportedMake()
    {
        const string helper = "def make() -> vs.VideoNode:\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = """
            def f():
                from helper import make
                def make() -> int:
                    return 1
                result = make()
                result.
            """;
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_NestedDefault_InfersFromEnclosingAssignment()
    {
        const string text = """
            def f():
                base = core.std.BlankClip()
                def inner(source=base):
                    source.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_ClassBodyAssignment_DoesNotShadowModule()
    {
        const string text = """
            clip = core.std.BlankClip()
            class C:
                clip = 1
            clip.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_ClassBodyImport_DoesNotLeak()
    {
        const string helper = "def Filter():\n    return 1\n";
        var service = VsService(Includes(Read));
        const string text = """
            class C:
                import helper as h
            h.
            """;
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void Analyze_ClassMethodNestedDef_TypesResult()
    {
        const string text = """
            class C:
                def g(self):
                    def make(clip) -> vs.VideoNode:
                        return clip
                    result = make(clip)
                    result.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_ClassMethodNestedDef_DoesNotLeak()
    {
        const string nested = """
            class C:
                def g(self):
                    def make(clip) -> vs.VideoNode:
                        return clip
                    result = make(clip)
                    result.
            """;
        var text = nested + "\nm";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "make");
    }

    [Fact]
    public void Analyze_LaterDeclaredHelper_TypesFunctionBody()
    {
        const string text = """
            def f():
                result = make()
                result.
            def make() -> vs.VideoNode:
                return clip
            """;
        var caret = text.IndexOf("result.", StringComparison.Ordinal) + "result.".Length;

        var reply = VsService().Analyze(text, caret, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_LaterDeclaredHelper_DoesNotTypeEarlierModuleUse()
    {
        const string text = """
            clip = make()
            clip.
            def make() -> vs.VideoNode:
                return clip
            """;
        var caret = text.IndexOf("clip.", StringComparison.Ordinal) + 5;

        var reply = VsService().Analyze(text, caret, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Hover_StringWithOperator_ShowsStr()
    {
        const string text = "name = \"a+b\"";

        var hover = VsService().Analyze(text, text.IndexOf("name", StringComparison.Ordinal) + 1, Vs).Hover;

        Assert.NotNull(hover);
        Assert.Equal("str", hover.Text);
    }

    [Fact]
    public void Hover_NativeReturnIdentifiers_StayCompatible()
    {
        const string text = """
            def as_str() -> str:
                return "x"
            def as_bool() -> bool:
                return True
            a = as_str()
            b = as_bool()
            """;

        var strHover = VsService().Analyze(text, text.IndexOf("a =", StringComparison.Ordinal) + 1, Vs).Hover;
        var boolHover = VsService().Analyze(text, text.IndexOf("b =", StringComparison.Ordinal) + 1, Vs).Hover;

        Assert.Equal("str", strHover?.Text);
        Assert.Equal("bool", boolHover?.Text);
    }

    [Fact]
    public void Insight_UnclosedHeader_DoesNotHideLaterFunction()
    {
        const string text = """
            def broken(clip
            def good(clip, radius=2):
                return clip
            good(
            """;

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("good", insight.Overloads[0].Name);
        Assert.Contains("radius=2", insight.Overloads[0].Signature);
    }

    [Fact]
    public void Insight_ImportedUnclosedHeader_DoesNotHideLaterFunction()
    {
        const string helper = """
            def broken(clip
            def Filter(clip, radius=2):
                return clip
            """;
        var service = VsService(Includes(Read));
        const string text = "from helper import Filter\nFilter(";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var insight = service.Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("Filter", insight.Overloads[0].Name);
    }

    [Fact]
    public void Insight_MismatchedDelimiter_ClosesCall()
    {
        const string text = "core.std.Crop([0),\n";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Null(reply.Insight);
    }

    [Fact]
    public void Insight_MismatchedDelimiter_AllowsLaterStatement()
    {
        const string text = """
            core.std.Crop([0)
            x = 1
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Null(reply.Insight);
    }

    [Fact]
    public void Analyze_MismatchedDelimiter_StillBindsClip()
    {
        const string text = """
            core.std.Crop([0)
            clip = core.std.BlankClip()
            clip.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Null(reply.Insight);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Theory]
    [InlineData("def f(source: 'vs.VideoNode'):")]
    [InlineData("def f(source: typing.Optional[vs.VideoNode]):")]
    [InlineData("def f(source: None | vs.VideoNode):")]
    [InlineData("def f(source: vs.VideoNode | None):")]
    public void Analyze_AnnotationWhitelist_MapsQuotedOptionalAndNullable(string header)
    {
        var text = header + "\n    source.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Theory]
    [InlineData("def f(source: int | vs.VideoNode):")]
    [InlineData("def f(source: vs.VideoNode | int):")]
    [InlineData("def f(source: vs.VideoNode | vs.AudioNode):")]
    public void Analyze_MixedUnionAnnotation_StaysUnknown(string header)
    {
        var text = header + "\n    source.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_CoreAnnotation_OffersCoreMembers()
    {
        const string text = "def f(c: Core):\n    c.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "num_threads");
    }

    [Fact]
    public void Analyze_VideoFrameAnnotation_OffersFrameMembers()
    {
        const string text = "def f(f: vs.VideoFrame):\n    f.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "copy");
    }

    [Fact]
    public void Insight_QueryVideoFormat_ShowsParameters()
    {
        const string text = "core.query_video_format(";

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Contains(insight.Overloads[0].Parameters!, x => x.Contains("subsampling_w", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_QueriedFormat_OffersMembers()
    {
        const string text = "fmt = core.query_video_format(vs.YUV, vs.INTEGER, 8)\nfmt.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "subsampling_w");
        Assert.Contains(reply.Items, x => x.InsertionText == "name");
    }

    [Fact]
    public void Analyze_CopiedFrame_OffersMembers()
    {
        const string text = "frame = core.std.BlankClip().get_frame(0).copy()\nframe.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "copy");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Insight_DirectFunctionAlias_KeepsOverload()
    {
        const string text = """
            crop = core.std.Crop
            crop(
            """;

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("core.std.Crop", insight.Overloads[0].Name);
    }

    [Fact]
    public void Analyze_DirectFunctionAlias_KeepsReturnType()
    {
        const string text = """
            crop = core.std.Crop
            clip = crop(core.std.BlankClip(), 0, 0, 0, 0)
            clip.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Insight_ImportedFunctionAlias_PreservesIdentity()
    {
        const string helper = "def Filter(clip, radius=2):\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = """
            import helper
            alias = helper.Filter
            alias(
            """;
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var insight = service.Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("Filter", insight.Overloads[0].Name);
        Assert.Contains("radius=2", insight.Overloads[0].Signature);
    }

    [Fact]
    public void Insight_ImportedAlias_OverridesLocalFunction()
    {
        const string helper = "def Filter(clip, radius=2):\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = """
            def Filter(x):
                return x
            import helper
            alias = helper.Filter
            alias(
            """;
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var insight = service.Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Contains("radius=2", insight.Overloads[0].Signature);
    }

    [Fact]
    public void Insight_HostFunctionAlias_PreservesIdentity()
    {
        const string text = """
            def query_video_format():
                return 1
            fmt = core.query_video_format
            fmt(
            """;

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Contains("subsampling_w", insight.Overloads[0].Signature);
    }

    [Fact]
    public void Insight_ReboundAlias_KeepsOriginalFunction()
    {
        const string text = """
            def Filter(clip, radius=2):
                return clip
            alias = Filter
            def Filter(x):
                return x
            alias(
            """;

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Contains("radius=2", insight.Overloads[0].Signature);
    }

    [Fact]
    public void Insight_UnknownReturnCall_DoesNotRemainCallable()
    {
        const string text = """
            def get_number():
                return 1
            result = get_number()
            result(
            """;

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.True(insight == null || insight.Overloads.All(x => x.Name != "get_number"));
    }

    [Theory]
    [InlineData("\"movie).mkv\"")]
    [InlineData("\"movie.mkv\"")]
    public void Analyze_StringParentheses_DoNotBreakAssignment(string file)
    {
        var catalog = Vs.Concat([new("core.demo.Source", ["file:str"], ReturnType: "clip:vnode;")])
            .ToArray();
        var text = $"""
            source = core.demo.Source({file})
            source.
            """;

        var reply = VsService().Analyze(text, text.Length, catalog);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_NotExpression_IsNotVideoNode()
    {
        const string text = """
            source = core.std.BlankClip()
            result = not source
            result.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_IdentityExpression_IsNotVideoNode()
    {
        const string text = """
            source = core.std.BlankClip()
            result = source is source
            result.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_UnfinishedMember_DoesNotCaptureNextStatement()
    {
        const string text = """
            source.
            core.std.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
        Assert.Contains(reply.Items, x => x.InsertionText == "BlankClip");
    }

    [Fact]
    public void Insight_BoundAudioFilterOnVideo_IsRejected()
    {
        const string text = """
            clip = core.std.BlankClip()
            clip.std.AudioTrim(
            """;

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.True(insight == null || insight.Overloads.All(x => x.Name != "core.std.AudioTrim"));
    }

    [Fact]
    public void Analyze_BoundAudioFilterOnVideo_DoesNotTypeResult()
    {
        const string text = """
            clip = core.std.BlankClip()
            out = clip.std.AudioTrim()
            out.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "sample_rate");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Insight_BoundAudioFilterAlias_IsRejected()
    {
        const string text = """
            clip = core.std.BlankClip()
            fn = clip.std.AudioTrim
            fn(
            """;

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.True(insight == null || insight.Overloads.All(x => x.Name != "core.std.AudioTrim"));
    }

    [Fact]
    public void Analyze_GroupedMultilineExpression_KeepsNodeType()
    {
        const string text = """
            source = (core.std.BlankClip()
            .std.Crop(left=2))
            source.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_RecoveredBracketMismatch_DoesNotCaptureNextStatement()
    {
        const string text = """
            broken = ([0)
            source.
            core.std.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
        Assert.Contains(reply.Items, x => x.InsertionText == "BlankClip");
    }

    [Fact]
    public void Analyze_IndexedWidth_DoesNotKeepClipType()
    {
        const string text = """
            source = core.std.BlankClip()
            result = source.width[0]
            result.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_IndexedUnknownMember_DoesNotKeepClipType()
    {
        const string text = """
            source = core.std.BlankClip()
            result = source.unknown[0]
            result.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_GroupedMultilineArithmetic_KeepsNodeType()
    {
        const string text = """
            source = (core.std
            .BlankClip() * 2)
            source.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_UnknownCallAssignment_KeepsClip()
    {
        const string text = """
            clip = core.std.BlankClip()
            clip = FrameRateConverter(clip, preset="faster")
            clip.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_MemberAccessBeforeRebind_OffersClip()
    {
        const string text = """
            clip = core.std.BlankClip()
            clip.
            clip = 1
            """;
        var caret = text.IndexOf("clip.", StringComparison.Ordinal) + "clip.".Length;

        var reply = VsService().Analyze(text, caret, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_ChainedRebinding_InvalidatesNodeType()
    {
        const string text = """
            source = core.std.BlankClip()
            other = source
            source = other = 3
            other.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_DeletedName_InvalidatesNodeType()
    {
        const string text = """
            source = core.std.BlankClip()
            del source
            source.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_LoopTarget_InvalidatesNodeType()
    {
        const string text = """
            source = core.std.BlankClip()
            for source in items:
                pass
            source.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Insight_ClassBinding_InvalidatesFunction()
    {
        const string text = """
            def Filter(clip) -> vs.VideoNode:
                return clip
            class Filter:
                pass
            Filter(
            """;

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.True(insight == null || insight.Overloads.All(x => x.Name != "Filter"));
    }

    [Fact]
    public void Analyze_UserFunctionNamedCore_ShadowsPredefinedAlias()
    {
        const string text = """
            def core() -> vs.VideoNode:
                return None
            source = core()
            source.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "num_threads");
    }

    [Fact]
    public void Insight_AliasedUserCore_KeepsFunction()
    {
        const string text = """
            def core() -> vs.VideoNode:
                return None
            make = core
            make(
            """;

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("core", insight.Overloads[0].Name);
    }

    [Fact]
    public void Analyze_CarriageReturnLineComment_DoesNotSuppressRest()
    {
        const string text = "source = core.std.BlankClip()\r# comment\rother = source\rother.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_CoreReturn_OffersCoreMembers()
    {
        const string text = """
            def make() -> vs.Core:
                return core
            c = make()
            c.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "num_threads");
    }

    [Fact]
    public void Analyze_FormatReplace_OffersFormatMembers()
    {
        const string text = """
            fmt = core.query_video_format(vs.YUV, vs.INTEGER, 8)
            out = fmt.replace(bits_per_sample=10)
            out.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "name");
        Assert.Contains(reply.Items, x => x.InsertionText == "replace");
    }

    [Fact]
    public void Insight_FormatReplace_ShowsParameters()
    {
        const string text = "fmt = core.query_video_format(vs.YUV, vs.INTEGER, 8)\nfmt.replace(";

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Contains(insight.Overloads[0].Parameters!, x => x.Contains("color_family", StringComparison.Ordinal));
        Assert.Contains(insight.Overloads[0].Parameters!, x => x.Contains("bits_per_sample", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_VideoNodeAlias_TypesParameter()
    {
        const string text = """
            from vapoursynth import VideoNode as Node
            def f(clip: Node):
                clip.
            """;

        var reply = VsService().Analyze(text, text.LastIndexOf('.') + 1, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_FrameWithAlias_OffersFrameMembers()
    {
        const string text = """
            source = core.std.BlankClip()
            with source.get_frame(0) as frame:
                frame.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "copy");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_DeletedSubscript_DoesNotUnbindCore()
    {
        const string text = """
            core
            state = {}
            del state["core"]
            core.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "num_threads");
    }

    [Fact]
    public void Analyze_DeletedClipInSubscript_KeepsNodeType()
    {
        const string text = """
            source = core.std.BlankClip()
            del entries[source]
            source.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_LoopSubscriptTarget_KeepsNodeType()
    {
        const string text = """
            source = core.std.BlankClip()
            for mapping[source] in things:
                pass
            source.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_DeletedLocal_ShadowsOuterBinding()
    {
        const string text = """
            source = core.std.BlankClip()
            def f():
                source: int
                del source
                source.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_AnnotationAlias_TypesLocal()
    {
        const string text = """
            from vapoursynth import VideoNode as Node
            def f():
                source: Node
                source.
            """;

        var reply = VsService().Analyze(text, text.LastIndexOf('.') + 1, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_ImportedAnnotationAlias_TypesReturn()
    {
        const string helper = """
            from vapoursynth import VideoNode as Node
            def Filter() -> Node:
                return None
            """;
        var service = VsService(Includes(Read));
        const string text = "from helper import Filter\nclip = Filter()\nclip.";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_GroupedWithAlias_OffersFrameMembers()
    {
        const string text = """
            source = core.std.BlankClip()
            with (source.get_frame(0) as frame):
                frame.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "copy");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText.Contains(')', StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_InlineWithAlias_OffersFrameMembers()
    {
        const string text = """
            source = core.std.BlankClip()
            with source.get_frame(0) as frame: pass
            frame.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "copy");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText.Contains("pass", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_ChainedWithAlias_OffersFrameMembers()
    {
        const string text = """
            source = core.std.BlankClip()
            def f():
                with source.get_frame(0) as first, first.copy() as second:
                    second.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "copy");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_StarParameter_IsLocalName()
    {
        const string text = """
            def f(*sources, **options):
                sou
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "sources");
    }

    [Fact]
    public void Analyze_KwargsParameter_IsLocalName()
    {
        const string text = """
            def f(*sources, **options):
                opt
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "options");
    }

    [Fact]
    public void Analyze_EscapedNewlineInString_StaysSilent()
    {
        const string text = "clip = core.std.BlankClip()\ns = \"\\n\"";
        var escapeAt = text.LastIndexOf('n');

        var reply = VsService().Analyze(text, escapeAt, Vs);

        Assert.Empty(reply.Items);
    }

    [Fact]
    public void Analyze_TripleQuoteCloser_StaysSilent()
    {
        const string text = "clip = core.std.BlankClip()\ns = \"\"\"x\"\"\"";
        var closer = text.LastIndexOf('x') + 2;

        var reply = VsService().Analyze(text, closer, Vs);

        Assert.Empty(reply.Items);
    }

    [Fact]
    public void Analyze_LocalFunction_ShadowsGlobalScalar()
    {
        const string text = """
            Filter = 2
            def f(clip):
                def Filter(clip) -> vs.VideoNode:
                    return clip
                result = Filter(clip)
                result.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_LocalImport_ShadowsGlobalScalar()
    {
        const string helper = "def Filter(clip) -> vs.VideoNode:\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = """
            Filter = 2
            def f(clip):
                from helper import Filter
                result = Filter(clip)
                result.
            """;
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_AssignedAnnotatedParameter_KeepsType()
    {
        const string text = """
            def process(source: vs.VideoNode):
                target = source
                target.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_AssignedFrameParameter_KeepsType()
    {
        const string text = """
            def process(frame: vs.VideoFrame):
                copy = frame
                copy.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "copy");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_CoreParameter_ShadowsPredefinedAlias()
    {
        const string text = """
            def process(core: vs.VideoNode):
                core.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "num_threads");
    }

    [Fact]
    public void Analyze_HeaderDefault_UsesEnclosingScope()
    {
        const string text = """
            source = core.std.BlankClip()
            def process(source: int, other=source):
                other.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_HeaderReturnAlias_UsesEnclosingScope()
    {
        const string text = """
            from vapoursynth import VideoNode as Node
            def process(Node: int) -> Node:
                return None
            clip = process()
            clip.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_ParameterSharingFunctionName_KeepsInference()
    {
        const string text = """
            def clip(clip: vs.VideoNode):
                target = clip
                target.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_ParenthesizedUnpacking_InvalidatesType()
    {
        const string text = """
            source = core.std.BlankClip()
            (source, other) = get_pair()
            source.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_StarredUnpacking_InvalidatesType()
    {
        const string text = """
            source = core.std.BlankClip()
            source, *other = get_pair()
            source.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_ExceptAlias_InvalidatesPreviousType()
    {
        const string text = """
            source = core.std.BlankClip()
            try:
                pass
            except Exception as source:
                source.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_CalledModule_IsUnknown()
    {
        const string helper = "def Filter():\n    return 1\n";
        var service = VsService(Includes(Read));
        const string text = """
            import helper
            result = helper()
            result.
            """;
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void Analyze_CalledFormatProperty_IsUnknown()
    {
        const string text = """
            clip = core.std.BlankClip()
            value = clip.format()
            value.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "replace");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_MultiplyAssign_KeepsClipMembers()
    {
        const string text = """
            clip = core.std.BlankClip()
            clip *= 2
            clip.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_GroupedSingleTarget_InfersRhs()
    {
        const string text = "(clip) = core.std.BlankClip()\nclip.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_AsyncDef_DoesNotLeakLocals()
    {
        const string text = """
            async def f():
                source = core.std.BlankClip()
            source.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_AsyncDefBody_KeepsLocals()
    {
        const string text = """
            async def f():
                source = core.std.BlankClip()
                source.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_NestedGroupedAssignment_InfersRhs()
    {
        const string text = "((clip)) = core.std.BlankClip()\nclip.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_SpacedGroupedAssignment_InfersRhs()
    {
        const string text = "( (clip) ) = core.std.BlankClip()\nclip.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_ConditionalSameBranchTypes_KeepsNode()
    {
        const string text = """
            left = core.std.BlankClip()
            right = core.std.BlankClip()
            result = left if enabled else right
            result.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_ConditionalMixedBranchTypes_StaysUnknown()
    {
        const string text = """
            left = core.std.BlankClip()
            result = left if enabled else 1
            result.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_ConditionalPluginFallback_KeepsClip()
    {
        const string text = """
            import os
            import vapoursynth as vs
            core = vs.core
            base = "/home/hanuman/GitHub/FrameRateConverter/Tests/base"
            src = os.path.join(base, "Motion Estimation Torture Clip.avi")
            clip = core.ffms2.Source(src) if hasattr(core, "ffms2") else core.bs.VideoSource(src)
            clip.
            """;
        Symbol[] catalog =
        [
            ..Vs,
            new("core.ffms2.Source", ["source:data"], ReturnType: "clip:vnode;"),
            new("core.bs.VideoSource", ["source:data"], ReturnType: "clip:vnode;")
        ];

        var reply = VsService().Analyze(text, text.Length, catalog);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_OsPathJoin_IsString()
    {
        const string text = """
            import os
            src = os.path.join(base, "Motion Estimation Torture Clip.avi")
            src.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_ConditionalPluginFallbackMissingPlugin_KeepsClip()
    {
        const string text = """
            clip = core.ffms2.Source(src) if hasattr(core, "ffms2") else core.bs.VideoSource(src)
            clip.
            """;
        Symbol[] catalog =
        [
            ..Vs,
            new("core.bs.VideoSource", ["source:data"], ReturnType: "clip:vnode;")
        ];

        var reply = VsService().Analyze(text, text.Length, catalog);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Theory]
    [InlineData("value = vs()\nvalue.")]
    [InlineData("value = vs.core()\nvalue.")]
    [InlineData("value = core.std()\nvalue.")]
    [InlineData("clip = core.std.BlankClip()\nvalue = clip.std()\nvalue.")]
    public void Analyze_InvalidNamespaceCall_StaysUnknown(string text)
    {
        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "core");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_GetCoreCall_KeepsCore()
    {
        const string text = "c = vs.get_core()\nc.";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_BrokenInputBeforeAsyncDef_RecoversFunction()
    {
        const string text = """
            broken = (
            async def good():
                clip = core.std.BlankClip()
                clip.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_AsyncDefCall_StaysUnknown()
    {
        const string text = """
            async def load() -> vs.VideoNode:
                return core.std.BlankClip()
            result = load()
            result.
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_ImportedAsyncDefCall_StaysUnknown()
    {
        const string helper = """
            async def load() -> vs.VideoNode:
                return None
            """;
        const string text = """
            from helper import load
            result = load()
            result.
            """;
        var service = VsService(Includes(Read));
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_DeeplyGroupedExpression_ReturnsUnknownPromptly()
    {
        const int depth = 2000;
        var text = "x = " + new string('(', depth) + "core.std.BlankClip()" + new string(')', depth) + "\nx.";
        var timer = Stopwatch.StartNew();

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.True(timer.Elapsed.TotalSeconds < 1);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }
}
