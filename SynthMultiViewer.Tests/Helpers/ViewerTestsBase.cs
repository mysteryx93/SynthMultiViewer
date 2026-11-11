using HanumanInstitute.FFmpeg;
using HanumanInstitute.MvvmDialogs.Avalonia;
using Microsoft.Extensions.Options;

namespace HanumanInstitute.SynthMultiViewer.Tests;

/// <summary>
/// Composes view models from independently configurable test dependencies.
/// Each test receives its own fixture instance.
/// </summary>
public abstract class ViewerTestsBase
{
    internal FakeSettingsProvider SettingsFake => field ??= new();
    internal FakeFileSystemService FilesFake => field ??= new();
    internal FakeEnvironmentService EnvironmentFake => field ??= new();
    internal FakeDialogManager DialogsFake => field ??= new();
    internal Mock<IAppUpdateService> UpdatesMock => field ??= new();
    internal Mock<IAppVersionClient> VersionsMock => field ??= new();
    internal Mock<IAppTheme> ThemeMock => field ??= new();
    internal Mock<IFrameworkDetectionService> DetectionMock => field ??= CreateDetection();
    internal Mock<IDefaultScriptService> ScriptsMock => field ??= Mock.Get(Mock.Of<IDefaultScriptService>(x => x.VapourSynth == "import vapoursynth as vs\ncore = vs.core\nclip = core.std.BlankClip()\nclip.set_output()\n" && x.AviSynth == "BlankClip()\n"));
    internal IEncoderService Encoder => field ??= new EncoderService();

    internal DialogService Dialogs => field ??= new DialogService(DialogsFake, viewModelFactory: CreateViewModel);
    internal MainViewModel Workspace => field ??= new MainViewModel(
        Dialogs, EnvironmentFake, ScriptsMock.Object, SettingsFake, UpdatesMock.Object, FilesFake);
    internal HelpViewModel HelpModel => field ??= new HelpViewModel(EnvironmentFake, VersionsMock.Object);
    internal SettingsViewModel SettingsModel => field ??= new SettingsViewModel(SettingsFake, ThemeMock.Object, DetectionMock.Object);

    // Supplies the unconfigured framework status used by most settings tests.
    private static Mock<IFrameworkDetectionService> CreateDetection()
    {
        var detection = new Mock<IFrameworkDetectionService>();
        detection.Setup(x => x.VapourSynth).Returns(new FrameworkInstall(false));
        detection.Setup(x => x.AviSynth).Returns(new FrameworkInstall(false));
        return detection;
    }

    // Resolves view models using the same collaborators as the workspace.
    private object CreateViewModel(Type type)
    {
        if (type == typeof(SettingsViewModel)) { return new SettingsViewModel(SettingsFake, ThemeMock.Object, DetectionMock.Object); }
        if (type == typeof(HelpViewModel)) { return new HelpViewModel(EnvironmentFake, VersionsMock.Object); }
        if (type == typeof(BenchmarkViewModel)) { return new BenchmarkViewModel(SettingsFake); }
        if (type == typeof(EncodeViewModel))
        {
            return new EncodeViewModel(Encoder, Options.Create(Encoder.Processes.Paths), SettingsFake, FilesFake, Dialogs);
        }
        if (type == typeof(SavePresetViewModel))
        {
            return new SavePresetViewModel(SettingsFake, Dialogs);
        }
        return Activator.CreateInstance(type)!;
    }
}
