
namespace HanumanInstitute.SynthMultiViewer.Tests;

public class CommandLineScriptsTests
{
    [Fact]
    public void FromArguments_HostAndScript_ReturnsScript()
    {
        const string script = "media/preview.vpy";

        var files = CommandLineScripts.FromArguments(["SynthMultiViewer", script]);

        Assert.Equal([script], files);
    }

    [Fact]
    public void FromArguments_DotnetHostDllAndScript_SkipsDll()
    {
        const string script = "home/clip.avs";

        var files = CommandLineScripts.FromArguments(["dotnet", "SynthMultiViewer.dll", script]);

        Assert.Equal([script], files);
    }

    [Fact]
    public void FromArguments_FlagsAndFileUri_ReturnsLocalPath()
    {
        const string script = "/scripts/script.vpy";

        var files = CommandLineScripts.FromArguments(["SynthMultiViewer", "--foo",
            new Uri("file://" + script).AbsoluteUri]);

        Assert.Equal([script], files);
    }

    [Fact]
    public void FromArguments_UnknownExtension_IsIgnored()
    {
        const string script = "job.avsi";

        var files = CommandLineScripts.FromArguments(["viewer", "notes.txt", script]);

        Assert.Equal([script], files);
    }
}
