using HanumanInstitute.ScriptAssist;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class ScriptAssistServiceTests
{
    [Fact]
    public void Create_WellKnownLanguages_AreRegistered()
    {
        var service = new ScriptAssistService(new FakeSettingsProvider(), new FakeFileSystemService());

        Assert.True(service.IsEnabled);
        Assert.NotNull(service.Create(ScriptLanguageFactory.VapourSynth));
        Assert.NotNull(service.Create(ScriptLanguageFactory.AviSynth));
    }

    [Fact]
    public void Constructor_ReadsEnhanceEditorSetting()
    {
        var settings = new FakeSettingsProvider
        {
            Value = { EnhanceEditorWithAutoComplete = false }
        };

        var service = new ScriptAssistService(settings, new FakeFileSystemService());

        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void EnhanceEditorSetting_ChangedAfterConstruct_DoesNotEnable()
    {
        var settings = new FakeSettingsProvider
        {
            Value = { EnhanceEditorWithAutoComplete = false }
        };
        var service = new ScriptAssistService(settings, new FakeFileSystemService());

        settings.Value.EnhanceEditorWithAutoComplete = true;

        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void Configure_WhenDisabled_DoesNotEnumerateNativeCatalogs()
    {
        var service = new ScriptAssistService(new FakeSettingsProvider(), new FakeFileSystemService())
        {
            IsEnabled = false
        };

        service.Configure(ScriptLanguageFactory.VapourSynth, "a");
        service.Configure(ScriptLanguageFactory.AviSynth, "b");
        service.Refresh();

        Assert.False(service.IsEnabled);
    }
}
