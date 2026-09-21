using System.Diagnostics.CodeAnalysis;
using HanumanInstitute.ScriptAssist.AvaloniaEdit;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests.User;

using static AssistHarness;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class VapourSynthAssistTests
{
    [Fact]
    public void Complete_NativeFunction_UsesDisplayTypes()
    {
        const string text = "core.std.Blank";

        var reply = VsService().Analyze(text, text.Length, Vs);

        var item = Assert.Single(reply.Items, x => x.InsertionText == "BlankClip");
        AssertDisplayTypes(item.Signature);
    }

    [Fact]
    public void Hover_NativeFunction_UsesDisplayTypes()
    {
        const string text = "core.std.BlankClip()";

        var hover = VsService().Analyze(text, text.IndexOf("BlankClip", StringComparison.Ordinal) + 1, Vs).Hover;

        Assert.NotNull(hover);
        AssertDisplayTypes(hover.Text);
    }

    [Fact]
    public void Insight_NativeFunction_UsesDisplayTypes()
    {
        const string text = "core.std.BlankClip(format=";

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        AssertDisplayTypes(insight.Overloads[0].Signature);
        Assert.Contains("VideoFormat", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
        Assert.DoesNotContain("vnode", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_BoundPlugin_MatchesHover()
    {
        const string members = "clip = core.std.BlankClip()\nclip.";
        const string hoverAt = members + "std";

        var item = Assert.Single(VsService().Analyze(members, members.Length, Vs).Items,
            x => x.InsertionText == "std");
        var hover = VsService().Analyze(hoverAt, hoverAt.Length, Vs).Hover;

        Assert.Equal(SymbolKind.Namespace, item.Kind);
        Assert.Equal("plugin", CompletionData.HintText(item));
        Assert.Equal(CompletionData.HintText(item), hover?.Text);
    }

    [Fact]
    public void Complete_VsCore_MatchesHover()
    {
        const string members = "vs.";
        const string hoverAt = "vs.core";

        var item = Assert.Single(VsService().Analyze(members, members.Length, Vs).Items,
            x => x.InsertionText == "core");
        var hover = VsService().Analyze(hoverAt, hoverAt.Length, Vs).Hover;

        Assert.Equal("Core", hover?.Text);
        Assert.Equal(CompletionData.HintText(item), hover?.Text);
    }

    [Fact]
    public void Complete_VsError_MatchesHover()
    {
        const string members = "vs.";
        const string hoverAt = "vs.Error";

        var item = Assert.Single(VsService().Analyze(members, members.Length, Vs).Items,
            x => x.InsertionText == "Error");
        var hover = VsService().Analyze(hoverAt, hoverAt.Length, Vs).Hover;

        Assert.Equal("Error()", CompletionData.HintText(item));
        Assert.Equal(CompletionData.HintText(item), hover?.Text);
    }

    [Fact]
    public void Complete_VsAudioNode_OmitsHint()
    {
        const string members = "vs.";
        const string hoverAt = "vs.AudioNode";

        var item = Assert.Single(VsService().Analyze(members, members.Length, Vs).Items,
            x => x.InsertionText == "AudioNode");
        var hover = VsService().Analyze(hoverAt, hoverAt.Length, Vs).Hover;

        Assert.Null(CompletionData.HintText(item));
        Assert.Null(hover);
    }

    [Fact]
    public void Hover_ConditionalPluginFallback_ShowsVideoNode()
    {
        const string text = """
            import os
            import vapoursynth as vs
            core = vs.core
            base = "/home/hanuman/GitHub/FrameRateConverter/Tests/base"
            src = os.path.join(base, "Motion Estimation Torture Clip.avi")
            clip = core.ffms2.Source(src) if hasattr(core, "ffms2") else core.bs.VideoSource(src)
            """;
        Symbol[] catalog =
        [
            ..Vs,
            new("core.bs.VideoSource", ["source:data"], ReturnType: "clip:vnode;")
        ];

        var hover = VsService().Analyze(text, text.IndexOf("clip =", StringComparison.Ordinal) + 2, catalog).Hover;

        Assert.Equal("VideoNode", hover?.Text);
    }

    [Fact]
    public void Hover_OsPathJoin_ShowsStr()
    {
        const string text = """
            import os
            src = os.path.join(base, "Motion Estimation Torture Clip.avi")
            """;

        var hover = VsService().Analyze(text, text.IndexOf("src =", StringComparison.Ordinal) + 2, Vs).Hover;

        Assert.Equal("str", hover?.Text);
    }

    [Fact]
    public void Complete_CorePlugin_MatchesHover()
    {
        const string members = "core.";
        const string hoverAt = "core.std";

        var item = Assert.Single(VsService().Analyze(members, members.Length, Vs).Items,
            x => x.InsertionText == "std");
        var hover = VsService().Analyze(hoverAt, hoverAt.Length, Vs).Hover;

        Assert.Equal(SymbolKind.Namespace, item.Kind);
        Assert.Equal("plugin", CompletionData.HintText(item));
        Assert.Equal(CompletionData.HintText(item), hover?.Text);
    }

    [Fact]
    public void Complete_NamedPlugin_HintIsTitle()
    {
        const string members = "core.";
        const string hoverAt = "core.bm3d";
        Symbol[] catalog =
        [
            new("core.bm3d.BM3D", ["clip:vnode"], ReturnType: "clip:vnode;", Title: "VapourSynth BM3D")
        ];

        var item = Assert.Single(VsService().Analyze(members, members.Length, catalog).Items,
            x => x.InsertionText == "bm3d");
        var hover = VsService().Analyze(hoverAt, hoverAt.Length, catalog).Hover;

        Assert.Equal("VapourSynth BM3D", CompletionData.HintText(item));
        Assert.Equal(CompletionData.HintText(item), hover?.Text);
    }

    [Fact]
    public void Hover_FormatNamedArgument_UsesDisplayType()
    {
        const string text = "core.std.BlankClip(format=1)";
        var caret = text.IndexOf("format", StringComparison.Ordinal) + 1;

        var hover = VsService().Analyze(text, caret, Vs).Hover;

        Assert.Equal("VideoFormat", hover?.Text);
    }

    [Fact]
    public void Insight_PackageImportThenDef_ReplacesNamespace()
    {
        var service = VsService(Includes(Read));
        const string text = "from pkg import sub\nsub(";
        IncludeFile? Read(string specifier, string? _) => Package(specifier);

        var insight = service.Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("sub", insight.Overloads[0].Name);
        Assert.Contains("clip", insight.Overloads[0].Parameters![0], StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_PackageMemberCall_UsesFunctionAfterDef()
    {
        var service = VsService(Includes(Read));
        const string text = "import pkg\npkg.sub(";
        IncludeFile? Read(string specifier, string? _) => Package(specifier);

        var insight = service.Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("sub", insight.Overloads[0].Name);
        Assert.Contains("clip", insight.Overloads[0].Parameters![0], StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_PackageMember_OffersFunctionAfterDef()
    {
        var service = VsService(Includes(Read));
        const string text = "import pkg\npkg.";
        IncludeFile? Read(string specifier, string? _) => Package(specifier);

        var reply = service.Analyze(text, text.Length, Vs);

        var sub = Assert.Single(reply.Items, x => x.InsertionText == "sub");
        Assert.Equal(SymbolKind.Function, sub.Kind);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "helper");
    }

    [Fact]
    public void Hover_ImportedPackageFunction_ShowsCall()
    {
        var service = VsService(Includes(Read));
        const string text = "from pkg import sub\nsub";
        IncludeFile? Read(string specifier, string? _) => Package(specifier);

        var hover = service.Analyze(text, text.Length, Vs).Hover;

        Assert.NotNull(hover);
        Assert.StartsWith("sub(", hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_FromPackageImport_OffersFunction()
    {
        var service = VsService(Includes(Read));
        const string text = "from pkg import sub\nsu";
        IncludeFile? Read(string specifier, string? _) => Package(specifier);

        var reply = service.Analyze(text, text.Length, Vs);

        var sub = Assert.Single(reply.Items, x => x.InsertionText == "sub");
        Assert.Equal(SymbolKind.Function, sub.Kind);
    }

    [Fact]
    public void Complete_PackageAfterAssign_OmitsName()
    {
        var service = VsService(Includes(Read));
        const string text = "import pkg\npkg.";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "pkg" => new IncludeFile("/plugins/pkg/__init__.py", "import pkg.sub\nsub = 1\n"),
            "pkg.sub" => new IncludeFile("/plugins/pkg/sub.py", "def helper():\n    pass\n"),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "sub");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "helper");
    }

    [Fact]
    public void Complete_PackageAfterDel_OmitsName()
    {
        var service = VsService(Includes(Read));
        const string text = "import pkg\npkg.";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "pkg" => new IncludeFile("/plugins/pkg/__init__.py", "import pkg.sub\ndel sub\n"),
            "pkg.sub" => new IncludeFile("/plugins/pkg/sub.py", "def helper():\n    pass\n"),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "sub");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "helper");
    }

    [Fact]
    public void Insight_NestedInner_UsesDisplayTypes()
    {
        const string text = "core.std.Crop(core.std.BlankClip(format=";

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("core.std.BlankClip", insight.Overloads[0].Name);
        AssertDisplayTypes(insight.Overloads[0].Signature);
        Assert.Contains("VideoFormat", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_NestedInner_OffersInnerNames()
    {
        const string text = "core.std.Crop(core.std.BlankClip(fo";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "format=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "left=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "right=");
    }

    [Fact]
    public void Hover_NestedInner_UsesDisplayTypes()
    {
        const string text = "core.std.Crop(core.std.BlankClip())";
        var caret = text.IndexOf("BlankClip", StringComparison.Ordinal) + 1;

        var hover = VsService().Analyze(text, caret, Vs).Hover;

        Assert.NotNull(hover);
        AssertDisplayTypes(hover.Text);
    }

    [Fact]
    public void Hover_NestedOuter_ShowsOuterCall()
    {
        const string text = "core.std.Crop(core.std.BlankClip())";
        var caret = text.IndexOf("Crop", StringComparison.Ordinal) + 1;

        var hover = VsService().Analyze(text, caret, Vs).Hover;

        Assert.NotNull(hover);
        Assert.StartsWith("Crop(", hover.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("format", hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_NestedAfterClose_ReturnsToOuter()
    {
        const string text = "core.std.Crop(core.std.BlankClip(), ";

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("core.std.Crop", insight.Overloads[0].Name);
        Assert.Contains("left", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_NestedAfterClose_OffersOuterNames()
    {
        const string text = "core.std.Crop(core.std.BlankClip(), ";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "left=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "format=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width=");
    }

    [Fact]
    public void Complete_PrefixInsideOuter_OffersInnerFunction()
    {
        const string text = "core.std.Crop(core.std.Bl";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "BlankClip");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "left=");
    }

    [Fact]
    public void Insight_NestedNamedValue_KeepsInner()
    {
        const string text = "core.std.Crop(left=core.std.BlankClip(format=";

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("core.std.BlankClip", insight.Overloads[0].Name);
        Assert.Contains("VideoFormat", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
        Assert.DoesNotContain("left:int", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_TripleNested_KeepsInnermost()
    {
        const string text = "core.std.Crop(core.std.Crop(core.std.BlankClip(format=";

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("core.std.BlankClip", insight.Overloads[0].Name);
        Assert.Contains("VideoFormat", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_BoundOuterNestedInner_KeepsInner()
    {
        const string text = "clip = core.std.BlankClip()\nclip.std.Crop(core.std.BlankClip(format=";

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("core.std.BlankClip", insight.Overloads[0].Name);
        Assert.Contains("VideoFormat", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_BoundOuterAfterNestedClose_MapsInnerAsLeft()
    {
        const string text = "clip = core.std.BlankClip()\nclip.std.Crop(core.std.BlankClip(), ";

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("core.std.Crop", insight.Overloads[0].Name);
        Assert.True(insight.ImplicitClip);
        Assert.Contains("right", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
        Assert.DoesNotContain("left:int", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_CommentBeforeNestedInner_KeepsInner()
    {
        const string text = """
            core.std.Crop(
            # margins
            core.std.BlankClip(format=
            """;

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("core.std.BlankClip", insight.Overloads[0].Name);
        Assert.Contains("VideoFormat", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_EqualsInsideString_StaysPositional()
    {
        const string text = "core.std.BlankClip(\"a=b\"";

        var insight = VsService().Analyze(text, text.Length, Vs).Insight!;

        Assert.Null(insight.Keyword);
        Assert.Contains("clip:VideoNode", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
        Assert.DoesNotContain("No more parameters", OverloadProvider.ActiveParameterText(insight),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_EscapedStringCrlf_StaysInsideLiteral()
    {
        const string text = "core.std.BlankClip(\"a\\\r\nb";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Empty(reply.Items);
        Assert.Null(reply.Insight);
    }

    [Fact]
    public void Insight_ImportedWithAlias_RemovesFunction()
    {
        const string helper = """
            def make(clip):
                return clip
            with open("x") as make:
                pass
            """;
        var service = VsService(Includes(Read));
        const string text = "from helper import make\nmake(";
        IncludeFile? Read(string specifier, string? _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", helper)
            : null;

        var insight = service.Analyze(text, text.Length, Vs).Insight;

        Assert.Null(insight);
    }

    [Fact]
    public void Insight_ImportedExceptAlias_RemovesFunction()
    {
        const string helper = """
            def make(clip):
                return clip
            try:
                pass
            except Exception as make:
                pass
            """;
        var service = VsService(Includes(Read));
        const string text = "from helper import make\nmake(";
        IncludeFile? Read(string specifier, string? _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", helper)
            : null;

        var insight = service.Analyze(text, text.Length, Vs).Insight;

        Assert.Null(insight);
    }

    [Fact]
    public void Complete_CallableAlias_ShowsSignature()
    {
        const string text = "crop = core.std.Crop\ncro";

        var reply = VsService().Analyze(text, text.Length, Vs);

        var item = Assert.Single(reply.Items, x => x.InsertionText == "crop");
        Assert.Equal(SymbolKind.Function, item.Kind);
        Assert.Contains("left:int", item.Signature, StringComparison.Ordinal);
        Assert.Contains("VideoNode", item.Signature, StringComparison.Ordinal);
    }

    [Fact]
    public void Hover_CallableAlias_ShowsSignature()
    {
        const string text = "crop = core.std.Crop\ncrop";

        var hover = VsService().Analyze(text, text.Length, Vs).Hover;

        Assert.NotNull(hover);
        Assert.StartsWith("crop(", hover.Text, StringComparison.Ordinal);
        Assert.Contains("left:int", hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_ReboundCore_UsesBinding()
    {
        const string text = "core = 1\nco";

        var reply = VsService().Analyze(text, text.Length, Vs);

        var item = Assert.Single(reply.Items, x => x.InsertionText == "core");
        Assert.Equal(SymbolKind.Local, item.Kind);
        Assert.Contains("int", item.Signature, StringComparison.Ordinal);
    }

    [Fact]
    public void Hover_ReboundCore_ShowsInt()
    {
        const string text = "core = 1\ncore";

        var hover = VsService().Analyze(text, text.Length, Vs).Hover;

        Assert.Equal("int", hover?.Text);
    }

    [Fact]
    public void Hover_EarlierAssignment_ShowsVideoNode()
    {
        const string text = """
            clip = core.std.BlankClip()
            clip = 1
            """;

        var hover = VsService().Analyze(text, text.IndexOf("clip =", StringComparison.Ordinal) + 2, Vs).Hover;

        Assert.Equal("VideoNode", hover?.Text);
    }

    [Fact]
    public void Insight_ImportedClassWithoutInit_HasNoCall()
    {
        const string helper = "class Filter:\n    pass\n";
        const string text = "import helper\nhelper.Filter(";
        var service = VsService(Includes(Read));
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var insight = service.Analyze(text, text.Length, []).Insight;

        Assert.True(insight == null || insight.Overloads.All(x => x.Name != "Filter"));
    }

    [Fact]
    public void Hover_FromImportedClassWithoutInit_ShowsClass()
    {
        const string helper = "class Filter:\n    pass\n";
        const string text = "import helper\nhelper.Filter";
        var service = VsService(Includes(Read));
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var hover = service.Analyze(text, text.Length, []).Hover;

        Assert.Equal("Class", hover?.Text);
    }

    [Fact]
    public void Insight_ImportedClassInit_OmitsSelf()
    {
        const string helper = "class Filter:\n    def __init__(self, clip):\n        pass\n";
        const string text = "import helper\nhelper.Filter(";
        var service = VsService(Includes(Read));
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var insight = service.Analyze(text, text.Length, []).Insight;

        Assert.NotNull(insight);
        Assert.Equal("Filter(clip)", insight.Overloads[0].Signature);
    }

    [Fact]
    public void Insight_ImportedClassInit_EmptyAfterSelf()
    {
        const string helper = "class MotionVectors:\n    def __init__(self) -> None:\n        pass\n";
        const string text = "import helper\nhelper.MotionVectors(";
        var service = VsService(Includes(Read));
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var insight = service.Analyze(text, text.Length, []).Insight;

        Assert.NotNull(insight);
        Assert.Equal("MotionVectors()", insight.Overloads[0].Signature);
    }

    [Fact]
    public void Insight_ImportedClass_NestedInitDoesNotBindOuter()
    {
        const string helper = "class Outer:\n    class Inner:\n        def __init__(self, x):\n            pass\n";
        const string text = "import helper\nhelper.Outer(";
        var service = VsService(Includes(Read));
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var insight = service.Analyze(text, text.Length, []).Insight;

        Assert.True(insight == null || insight.Overloads.All(item => item.Name != "Outer"));
    }

    [Fact]
    public void Complete_ImportedEnum_OffersMembers()
    {
        const string helper = "class MeanMode(CustomEnum):\n    ARITHMETIC = 1\n    MEDIAN = auto()\n";
        const string text = "import helper\nhelper.MeanMode.";
        var service = VsService(Includes(Read));
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, []);

        Assert.Contains(reply.Items, x => x.InsertionText == "ARITHMETIC" && x.Kind == SymbolKind.Property);
        Assert.Contains(reply.Items, x => x.InsertionText == "MEDIAN" && x.Kind == SymbolKind.Property);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "MeanMode");
    }

    [Fact]
    public void Complete_ImportedStrEnum_OffersMembers()
    {
        const string helper = "class ConvMode(CustomStrEnum):\n    SQUARE = \"s\"\n    VERTICAL = \"v\"\n";
        const string text = "import helper\nhelper.ConvMode.";
        var service = VsService(Includes(Read));
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, []);

        Assert.Contains(reply.Items, x => x.InsertionText == "SQUARE");
        Assert.Contains(reply.Items, x => x.InsertionText == "VERTICAL");
    }

    [Fact]
    public void Complete_ImportedEnum_HintsMembers()
    {
        const string helper = "class MeanMode(CustomEnum):\n    ARITHMETIC = 1\n    MEDIAN = auto()\n";
        const string text = "import helper\nhelper.";
        var service = VsService(Includes(Read));
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var item = Assert.Single(service.Analyze(text, text.Length, []).Items, x => x.InsertionText == "MeanMode");

        Assert.Equal(SymbolKind.Namespace, item.Kind);
        Assert.Equal("Enum: ARITHMETIC | MEDIAN", CompletionData.HintText(item));
    }

    [Fact]
    public void Complete_FromImportedEnum_OffersMembers()
    {
        const string helper = "class MeanMode(CustomEnum):\n    ARITHMETIC = 1\n    MEDIAN = auto()\n";
        const string text = "from helper import MeanMode\nMeanMode.";
        var service = VsService(Includes(Read));
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, []);

        Assert.Contains(reply.Items, x => x.InsertionText == "ARITHMETIC");
        Assert.Contains(reply.Items, x => x.InsertionText == "MEDIAN");
    }

    [Fact]
    public void Insight_ImportedDataclass_UsesFields()
    {
        const string helper = "@dataclass(kw_only=True)\nclass NNEDI3:\n    nsize: int = 0\n    opencl: bool = False\n";
        const string text = "import helper\nhelper.NNEDI3(";
        var service = VsService(Includes(Read));
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var insight = service.Analyze(text, text.Length, []).Insight;

        Assert.NotNull(insight);
        Assert.Contains("nsize", insight.Overloads[0].Signature, StringComparison.Ordinal);
        Assert.Contains("opencl", insight.Overloads[0].Signature, StringComparison.Ordinal);
        Assert.DoesNotContain("parameters unknown", insight.Overloads[0].Signature, StringComparison.Ordinal);
    }

    private static void AssertDisplayTypes(string text)
    {
        Assert.Contains("clip:VideoNode", text, StringComparison.Ordinal);
        Assert.Contains("format:VideoFormat", text, StringComparison.Ordinal);
        Assert.DoesNotContain("vnode", text, StringComparison.Ordinal);
    }

    private static IncludeFile? Package(string specifier) => specifier switch
    {
        "pkg" => new IncludeFile("/plugins/pkg/__init__.py",
            "import pkg.sub\ndef sub(clip):\n    pass\n"),
        "pkg.sub" => new IncludeFile("/plugins/pkg/sub.py", "def helper():\n    pass\n"),
        _ => null
    };
}
