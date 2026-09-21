using HanumanInstitute.ScriptAssist.AviSynth;
using HanumanInstitute.ScriptAssist.VapourSynth;
using Moq;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests.Host;

public class CatalogSourceTests : TestsBase
{
    public CatalogSourceTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Enumerate_VapourSynthDump_MapsCoreNameAndTitle()
    {
        var native = InitMock<IVapourSynthNativeCatalog>(n => n.Setup(x => x.Read()).Returns(
        [
            new VapourSynthFunction("bm3d", "BM3D", "clip:vnode", "clip:vnode;", "VapourSynth BM3D")
        ]));

        var symbols = new VapourSynthSymbolSource(native.Object).Enumerate();

        Assert.Contains(symbols, s => s.Kind == SymbolKind.Namespace && s.Name == "core.bm3d"
            && s.Title == "VapourSynth BM3D");
        var function = Assert.Single(symbols, s => s.Kind == SymbolKind.Function);
        Assert.Equal("core.bm3d.BM3D", function.Name);
        Assert.Equal("VapourSynth BM3D", function.Title);
    }

    [Fact]
    public void Enumerate_AviSynthDump_SetsGroupAndMergesScripts()
    {
        var native = InitMock<IAviSynthNativeCatalog>(n => n.Setup(x => x.Read()).Returns(
        [
            new AviSynthFilter("Crop", "c[left]i", "InternalFunctions"),
            new AviSynthFilter("AutoloadOnly", "c", "UserFunctions")
        ]));
        var folders = InitMock<IScriptDirectory>(d =>
        {
            d.Setup(x => x.Roots()).Returns(["/plugins"]);
            d.Setup(x => x.Files("/plugins", It.IsAny<IReadOnlyList<string>>()))
                .Returns(["/plugins/helpers.avsi"]);
            d.Setup(x => x.TryRead("/plugins/helpers.avsi"))
                .Returns("function Helper(clip c) { c }\n");
        });
        var includes = InitMock<IIncludeSource>(s =>
            s.Setup(x => x.Read(It.IsAny<string>(), It.IsAny<string?>())).Returns((IncludeFile?)null));

        var symbols = new AviSynthSymbolSource(native.Object, folders.Object, includes.Object)
            .Enumerate();

        Assert.Equal("Internal", Assert.Single(symbols, s => s.Name == "Crop").Group);
        Assert.Equal("User", Assert.Single(symbols, s => s.Name == "AutoloadOnly").Group);
        Assert.Contains(symbols, s => s.Name == "Helper");
    }
}
