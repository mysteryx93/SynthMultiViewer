using System.Diagnostics.CodeAnalysis;
using HanumanInstitute.ScriptAssist.AviSynth;
using HanumanInstitute.ScriptAssist.VapourSynth;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests.Imports;

using static AssistHarness;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class ScriptIncludesTests
{
    [Theory]
    [InlineData("int [left]", "left")]
    [InlineData("int \"width\"", "width")]
    [InlineData("clip c", "c")]
    [InlineData("clip Input", "Input")]
    [InlineData("clip", null)]
    [InlineData("int", null)]
    [InlineData("float", null)]
    [InlineData("function", null)]
    [InlineData("any", null)]
    [InlineData("*args", null)]
    [InlineData("", null)]
    public void Parse_AviSynthParameter_ReturnsName(string parameter, string? expected)
    {
        var name = ParameterNames.OfAviSynth(parameter);

        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData("*", false, "Separator", true)]
    [InlineData("/", false, "Separator", false)]
    [InlineData("*args", false, "Varargs", true)]
    [InlineData("**kwargs", false, "Kwargs", false)]
    [InlineData("radius=2", false, "Positional", false)]
    [InlineData("radius=2", true, "KeywordOnly", true)]
    public void Parse_PythonParameter_ClassifiesKind(string parameter, bool keywordOnly, string expected, bool after)
    {
        var flag = keywordOnly;

        var kind = ParameterNames.Classify(parameter, ref flag);

        Assert.Equal(expected, kind.ToString());
        Assert.Equal(after, flag);
    }

    [Theory]
    [InlineData("left:int:opt", "left")]
    [InlineData("radius=1", "radius")]
    [InlineData("radius: int = 1", "radius")]
    [InlineData("radius: Optional[int] = None", "radius")]
    [InlineData("planes=[0, 1]", "planes")]
    [InlineData("Preset='Slow'", "Preset")]
    [InlineData("clip", "clip")]
    [InlineData("lambda:float:opt", "lambda_")]
    [InlineData("from:int:opt", "from_")]
    [InlineData("*args", null)]
    [InlineData("", null)]
    public void Parse_PythonParameter_ReturnsName(string parameter, string? expected)
    {
        var name = ParameterNames.OfPython(parameter);

        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData("left:int:opt", "int")]
    [InlineData("clip:vnode:opt", "vnode")]
    [InlineData("format:int:opt", "int")]
    [InlineData("radius: int = 1", "int")]
    [InlineData("clip: vs.VideoNode", "vs.VideoNode")]
    [InlineData("radius: Optional[int] = None", "Optional[int]")]
    [InlineData("offsets:int[]", "int[]")]
    [InlineData("planes=[0, 1]", null)]
    [InlineData("clip", null)]
    public void Parse_PythonParameter_ReturnsType(string parameter, string? expected)
    {
        var type = ParameterNames.PythonType(parameter);

        Assert.Equal(expected, type);
    }

    [Theory]
    [InlineData("int [height]", "int")]
    [InlineData("int [blksizev]", "int")]
    [InlineData("clip c", "clip")]
    [InlineData("int \"width\"", "int")]
    [InlineData("int* [planes]", "int")]
    [InlineData("string \"Preset\"", "string")]
    [InlineData("clip", "clip")]
    [InlineData("*args", null)]
    public void Parse_AviSynthParameter_ReturnsType(string parameter, string? expected)
    {
        var type = ParameterNames.AviSynthType(parameter);

        Assert.Equal(expected, type);
    }

    [Fact]
    public void Parse_NestedCommas_KeepsParts()
    {
        var parts = ParameterNames.Split("clip c, int [left], val \"a, b\", radius: int = (1, 2)");

        Assert.Equal(["clip c", "int [left]", "val \"a, b\"", "radius: int = (1, 2)"], parts);
    }

    [Fact]
    public void Parse_QuotedAndTypedHeaders_ReadsParameters()
    {
        const string text = """
            # function Hidden() { }
            function QTGMC(clip Input, int "TR0", string "Preset") {
                return Input
            }
            """;

        var symbols = AviSynthFunctions.Parse(text, new AviSynthLanguage().Lexer);

        var qtgmc = Assert.Single(symbols);
        Assert.Equal("QTGMC", qtgmc.Name);
        Assert.Equal(["clip Input", "int \"TR0\"", "string \"Preset\""], qtgmc.Parameters!);
        Assert.Equal("TR0", ParameterNames.OfAviSynth(qtgmc.Parameters![1]));
        Assert.Equal("Preset", ParameterNames.OfAviSynth(qtgmc.Parameters[2]));
        Assert.True(AviSynthTypes.TakesClip(qtgmc));
    }

    [Fact]
    public void Parse_Union_PrefersParsedWhenNativeHasNoNames()
    {
        var native = new[]
        {
            new Symbol("QTGMC", ["clip"]),
            new Symbol("Crop", ["clip", "int [left]", "int [top]"])
        };
        var parsed = new[]
        {
            new Symbol("QTGMC", ["clip Input", "int \"TR0\""]),
            new Symbol("Crop", ["clip c"])
        };

        var merged = AviSynthFunctions.UnionByName(native, parsed);

        var qtgmc = Assert.Single(merged, x => x.Name == "QTGMC");
        Assert.Equal(["clip Input", "int \"TR0\""], qtgmc.Parameters!);
        var crop = Assert.Single(merged, x => x.Name == "Crop");
        Assert.Equal(["clip", "int [left]", "int [top]"], crop.Parameters!);
    }

    [Fact]
    public void Parse_Union_KeepsNativeOverloads()
    {
        var native = new[]
        {
            new Symbol("Foo", ["clip", "int [a]"]),
            new Symbol("Foo", ["clip", "float [a]"])
        };

        var merged = AviSynthFunctions.UnionByName(native, []);

        Assert.Equal(2, merged.Count(x => x.Name == "Foo"));
    }

    [Fact]
    public void Parse_Union_EnrichesMatchingAndKeepsUnmatched()
    {
        var native = new[]
        {
            new Symbol("Foo", ["clip"]),
            new Symbol("Foo", ["int"])
        };
        var parsed = new[]
        {
            new Symbol("Foo", ["clip c"])
        };

        var merged = AviSynthFunctions.UnionByName(native, parsed);

        Assert.Equal(2, merged.Count(x => x.Name == "Foo"));
        Assert.Contains(merged, x => x is { Name: "Foo", Parameters: ["clip c"] });
        Assert.Contains(merged, x => x is { Name: "Foo", Parameters: ["int"] });
    }

    [Fact]
    public void Parse_Union_KeepsRepeatingNativeWhenParsedDropsModifier()
    {
        var native = new[] { new Symbol("Foo", ["clip", "int+"]) };
        var parsed = new[] { new Symbol("Foo", ["clip c", "int count"]) };

        var merged = AviSynthFunctions.UnionByName(native, parsed);

        var foo = Assert.Single(merged, x => x.Name == "Foo");
        Assert.Equal(["clip", "int+"], foo.Parameters!);
    }

    [Fact]
    public void Parse_Union_KeepsIncompatibleSingleNativeSignature()
    {
        var native = new[] { new Symbol("Foo", ["int"]) };
        var parsed = new[] { new Symbol("Foo", ["clip c"]) };

        var merged = AviSynthFunctions.UnionByName(native, parsed);

        var foo = Assert.Single(merged, x => x.Name == "Foo");
        Assert.Equal(["int"], foo.Parameters!);
    }

    [Fact]
    public void Parse_NestedDefs_IgnoresClassMethods()
    {
        const string text = """
            def QTGMC(clip, Preset='Slow'):
                return clip
            class Wrapper:
                def method(self, clip):
                    return clip
            """;

        var symbols = VapourSynthFunctions.Parse(text, new VapourSynthLanguage().Lexer);

        var qtgmc = Assert.Single(symbols);
        Assert.Equal("QTGMC", qtgmc.Name);
        Assert.Equal(["clip", "Preset='Slow'"], qtgmc.Parameters!);
    }

    [Fact]
    public void Parse_AsyncDef_OmitsReturnType()
    {
        const string text = """
            async def load(clip) -> vs.VideoNode:
                return clip
            def sync(clip) -> vs.VideoNode:
                return clip
            """;

        var symbols = VapourSynthFunctions.Parse(text, new VapourSynthLanguage().Lexer);

        var load = Assert.Single(symbols, x => x.Name == "load");
        var sync = Assert.Single(symbols, x => x.Name == "sync");
        Assert.Null(load.ReturnType);
        Assert.Equal("vnode", sync.ReturnType);
    }

    [Fact]
    public void Parse_PythonIncludePaths_PrefersBufferThenPluginRoots()
    {
        var files = new FakeFileSystemService();
        var fromPath = files.Path.Combine("/scripts", "job.vpy");
        var roots = new[] { "/plugins" };

        var paths = IncludePaths.PythonModule("havsfunc", fromPath, roots, files.Path).ToArray();

        Assert.Contains(files.Path.Combine("/scripts", "havsfunc.py"), paths);
        Assert.Contains(files.Path.Combine("/scripts", "havsfunc.pyi"), paths);
        Assert.Contains(files.Path.Combine("/scripts", "havsfunc", "__init__.py"), paths);
        Assert.Contains(files.Path.Combine("/scripts", "havsfunc", "__init__.pyi"), paths);
        Assert.Contains(files.Path.Combine("/plugins", "havsfunc.py"), paths);
        Assert.Contains(files.Path.Combine("/plugins", "havsfunc.pyi"), paths);
        Assert.Contains(files.Path.Combine("/plugins", "havsfunc", "__init__.py"), paths);
        Assert.Contains(files.Path.Combine("/plugins", "havsfunc", "__init__.pyi"), paths);
    }

    [Fact]
    public void Parse_PythonRelativeInclude_UsesFileDirectory()
    {
        var files = new FakeFileSystemService();
        var fromPath = files.Path.Combine("/plugins", "havsfunc", "__init__.py");

        var paths = IncludePaths.PythonModule(".qtgmc", fromPath, ["/unused"], files.Path).ToArray();

        Assert.Equal(files.Path.Combine("/plugins", "havsfunc", "qtgmc.py"), paths[0]);
        Assert.Equal(files.Path.Combine("/plugins", "havsfunc", "qtgmc.pyi"), paths[1]);
        Assert.Equal(files.Path.Combine("/plugins", "havsfunc", "qtgmc", "__init__.py"), paths[2]);
        Assert.Equal(files.Path.Combine("/plugins", "havsfunc", "qtgmc", "__init__.pyi"), paths[3]);
        Assert.DoesNotContain(paths, path => path.Contains("unused", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_PythonDotOnlyRelative_UsesPackageInit()
    {
        var files = new FakeFileSystemService();
        var fromPath = files.Path.Combine("/project", "pkg", "filter.py");

        var paths = IncludePaths.PythonModule(".", fromPath, ["/unused"], files.Path).ToArray();

        Assert.Equal(
            [
                files.Path.Combine("/project", "pkg", "__init__.py"),
                files.Path.Combine("/project", "pkg", "__init__.pyi")
            ], paths);
    }

    [Fact]
    public void Parse_AviSynthIncludePaths_UsesSpecifierNextToDocument()
    {
        var files = new FakeFileSystemService();
        var fromPath = files.Path.Combine("/scripts", "job.avs");

        var paths = IncludePaths.AviSynth("helpers.avsi", fromPath, ["/plugins"], files.Path).ToArray();

        Assert.Equal(files.Path.Combine("/scripts", "helpers.avsi"), paths[0]);
        Assert.Contains(files.Path.Combine("/plugins", "helpers.avsi"), paths);
    }

    [Theory]
    [InlineData("ci[left]i[top]i", "clip,int,int [left],int [top]")]
    [InlineData("c[planes]i*", "clip,int* [planes]")]
    [InlineData("c[items]a", "clip,array [items]")]
    [InlineData("", "")]
    public void Parse_AviSynthParameterFormat_ExpandsTokens(string format, string expected)
    {
        var parsed = AviSynthParameters.Parse(format)!;

        Assert.Equal(expected, string.Join(",", parsed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("c[broken")]
    [InlineData("z")]
    public void Parse_UnknownAviSynthParameters_StayUnknown(string? format)
    {
        var parsed = AviSynthParameters.Parse(format);

        Assert.Null(parsed);
    }

    [Fact]
    public void Parse_LoadImports_ReturnsImportedFunctions()
    {
        const string avsi = "function Helper(clip c) { }\n";
        var lexer = new AviSynthLanguage().Lexer;
        IncludeFile? Read(string specifier, string? _) => specifier is "helper.avsi" or "/h.avsi"
            ? new IncludeFile("/h.avsi", avsi)
            : null;

        var symbols = AviSynthFunctions.LoadImports("Import(\"helper.avsi\")", null, Includes(Read), lexer);

        Assert.Contains(symbols, x => x.Name.Equals("Helper", StringComparison.OrdinalIgnoreCase));
    }
}
