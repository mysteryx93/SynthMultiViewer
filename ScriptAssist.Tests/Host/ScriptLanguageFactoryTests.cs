using System.Diagnostics.CodeAnalysis;
using HanumanInstitute.ScriptAssist.VapourSynth;
using Moq;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests.Host;

using static AssistHarness;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class ScriptLanguageFactoryTests : TestsBase
{
    public ScriptLanguageFactoryTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void HostConstructor_RegistersVapourSynthAndAviSynth()
    {
        var factory = Languages();

        var vs = factory.Create(ScriptLanguageFactory.VapourSynth);
        var avs = factory.Create(ScriptLanguageFactory.AviSynth);
        var missing = factory.Create("missing");

        Assert.True(factory.IsEnabled);
        Assert.NotNull(vs);
        Assert.NotNull(avs);
        Assert.Null(missing);
    }

    [Fact]
    public async Task IsEnabled_Disabled_SkipsEnumerationUntilEnabled()
    {
        var native = InitMock<IVapourSynthNativeCatalog>(n => n.Setup(x => x.Read()).Returns([]));
        var factory = Languages(native.Object);
        factory.IsEnabled = false;
        factory.Configure(ScriptLanguageFactory.VapourSynth, "a");
        var disabled = factory.Create(ScriptLanguageFactory.VapourSynth);
        factory.IsEnabled = true;
        factory.Configure(ScriptLanguageFactory.VapourSynth, "a");

        await factory.Create(ScriptLanguageFactory.VapourSynth)!.GetAsync("im", 2, CancellationToken.None);

        Assert.Null(disabled);
        native.Verify(n => n.Read(), Times.Once);
    }

    [Fact]
    public void Create_UnknownLanguage_ReturnsNull()
    {
        var factory = Languages();

        var service = factory.Create("missing");

        Assert.Null(service);
    }

    [Fact]
    public void Configure_UnknownLanguage_DoesNotThrow()
    {
        var factory = Languages();

        var exception = Record.Exception(() => factory.Configure("missing", "key"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task GetAsync_EnumerationFailure_IsCached()
    {
        const string text = "im";
        var source = InitMock<ISymbolSource>(s => s.Setup(x => x.Enumerate()).Throws<InvalidOperationException>());
        var cache = new CatalogCache(source.Object);
        cache.Refresh("path1");
        var service = new LanguageService(new VapourSynthLanguage(), cache);

        for (var i = 0; i < 5; i++)
        {
            Assert.Contains((await service.GetAsync(text, 2, CancellationToken.None)).Items,
                x => x.InsertionText == "import");
            cache.Refresh("path1");
        }

        source.Verify(s => s.Enumerate(), Times.Once);
    }

    [Fact]
    public async Task Refresh_SameKey_EnumeratesOnceUntilForced()
    {
        var source = InitMock<ISymbolSource>(s => s.Setup(x => x.Enumerate()).Returns([]));
        var cache = new CatalogCache(source.Object);
        cache.Refresh("path1");
        await cache.GetAsync(CancellationToken.None);
        cache.Refresh("path1");
        await cache.GetAsync(CancellationToken.None);

        cache.Refresh("path2");
        await cache.GetAsync(CancellationToken.None);

        cache.Refresh("path2", true);
        await cache.GetAsync(CancellationToken.None);

        source.Verify(s => s.Enumerate(), Times.Exactly(3));
    }

    [Fact]
    public async Task Refresh_ForcedFailure_KeepsLastCatalog()
    {
        var source = InitMock<ISymbolSource>(s => s.SetupSequence(x => x.Enumerate())
            .Returns([new Symbol("core.std.Crop", ["clip:vnode"])])
            .Throws<InvalidOperationException>());
        var cache = new CatalogCache(source.Object);
        cache.Refresh("path1");

        var first = await cache.GetAsync(CancellationToken.None);
        cache.Refresh("path1", true);
        var second = await cache.GetAsync(CancellationToken.None);

        Assert.Equal("core.std.Crop", Assert.Single(first).Name);
        Assert.Equal("core.std.Crop", Assert.Single(second).Name);
        source.Verify(s => s.Enumerate(), Times.Exactly(2));
    }

    [Fact]
    public async Task Refresh_ReusedCatalogList_SnapshotsNewSymbols()
    {
        const string text = "core.std.";
        var functions = new List<VapourSynthFunction>
        {
            new("std", "Before", "clip:vnode", "clip:vnode;")
        };
        var native = InitMock<IVapourSynthNativeCatalog>(n => n.Setup(x => x.Read()).Returns(() => functions.ToArray()));
        var factory = Languages(native.Object);
        var service = factory.Create(ScriptLanguageFactory.VapourSynth)!;
        var first = await service.GetAsync(text, text.Length, CancellationToken.None);
        Assert.Contains(first.Items, x => x.InsertionText == "Before");
        functions.Clear();
        functions.Add(new("std", "After", "clip:vnode", "clip:vnode;"));

        factory.Refresh();

        var second = await service.GetAsync(text, text.Length, CancellationToken.None);
        Assert.Contains(second.Items, x => x.InsertionText == "After");
        Assert.DoesNotContain(second.Items, x => x.InsertionText == "Before");
    }

    [Fact]
    public void Configure_NewKey_InvalidatesIncludeCache()
    {
        const string text = "import helper as h\nh.";
        var current = "def Old():\n    return 1\n";
        var includes = Includes((_, _) => new IncludeFile("/plugins/helper.py", current));
        var factory = Languages(vapoursynthIncludes: includes);
        var service = (LanguageService)factory.Create(ScriptLanguageFactory.VapourSynth)!;
        var native = Array.Empty<Symbol>();
        Assert.Contains(service.Analyze(text, text.Length, native).Items, x => x.InsertionText == "Old");
        current = "def New():\n    return 1\n";

        factory.Configure(ScriptLanguageFactory.VapourSynth, "other");

        var reply = service.Analyze(text, text.Length, native);
        Assert.Contains(reply.Items, x => x.InsertionText == "New");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Old");
    }

    [Fact]
    public async Task Refresh_WhenDisabled_EnumeratesCatalog()
    {
        var native = InitMock<IVapourSynthNativeCatalog>(n => n.Setup(x => x.Read()).Returns([]));
        var factory = Languages(native.Object);
        factory.IsEnabled = false;

        factory.Refresh();
        factory.IsEnabled = true;
        await factory.Create(ScriptLanguageFactory.VapourSynth)!.GetAsync("im", 2, CancellationToken.None);

        native.Verify(n => n.Read(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Refresh_ThenConfigureSameKey_DoesNotEnumerateAgain()
    {
        const string text = "im";
        var native = InitMock<IVapourSynthNativeCatalog>(n => n.Setup(x => x.Read()).Returns([]));
        var factory = Languages(native.Object);
        factory.Configure(ScriptLanguageFactory.VapourSynth, "A");
        var service = factory.Create(ScriptLanguageFactory.VapourSynth)!;
        await service.GetAsync(text, text.Length, CancellationToken.None);
        native.Verify(n => n.Read(), Times.Once);

        factory.Refresh();
        await service.GetAsync(text, text.Length, CancellationToken.None);
        factory.Configure(ScriptLanguageFactory.VapourSynth, "A");
        await service.GetAsync(text, text.Length, CancellationToken.None);

        native.Verify(n => n.Read(), Times.Exactly(2));
    }
}
