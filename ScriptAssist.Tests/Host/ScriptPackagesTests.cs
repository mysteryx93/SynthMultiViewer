using System.Diagnostics.CodeAnalysis;
using HanumanInstitute.ScriptAssist.Services;
using Moq;
using System.IO.Abstractions;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests.Host;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class ScriptPackagesTests : TestsBase
{
    public ScriptPackagesTests(ITestOutputHelper output) : base(output)
    {
    }

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
    public void List_RecordModules_IncludesEveryTopLevel()
    {
        var record = string.Join('\n',
        [
            "vsaa/__init__.py,sha256=a,1",
            "vsdeband/__init__.py,sha256=a,1",
            "vsdehalo/__init__.py,sha256=a,1",
            "vsdeinterlace/__init__.py,sha256=a,1",
            "vsdenoise/__init__.py,sha256=a,1",
            "vsexprtools/__init__.py,sha256=a,1",
            "vsjetpack/__init__.py,sha256=a,1",
            "vskernels/__init__.py,sha256=a,1",
            "vsmasktools/__init__.py,sha256=a,1",
            "vsrgtools/__init__.py,sha256=a,1",
            "vsscale/__init__.py,sha256=a,1",
            "vssource/__init__.py,sha256=a,1",
            "vstools/__init__.py,sha256=a,1"
        ]);
        var files = new FakeFileSystemService()
            .Add("/site-packages/vsjetpack-1.0.dist-info/METADATA", "Name: vsjetpack\n")
            .Add("/site-packages/vsjetpack-1.0.dist-info/RECORD", record);

        var names = ScriptPackages.List(["/site-packages"], files);

        Assert.DoesNotContain("vsjetpack", names);
        Assert.Contains("vstools", names);
        Assert.Contains("vsmasktools", names);
        Assert.Contains("vsrgtools", names);
        Assert.Contains("vsscale", names);
        Assert.Contains("vssource", names);
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
        var path = InitMock<IPath>(p =>
        {
            p.Setup(x => x.GetFileName(It.IsAny<string>()))
                .Returns((string value) => System.IO.Path.GetFileName(value));
            p.Setup(x => x.Combine(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string left, string right) => System.IO.Path.Combine(left, right));
        });
        var directory = InitMock<IDirectory>(d =>
        {
            d.Setup(x => x.Exists("/site")).Returns(true);
            d.Setup(x => x.EnumerateDirectories("/site"))
                .Returns(Enumerable.Range(0, 40).Select(i => "/site/pkg" + i + "-1.0.dist-info"));
            d.Setup(x => x.EnumerateFiles("/site", "*.egg-info")).Returns([]);
            d.Setup(x => x.EnumerateFiles("/site", "*.py")).Returns([]);
        });
        var file = InitMock<IFile>(f =>
        {
            f.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
            f.Setup(x => x.ReadAllText(It.IsAny<string>())).Returns(() =>
            {
                reads++;
                if (reads == 1)
                {
                    cts.Cancel();
                }

                return "Name: pkg\nRequires-Dist: vapoursynth\n";
            });
        });
        var files = InitMock<IFileSystemService>(f =>
        {
            f.Setup(x => x.Directory).Returns(directory.Object);
            f.Setup(x => x.Path).Returns(path.Object);
            f.Setup(x => x.File).Returns(file.Object);
        });

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
