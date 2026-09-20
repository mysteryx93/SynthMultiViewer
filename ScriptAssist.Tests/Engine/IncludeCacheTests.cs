using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests.Engine;

public class IncludeCacheTests
{
    [Fact]
    public void IncludeCache_ManySmallEntries_AreRetained()
    {
        var cache = new IncludeCache();
        var session = new IncludeSession(cache);
        for (var i = 0; i < 100; i++)
        {
            session.SetEntry("/plugins/f" + i + ".py", new IncludeEntry([], []));
        }

        Assert.True(cache.TryEntry("/plugins/f0.py", out _));
        Assert.True(cache.TryEntry("/plugins/f99.py", out _));
    }

    [Fact]
    public void IncludeCache_PinnedEntries_SurviveUnpinnedFlood()
    {
        var cache = new IncludeCache();
        PinDocuments(cache, 8);
        var session = new IncludeSession(cache);
        var huge = new IncludeEntry([new Symbol("F", [new string('x', 1_200_000)])], []);
        for (var i = 0; i < 8; i++)
        {
            session.SetEntry("/plugins/f" + i + ".py", huge);
        }

        Assert.True(cache.TryEntry("/plugins/d0.py", out _));
        Assert.True(cache.TryEntry("/plugins/d7.py", out _));
        Assert.False(cache.TryEntry("/plugins/f0.py", out _));
        Assert.True(cache.TryEntry("/plugins/f7.py", out _));
    }

    [Fact]
    public void IncludeCache_ReleasedWorkingSet_CanEvict()
    {
        var cache = new IncludeCache();
        PinDocuments(cache, 8);
        var huge = new IncludeEntry([new Symbol("F", [new string('x', 1_200_000)])], []);
        var session = new IncludeSession(cache);
        cache.Release("/doc0.vpy");
        for (var i = 0; i < 8; i++)
        {
            session.SetEntry("/plugins/g" + i + ".py", huge);
        }

        Assert.False(cache.TryEntry("/plugins/d0.py", out _));
        Assert.True(cache.TryEntry("/plugins/d1.py", out _));
    }

    [Fact]
    public void IncludeCache_UnpinnedBytes_EvictsLargeHelpers()
    {
        var cache = new IncludeCache();
        var session = new IncludeSession(cache);
        var huge = new IncludeEntry([new Symbol("F", [new string('x', 1_200_000)])], []);
        for (var i = 0; i < 8; i++)
        {
            session.SetEntry("/plugins/h" + i + ".py", huge);
        }

        Assert.False(cache.TryEntry("/plugins/h0.py", out _));
        Assert.True(cache.TryEntry("/plugins/h7.py", out _));
    }

    [Fact]
    public void IncludeCache_PinnedLargeHelpers_SurviveByteEviction()
    {
        var cache = new IncludeCache();
        var huge = new IncludeEntry([new Symbol("F", [new string('x', 1_200_000)])], []);
        cache.Retain("/doc.vpy", new Dictionary<string, IncludeEntry>(StringComparer.Ordinal)
        {
            ["/plugins/pinned.py"] = huge
        }, new Dictionary<(string Specifier, string? From), string?>(), cache.Version);
        var session = new IncludeSession(cache);
        for (var i = 0; i < 8; i++)
        {
            session.SetEntry("/plugins/h" + i + ".py", huge);
        }

        Assert.True(cache.TryEntry("/plugins/pinned.py", out _));
    }

    private static void PinDocuments(IncludeCache cache, int count)
    {
        var version = cache.Version;
        var paths = new Dictionary<(string Specifier, string? From), string?>();
        for (var i = 0; i < count; i++)
        {
            var entries = new Dictionary<string, IncludeEntry>(StringComparer.Ordinal)
            {
                ["/plugins/d" + i + ".py"] = new([], [])
            };
            cache.Retain("/doc" + i + ".vpy", entries, paths, version);
        }
    }
}
