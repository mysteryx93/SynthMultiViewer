using HanumanInstitute.FFmpeg;
using HanumanInstitute.MvvmDialogs.Avalonia;
using Microsoft.Extensions.Options;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class EncodeViewModelTests
{
    [Fact]
    public void Constructor_SavedPreset_LeavesArgumentsEmpty()
    {
        var settings = new FakeSettingsProvider
        {
            Value = new AppSettingsData
            {
                EncodePresets = [new EncodePreset { Name = "AV1", Arguments = "-c:v libsvtav1 -crf 25" }]
            }
        };

        var model = CreateModel(settings, new FakeDialogManager());

        Assert.Equal("", model.Arguments);
        Assert.Null(model.Presets.SelectedValue);
        Assert.Contains(model.Presets, item => item.Value?.Name == "AV1");
    }

    [Fact]
    public async Task SavePreset_AcceptedName_StoresArguments()
    {
        var settings = new FakeSettingsProvider();
        var dialogs = new FakeDialogManager();
        var service = new DialogService(dialogs, viewModelFactory: CreateSaveDialog);
        var model = new EncodeViewModel(new EncoderService(), Options.Create(new AppPaths()), settings, new FakeFileSystemService(), service);
        model.Arguments = "-an";
        dialogs.OnShow = AcceptSilent;
        object CreateSaveDialog(Type type) => new SavePresetViewModel(settings, new DialogService(dialogs));
        void AcceptSilent(HanumanInstitute.MvvmDialogs.IModalDialogViewModel shown)
        {
            var save = Assert.IsType<SavePresetViewModel>(shown);
            save.PresetName = "Silent";
            ((System.Windows.Input.ICommand)save.Ok).Execute(null);
        }

        await model.SavePreset.Execute();

        var preset = Assert.Single(settings.Value.EncodePresets, item => item.Name == "Silent");
        Assert.Equal("-an", preset.Arguments);
        Assert.Equal("Silent", model.Presets.SelectedValue?.Name);
        Assert.Equal("-an", model.Arguments);
    }

    [AvaloniaFact]
    public void OnClosed_RunningProcess_CancelsWorker()
    {
        var worker = new Mock<IProcessWorkerEncoder>();
        var model = new EncodeProgressViewModel();
        model.DisplayTask(worker.Object);

        model.OnClosed();

        worker.Verify(x => x.Cancel(), Times.Once);
    }

    [AvaloniaFact]
    public void DisplayTask_EncoderProgress_UpdatesMetrics()
    {
        var worker = new Mock<IProcessWorkerEncoder>();
        worker.SetupGet(x => x.Options).Returns(new ProcessOptionsEncoder { FrameCount = 1000 });
        var model = new EncodeProgressViewModel();
        model.DisplayTask(worker.Object);

        worker.Raise(x => x.ProgressReceived += null, worker.Object,
            new ProgressReceivedEventArgs(new ProgressStatusFFmpeg { Frame = 120, Fps = 30 }));

        Assert.Equal(1000, model.ProgressBarMax);
        Assert.Equal(120, model.ProgressBarValue);
        Assert.Equal(30.ToString(System.Globalization.CultureInfo.CurrentCulture), model.Fps);
    }

    private static EncodeViewModel CreateModel(FakeSettingsProvider settings, FakeDialogManager dialogs)
    {
        var service = new DialogService(dialogs, viewModelFactory: type => new SavePresetViewModel(settings, new DialogService(dialogs)));
        return new EncodeViewModel(new EncoderService(), Options.Create(new AppPaths()), settings, new FakeFileSystemService(), service);
    }
}
