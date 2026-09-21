using HanumanInstitute.ApiAviSynth;
using HanumanInstitute.ApiVapourSynth;
using HanumanInstitute.ScriptAssist.AviSynth;
using HanumanInstitute.ScriptAssist.Services;
using HanumanInstitute.ScriptAssist.VapourSynth;
using System.IO.Abstractions;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests.Integration;

using static AssistHarness;

[CollectionDefinition("NativeCatalog", DisableParallelization = true)]
public class NativeCatalogCollection;

[Collection("NativeCatalog")]
public class BrowseCatalogTests : TestsBase
{
    public BrowseCatalogTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task Analyze_InstalledSourceFallback_TypesClip()
    {
        Assert.SkipUnless(VsHelper.TryFindLibrary(out _), "VapourSynth native library was not found.");
        const string text = """
            import os
            import vapoursynth as vs
            core = vs.core
            base = "/home/hanuman/GitHub/FrameRateConverter/Tests/base"
            src = os.path.join(base, "Motion Estimation Torture Clip.avi")
            clip = core.ffms2.Source(src) if hasattr(core, "ffms2") else core.bs.VideoSource(src)
            """;
        var catalog = new InstalledVapourSynthCatalog().Read();
        WriteSourceReturns(catalog);
        Assert.SkipUnless(catalog.Any(function =>
                function is { Namespace: "ffms2", Name: "Source" } or { Namespace: "bs", Name: "VideoSource" }),
            "Neither ffms2.Source nor bs.VideoSource is installed.");
        var factory = Languages(new InstalledVapourSynthCatalog());
        var language = factory.Create(ScriptLanguageFactory.VapourSynth);
        Assert.NotNull(language);
        var clipAt = text.IndexOf("clip =", StringComparison.Ordinal) + 2;
        var vsCoreAt = text.IndexOf("vs.core", StringComparison.Ordinal) + "vs.".Length;

        var clip = await language.GetAsync(text, clipAt, TestContext.Current.CancellationToken);
        var vsCore = await language.GetAsync(text, vsCoreAt, TestContext.Current.CancellationToken);

        Output.WriteLine("clip hover: " + (clip.Hover?.Text ?? "(none)"));
        Output.WriteLine("vs.core hover: " + (vsCore.Hover?.Text ?? "(none)"));
        Assert.Equal("VideoNode", clip.Hover?.Text);
        Assert.Equal("Core", vsCore.Hover?.Text);
    }

    [Fact]
    public async Task BrowseAsync_InstalledVapourSynth_EveryRowHasHint()
    {
        Assert.SkipUnless(VsHelper.TryFindLibrary(out _), "VapourSynth native library was not found.");
        var files = new FileSystemService(new FileSystem());
        var roots = VsRoots();
        Assert.SkipWhen(roots.Count == 0, "VapourSynth search roots were not found.");
        var extra = ScriptPackages.List(roots, files, TestContext.Current.CancellationToken);
        var factory = Languages(new InstalledVapourSynthCatalog(), vapoursynthIncludes: new InstalledPythonIncludes(roots, files));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.VapourSynth, "import vapoursynth as vs\n",
            TestContext.Current.CancellationToken, extraPackages: extra);

        WriteCatalog(groups);
        Assert.NotEmpty(groups);
        var bad = MissingHints(groups);
        Assert.True(bad.Length == 0, "Explorer hints missing or unknown:\n" + string.Join("\n", bad));
    }

    [Fact]
    public async Task BrowseAsync_InstalledAviSynth_EveryRowHasHint()
    {
        Assert.SkipUnless(AvsScript.TryFindLibrary(out _), "AviSynth+ native library was not found.");
        var files = new FileSystemService(new FileSystem());
        var factory = Languages(avisynth: new InstalledAviSynthCatalog(), autoload: new InstalledAviSynthDirectory(files),
            avisynthIncludes: new InstalledAviSynthIncludes(files));

        var groups = await factory.BrowseAsync(ScriptLanguageFactory.AviSynth, "BlankClip()\n",
            TestContext.Current.CancellationToken);

        WriteCatalog(groups);
        Assert.NotEmpty(groups);
        var bad = MissingHints(groups);
        Assert.True(bad.Length == 0, "Explorer hints missing or unknown:\n" + string.Join("\n", bad));
    }

    private void WriteSourceReturns(IReadOnlyList<VapourSynthFunction> catalog)
    {
        foreach (var function in catalog)
        {
            if (function.Name is not ("Source" or "VideoSource" or "BlankClip"))
            {
                continue;
            }

            Output.WriteLine(function.Namespace + "." + function.Name + " return=" +
                (function.ReturnType ?? "(null)") + " plugin=" + (function.PluginName ?? "(null)"));
        }
    }

    private void WriteCatalog(IReadOnlyList<BrowseGroup> groups)
    {
        foreach (var group in groups)
        {
            Output.WriteLine(group.Name + " (" + group.Functions.Count + ")");
            foreach (var function in group.Functions)
            {
                Output.WriteLine("  " + function.Name + "  " + function.Signature);
            }
        }
    }

    private static string[] MissingHints(IReadOnlyList<BrowseGroup> groups) =>
        groups.SelectMany(group => group.Functions.Select(function => (group.Name, function)))
            .Where(row => MissingHint(row.function))
            .Select(row => row.Name + "." + row.function.Name + " => " + row.function.Signature)
            .ToArray();

    private static bool MissingHint(BrowseFunction function) =>
        string.IsNullOrWhiteSpace(function.Signature)
        || string.Equals(function.Signature, function.Name, StringComparison.Ordinal)
        || function.Signature.Contains("parameters unknown", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> VsRoots()
    {
        VsHelper.TryFindLibrary(out var library);
        return VsHelper.GetPluginDirectories(library)
            .Concat(VsPathResolver.GetPythonModuleDirectories(library))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private sealed class InstalledVapourSynthCatalog : IVapourSynthNativeCatalog
    {
        public IReadOnlyList<VapourSynthFunction> Read() =>
            VsCatalog.Read().Select(x =>
                new VapourSynthFunction(x.Namespace, x.Name, x.Arguments, x.ReturnType, x.PluginName)).ToArray();
    }

    private sealed class InstalledAviSynthCatalog : IAviSynthNativeCatalog
    {
        public IReadOnlyList<AviSynthFilter> Read() =>
            AvsCatalog.Read().Select(x => new AviSynthFilter(x.Name, x.Arguments, x.Category)).ToArray();
    }

    private sealed class InstalledPythonIncludes(IReadOnlyList<string> roots, IFileSystemService files) : IIncludeSource
    {
        public IncludeFile? Read(string specifier, string? fromPath) =>
            ScriptFiles.PythonModule(specifier, fromPath, roots, files);
    }

    private sealed class InstalledAviSynthIncludes(IFileSystemService files) : IIncludeSource
    {
        public IncludeFile? Read(string specifier, string? fromPath) =>
            ScriptFiles.AviSynth(specifier, fromPath, AvsScript.GetPluginDirectories(), files);
    }

    private sealed class InstalledAviSynthDirectory(IFileSystemService files) : IScriptDirectory
    {
        public IReadOnlyList<string> Roots() => AvsScript.GetPluginDirectories();

        public IEnumerable<string> Files(string directory, IReadOnlyList<string> extensions) =>
            files.GetFilesByExtensions(directory, extensions);

        public string? TryRead(string path)
        {
            try
            {
                return files.File.Exists(path) ? files.File.ReadAllText(path) : null;
            }
            catch (System.IO.IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
