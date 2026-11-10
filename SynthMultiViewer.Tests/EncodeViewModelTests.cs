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
        Assert.Equal("", model.SelectedPreset?.Name);
        Assert.Contains(model.Presets, preset => preset.Name == "AV1");
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
        object CreateSaveDialog(Type type) => new SavePresetViewModel(settings);
        void AcceptSilent(HanumanInstitute.MvvmDialogs.IModalDialogViewModel shown)
        {
            var save = Assert.IsType<SavePresetViewModel>(shown);
            save.PresetName = "Silent";
            ((System.Windows.Input.ICommand)save.Ok).Execute(null);
        }

        await model.SavePreset.Execute();

        var preset = Assert.Single(settings.Value.EncodePresets, item => item.Name == "Silent");
        Assert.Equal("-an", preset.Arguments);
        Assert.Equal("Silent", model.SelectedPreset?.Name);
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
        worker.SetupGet(x => x.Options).Returns(new ProcessOptionsEncoder());
        var model = new EncodeProgressViewModel();
        model.DisplayTask(worker.Object);

        worker.Raise(x => x.ProgressReceived += null, worker.Object,
            new ProgressReceivedEventArgs(new ProgressStatusFFmpeg { Frame = 120, Speed = 1.5f, Size = "42kB", Bitrate = "320kbits/s" }));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("120", model.Frame);
        Assert.Equal("1.5x", model.Speed);
        Assert.Equal("42kB", model.Size);
        Assert.Equal("320kbits/s", model.Bitrate);
    }

    private static EncodeViewModel CreateModel(FakeSettingsProvider settings, FakeDialogManager dialogs)
    {
        var service = new DialogService(dialogs, viewModelFactory: type => new SavePresetViewModel(settings));
        return new EncodeViewModel(new EncoderService(), Options.Create(new AppPaths()), settings, new FakeFileSystemService(), service);
    }
}
