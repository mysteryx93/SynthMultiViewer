using System.Diagnostics.CodeAnalysis;
using HanumanInstitute.ScriptAssist.AviSynth;
using HanumanInstitute.ScriptAssist.VapourSynth;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests.Imports;

using static AssistHarness;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class ScriptImportTests
{
    [Fact]
    public void Analyze_AviSynthImportedFunction_DoesNotExposeParameters()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        const string avsi = "function QTGMC(clip Input, int \"TR0\") { return Input }\n";
        var service = new LanguageService(new AviSynthLanguage(Includes(Read)), Catalog());
        const string text = "Import(\"QTGMC.avsi\")\nInput.";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "QTGMC.avsi" ? new IncludeFile("/plugins/QTGMC.avsi", avsi) : null;

        var reply = service.Analyze(text, text.Length, [crop]);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_ManyAviSynthImports_KeepsAllExports()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var script = "";
        for (var i = 0; i < 80; i++)
        {
            var name = "Child" + i;
            files[name + ".avsi"] = "function " + name + "(clip c) { c }\n";
            script += "Import(\"" + name + ".avsi\")\n";
        }

        script += "Child79(";
        var service = new LanguageService(new AviSynthLanguage(Includes(Read)), Catalog());
        IncludeFile? Read(string specifier, string? _) =>
            files.TryGetValue(specifier, out var text)
                ? new IncludeFile("/plugins/" + specifier, text)
                : null;

        var reply = service.Analyze(script, script.Length, []);

        Assert.NotNull(reply.Insight);
        Assert.Equal("Child79", reply.Insight.Overloads[0].Name);
        Assert.Contains(reply.Insight.Overloads[0].Parameters ?? [], x => x.Contains("clip", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DeepPythonImportChain_DoesNotThrow()
    {
        const int depth = 200;
        var service = VsService(Includes(Read));
        const string text = "import n0 as m\nm.";
        IncludeFile? Read(string specifier, string? _)
        {
            if (!specifier.StartsWith('n') || !int.TryParse(specifier[1..], out var n) || n >= depth)
            {
                return null;
            }

            var body = n + 1 >= depth ? "def leaf():\n    pass\n" : "import n" + (n + 1) + " as m\n";
            return new IncludeFile("/plugins/" + specifier + ".py", body);
        }

        var exception = Record.Exception(() => service.Analyze(text, text.Length, Vs));

        Assert.Null(exception);
    }

    [Fact]
    public void Analyze_DeepAviSynthImportChain_DoesNotThrow()
    {
        const int depth = 200;
        var service = AvsService(Includes(Read));
        const string text = "Import(\"0.avsi\")\n";
        IncludeFile? Read(string specifier, string? _)
        {
            if (!specifier.EndsWith(".avsi", StringComparison.Ordinal) ||
                !int.TryParse(specifier[..^5], out var n) || n >= depth)
            {
                return null;
            }

            var body = n + 1 >= depth
                ? "function Leaf(clip c) { c }\n"
                : "Import(\"" + (n + 1) + ".avsi\")\n";
            return new IncludeFile("/plugins/" + specifier, body);
        }

        var exception = Record.Exception(() => service.Analyze(text, text.Length, []));

        Assert.Null(exception);
    }

    [Fact]
    public void Bind_ManyPythonImports_DoesNotRereadOnSecondBind()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var script = "";
        for (var i = 0; i < 80; i++)
        {
            files["child" + i] = "def F" + i + "(clip):\n    return clip\n";
            script += "from child" + i + " import F" + i + "\n";
        }

        script += "F79(";
        var reads = 0;
        var language = new VapourSynthLanguage(Includes(Read));
        var native = Array.Empty<Symbol>();
        language.Bind(script, native, CancellationToken.None, "/plugins/root.py");
        var first = reads;
        IncludeFile? Read(string specifier, string? _)
        {
            Interlocked.Increment(ref reads);
            return files.TryGetValue(specifier, out var text)
                ? new IncludeFile("/plugins/" + specifier + ".py", text)
                : null;
        }

        var bindings = language.Bind(script, native, CancellationToken.None, "/plugins/root.py");

        Assert.Equal(80, first);
        Assert.Equal(first, reads);
        Assert.Contains(bindings.BufferSymbols, x => x.Name == "F79");
        Assert.Equal(80, bindings.BufferSymbols.Count(x => x.Name.StartsWith('F')));
    }

    [Fact]
    public void Bind_ManyIndependentPythonImports_RestoresWithoutThrowing()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var script = "";
        const int count = 150;
        for (var i = 0; i < count; i++)
        {
            files["child" + i] = "def F" + i + "(clip):\n    return clip\n";
            script += "from child" + i + " import F" + i + "\n";
        }

        script += "F149(";
        var language = new VapourSynthLanguage(Includes(Read));
        var native = Array.Empty<Symbol>();
        IncludeFile? Read(string specifier, string? _) =>
            files.TryGetValue(specifier, out var text)
                ? new IncludeFile("/plugins/" + specifier + ".py", text)
                : null;

        var first = language.Bind(script, native, CancellationToken.None, "/plugins/root.py");
        var second = language.Bind(script, native, CancellationToken.None, "/plugins/root.py");
        var third = Record.Exception(() =>
            language.Bind(script, native, CancellationToken.None, "/plugins/root.py"));

        Assert.Contains(first.BufferSymbols, x => x.Name == "F149");
        Assert.Contains(second.BufferSymbols, x => x.Name == "F149");
        Assert.Null(third);
        Assert.Contains(language.Bind(script, native, CancellationToken.None, "/plugins/root.py").BufferSymbols,
            x => x.Name == "F0");
    }

    [Fact]
    public void Analyze_ImportedModule_CompletesMembers()
    {
        const string havs = """
            def QTGMC(clip, Preset='Slow', TR0=0):
                return clip
            def helper():
                def nested(clip):
                    return clip
                return nested
            """;
        var service = new LanguageService(new VapourSynthLanguage(HavsReader(havs)), Catalog());
        const string text = "import havsfunc as haf\nhaf.";

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "QTGMC");
        Assert.Contains(reply.Items, x => x.InsertionText == "helper");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "nested");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Insight_ImportedModule_ShowsCallSignature()
    {
        const string havs = """
            def QTGMC(clip, Preset='Slow', TR0=0):
                return clip
            def helper():
                def nested(clip):
                    return clip
                return nested
            """;
        var service = new LanguageService(new VapourSynthLanguage(HavsReader(havs)), Catalog());
        const string text = "import havsfunc as haf\nhaf.QTGMC(";

        var insight = service.Analyze(text, text.Length, Vs).Insight!;

        Assert.Equal("QTGMC", insight.Overloads[0].Name);
        Assert.Contains("Preset='Slow'", insight.Overloads[0].Signature);
    }

    [Fact]
    public void Analyze_FromImportStar_OffersFunctionsAtRoot()
    {
        const string havs = "def QTGMC(clip, Preset='Slow'):\n    return clip\n";
        var service = new LanguageService(new VapourSynthLanguage(HavsReader(havs)), Catalog());
        const string text = "from havsfunc import *\nQTG";

        var reply = service.Analyze(text, text.Length, Vs);

        var item = Assert.Single(reply.Items, x => x.InsertionText == "QTGMC");
        Assert.Contains("Preset='Slow'", item.Signature);
    }

    [Fact]
    public void Hover_FromImportStar_ShowsSignature()
    {
        const string havs = "def QTGMC(clip, Preset='Slow'):\n    return clip\n";
        var service = new LanguageService(new VapourSynthLanguage(HavsReader(havs)), Catalog());
        const string text = "from havsfunc import *\nQTGMC";

        var hover = service.Analyze(text, text.Length, Vs).Hover;

        Assert.NotNull(hover);
        Assert.Contains("QTGMC(", hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_UnknownImport_StaysSilent()
    {
        var service = new LanguageService(new VapourSynthLanguage(HavsReader(null)), Catalog());
        const string text = "import os\nos.";

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "QTGMC");
        Assert.Empty(reply.Items);
    }

    [Fact]
    public void Analyze_NestedImport_ExposesModuleMembers()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pack"] = "import mid as m\ndef PackFn(clip):\n    return clip\n",
            ["mid"] = "import leaf as l\ndef MidFn(clip):\n    return clip\n",
            ["leaf"] = "def Deep(clip, radius=1):\n    return clip\n"
        };
        var service = new LanguageService(new VapourSynthLanguage(FilesReader(files)), Catalog());
        const string text = "import pack as p\np.";

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "PackFn");
        Assert.Contains(reply.Items, x => x.InsertionText == "m");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Deep");
    }

    [Fact]
    public void Analyze_NestedImport_OffersDeepMembers()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pack"] = "import mid as m\ndef PackFn(clip):\n    return clip\n",
            ["mid"] = "import leaf as l\ndef MidFn(clip):\n    return clip\n",
            ["leaf"] = "def Deep(clip, radius=1):\n    return clip\n"
        };
        var service = new LanguageService(new VapourSynthLanguage(FilesReader(files)), Catalog());
        const string text = "import pack as p\np.m.l.";

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "Deep");
    }

    [Fact]
    public void Insight_NestedImport_ShowsDeepSignature()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pack"] = "import mid as m\ndef PackFn(clip):\n    return clip\n",
            ["mid"] = "import leaf as l\ndef MidFn(clip):\n    return clip\n",
            ["leaf"] = "def Deep(clip, radius=1):\n    return clip\n"
        };
        var service = new LanguageService(new VapourSynthLanguage(FilesReader(files)), Catalog());
        const string text = "import pack as p\np.m.l.Deep(";

        var insight = service.Analyze(text, text.Length, Vs).Insight!;

        Assert.Equal("Deep", insight.Overloads[0].Name);
        Assert.Contains("radius=1", insight.Overloads[0].Signature);
    }

    [Fact]
    public void Analyze_ReexportedFromImport_CompletesOnImporter()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pack"] = "from mid import Deep\n",
            ["mid"] = "def Deep(clip, radius=1):\n    return clip\n"
        };
        var service = new LanguageService(new VapourSynthLanguage(FilesReader(files)), Catalog());
        const string text = "import pack as p\np.";

        var reply = service.Analyze(text, text.Length, Vs);

        var deep = Assert.Single(reply.Items, x => x.InsertionText == "Deep");
        Assert.Contains("radius=1", deep.Signature);
    }

    [Fact]
    public void Analyze_FromImportStar_FollowsNestedStar()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pack"] = "from mid import *\n",
            ["mid"] = "from leaf import *\n",
            ["leaf"] = "def Deep(clip, radius=1):\n    return clip\n"
        };
        var service = new LanguageService(new VapourSynthLanguage(FilesReader(files)), Catalog());
        const string text = "from pack import *\nDee";

        var reply = service.Analyze(text, text.Length, Vs);

        var deep = Assert.Single(reply.Items, x => x.InsertionText == "Deep");
        Assert.Contains("radius=1", deep.Signature);
    }

    [Fact]
    public void Analyze_RelativeFromImport_ReadsSiblingModule()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["havsfunc"] = "from .qtgmc import QTGMC\n",
            [".qtgmc"] = "def QTGMC(clip, Preset='Slow'):\n    return clip\n"
        };
        var service = new LanguageService(new VapourSynthLanguage(FilesReader(files)), Catalog());
        const string text = "import havsfunc as haf\nhaf.";

        var reply = service.Analyze(text, text.Length, Vs);

        var qtgmc = Assert.Single(reply.Items, x => x.InsertionText == "QTGMC");
        Assert.Contains("Preset='Slow'", qtgmc.Signature);
    }

    [Fact]
    public void Analyze_AviSynthImport_AddsParsedSignatures()
    {
        const string avsi = """
            function QTGMC(clip Input, int "TR0", string "Preset") {
                return Input
            }
            """;
        var service = new LanguageService(new AviSynthLanguage(Includes(Read)), Catalog());
        var native = new[] { new Symbol("QTGMC", ["clip"]) };
        const string text = "Import(\"QTGMC.avsi\")\nlast.QTG";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "QTGMC.avsi" ? new IncludeFile("/plugins/QTGMC.avsi", avsi) : null;

        var reply = service.Analyze(text, text.Length, native);

        var item = Assert.Single(reply.Items, x => x.InsertionText == "QTGMC");
        Assert.Contains("TR0", item.Signature);
        Assert.Contains("Preset", item.Signature);
    }

    [Fact]
    public void Insight_AviSynthImport_ShowsImplicitClip()
    {
        const string avsi = """
            function QTGMC(clip Input, int "TR0", string "Preset") {
                return Input
            }
            """;
        var service = new LanguageService(new AviSynthLanguage(Includes(Read)), Catalog());
        var native = new[] { new Symbol("QTGMC", ["clip"]) };
        const string text = "Import(\"QTGMC.avsi\")\nlast.QTGMC(";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "QTGMC.avsi" ? new IncludeFile("/plugins/QTGMC.avsi", avsi) : null;

        var insight = service.Analyze(text, text.Length, native).Insight!;

        Assert.True(insight.ImplicitClip);
        Assert.Contains("Preset", insight.Overloads[0].Signature);
    }

    [Fact]
    public void Analyze_AviSynthNestedImport_LoadsGrandchildSignatures()
    {
        var service = new LanguageService(new AviSynthLanguage(Includes(Read)), Catalog());
        const string text = "Import(\"a.avs\")\nlast.";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "a.avs" => new IncludeFile("/a.avs", "Import(\"b.avs\")\nfunction FromA(clip c) { return c }"),
            "b.avs" => new IncludeFile("/b.avs", "Import(\"c.avs\")\nfunction FromB(clip c) { return c }"),
            "c.avs" => new IncludeFile("/c.avs", "function FromC(clip Input, int \"radius\") { return Input }"),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, []);

        Assert.Contains(reply.Items, x => x.InsertionText == "FromA");
        Assert.Contains(reply.Items, x => x.InsertionText == "FromB");
        var fromC = Assert.Single(reply.Items, x => x.InsertionText == "FromC");
        Assert.Contains("radius", fromC.Signature);
    }

    [Fact]
    public void Insight_AviSynthNestedImport_ShowsGrandchildSignature()
    {
        var service = new LanguageService(new AviSynthLanguage(Includes(Read)), Catalog());
        const string text = "Import(\"a.avs\")\nlast.FromC(";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "a.avs" => new IncludeFile("/a.avs", "Import(\"b.avs\")\nfunction FromA(clip c) { return c }"),
            "b.avs" => new IncludeFile("/b.avs", "Import(\"c.avs\")\nfunction FromB(clip c) { return c }"),
            "c.avs" => new IncludeFile("/c.avs", "function FromC(clip Input, int \"radius\") { return Input }"),
            _ => null
        };

        var insight = service.Analyze(text, text.Length, []).Insight!;

        Assert.Contains("radius", insight.Overloads[0].Signature);
    }

    [Fact]
    public void Analyze_AviSynthImportCycle_DoesNotRecurseForever()
    {
        var service = new LanguageService(new AviSynthLanguage(Includes(Read)), Catalog());
        const string text = "Import(\"a.avs\")\nlast.";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "a.avs" => new IncludeFile("/a.avs", "Import(\"b.avs\")\nfunction FromA(clip c) { return c }"),
            "b.avs" => new IncludeFile("/b.avs", "Import(\"a.avs\")\nfunction FromB(clip c) { return c }"),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, []);

        Assert.Contains(reply.Items, x => x.InsertionText == "FromA");
        Assert.Contains(reply.Items, x => x.InsertionText == "FromB");
    }

    [Fact]
    public void Analyze_VapourSynthImportCycle_DoesNotRecurseForever()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["a"] = "import b as x\ndef FromA(clip):\n    return clip\n",
            ["b"] = "import a as y\ndef FromB(clip):\n    return clip\n"
        };
        var service = new LanguageService(new VapourSynthLanguage(FilesReader(files)), Catalog());
        const string text = "import a as m\nm.";

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "FromA");
        Assert.Contains(reply.Items, x => x.InsertionText == "x");
    }

    [Fact]
    public void Insight_ImportedFunction_ShowsOverload()
    {
        const string helper = "def Filter(clip) -> vs.VideoNode:\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = "from helper import Filter\nFilter(";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var insight = service.Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("Filter", insight.Overloads[0].Name);
    }

    [Fact]
    public void Analyze_ImportedFunction_InfersReturnType()
    {
        const string helper = "def Filter(clip) -> vs.VideoNode:\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = "from helper import Filter\nclip = Filter()\nclip.";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_RelativeImportA_DoesNotSeeB()
    {
        var service = VsService(Includes(Read));
        const string text = "import a\nimport b\na.";
        IncludeFile? Read(string specifier, string? fromPath)
        {
            if (specifier is "a" or "b")
            {
                return new IncludeFile("/pkg/" + specifier + "/__init__.py", "from .helper import *\n");
            }
            if (specifier != ".helper" && specifier != "helper")
            {
                return null;
            }
            if (fromPath?.Contains("/a/", StringComparison.Ordinal) == true)
            {
                return new IncludeFile("/pkg/a/helper.py", "def OnlyA(clip):\n    return clip\n");
            }
            if (fromPath?.Contains("/b/", StringComparison.Ordinal) == true)
            {
                return new IncludeFile("/pkg/b/helper.py", "def OnlyB(clip):\n    return clip\n");
            }
            return null;
        }

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "OnlyA");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "OnlyB");
    }

    [Fact]
    public void Analyze_RelativeImportB_DoesNotSeeA()
    {
        var service = VsService(Includes(Read));
        const string text = "import a\nimport b\nb.";
        IncludeFile? Read(string specifier, string? fromPath)
        {
            if (specifier is "a" or "b")
            {
                return new IncludeFile("/pkg/" + specifier + "/__init__.py", "from .helper import *\n");
            }
            if (specifier != ".helper" && specifier != "helper")
            {
                return null;
            }
            if (fromPath?.Contains("/a/", StringComparison.Ordinal) == true)
            {
                return new IncludeFile("/pkg/a/helper.py", "def OnlyA(clip):\n    return clip\n");
            }
            if (fromPath?.Contains("/b/", StringComparison.Ordinal) == true)
            {
                return new IncludeFile("/pkg/b/helper.py", "def OnlyB(clip):\n    return clip\n");
            }
            return null;
        }

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "OnlyB");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "OnlyA");
    }

    [Fact]
    public void Analyze_ScopedImport_DoesNotLeak()
    {
        const string helper = "def Filter(clip):\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = """
            def f():
                from helper import Filter
            Fil
            """;
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void Analyze_ScopedImport_OffersInsideBody()
    {
        const string helper = "def Filter(clip):\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = """
            def f():
                from helper import Filter
                Fil
            """;
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void Analyze_ReboundModuleAlias_OffersImportedMembers()
    {
        const string helper = "def Filter(clip):\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = """
            local = 2
            import helper as local
            local.
            """;
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "Filter");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_ParenthesizedFromImport_OffersMember()
    {
        const string helper = "def Filter(clip):\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = """
            from helper import (
                Filter,
            )
            Fil
            """;
        IncludeFile? Read(string specifier, string? _) =>
            specifier is "helper" or "package.helper"
                ? new IncludeFile("/plugins/" + specifier.Replace('.', '/') + ".py", helper)
                : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void Analyze_DottedPackageImport_OffersModuleNotMembers()
    {
        const string helper = "def Filter(clip):\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = "import package.helper\npackage.";
        IncludeFile? Read(string specifier, string? _) =>
            specifier is "helper" or "package.helper"
                ? new IncludeFile("/plugins/" + specifier.Replace('.', '/') + ".py", helper)
                : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "helper");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void Analyze_DottedPackageImport_OffersNestedMembers()
    {
        const string helper = "def Filter(clip):\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = "import package.helper\npackage.helper.";
        IncludeFile? Read(string specifier, string? _) =>
            specifier is "helper" or "package.helper"
                ? new IncludeFile("/plugins/" + specifier.Replace('.', '/') + ".py", helper)
                : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void Analyze_ThreeComponentImport_KeepsIntermediateNamespace()
    {
        const string helper = "def Filter(clip):\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = "import pkg.sub.helper\npkg.sub.";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "pkg.sub.helper"
                ? new IncludeFile("/plugins/pkg/sub/helper.py", helper)
                : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "helper");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void Analyze_ThreeComponentImport_OffersLeafMembers()
    {
        const string helper = "def Filter(clip):\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = "import pkg.sub.helper\npkg.sub.helper.";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "pkg.sub.helper"
                ? new IncludeFile("/plugins/pkg/sub/helper.py", helper)
                : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void Analyze_InnerFunctionImport_ShadowsGlobalFunction()
    {
        var service = VsService(Includes(Read));
        const string text = """
            from helper import Filter
            def f():
                from other import Filter
                x = Filter()
                x.
            """;
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "helper" => new IncludeFile("/plugins/helper.py",
                "def Filter(clip) -> vs.VideoNode:\n    return clip\n"),
            "other" => new IncludeFile("/plugins/other.py", "def Filter() -> int:\n    return 1\n"),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void Analyze_PackageAndSubmoduleImports_PreserveBothSides()
    {
        var service = VsService(Includes(Read));
        const string text = "import pkg\nimport pkg.sub\npkg.";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "pkg" => new IncludeFile("/plugins/pkg/__init__.py", "def RootFunction():\n    return 1\n"),
            "pkg.sub" => new IncludeFile("/plugins/pkg/sub/__init__.py", "def SubFunction():\n    return 1\n"),
            "pkg.sub.helper" => new IncludeFile("/plugins/pkg/sub/helper.py", "def HelperFn():\n    return 1\n"),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "RootFunction");
        Assert.Contains(reply.Items, x => x.InsertionText == "sub");
    }

    [Fact]
    public void Analyze_SubmoduleThenPackageImport_PreservesBothSides()
    {
        var service = VsService(Includes(Read));
        const string text = "import pkg.sub\nimport pkg\npkg.";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "pkg" => new IncludeFile("/plugins/pkg/__init__.py", "def RootFunction():\n    return 1\n"),
            "pkg.sub" => new IncludeFile("/plugins/pkg/sub/__init__.py", "def SubFunction():\n    return 1\n"),
            "pkg.sub.helper" => new IncludeFile("/plugins/pkg/sub/helper.py", "def HelperFn():\n    return 1\n"),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "RootFunction");
        Assert.Contains(reply.Items, x => x.InsertionText == "sub");
    }

    [Fact]
    public void Analyze_PackageSubmodule_OffersSubMembers()
    {
        var service = VsService(Includes(Read));
        const string text = "import pkg.sub\nimport pkg.sub.helper\npkg.sub.";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "pkg" => new IncludeFile("/plugins/pkg/__init__.py", "def RootFunction():\n    return 1\n"),
            "pkg.sub" => new IncludeFile("/plugins/pkg/sub/__init__.py", "def SubFunction():\n    return 1\n"),
            "pkg.sub.helper" => new IncludeFile("/plugins/pkg/sub/helper.py", "def HelperFn():\n    return 1\n"),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "SubFunction");
        Assert.Contains(reply.Items, x => x.InsertionText == "helper");
    }

    [Fact]
    public void Analyze_ReboundModuleAlias_DoesNotMergeUnrelatedModules()
    {
        var service = VsService(Includes(Read));
        const string text = """
            import helper as mod
            import other as mod
            mod.
            """;
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "helper" => new IncludeFile("/plugins/helper.py", "def Filter(clip):\n    return clip\n"),
            "other" => new IncludeFile("/plugins/other.py", "def OtherFn():\n    return 1\n"),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "OtherFn");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void Analyze_ReboundModuleAlias_LeavesOtherAliasIntact()
    {
        var service = VsService(Includes(Read));
        const string text = """
            import other as o
            import helper as mod
            import other as mod
            o.
            """;
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "helper" => new IncludeFile("/plugins/helper.py", "def Filter(clip):\n    return clip\n"),
            "other" => new IncludeFile("/plugins/other.py", "def OtherFn():\n    return 1\n"),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "OtherFn");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void Analyze_PlaceholderPackage_DoesNotMergeIntoUnrelatedAlias()
    {
        var service = VsService(Includes(Read));
        const string text = "import pkg.sub\nimport other as pkg\npkg.";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "pkg.sub" => new IncludeFile("/plugins/pkg/sub/__init__.py", "def SubFn():\n    return 1\n"),
            "other" => new IncludeFile("/plugins/other.py", "def OtherFn():\n    return 1\n"),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "OtherFn");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "sub");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "SubFn");
    }

    [Fact]
    public void Analyze_ImportedClassBodyImports_DoNotLeak()
    {
        const string helper = "def Filter():\n    return 1\n";
        const string wrapper = """
            class C:
                from helper import Filter
            def Keep():
                return 1
            """;
        var service = VsService(Includes(Read));
        const string text = "import wrapper\nwrapper.";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "helper" => new IncludeFile("/plugins/helper.py", helper),
            "wrapper" => new IncludeFile("/plugins/wrapper.py", wrapper),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "Keep");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void Analyze_FromImport_ResolvesSubmoduleMembers()
    {
        var service = VsService(Includes(Read));
        const string text = "from pkg import helper\nhelper.";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "pkg" => new IncludeFile("/plugins/pkg/__init__.py", "# package\n"),
            "pkg.helper" => new IncludeFile("/plugins/pkg/helper.py",
                "def Filter(clip) -> vs.VideoNode:\n    return clip\n"),
            "." => new IncludeFile("/pkg/__init__.py", "def Filter(clip) -> vs.VideoNode:\n    return clip\n"),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void Analyze_RelativeFromImport_InfersReturnType()
    {
        var service = VsService(Includes(Read));
        const string text = "from . import Filter\nclip = Filter()\nclip.";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "pkg" => new IncludeFile("/plugins/pkg/__init__.py", "# package\n"),
            "pkg.helper" => new IncludeFile("/plugins/pkg/helper.py",
                "def Filter(clip) -> vs.VideoNode:\n    return clip\n"),
            "." => new IncludeFile("/pkg/__init__.py", "def Filter(clip) -> vs.VideoNode:\n    return clip\n"),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, Vs, documentPath: "/pkg/script.py");

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_WildcardImport_OffersPublic()
    {
        const string helper = """
            def Filter(clip) -> vs.VideoNode:
                return clip
            def _private(clip):
                return clip
            """;
        var service = VsService(Includes(Read));
        const string text = "from helper import *\nFil";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void Analyze_WildcardImport_SkipsPrivate()
    {
        const string helper = """
            def Filter(clip) -> vs.VideoNode:
                return clip
            def _private(clip):
                return clip
            """;
        var service = VsService(Includes(Read));
        const string text = "from helper import *\n_pri";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "_private");
    }

    [Fact]
    public void Analyze_WildcardImport_AllowsExplicitPrivate()
    {
        const string helper = """
            def Filter(clip) -> vs.VideoNode:
                return clip
            def _private(clip):
                return clip
            """;
        var service = VsService(Includes(Read));
        const string text = "from helper import _private\n_pri";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "_private");
    }

    [Fact]
    public void Insight_WildcardImport_ReplacesLocalFunction()
    {
        const string helper = """
            def Filter(clip) -> vs.VideoNode:
                return clip
            def _private(clip):
                return clip
            """;
        var service = VsService(Includes(Read));
        const string text = """
            def Filter(clip, radius=2):
                return clip
            from helper import *
            Filter(
            """;
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var insight = service.Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("Filter", insight.Overloads[0].Name);
        Assert.DoesNotContain("radius=2", insight.Overloads[0].Signature, StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_ImportedModuleAssignment_InvalidatesExport()
    {
        const string helper = """
            def Filter(clip) -> vs.VideoNode:
                return clip
            Filter = None
            """;
        var service = VsService(Includes(Read));
        const string text = "from helper import Filter\nFilter(";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var insight = service.Analyze(text, text.Length, Vs).Insight;

        Assert.True(insight == null || insight.Overloads.All(x => x.Name != "Filter"));
    }

    [Fact]
    public void Analyze_ImportedModuleAssignment_DoesNotInferReturnType()
    {
        const string helper = """
            def Filter(clip) -> vs.VideoNode:
                return clip
            Filter = None
            """;
        var service = VsService(Includes(Read));
        const string text = "from helper import Filter\nclip = Filter()\nclip.";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_ImportAlias_AllowsTabAroundAs()
    {
        const string helper = "def Filter(clip) -> vs.VideoNode:\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = "import helper\tas h\nh.";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void Analyze_FromImportAlias_AllowsTabBeforeName()
    {
        const string helper = "def Filter(clip) -> vs.VideoNode:\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = "from helper import Filter as\tF\nclip = F()\nclip.";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void Analyze_AviSynthIncompleteImport_ReloadsWithoutRefresh()
    {
        var reads = 0;
        var language = new AviSynthLanguage(Includes(Read));
        language.Includes.SetPath("helper", null, "/h.avsi", language.Includes.Version);
        var service = new LanguageService(language, Catalog());
        const string text = "Import(\"helper\")\nHelper(";
        IncludeFile? Read(string specifier, string? _)
        {
            reads++;
            return specifier is "helper" or "/h.avsi"
                ? new IncludeFile("/h.avsi", "function Helper(clip c) { }\n")
                : null;
        }

        var insight = service.Analyze(text, text.Length, []).Insight;

        Assert.True(reads > 0);
        Assert.NotNull(insight);
        Assert.Equal("Helper", insight.Overloads[0].Name);
    }

    [Fact]
    public void Analyze_AviSynthCachedExports_DoNotDependOnVisitOrder()
    {
        var service = new LanguageService(new AviSynthLanguage(Includes(Read)), Catalog());
        const string first = "Import(\"b.avs\")\nlast.";
        service.Analyze(first, first.Length, []);
        const string onlyA = "Import(\"a.avs\")\nlast.";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "a.avs" => new IncludeFile("/a.avs", "Import(\"b.avs\")\nfunction FromA(clip c) { return c }"),
            "b.avs" => new IncludeFile("/b.avs", "function FromB(clip c) { return c }"),
            _ => null
        };

        var reply = service.Analyze(onlyA, onlyA.Length, []);

        Assert.Contains(reply.Items, x => x.InsertionText == "FromA");
        Assert.Contains(reply.Items, x => x.InsertionText == "FromB");
    }

    [Fact]
    public void Analyze_DottedImportRoot_DoesNotOfferChild()
    {
        var service = VsService(Includes(Read));
        const string text = "import pkg\npkg.";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "pkg" => new IncludeFile("/plugins/pkg/__init__.py", "def Root():\n    return 1\n"),
            "pkg.child" => new IncludeFile("/plugins/pkg/child.py", "def Deep():\n    return 1\n"),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "Root");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "child");
    }

    [Fact]
    public void Analyze_DottedImportChild_OffersChildOnPackage()
    {
        var service = VsService(Includes(Read));
        const string text = "import pkg.child\npkg.";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "pkg" => new IncludeFile("/plugins/pkg/__init__.py", "def Root():\n    return 1\n"),
            "pkg.child" => new IncludeFile("/plugins/pkg/child.py", "def Deep():\n    return 1\n"),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "child");
    }

    [Fact]
    public void Analyze_DottedImport_DoesNotMutateOtherDocumentCache()
    {
        var service = VsService(Includes(Read));
        const string root = "import pkg\npkg.";
        service.Analyze(root, root.Length, Vs);
        const string child = "import pkg.child\npkg.";
        service.Analyze(child, child.Length, Vs);
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "pkg" => new IncludeFile("/plugins/pkg/__init__.py", "def Root():\n    return 1\n"),
            "pkg.child" => new IncludeFile("/plugins/pkg/child.py", "def Deep():\n    return 1\n"),
            _ => null
        };

        var reply = service.Analyze(root, root.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "Root");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "child");
    }

    [Fact]
    public void Analyze_AviSynthCancelledCycle_ReloadsMissingDependency()
    {
        var cts = new CancellationTokenSource();
        var service = new LanguageService(new AviSynthLanguage(Includes(Read)), Catalog());
        const string first = "Import(\"a.avs\")\n";
        try
        {
            service.Analyze(first, first.Length, [], cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        const string onlyB = "Import(\"b.avs\")\nlast.";
        IncludeFile? Read(string specifier, string? _)
        {
            if (specifier is "c.avs" or "/c.avs")
            {
                cts.Cancel();
            }
            return specifier switch
            {
                "a.avs" or "/a.avs" => new IncludeFile("/a.avs",
                    "Import(\"b.avs\")\nImport(\"c.avs\")\nfunction FromA(clip c) { return c }"),
                "b.avs" or "/b.avs" => new IncludeFile("/b.avs",
                    "Import(\"a.avs\")\nfunction FromB(clip c) { return c }"),
                "c.avs" or "/c.avs" => new IncludeFile("/c.avs", "function FromC(clip c) { return c }"),
                _ => null
            };
        }

        var reply = service.Analyze(onlyB, onlyB.Length, []);

        Assert.Contains(reply.Items, x => x.InsertionText == "FromA");
        Assert.Contains(reply.Items, x => x.InsertionText == "FromB");
    }

    [Fact]
    public void Analyze_VapourSynthCancelledCycle_ReloadsMissingDependency()
    {
        var cts = new CancellationTokenSource();
        var service = VsService(Includes(Read));
        const string first = "import a\n";
        try
        {
            service.Analyze(first, first.Length, Vs, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        const string onlyB = "import b\nb.a.A(";
        IncludeFile? Read(string specifier, string? _)
        {
            if (specifier is "c" or "/c.py")
            {
                cts.Cancel();
            }
            return specifier switch
            {
                "a" or "/a.py" => new IncludeFile("/a.py",
                    "import b\nimport c\ndef A():\n    return 1\n"),
                "b" or "/b.py" => new IncludeFile("/b.py", "import a\n"),
                "c" or "/c.py" => new IncludeFile("/c.py", "def C():\n    return 1\n"),
                _ => null
            };
        }

        var insight = service.Analyze(onlyB, onlyB.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("A", insight.Overloads[0].Name);
    }

    [Fact]
    public void Analyze_CyclicImport_SeesDeclarationsAlreadyEncountered()
    {
        var service = VsService(Includes(Read));
        const string text = "import a\nimport b\nb.";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "a" or "/a.py" => new IncludeFile("/a.py", "def First():\n    return 1\nimport b\n"),
            "b" or "/b.py" => new IncludeFile("/b.py", "from a import First\n"),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "First");
    }

    [Fact]
    public void Analyze_CyclicImport_SeesDeclarationsThroughB()
    {
        var service = VsService(Includes(Read));
        const string text = "import b\nb.";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "a" or "/a.py" => new IncludeFile("/a.py", "def First():\n    return 1\nimport b\n"),
            "b" or "/b.py" => new IncludeFile("/b.py", "from a import First\n"),
            _ => null
        };

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "First");
    }

    [Fact]
    public void Analyze_WildcardSelfImport_DoesNotThrow()
    {
        const string helper = """
            def Filter():
                return 1
            from helper import *
            """;
        var service = VsService(Includes(Read));
        const string text = "from helper import Filter\nFilter(";
        IncludeFile? Read(string specifier, string? _) =>
            specifier is "helper" or "/helper.py"
                ? new IncludeFile("/helper.py", helper)
                : null;

        var insight = service.Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("Filter", insight.Overloads[0].Name);
    }

    [Fact]
    public void Analyze_WorkLimit_DoesNotReadBeyondBudget()
    {
        var reads = 0;
        var script = "";
        for (var i = 0; i < 1000; i++)
        {
            script += "import m" + i + "\n";
        }

        var service = VsService(Includes(Read));
        IncludeFile? Read(string specifier, string? _)
        {
            reads++;
            return new IncludeFile("/plugins/" + specifier + ".py", "def F():\n    pass\n");
        }

        service.Analyze(script, script.Length, []);

        Assert.Equal(IncludeCache.ImportWorkLimit, reads);
    }

    [Fact]
    public void Analyze_AviSynthWorkLimit_DoesNotReadBeyondBudget()
    {
        var reads = 0;
        var script = "";
        for (var i = 0; i < 1000; i++)
        {
            script += "Import(\"m" + i + ".avsi\")\n";
        }

        var service = new LanguageService(new AviSynthLanguage(Includes(Read)), Catalog());
        IncludeFile? Read(string specifier, string? _)
        {
            reads++;
            return new IncludeFile("/plugins/" + specifier, "function F(clip c) { c }\n");
        }

        service.Analyze(script, script.Length, []);

        Assert.Equal(IncludeCache.ImportWorkLimit, reads);
    }

    [Fact]
    public void Analyze_WorkLimit_DoesNotCachePartialPackage()
    {
        var script = "";
        for (var i = 0; i < IncludeCache.ImportWorkLimit - 1; i++)
        {
            script += "import m" + i + "\n";
        }

        script += "import pkg\n";
        var service = VsService(Includes(Read));
        service.Analyze(script, script.Length, []);
        const string small = "import pkg\npkg.";
        IncludeFile? Read(string specifier, string? _) => specifier switch
        {
            "pkg" => new IncludeFile("/plugins/pkg.py", "from pkg.sub import Foo\ndef Own():\n    pass\n"),
            "pkg.sub" => new IncludeFile("/plugins/pkg.sub.py", "def Foo():\n    pass\n"),
            _ => new IncludeFile("/plugins/" + specifier + ".py", "def F():\n    pass\n")
        };

        var reply = service.Analyze(small, small.Length, []);

        Assert.Contains(reply.Items, x => x.InsertionText == "Foo");
    }
}
