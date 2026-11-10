namespace HanumanInstitute.SynthMultiViewer.Tests;

public class ScriptJobFileTests
{
    [Fact]
    public void Dispose_SavedScript_DeletesTempBesideSource()
    {
        const string script = "clip = 1\n";
        var files = new FakeFileSystemService().Add("/scripts/clip.vpy", "previous");
        var job = new ScriptJobFile(files, "/scripts/clip.vpy", script, ScriptKind.VapourSynth, 0, null);
        var path = job.Path;
        var written = files.File.ReadAllText(path);

        job.Dispose();

        Assert.Equal(script, written);
        Assert.StartsWith("/scripts/clip_", path);
        Assert.EndsWith(".vpy", path);
        Assert.NotEqual("/scripts/clip.vpy", path);
        Assert.False(files.File.Exists(path));
        Assert.Single(files.Directory.GetFiles("/scripts"));
    }

    [Fact]
    public void Create_VapourSynthRange_TrimsSourceClip()
    {
        var files = new FakeFileSystemService();
        using var job = new ScriptJobFile(files, null, "clip.set_output()", ScriptKind.VapourSynth, 5, 14);

        var text = files.File.ReadAllText(job.Path);

        Assert.Contains("vs.get_output(0).clip", text);
        Assert.Contains("_vs_job[5:15]", text);
    }

    [Fact]
    public void Create_AviSynthSingleFrame_UsesOneFrameLength()
    {
        var files = new FakeFileSystemService();
        using var job = new ScriptJobFile(files, null, "BlankClip()", ScriptKind.AviSynth, 0, 0);

        var text = files.File.ReadAllText(job.Path);

        Assert.Contains("Trim(0, -1)", text);
    }
}
