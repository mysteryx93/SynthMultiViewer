using System.Diagnostics.CodeAnalysis;
using HanumanInstitute.ScriptAssist.VapourSynth;
using Xunit;

// ReSharper disable AccessToModifiedClosure

namespace HanumanInstitute.ScriptAssist.Tests.Engine;

using static AssistHarness;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class LanguageServiceTests
{
    [Fact]
    public void Invalidate_IncludeSnapshots_RefreshesMembers()
    {
        const string text = "import helper as h\nh.";
        var current = "def Old():\n    return 1\n";
        var catalog = Catalog();
        var service = new LanguageService(new VapourSynthLanguage(Includes(Read)), catalog);
        var native = Array.Empty<Symbol>();
        Assert.Contains(service.Analyze(text, text.Length, native).Items, x => x.InsertionText == "Old");
        current = "def New():\n    return 1\n";
        Assert.Contains(service.Analyze(text, text.Length, native).Items, x => x.InsertionText == "Old");
        IncludeFile? Read(string specifier, string? _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", current) : null;

        service.Invalidate();

        var refreshed = service.Analyze(text, text.Length, native);
        Assert.Contains(refreshed.Items, x => x.InsertionText == "New");
        Assert.DoesNotContain(refreshed.Items, x => x.InsertionText == "Old");
    }

    [Fact]
    public async Task Invalidate_StaleInFlightSnapshot_DoesNotPublish()
    {
        const string text = "import helper as h\nh.";
        var started = new ManualResetEventSlim(false);
        var proceed = new ManualResetEventSlim(false);
        var current = "def Old():\n    return 1\n";
        var service = new LanguageService(new VapourSynthLanguage(Includes(Read)), Catalog());
        var native = Array.Empty<Symbol>();
        var first = Task.Run(() => service.Analyze(text, text.Length, native));
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        current = "def New():\n    return 1\n";
        IncludeFile? Read(string specifier, string? _)
        {
            if (specifier != "helper")
            {
                return null;
            }
            var file = new IncludeFile("/plugins/helper.py", current);
            started.Set();
            proceed.Wait();
            return file;
        }

        service.Invalidate();
        proceed.Set();
        try
        {
            await first;
        }
        catch (OperationCanceledException)
        {
        }

        var next = service.Analyze(text, text.Length, native);
        Assert.Contains(next.Items, x => x.InsertionText == "New");
        Assert.DoesNotContain(next.Items, x => x.InsertionText == "Old");
    }

    [Fact]
    public void Refresh_RepeatedFromImport_ReadsHelperOnce()
    {
        var reads = 0;
        var helper = string.Concat(Enumerable.Range(0, 30)
            .Select(i => $"def Filter{i}(clip) -> vs.VideoNode:\n    return clip\n"));
        var service = VsService(Includes(Read));
        var text = string.Concat(Enumerable.Range(0, 30).Select(i => $"from helper import Filter{i}\n")) +
            "core.std.Crop(";
        IncludeFile? Read(string specifier, string? _)
        {
            reads++;
            return specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;
        }

        service.Analyze(text, text.Length, Vs);

        Assert.Equal(1, reads);
    }

    [Fact]
    public void Refresh_RepeatedFromImport_DoesNotRereadUntilInvalidate()
    {
        var reads = 0;
        var helper = string.Concat(Enumerable.Range(0, 30)
            .Select(i => $"def Filter{i}(clip) -> vs.VideoNode:\n    return clip\n"));
        var service = VsService(Includes(Read));
        var text = string.Concat(Enumerable.Range(0, 30).Select(i => $"from helper import Filter{i}\n")) +
            "core.std.Crop(";
        service.Analyze(text, text.Length, Vs);
        reads = 0;
        IncludeFile? Read(string specifier, string? _)
        {
            reads++;
            return specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;
        }

        service.Analyze(text + "\n", text.Length, Vs);

        Assert.Equal(0, reads);
    }

    [Fact]
    public void Refresh_RepeatedFromImport_RereadsAfterInvalidate()
    {
        var reads = 0;
        var helper = string.Concat(Enumerable.Range(0, 30)
            .Select(i => $"def Filter{i}(clip) -> vs.VideoNode:\n    return clip\n"));
        var service = VsService(Includes(Read));
        var text = string.Concat(Enumerable.Range(0, 30).Select(i => $"from helper import Filter{i}\n")) +
            "core.std.Crop(";
        service.Analyze(text, text.Length, Vs);
        reads = 0;
        service.Analyze(text + "\n", text.Length, Vs);
        service.Invalidate();
        IncludeFile? Read(string specifier, string? _)
        {
            reads++;
            return specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;
        }

        service.Analyze(text, text.Length, Vs);

        Assert.Equal(1, reads);
    }

    [Fact]
    public void GetAsync_TwoDocuments_KeepsSnapshotCache()
    {
        const string a = "def one():\n    pass\ncore.ns.F0(";
        const string b = "def two():\n    pass\ncore.ns.F0(";
        var catalog = Enumerable.Range(0, 40)
            .Select(i => new Symbol("core.ns.F" + i, ["clip:vnode"], ReturnType: "clip:vnode;"))
            .ToArray();
        var language = new CountingLanguage(new VapourSynthLanguage());
        var service = new LanguageService(language, new CatalogCache(Symbols(catalog)));
        Assert.NotNull(service.Analyze(a, a.Length, catalog).Insight);
        Assert.NotNull(service.Analyze(b, b.Length, catalog).Insight);
        var binds = language.Binds;
        Assert.Equal(2, binds);

        var secondA = service.Analyze(a, a.Length, catalog);
        var secondB = service.Analyze(b, b.Length, catalog);

        Assert.NotNull(secondA.Insight);
        Assert.NotNull(secondB.Insight);
        Assert.Equal(binds, language.Binds);
    }

    [Fact]
    public async Task GetAsync_ParallelBinds_SurvivesIncludeCache()
    {
        const string helper = "def Filter(clip):\n    return clip\n";
        const string text = "from helper import Filter\nFilter(";
        var service = VsService(Includes(Read));
        var tasks = Enumerable.Range(0, 24)
            .Select(_ => Task.Run(() => service.Analyze(text, text.Length, Vs)));
        IncludeFile? Read(string specifier, string? _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", helper)
            : null;

        var results = await Task.WhenAll(tasks);

        Assert.All(results, reply =>
        {
            Assert.NotNull(reply.Insight);
            Assert.Equal("Filter", reply.Insight.Overloads[0].Name);
        });
    }

    [Fact]
    public async Task GetAsync_ParallelSameSnapshot_BindsOnce()
    {
        const string text = "core.std.BlankClip(";
        var started = new ManualResetEventSlim(false);
        var proceed = new ManualResetEventSlim(false);
        var catalog = new[] { new Symbol("core.std.BlankClip", ["clip:vnode"], ReturnType: "clip:vnode;") };
        var language = new CountingLanguage(new VapourSynthLanguage(), started, proceed);
        var service = new LanguageService(language, new CatalogCache(Symbols(catalog)));
        var first = Task.Run(() => service.Analyze(text, text.Length, catalog));
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        var second = Task.Run(() => service.Analyze(text, text.Length, catalog));
        var duplicated = SpinWait.SpinUntil(() => Volatile.Read(ref language.Binds) > 1, 250);

        proceed.Set();
        var results = await Task.WhenAll(first, second);

        Assert.False(duplicated);
        Assert.Equal(1, language.Binds);
        Assert.All(results, reply => Assert.NotNull(reply.Insight));
    }

    [Fact]
    public async Task GetAsync_CancelledFirstCaller_DoesNotWaitForBuild()
    {
        const string text = "import helper as h\nh.";
        var started = new ManualResetEventSlim(false);
        var proceed = new ManualResetEventSlim(false);
        var service = new LanguageService(new VapourSynthLanguage(Includes(Read)), Catalog());
        var native = Array.Empty<Symbol>();
        using var cts = new CancellationTokenSource();
        var first = Task.Run(() => service.Analyze(text, text.Length, native, cts.Token));
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        cts.Cancel();
        IncludeFile? Read(string specifier, string? _)
        {
            if (specifier != "helper")
            {
                return null;
            }

            started.Set();
            proceed.Wait();
            return new IncludeFile("/plugins/helper.py", "def Old():\n    return 1\n");
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        proceed.Set();
    }

    [Fact]
    public async Task Invalidate_OverlappingRequest_DoesNotReuseInflightSnapshot()
    {
        const string text = "import helper as h\nh.";
        var started = new ManualResetEventSlim(false);
        var proceed = new ManualResetEventSlim(false);
        var current = "def Old():\n    return 1\n";
        var reads = 0;
        var service = new LanguageService(new VapourSynthLanguage(Includes(Read)), Catalog());
        var native = Array.Empty<Symbol>();
        var first = Task.Run(() => service.Analyze(text, text.Length, native));
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        current = "def New():\n    return 1\n";
        IncludeFile? Read(string specifier, string? _)
        {
            if (specifier != "helper")
            {
                return null;
            }

            Interlocked.Increment(ref reads);
            started.Set();
            proceed.Wait();
            return new IncludeFile("/plugins/helper.py", current);
        }

        service.Invalidate();
        var second = Task.Run(() => service.Analyze(text, text.Length, native));
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref reads) >= 2, 2000));
        proceed.Set();
        var later = await second;
        try
        {
            await first;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Contains(later.Items, x => x.InsertionText == "New");
        Assert.DoesNotContain(later.Items, x => x.InsertionText == "Old");
    }

    [Fact]
    public void GetAsync_NinthDocument_EvictsOldestSnapshot()
    {
        var catalog = new[] { new Symbol("core.std.BlankClip", ["clip:vnode"], ReturnType: "clip:vnode;") };
        var language = new CountingLanguage(new VapourSynthLanguage());
        var service = new LanguageService(language, new CatalogCache(Symbols(catalog)));
        for (var i = 0; i < 9; i++)
        {
            var text = "clip" + i + " = core.std.BlankClip()\nclip" + i + ".";
            service.Analyze(text, text.Length, catalog, documentPath: "/doc" + i + ".vpy");
        }
        const string first = "clip0 = core.std.BlankClip()\nclip0.";
        var binds = language.Binds;

        service.Analyze(first, first.Length, catalog, documentPath: "/doc0.vpy");

        Assert.Equal(9, binds);
        Assert.Equal(10, language.Binds);
    }

    [Fact]
    public void Bind_ImportedFunctions_RetainedBytesIsStable()
    {
        const string helper = "def Filter(clip):\n    return clip\n";
        const string text = "from helper import Filter\nFilter(";
        var language = new VapourSynthLanguage(Includes(Read));
        IncludeFile? Read(string specifier, string? _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", helper)
            : null;
        var bindings = language.Bind(text, Vs, CancellationToken.None, "/script.vpy");

        var first = bindings.RetainedBytes();
        var second = bindings.RetainedBytes();

        Assert.True(first > 0);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Bind_ScopeViews_IncreaseRetainedBytes()
    {
        var names = string.Concat(Enumerable.Range(0, 200).Select(i => "g" + i + " = 1\n"));
        var text = names + "def a():\n    x = 1\n\ndef b():\n    y = 1\n\ndef c():\n    z = 1\n\ndef d():\n    w = 1\n";
        var bindings = new VapourSynthLanguage().Bind(text, [], CancellationToken.None);
        var before = bindings.RetainedBytes();

        _ = bindings.At(text.IndexOf("x =", StringComparison.Ordinal));
        _ = bindings.At(text.IndexOf("y =", StringComparison.Ordinal));
        _ = bindings.At(text.IndexOf("z =", StringComparison.Ordinal));
        _ = bindings.At(text.IndexOf("w =", StringComparison.Ordinal));

        Assert.True(bindings.RetainedBytes() > before);
    }

    [Fact]
    public void GetAsync_CachedSnapshot_AnalyzesOffCaller()
    {
        var language = new CountingLanguage(new VapourSynthLanguage());
        var service = new LanguageService(language, Catalog(Vs.ToArray()));
        const string text = "core.std.Blank";
        Thread? caller = null;
        Exception? error = null;
        var worker = new Thread(() =>
        {
            try
            {
                service.GetAsync(text, text.Length, CancellationToken.None).GetAwaiter().GetResult();
                language.LastMembersThread = null;
                caller = Thread.CurrentThread;
                service.GetAsync(text, text.Length, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        worker.Start();
        worker.Join();

        Assert.Null(error);
        Assert.NotNull(language.LastMembersThread);
        Assert.NotSame(caller, language.LastMembersThread);
    }
}
