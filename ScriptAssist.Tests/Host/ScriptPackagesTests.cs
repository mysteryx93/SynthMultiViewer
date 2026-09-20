using System.Diagnostics.CodeAnalysis;
using HanumanInstitute.ScriptAssist.Services;
using Moq;
using System.IO.Abstractions;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests.Host;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class ScriptPackagesTests
{
    [Fact]
    public void List_RequiresVapourSynth_ReturnsName()
    {
        var files = new FakeFileSystemService()
            .Add("/site-packages/foo-1.0.dist-info/METADATA", "Name: foo\nRequires-Dist: VapourSynth\n");

        var names = ScriptPackages.List(["/site-packages"], files);

        Assert.Contains("foo", names);
    }

    [Fact]
    public void List_KnownNameWithoutRequires_ReturnsName()
    {
        var files = new FakeFileSystemService()
            .Add("/site-packages/havsfunc-0.6.dist-info/METADATA", "Name: havsfunc\n");

        var names = ScriptPackages.List(["/site-packages"], files);

        Assert.Contains("havsfunc", names);
    }

    [Fact]
    public void List_VapourSynthBinding_Skipped()
    {
        var files = new FakeFileSystemService()
            .Add("/site-packages/VapourSynth-72.dist-info/METADATA", "Name: VapourSynth\n");

        var names = ScriptPackages.List(["/site-packages"], files);

        Assert.DoesNotContain("VapourSynth", names, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("vapoursynth", names, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void List_NativeOnlyRecord_Skipped()
    {
        var files = new FakeFileSystemService()
            .Add("/site-packages/bm3d-1.0.dist-info/METADATA", "Name: bm3d\nRequires-Dist: VapourSynth\n")
            .Add("/site-packages/bm3d-1.0.dist-info/RECORD", "bm3d/bm3d.so,sha256=abc,100\n");

        var names = ScriptPackages.List(["/site-packages"], files);

        Assert.DoesNotContain("bm3d", names);
    }

    [Fact]
    public void List_VsjetpackWithSibling_Skipped()
    {
        var files = new FakeFileSystemService()
            .Add("/site-packages/vsjetpack-1.0.dist-info/METADATA", "Name: vsjetpack\n")
            .Add("/site-packages/vsdenoise-1.0.dist-info/METADATA", "Name: vsdenoise\nRequires-Dist: vapoursynth\n");

        var names = ScriptPackages.List(["/site-packages"], files);

        Assert.DoesNotContain("vsjetpack", names);
        Assert.Contains("vsdenoise", names);
    }

    [Fact]
    public void List_VsjetpackAlone_Returned()
    {
        var files = new FakeFileSystemService()
            .Add("/site-packages/vsjetpack-1.0.dist-info/METADATA", "Name: vsjetpack\n");

        var names = ScriptPackages.List(["/site-packages"], files);

        Assert.Contains("vsjetpack", names);
    }

    [Fact]
    public void List_LoosePyWithMention_ReturnsName()
    {
        var files = new FakeFileSystemService()
            .Add("/site-packages/custom.py", "import vapoursynth as vs\n");

        var names = ScriptPackages.List(["/site-packages"], files);

        Assert.Contains("custom", names);
    }

    [Fact]
    public void List_LoosePyWithoutMention_Skipped()
    {
        var files = new FakeFileSystemService()
            .Add("/site-packages/custom.py", "def helper():\n    return 1\n");

        var names = ScriptPackages.List(["/site-packages"], files);

        Assert.DoesNotContain("custom", names);
    }

    [Fact]
    public void List_NumpyRequires_NotListed()
    {
        var files = new FakeFileSystemService()
            .Add("/site-packages/foo-1.0.dist-info/METADATA",
                "Name: foo\nRequires-Dist: numpy\nRequires-Dist: jetpytools\nRequires-Dist: rich\n");

        var names = ScriptPackages.List(["/site-packages"], files);

        Assert.DoesNotContain("foo", names);
        Assert.DoesNotContain("numpy", names);
        Assert.DoesNotContain("jetpytools", names);
        Assert.DoesNotContain("rich", names);
    }

    [Fact]
    public void List_RequiresWithoutSpace_ReturnsName()
    {
        var files = new FakeFileSystemService()
            .Add("/site-packages/foo-1.0.dist-info/METADATA", "Name: foo\nRequires-Dist: VapourSynth>=70\n")
            .Add("/site-packages/bar-1.0.dist-info/METADATA", "Name: bar\nRequires-Dist: VapourSynth!=65\n");

        var names = ScriptPackages.List(["/site-packages"], files);

        Assert.Contains("foo", names);
        Assert.Contains("bar", names);
    }

    [Fact]
    public void List_TopLevelTxt_UsesDeclaredImportName()
    {
        var files = new FakeFileSystemService()
            .Add("/site-packages/vs_tools-1.0.dist-info/METADATA", "Name: vs-tools\nRequires-Dist: VapourSynth\n")
            .Add("/site-packages/vs_tools-1.0.dist-info/top_level.txt", "vstools\n");

        var names = ScriptPackages.List(["/site-packages"], files);

        Assert.Contains("vstools", names);
        Assert.DoesNotContain("vs_tools", names);
    }

    [Fact]
    public void List_CancelledAfterFirstRead_Stops()
    {
        var cts = new CancellationTokenSource();
        var reads = 0;
        var path = new Mock<IPath>();
        path.Setup(p => p.GetFileName(It.IsAny<string>()))
            .Returns((string value) => System.IO.Path.GetFileName(value));
        path.Setup(p => p.Combine(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string left, string right) => System.IO.Path.Combine(left, right));
        var directory = new Mock<IDirectory>();
        directory.Setup(d => d.Exists("/site")).Returns(true);
        directory.Setup(d => d.EnumerateDirectories("/site"))
            .Returns(Enumerable.Range(0, 40).Select(i => "/site/pkg" + i + "-1.0.dist-info"));
        directory.Setup(d => d.EnumerateFiles("/site", "*.egg-info")).Returns([]);
        directory.Setup(d => d.EnumerateFiles("/site", "*.py")).Returns([]);
        var file = new Mock<IFile>();
        file.Setup(f => f.Exists(It.IsAny<string>())).Returns(true);
        file.Setup(f => f.ReadAllText(It.IsAny<string>())).Returns(() =>
        {
            reads++;
            if (reads == 1)
            {
                cts.Cancel();
            }

            return "Name: pkg\nRequires-Dist: vapoursynth\n";
        });
        var files = new Mock<IFileSystemService>();
        files.Setup(f => f.Directory).Returns(directory.Object);
        files.Setup(f => f.Path).Returns(path.Object);
        files.Setup(f => f.File).Returns(file.Object);

        var act = () => ScriptPackages.List(["/site"], files.Object, cts.Token);

        Assert.Throws<OperationCanceledException>(act);
        Assert.Equal(1, reads);
    }

    [Fact]
    public void List_CommentOnlyMention_Skipped()
    {
        var files = new FakeFileSystemService()
            .Add("/site-packages/custom.py", "# does not import vapoursynth\n");

        var names = ScriptPackages.List(["/site-packages"], files);

        Assert.DoesNotContain("custom", names);
    }

    [Fact]
    public void List_InvalidIdentifier_Skipped()
    {
        var files = new FakeFileSystemService()
            .Add("/site-packages/bad-name.py", "import vapoursynth as vs\n");

        var names = ScriptPackages.List(["/site-packages"], files);

        Assert.DoesNotContain("bad-name", names);
    }

    [Fact]
    public void List_KeywordName_Skipped()
    {
        var files = new FakeFileSystemService()
            .Add("/site-packages/class.py", "import vapoursynth as vs\n");

        var names = ScriptPackages.List(["/site-packages"], files);

        Assert.DoesNotContain("class", names);
    }
}
