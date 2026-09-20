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
        var factory = Languages(VsNative(new VapourSynthFunction("std", "Crop", "clip:vnode", "clip:vnode;")));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, "", CancellationToken.None);

        var std = Assert.Single(groups, g => g.Name == "std");
        var crop = Assert.Single(std.Functions, f => f.Name == "Crop");
        Assert.Equal("core.std.Crop()", crop.InsertText);
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
        Assert.Equal("h.Filter()", filter.InsertText);
        Assert.Contains("parameters unknown", filter.Signature, StringComparison.Ordinal);
        Assert.Contains(imported.Functions, f => f.Name == "Foo");
        Assert.DoesNotContain(imported.Functions, f => f.Name == "apply");
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
