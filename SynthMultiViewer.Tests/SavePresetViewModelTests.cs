using HanumanInstitute.MvvmDialogs.Avalonia;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class SavePresetViewModelTests
{
    [Fact]
    public void Ok_BlankName_IsDisabled()
    {
        var model = Create(new FakeSettingsProvider()).Load("");

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
        var model = Create(settings).Load("");

        model.Presets.CurrentItem = model.Presets.First(item => item.Value?.Name == "AV1");

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
        var model = Create(settings).Load("");
        model.Presets.CurrentItem = model.Presets.First(item => item.Value?.Name == "AV1");

        ((System.Windows.Input.ICommand)model.Delete).Execute(null);

        var remaining = Assert.Single(settings.Value.EncodePresets);
        Assert.Equal("Silent", remaining.Name);
        Assert.Equal(1, settings.SaveCount);
    }

    [Fact]
    public async Task Reset_Declined_LeavesPresets()
    {
        var settings = SavedAv1();
        var dialogs = new FakeDialogManager();
        var model = Create(settings, dialogs).Load("");

        await model.Reset.Execute();

        Assert.Equal("AV1", Assert.Single(settings.Value.EncodePresets).Name);
        Assert.Equal(MessageBoxButton.YesNo, Assert.IsType<MessageBoxSettings>(dialogs.LastFrameworkSettings).Button);
        Assert.Equal(0, settings.SaveCount);
    }

    [Fact]
    public async Task Reset_Accepted_RestoresExamples()
    {
        var settings = SavedAv1();
        var dialogs = new FakeDialogManager();
        dialogs.QueueFrameworkResult(true);
        var model = Create(settings, dialogs).Load("");

        await model.Reset.Execute();

        Assert.Equal(EncodePreset.Examples.Select(preset => preset.Name), settings.Value.EncodePresets.Select(preset => preset.Name));
        Assert.Equal(EncodePreset.Examples.Select(preset => preset.Arguments), settings.Value.EncodePresets.Select(preset => preset.Arguments));
        Assert.Equal(1, settings.SaveCount);
    }

    private static FakeSettingsProvider SavedAv1() => new()
    {
        Value = new AppSettingsData
        {
            EncodePresets = [new EncodePreset { Name = "AV1", Arguments = "-c:v libsvtav1 -crf 25" }]
        }
    };

    private static SavePresetViewModel Create(FakeSettingsProvider settings, FakeDialogManager? dialogs = null) =>
        new(settings, new DialogService(dialogs ?? new FakeDialogManager()));
}
