namespace HanumanInstitute.SynthMultiViewer.Tests;

public class SavePresetViewModelTests
{
    [Fact]
    public void Ok_BlankName_IsDisabled()
    {
        var model = new SavePresetViewModel(new FakeSettingsProvider()).Load("");

        var canOk = ((System.Windows.Input.ICommand)model.Ok).CanExecute(null);

        Assert.False(canOk);
    }

    [Fact]
    public void Select_NamedPreset_CopiesName()
    {
        var settings = new FakeSettingsProvider
        {
            Value = new AppSettingsData
            {
                EncodePresets = [new EncodePreset { Name = "AV1", Arguments = "-c:v libsvtav1 -crf 25" }]
            }
        };
        var model = new SavePresetViewModel(settings).Load("");

        model.SelectedPreset = model.Presets[0];

        Assert.Equal("AV1", model.PresetName);
    }

    [Fact]
    public void Delete_SelectedPreset_PersistsRest()
    {
        var settings = new FakeSettingsProvider
        {
            Value = new AppSettingsData
            {
                EncodePresets =
                [
                    new EncodePreset { Name = "AV1", Arguments = "-c:v libsvtav1 -crf 25" },
                    new EncodePreset { Name = "Silent", Arguments = "-an" }
                ]
            }
        };
        var model = new SavePresetViewModel(settings).Load("");
        model.SelectedPreset = model.Presets[0];

        ((System.Windows.Input.ICommand)model.Delete).Execute(null);

        var remaining = Assert.Single(settings.Value.EncodePresets);
        Assert.Equal("Silent", remaining.Name);
        Assert.Equal(1, settings.SaveCount);
    }
}
