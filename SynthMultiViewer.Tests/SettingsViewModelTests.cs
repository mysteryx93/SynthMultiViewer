using System.Windows.Input;
using Avalonia.Input;
using HanumanInstitute.SynthMultiViewer.Views;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class SettingsViewModelTests : ViewerTestsBase
{
    private readonly UiTestDriver _ui = new();

    [Fact]
    public void Ok_DarkThemeSelected_SavesAndAppliesTheme()
    {
        var settings = SettingsFake;
        var theme = ThemeMock;
        theme.SetupProperty(x => x.RequestedTheme, "Light");
        var model = SettingsModel;
        model.Theme = AppTheme.Dark;

        ((ICommand)model.Ok).Execute(null);

        Assert.Equal(AppTheme.Dark, settings.Value.Theme);
        Assert.Equal("Dark", theme.Object.RequestedTheme);
        Assert.Equal(1, settings.SaveCount);
        Assert.True(model.DialogResult);
    }

    [Fact]
    public void Cancel_ThemeChanged_DoesNotSave()
    {
        var settings = SettingsFake;
        settings.Value = new AppSettingsData { Theme = AppTheme.Light };
        var theme = ThemeMock;
        theme.SetupProperty(x => x.RequestedTheme, "Light");
        var model = SettingsModel;
        model.Theme = AppTheme.Dark;

        ((ICommand)model.Cancel).Execute(null);

        Assert.Equal(AppTheme.Light, settings.Value.Theme);
        Assert.Equal("Light", theme.Object.RequestedTheme);
        Assert.Equal(0, settings.SaveCount);
        Assert.Null(model.DialogResult);
    }

    [Fact]
    public void Apply_DarkThemeSelected_SavesWithoutClosing()
    {
        var settings = SettingsFake;
        var theme = ThemeMock;
        theme.SetupProperty(x => x.RequestedTheme, "Light");
        var model = SettingsModel;
        var closed = false;
        model.RequestClose += (_, _) => closed = true;
        model.Theme = AppTheme.Dark;

        ((ICommand)model.Apply).Execute(null);

        Assert.Equal(AppTheme.Dark, settings.Value.Theme);
        Assert.Equal("Dark", theme.Object.RequestedTheme);
        Assert.Equal(1, settings.SaveCount);
        Assert.False(closed);
        Assert.Null(model.DialogResult);
    }

    [Fact]
    public void RestoreDefault_DarkThemeSelected_ResetsWorkingCopy()
    {
        var settings = SettingsFake;
        settings.Value = new AppSettingsData { Theme = AppTheme.Dark };
        var model = SettingsModel;
        model.Theme = AppTheme.Dark;

        ((ICommand)model.RestoreDefault).Execute(null);

        Assert.Equal(AppTheme.Light, model.Theme);
        Assert.True(model.EnhanceEditorWithAutoComplete);
        Assert.Equal(Environment.ProcessorCount, model.VapourSynthThreads);
        Assert.Equal(UpdateInterval.Biweekly, model.CheckForUpdates);
        Assert.Equal(AppTheme.Dark, settings.Value.Theme);
        Assert.Equal(0, settings.SaveCount);
    }

    [Fact]
    public void Constructor_StoredInterval_UsesSettingsValue()
    {
        var settings = SettingsFake;
        settings.Value = new AppSettingsData { CheckForUpdates = UpdateInterval.Never };
        var model = SettingsModel;

        Assert.Equal(UpdateInterval.Never, model.CheckForUpdates);
    }

    [Fact]
    public void Ok_CheckForUpdatesChanged_SavesInterval()
    {
        var settings = SettingsFake;
        var model = SettingsModel;
        model.CheckForUpdates = UpdateInterval.Daily;

        ((ICommand)model.Ok).Execute(null);

        Assert.Equal(UpdateInterval.Daily, settings.Value.CheckForUpdates);
        Assert.Equal(1, settings.SaveCount);
    }

    [Fact]
    public void Constructor_FrameworkStatus_UsesDetectionResults()
    {
        var detection = DetectionMock;
        detection.Setup(x => x.VapourSynth).Returns(new FrameworkInstall(FrameworkStatus.Detected, "/usr/lib/libvapoursynth-script.so", Version: "R65"));
        detection.Setup(x => x.AviSynth).Returns(new FrameworkInstall(false));
        var model = SettingsModel;

        Assert.True(model.VapourSynthFound);
        Assert.False(model.AviSynthFound);
        Assert.Equal("/usr/lib/libvapoursynth-script.so", model.VapourSynthLibraryDisplay);
        Assert.Equal("(Detected R65)", model.VapourSynthStatus);
        Assert.Equal("(Not Found)", model.AviSynthStatus);
    }

    [Fact]
    public void Constructor_DetectedVersionDetail_ShowsCompactStatusAndFullTip()
    {
        var detection = DetectionMock;
        detection.Setup(x => x.VapourSynth).Returns(new FrameworkInstall(
                FrameworkStatus.Detected, "/usr/lib/libvsscript.so", Version: "R79",
                VersionDetail: "VapourSynth Video Processing Library\nCore R79"));
        detection.Setup(x => x.AviSynth).Returns(new FrameworkInstall(
                FrameworkStatus.Detected, "/usr/lib/libavisynth.so", Version: "AviSynth+ 3.7.5",
                VersionDetail: "AviSynth+ 3.7.5 (r4228, 3.7, x86_64)"));
        var model = SettingsModel;

        Assert.Equal("(Detected R79)", model.VapourSynthStatus);
        Assert.Contains("Core R79", model.VapourSynthStatusTip, StringComparison.Ordinal);
        Assert.Equal("(Detected AviSynth+ 3.7.5)", model.AviSynthStatus);
        Assert.Contains("r4228", model.AviSynthStatusTip, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_ConfiguredPathMissing_ShowsErrorStatusAndTip()
    {
        var detection = DetectionMock;
        detection.Setup(x => x.VapourSynth).Returns(new FrameworkInstall(
                FrameworkStatus.Error, null, null, "Could not load VapourSynth from '/missing/vs'."));
        detection.Setup(x => x.AviSynth).Returns(new FrameworkInstall(FrameworkStatus.Error));
        var model = SettingsModel;

        Assert.Equal("(Error)", model.VapourSynthStatus);
        Assert.False(model.VapourSynthFound);
        Assert.Contains("/missing/vs", model.VapourSynthStatusTip, StringComparison.Ordinal);
        Assert.Equal("(Error)", model.AviSynthStatus);
        Assert.False(model.AviSynthFound);
        Assert.Contains("could not be loaded", model.AviSynthStatusTip, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_LibraryFailsToRun_ShowsErrorStatusAndTip()
    {
        var detection = DetectionMock;
        detection.Setup(x => x.VapourSynth).Returns(new FrameworkInstall(
                FrameworkStatus.Error, "/vs/VSScript.dll", null,
                "expected str, bytes or os.PathLike object, not NoneType"));
        detection.Setup(x => x.AviSynth).Returns(new FrameworkInstall(
                FrameworkStatus.Error, "/avs/AviSynth.dll", null,
                "AviSynth could not evaluate the script."));
        var model = SettingsModel;

        Assert.Equal("(Error)", model.VapourSynthStatus);
        Assert.False(model.VapourSynthFound);
        Assert.Contains("PathLike", model.VapourSynthStatusTip, StringComparison.Ordinal);
        Assert.Equal("(Error)", model.AviSynthStatus);
        Assert.False(model.AviSynthFound);
        Assert.Contains("evaluate", model.AviSynthStatusTip, StringComparison.Ordinal);
    }

    [Fact]
    public void Ok_VapourSynthThreadsEntered_SavesThreadCount()
    {
        var settings = SettingsFake;
        var model = SettingsModel;
        model.VapourSynthThreads = 4;

        ((ICommand)model.Ok).Execute(null);

        Assert.Equal(4, settings.Value.VapourSynthThreads);
    }

    [Fact]
    public void Constructor_ZeroVapourSynthThreads_UsesProcessorCount()
    {
        var settings = SettingsFake;
        settings.Value = new AppSettingsData { VapourSynthThreads = 0 };
        var model = SettingsModel;

        Assert.Equal(Environment.ProcessorCount, model.VapourSynthThreads);
    }

    [AvaloniaFact]
    public void SettingsView_ClearedThreads_LostFocusRestoresValue()
    {
        var settings = SettingsFake;
        settings.Value = new AppSettingsData { VapourSynthThreads = 6 };
        var model = SettingsModel;
        var view = new SettingsView { DataContext = model };
        using var window = _ui.Show(view);
        var box = view.FindControl<NumericUpDown>("VapourSynthThreads")!;
        var shown = box.Value;

        box.Value = null;
        box.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));

        Assert.Equal(6, shown);
        Assert.Equal(6, box.Value);
        Assert.Equal(6, model.VapourSynthThreads);
        Assert.False(DataValidationErrors.GetHasErrors(box));
    }
}
