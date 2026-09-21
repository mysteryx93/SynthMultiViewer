using System.Diagnostics.CodeAnalysis;
using HanumanInstitute.ScriptAssist.AviSynth;
using HanumanInstitute.ScriptAssist.VapourSynth;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests.Host;

using static AssistHarness;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class BrowseTests
{
    [Fact]
    public async Task BrowseAsync_NativePlugin_GroupsNamespace()
    {
        var factory = Languages(VsNative(new VapourSynthFunction("std", "Crop", "clip:vnode", "clip:vnode;",
            "VapourSynth Core Functions")));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, "", CancellationToken.None);

        var std = Assert.Single(groups, g => g.Name == "std");
        var crop = Assert.Single(std.Functions, f => f.Name == "Crop");
        Assert.Equal("core.std.Crop()", crop.InsertText);
        Assert.Equal("VapourSynth Core Functions", std.Tip);
    }

    [Fact]
    public async Task BrowseAsync_BufferDef_ListsThisFile()
    {
        const string text = "def Foo():\n    pass\n";
        var factory = Languages();

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        var local = Assert.Single(groups, g => g.Name == "This file");
        var foo = Assert.Single(local.Functions, f => f.Name == "Foo");
        Assert.Equal("Foo()", foo.InsertText);
        Assert.Equal(0, foo.Offset);
    }

    [Fact]
    public async Task BrowseAsync_ImportedAlias_UsesAlias()
    {
        const string text = "import helper as h\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["helper"] = "def Foo():\n    return 1\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        var imported = Assert.Single(groups, g => g.Name == "h");
        Assert.Contains(imported.Functions, f => f.Name == "Foo" && f.InsertText == "h.Foo()");
        Assert.Equal("module /plugins/helper.py", imported.Tip);
        Assert.DoesNotContain(groups, g => g.Name == "helper");
    }

    [Fact]
    public async Task BrowseAsync_UnimportedPackage_ListsWithoutCompleting()
    {
        const string text = "import vapoursynth as vs\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["havsfunc"] = "def QTGMC():\n    return 1\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);
        var reply = await factory.Create(ScriptLanguageFactory.VapourSynth)!
            .GetAsync(text, text.Length, CancellationToken.None);

        var havs = Assert.Single(groups, g => g.Name == "havsfunc");
        var qtgmc = Assert.Single(havs.Functions, f => f.Name == "QTGMC");
        Assert.Equal("havsfunc.QTGMC()", qtgmc.InsertText);
        Assert.Equal("havsfunc", qtgmc.Import);
        Assert.Equal("module /plugins/havsfunc.py", havs.Tip);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "QTGMC");
    }

    [Fact]
    public async Task BrowseAsync_VapourSynthModule_Skipped()
    {
        const string text = "import vapoursynth as vs\n";
        var factory = Languages();

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        Assert.DoesNotContain(groups, g => g.Name is "vs" or "vapoursynth");
    }

    [Fact]
    public async Task BrowseAsync_ImportedClass_ListsClassNotNested()
    {
        const string text = "import helper as h\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["helper"] = "class Filter:\n    def apply(self):\n        pass\n\ndef Foo():\n    pass\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        var imported = Assert.Single(groups, g => g.Name == "h");
        var filter = Assert.Single(imported.Functions, f => f.Name == "Filter");
        Assert.Equal("h.Filter", filter.InsertText);
        Assert.Equal("Class", filter.Signature);
        Assert.DoesNotContain("parameters unknown", filter.Signature, StringComparison.Ordinal);
        Assert.Contains(imported.Functions, f => f.Name == "Foo");
        Assert.DoesNotContain(imported.Functions, f => f.Name == "apply");
    }

    [Fact]
    public async Task BrowseAsync_ImportedClassInit_UsesConstructorParameters()
    {
        const string text = "import helper as h\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["helper"] = "class Filter:\n    def __init__(self, clip):\n        pass\n    def apply(self):\n        pass\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        var imported = Assert.Single(groups, g => g.Name == "h");
        var filter = Assert.Single(imported.Functions, f => f.Name == "Filter");
        Assert.Equal("Filter(clip)", filter.Signature);
        Assert.DoesNotContain(imported.Functions, f => f.Name == "apply");
    }

    [Fact]
    public async Task BrowseAsync_ImportedEnum_ListsTypeName()
    {
        const string text = "import helper as h\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["helper"] = "class MotionMode(CustomIntEnum):\n    SAD = 0\n    COHERENCE = 1\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        var imported = Assert.Single(groups, g => g.Name == "h");
        var mode = Assert.Single(imported.Functions, f => f.Name == "MotionMode");
        Assert.Equal("h.MotionMode", mode.InsertText);
        Assert.Equal("Enum: SAD | COHERENCE", mode.Signature);
        Assert.DoesNotContain("MotionMode", mode.Signature, StringComparison.Ordinal);
        Assert.DoesNotContain("parameters unknown", mode.Signature, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BrowseAsync_ImportedEnum_DocstringThenMembers()
    {
        const string text = "import helper as h\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["helper"] =
                "class _NoSubmoduleRepr:\n    def __repr__(self):\n        return 'x'\n\n" +
                "class Dither(_NoSubmoduleRepr, str, Enum):\n    \"\"\"\n    Enum for zimg.\n    \"\"\"\n" +
                "    NONE = 'none'\n    ORDERED = 'ordered'\n    RANDOM = 'random'\n    ERROR_DIFFUSION = 'error_diffusion'\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        var dither = Assert.Single(Assert.Single(groups, g => g.Name == "h").Functions, f => f.Name == "Dither");
        Assert.Equal("h.Dither", dither.InsertText);
        Assert.Equal("Enum: NONE | ORDERED | RANDOM | ERROR_DIFFUSION", dither.Signature);
    }

    [Fact]
    public async Task BrowseAsync_ImportedClassInit_MapsVideoNodeHint()
    {
        const string text = "import helper as h\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["helper"] = "class Filter:\n    def __init__(self, clip: vs.VideoNode):\n        pass\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        var filter = Assert.Single(Assert.Single(groups, g => g.Name == "h").Functions, f => f.Name == "Filter");
        Assert.Equal("Filter(clip:VideoNode)", filter.Signature);
    }

    [Fact]
    public async Task BrowseAsync_ImportedClassInit_KeepsKeywordOnly()
    {
        const string text = "import helper as h\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["helper"] = "class Preset:\n    def __init__(self, *, tr: int | None = None):\n        pass\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        var preset = Assert.Single(Assert.Single(groups, g => g.Name == "h").Functions, f => f.Name == "Preset");
        Assert.Contains("tr", preset.Signature, StringComparison.Ordinal);
        Assert.DoesNotContain("self", preset.Signature, StringComparison.Ordinal);
        Assert.DoesNotContain("parameters unknown", preset.Signature, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BrowseAsync_ImportedTypedDict_UsesFields()
    {
        const string text = "import helper as h\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["helper"] =
                "class AnalyzeArgs(TypedDict, total=False):\n    blksize: int | None\n    search: SearchMode | None\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        var args = Assert.Single(Assert.Single(groups, g => g.Name == "h").Functions, f => f.Name == "AnalyzeArgs");
        Assert.Equal("h.AnalyzeArgs()", args.InsertText);
        Assert.Contains("blksize:int", args.Signature, StringComparison.Ordinal);
        Assert.Contains("search", args.Signature, StringComparison.Ordinal);
        Assert.DoesNotContain("parameters unknown", args.Signature, StringComparison.Ordinal);
        Assert.NotEqual("", args.Signature);
    }

    [Fact]
    public async Task BrowseAsync_ImportedDataclass_UsesFields()
    {
        const string text = "import helper as h\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["helper"] = "@dataclass\nclass NNEDI3:\n    nsize: int = 0\n    nns: int = 4\n    def apply(self):\n        pass\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        var nnedi = Assert.Single(Assert.Single(groups, g => g.Name == "h").Functions, f => f.Name == "NNEDI3");
        Assert.Equal("h.NNEDI3()", nnedi.InsertText);
        Assert.Contains("nsize", nnedi.Signature, StringComparison.Ordinal);
        Assert.Contains("nns", nnedi.Signature, StringComparison.Ordinal);
        Assert.DoesNotContain("self", nnedi.Signature, StringComparison.Ordinal);
        Assert.DoesNotContain("apply", nnedi.Signature, StringComparison.Ordinal);
        Assert.DoesNotContain("parameters unknown", nnedi.Signature, StringComparison.Ordinal);
        Assert.DoesNotContain(Assert.Single(groups, g => g.Name == "h").Functions, f => f.Name == "apply");
    }

    [Fact]
    public async Task BrowseAsync_GenericClassInit_UsesConstructorParameters()
    {
        const string text = "import helper as h\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["helper"] = "class MixedScalerProcess[T: Scaler, *Ts](Base, abstract=True):\n    def __init__(self, *, function, **kwargs):\n        pass\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        var mixed = Assert.Single(Assert.Single(groups, g => g.Name == "h").Functions, f => f.Name == "MixedScalerProcess");
        Assert.Contains("function", mixed.Signature, StringComparison.Ordinal);
        Assert.DoesNotContain("self", mixed.Signature, StringComparison.Ordinal);
        Assert.DoesNotContain("parameters unknown", mixed.Signature, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BrowseAsync_ImportedUnion_UnwrapsNone()
    {
        const string text = "import helper as h\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["helper"] =
                "def util(clip: vs.VideoNode, bitdepth: int | None = None) -> vs.VideoNode:\n    pass\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        var util = Assert.Single(Assert.Single(groups, g => g.Name == "h").Functions, f => f.Name == "util");
        Assert.Contains("clip:VideoNode", util.Signature, StringComparison.Ordinal);
        Assert.Contains("bitdepth:int", util.Signature, StringComparison.Ordinal);
        Assert.DoesNotContain("| None", util.Signature, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BrowseAsync_ImportedNestedUnion_KeepsInnerPipe()
    {
        const string text = "import helper as h\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["helper"] =
                "def util(color_family: Iterable[VideoFormatLike | vs.ColorFamily] | None = None):\n    pass\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        var util = Assert.Single(Assert.Single(groups, g => g.Name == "h").Functions, f => f.Name == "util");
        Assert.Contains("Iterable[VideoFormatLike | vs.ColorFamily]", util.Signature, StringComparison.Ordinal);
        Assert.DoesNotContain("parameters unknown", util.Signature, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("class CustomEnum(Enum):\n    def from_param(self):\n        pass\n", "CustomEnum",
        "Class bases: Enum")]
    [InlineData("class Shape(ABC):\n    pass\n", "Shape", "Class bases: ABC")]
    [InlineData("class Fn(Protocol):\n    pass\n", "Fn", "Class bases: Protocol")]
    [InlineData("class CustomIntEnum(int, CustomEnum, ReprEnum):\n    pass\n", "CustomIntEnum",
        "Class bases: int, CustomEnum, ReprEnum")]
    [InlineData("class Empty(_Hidden, str, Enum):\n    pass\n", "Empty", "Class bases: str, Enum")]
    [InlineData("class EnumABCMeta(EnumMeta, ABCMeta):\n    pass\n", "EnumABCMeta",
        "Class bases: EnumMeta, ABCMeta")]
    [InlineData("class Scaler(Kernel):\n    pass\n", "Scaler", "Class bases: Kernel")]
    public async Task BrowseAsync_ImportedTypeName_HintsBases(string helper, string name, string hint)
    {
        const string text = "import helper as h\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["helper"] = helper
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        var item = Assert.Single(Assert.Single(groups, g => g.Name == "h").Functions, f => f.Name == name);
        Assert.Equal("h." + name, item.InsertText);
        Assert.Equal(hint, item.Signature);
        Assert.DoesNotContain("parameters unknown", item.Signature, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BrowseAsync_ImportedErrorClass_HintsBases()
    {
        const string text = "import helper as h\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["helper"] = "class CustomValueError(CustomError, ValueError):\n    pass\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        var error = Assert.Single(Assert.Single(groups, g => g.Name == "h").Functions, f => f.Name == "CustomValueError");
        Assert.Equal("h.CustomValueError", error.InsertText);
        Assert.Equal("Class bases: CustomError, ValueError", error.Signature);
    }

    [Fact]
    public async Task BrowseAsync_LaterPackage_LoadsAfterWorkBudget()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var heavy = "";
        for (var i = 0; i < IncludeCache.ImportWorkLimit; i++)
        {
            files["m" + i] = "def F():\n    pass\n";
            heavy += "import m" + i + "\n";
        }

        files["heavy"] = heavy;
        files["late"] = "def Late():\n    pass\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(files));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, "import vapoursynth as vs\n",
            CancellationToken.None, extraPackages: ["heavy", "late"]);

        var late = Assert.Single(groups, g => g.Name == "late");
        Assert.Contains(late.Functions, f => f.Name == "Late");
    }

    [Fact]
    public async Task BrowseAsync_AviSynthGroups_LabelsAutoload()
    {
        const string text = "function Local(clip c) { c }\n";
        var factory = Languages(avisynth: AvsNative(
            new AviSynthFilter("Crop", "c[left]i", "InternalFunctions"),
            new AviSynthFilter("FFVideoSource", "s", "PluginFunctions"),
            new AviSynthFilter("QTGMC", "c", "UserFunctions")));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.AviSynth, text, CancellationToken.None);

        Assert.Contains(Assert.Single(groups, g => g.Name == "Internal").Functions, f => f.Name == "Crop");
        Assert.Contains(Assert.Single(groups, g => g.Name == "Plugin").Functions, f => f.Name == "FFVideoSource");
        Assert.Contains(Assert.Single(groups, g => g.Name == "Autoload").Functions, f => f.Name == "QTGMC");
        var local = Assert.Single(Assert.Single(groups, g => g.Name == "This file").Functions);
        Assert.Equal("Local", local.Name);
        Assert.Equal("Local()", local.InsertText);
        Assert.Equal(0, local.Offset);
    }

    [Fact]
    public async Task BrowseAsync_WhenDisabled_ReturnsGroups()
    {
        var factory = Languages(VsNative(new VapourSynthFunction("std", "Crop", "clip:vnode", "clip:vnode;")));
        factory.IsEnabled = false;

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, "", CancellationToken.None);

        Assert.Null(factory.Create(ScriptLanguageFactory.VapourSynth));
        Assert.Contains(groups, g => g.Name == "std");
    }

    [Fact]
    public async Task BrowseAsync_NativePlugins_SortsGroupsAndFunctions()
    {
        var factory = Languages(VsNative(
            new VapourSynthFunction("std", "Crop", "clip:vnode", "clip:vnode;"),
            new VapourSynthFunction("std", "BlankClip", "", "clip:vnode;"),
            new VapourSynthFunction("resize", "Bilinear", "clip:vnode", "clip:vnode;")));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, "", CancellationToken.None);

        Assert.Equal(["resize", "std"], groups.Select(group => group.Name));
        Assert.Equal(["BlankClip", "Crop"],
            Assert.Single(groups, group => group.Name == "std").Functions.Select(item => item.Name));
    }

    [Fact]
    public async Task BrowseAsync_ThisFile_SortsFirst()
    {
        const string text = "def Zoo():\n    pass\ndef Apple():\n    pass\n";
        var factory = Languages(VsNative(new VapourSynthFunction("std", "Crop", "clip:vnode", "clip:vnode;")));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        Assert.Equal("This file", groups[0].Name);
        Assert.Equal(["Apple", "Zoo"], groups[0].Functions.Select(item => item.Name));
    }

    [Fact]
    public async Task BrowseAsync_MixedCaseFunctions_SortsTogether()
    {
        const string text = "import vapoursynth as vs\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["havsfunc"] = "def YAHR():\n    pass\ndef aaf():\n    pass\ndef AverageFrames():\n    pass\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        Assert.Equal(["aaf", "AverageFrames", "YAHR"],
            Assert.Single(groups, group => group.Name == "havsfunc").Functions.Select(item => item.Name));
    }

    [Fact]
    public async Task BrowseAsync_AviSynthFunctions_SortsIgnoreCase()
    {
        var factory = Languages(avisynth: AvsNative(
            new AviSynthFilter("Crop", "c[left]i", "InternalFunctions"),
            new AviSynthFilter("BlankClip", "", "InternalFunctions"),
            new AviSynthFilter("zzSource", "s", "PluginFunctions"),
            new AviSynthFilter("FFVideoSource", "s", "PluginFunctions")));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.AviSynth, "", CancellationToken.None);

        Assert.Equal(["Internal", "Plugin"], groups.Select(group => group.Name));
        Assert.Equal(["BlankClip", "Crop"],
            Assert.Single(groups, group => group.Name == "Internal").Functions.Select(item => item.Name));
        Assert.Equal(["FFVideoSource", "zzSource"],
            Assert.Single(groups, group => group.Name == "Plugin").Functions.Select(item => item.Name));
    }

    [Fact]
    public async Task BrowseAsync_UnknownLanguage_ReturnsEmpty()
    {
        var factory = Languages();

        var groups = await factory.BrowseAsync("missing", "", CancellationToken.None);

        Assert.Empty(groups);
    }

    [Fact]
    public async Task BrowseAsync_ExtraPackage_LoadsUnimported()
    {
        const string text = "import vapoursynth as vs\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["helper"] = "def Foo():\n    return 1\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None,
            extraPackages: ["helper"]);

        var helper = Assert.Single(groups, g => g.Name == "helper");
        Assert.Contains(helper.Functions, f => f.Name == "Foo");
    }

    [Fact]
    public async Task BrowseAsync_ImportedSubmodule_ListsNestedFunction()
    {
        const string text = "import pkg.sub\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["pkg"] = "",
            ["pkg.sub"] = "def Deep():\n    pass\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None);

        var pkg = Assert.Single(groups, g => g.Name == "pkg");
        var deep = Assert.Single(pkg.Functions, f => f.Name == "Deep");
        Assert.Equal("pkg.sub.Deep()", deep.InsertText);
    }

    [Fact]
    public async Task BrowseAsync_AviSynthImport_ListsImportedFunction()
    {
        const string text = "Import(\"helper.avsi\")\n";
        var factory = Languages(avisynthIncludes: Includes((_, _) =>
            new IncludeFile("/plugins/helper.avsi", "function Helper(clip c) { c }\n")));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.AviSynth, text, CancellationToken.None);

        var local = Assert.Single(groups, g => g.Name == "This file");
        var helper = Assert.Single(local.Functions, f => f.Name == "Helper");
        Assert.Null(helper.Offset);
    }

    [Fact]
    public async Task BrowseAsync_AviSynthDuplicateOverloads_KeepsOneRow()
    {
        var factory = Languages(avisynth: AvsNative(
            new AviSynthFilter("Crop", "c[left]i", "InternalFunctions"),
            new AviSynthFilter("Crop", "c[left]i", "InternalFunctions"),
            new AviSynthFilter("Crop", "c[left]i", "InternalFunctions")));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.AviSynth, "", CancellationToken.None);

        Assert.Single(Assert.Single(groups, g => g.Name == "Internal").Functions, f => f.Name == "Crop");
    }

    [Fact]
    public async Task BrowseAsync_ManyPackagesTwice_DoesNotReread()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var extra = new List<string>();
        for (var i = 0; i < 100; i++)
        {
            var name = "pkg" + i;
            files[name] = "def F" + i + "():\n    pass\n";
            extra.Add(name);
        }

        var reads = 0;
        var factory = Languages(vapoursynthIncludes: Includes((specifier, _) =>
        {
            if (!files.TryGetValue(specifier, out var text))
            {
                return null;
            }

            Interlocked.Increment(ref reads);
            return new IncludeFile("/site/" + specifier + ".py", text);
        }));

        await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, "import vapoursynth as vs\n",
            CancellationToken.None, extraPackages: extra);
        var first = reads;
        await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, "import vapoursynth as vs\n",
            CancellationToken.None, extraPackages: extra);
        await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, "import vapoursynth as vs\n",
            CancellationToken.None, extraPackages: extra);

        Assert.Equal(100, first);
        Assert.Equal(first, reads);
    }

    [Fact]
    public async Task BrowseAsync_UnimportedPackage_ListsSubmoduleExports()
    {
        const string text = "import vapoursynth as vs\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["pkg"] = "from . import sub\n",
            [".sub"] = "def Deep():\n    pass\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None,
            extraPackages: ["pkg"]);

        var pkg = Assert.Single(groups, g => g.Name == "pkg");
        var deep = Assert.Single(pkg.Functions, f => f.Name == "Deep");
        Assert.Equal("pkg.sub.Deep()", deep.InsertText);
    }

    [Fact]
    public async Task BrowseAsync_FromImport_DoesNotListUnderThisFile()
    {
        const string text = "from helper import Filter\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["helper"] = "class Filter:\n    pass\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None,
            extraPackages: ["helper"]);

        Assert.DoesNotContain(groups, g => g.Name == "This file");
        var helper = Assert.Single(groups, g => g.Name == "helper");
        Assert.Contains(helper.Functions, f => f.Name == "Filter");
    }

    [Fact]
    public async Task BrowseAsync_AssignedQualifier_OmitsInstalledPackage()
    {
        const string text = "helper = 1\n";
        var factory = Languages(vapoursynthIncludes: FilesReader(new Dictionary<string, string>
        {
            ["helper"] = "def Foo():\n    pass\n"
        }));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, text, CancellationToken.None,
            extraPackages: ["helper"]);

        Assert.DoesNotContain(groups, g => g.Name == "helper");
    }
}
