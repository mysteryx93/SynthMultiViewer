using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests.Host;

public class ScriptFilesTests
{
    [Fact]
    public void PythonModule_ExistingFile_ReturnsText()
    {
        var files = new FakeFileSystemService(new Dictionary<string, MockFileData>
        {
            ["/site-packages/havsfunc.py"] = "def QTGMC():\n    return 1\n"
        });

        var file = ScriptFiles.PythonModule("havsfunc", null, ["/site-packages"], files);

        Assert.NotNull(file);
        Assert.Contains("def QTGMC(", file.Value.Text, StringComparison.Ordinal);
        Assert.EndsWith("havsfunc.py", file.Value.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void AviSynth_ExistingFile_ReturnsText()
    {
        var files = new FakeFileSystemService(new Dictionary<string, MockFileData>
        {
            ["/plugins/helpers.avsi"] = "function Helper(clip c) { c }\n"
        });

        var file = ScriptFiles.AviSynth("helpers.avsi", null, ["/plugins"], files);

        Assert.NotNull(file);
        Assert.Contains("function Helper", file.Value.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void PythonModule_MissingFile_ReturnsNull()
    {
        var files = new FakeFileSystemService();

        var file = ScriptFiles.PythonModule("havsfunc", null, ["/site-packages"], files);

        Assert.Null(file);
    }

    [Fact]
    public void PythonModule_StubOnly_ReadsPyi()
    {
        var files = new FakeFileSystemService(new Dictionary<string, MockFileData>
        {
            ["/site-packages/helper.pyi"] = "def Foo():\n    ...\n"
        });

        var file = ScriptFiles.PythonModule("helper", null, ["/site-packages"], files);

        Assert.NotNull(file);
        Assert.Contains("def Foo(", file.Value.Text, StringComparison.Ordinal);
        Assert.EndsWith("helper.pyi", file.Value.Path, StringComparison.Ordinal);
    }
}
