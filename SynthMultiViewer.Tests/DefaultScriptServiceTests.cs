
namespace HanumanInstitute.SynthMultiViewer.Tests;

public class DefaultScriptServiceTests : ViewerTestsBase
{

    [AvaloniaFact]
    public void VapourSynth_Asset_ContainsBlankClipAndOutput()
    {
        var scripts = new DefaultScriptService();

        var script = scripts.VapourSynth;

        Assert.Contains("import vapoursynth as vs", script, StringComparison.Ordinal);
        Assert.Contains("BlankClip", script, StringComparison.Ordinal);
        Assert.Contains("set_output()", script, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void AviSynth_Asset_ContainsBlankClip()
    {
        var scripts = new DefaultScriptService();

        var script = scripts.AviSynth;

        Assert.Contains("BlankClip", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task New_Executed_UsesDefaultVapourSynthScript()
    {
        var defaults = Mock.Of<IDefaultScriptService>(x => x.VapourSynth == "example vs" && x.AviSynth == "example avs");
        var model = new MainViewModel(
            Dialogs, new FakeEnvironmentService(), defaults,
            new FakeSettingsProvider(), Mock.Of<IAppUpdateService>(),
            new FakeFileSystemService());

        await model.New.Execute();

        var editor = Assert.IsType<EditorViewModel>(Assert.Single(model.ScriptList));
        Assert.Equal(defaults.VapourSynth, editor.Script);
        Assert.Equal(ScriptKind.VapourSynth, editor.Kind);
    }

    [Fact]
    public async Task NewAviSynth_Executed_UsesDefaultAviSynthScript()
    {
        var defaults = Mock.Of<IDefaultScriptService>(x => x.VapourSynth == "example vs" && x.AviSynth == "example avs");
        var model = new MainViewModel(
            Dialogs, new FakeEnvironmentService(), defaults,
            new FakeSettingsProvider(), Mock.Of<IAppUpdateService>(),
            new FakeFileSystemService());

        await model.NewAviSynth.Execute();

        var editor = Assert.IsType<EditorViewModel>(Assert.Single(model.ScriptList));
        Assert.Equal(defaults.AviSynth, editor.Script);
        Assert.Equal(ScriptKind.AviSynth, editor.Kind);
    }
}
