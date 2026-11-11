using System.Reactive.Linq;
using HanumanInstitute.SynthMultiViewer.Controls;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.SynthMultiViewer.Models;
using HanumanInstitute.SynthMultiViewer.Services;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Edits a copy of application settings and applies them on OK or Apply.
/// </summary>
public partial class SettingsViewModel : WorkspaceViewModel, IModalDialogViewModel
{
    private readonly ISettingsProvider<AppSettingsData> _settingsProvider;
    private readonly IAppTheme _appTheme;
    private readonly IFrameworkDetectionService _frameworks;

    /// <summary>
    /// Creates a settings dialog with a working copy of the current theme and engine options.
    /// </summary>
    public SettingsViewModel(ISettingsProvider<AppSettingsData> settingsProvider, IAppTheme appTheme,
        IFrameworkDetectionService frameworks)
    {
        _settingsProvider = settingsProvider;
        _appTheme = appTheme;
        _frameworks = frameworks;
        DisplayName = "Settings";
        Theme = settingsProvider.Value.Theme;
        VapourSynthThreads = EffectiveThreads(settingsProvider.Value.VapourSynthThreads);
        EnhanceEditorWithAutoComplete = settingsProvider.Value.EnhanceEditorWithAutoComplete;
        CheckForUpdates = settingsProvider.Value.CheckForUpdates;
        FFmpegPath = settingsProvider.Value.FFmpegPath;
        ApplyDetection(_frameworks.VapourSynth, _frameworks.AviSynth);
        ThemeList.SelectedValue = Theme;
        CheckForUpdateList.SelectedValue = CheckForUpdates;
        this.WhenAnyValue(x => x.Theme).Subscribe(theme => ThemeList.SelectedValue = theme);
        this.WhenAnyValue(x => x.CheckForUpdates).Subscribe(interval => CheckForUpdateList.SelectedValue = interval);
        this.WhenAnyValue(x => x.ThemeList.SelectedValue).Subscribe(ApplyThemeChoice);
        this.WhenAnyValue(x => x.CheckForUpdateList.SelectedValue).Subscribe(ApplyUpdateChoice);
    }

    /// <inheritdoc />
    public bool? DialogResult { get; private set; }

    /// <summary>
    /// Gets or sets the theme being edited.
    /// </summary>
    [Reactive]
    public partial AppTheme Theme { get; set; }

    /// <summary>
    /// Gets the themes for display. Light, the default, is first.
    /// The combo reflects <see cref="Theme"/>.
    /// </summary>
    public ListItemCollectionView<AppTheme> ThemeList { get; } = new()
    {
        { AppTheme.Light, "Light" },
        { AppTheme.Dark, "Dark" }
    };

    /// <summary>
    /// Gets or sets when to automatically check for updates.
    /// </summary>
    [Reactive]
    public partial UpdateInterval CheckForUpdates { get; set; }

    /// <summary>
    /// Gets the update intervals for display.
    /// The combo reflects <see cref="CheckForUpdates"/>.
    /// </summary>
    public ListItemCollectionView<UpdateInterval> CheckForUpdateList { get; } = new()
    {
        { UpdateInterval.Daily, "Daily" },
        { UpdateInterval.Biweekly, "Biweekly" },
        { UpdateInterval.Weekly, "Weekly" },
        { UpdateInterval.Bimonthly, "Bimonthly" },
        { UpdateInterval.Monthly, "Monthly" },
        { UpdateInterval.Never, "Never" }
    };

    /// <summary>
    /// Gets or sets the VapourSynth worker count used when multi-threading is enabled.
    /// </summary>
    [Reactive]
    public partial int VapourSynthThreads { get; set; }

    /// <summary>
    /// Gets or sets whether the script editor offers catalog-driven assistance.
    /// </summary>
    [Reactive]
    public partial bool EnhanceEditorWithAutoComplete { get; set; }

    /// <summary>
    /// Gets or sets the FFmpeg executable. Empty uses ffmpeg on PATH.
    /// </summary>
    [Reactive]
    public partial string FFmpegPath { get; set; } = "";

    /// <summary>
    /// Gets whether VapourSynth was loaded.
    /// </summary>
    [Reactive]
    public partial bool VapourSynthFound { get; set; }

    /// <summary>
    /// Gets the VapourSynth probe caption shown beside the engine name.
    /// </summary>
    [Reactive]
    public partial string VapourSynthStatus { get; set; } = "(Not Found)";

    /// <summary>
    /// Gets the VapourSynth probe details shown as a tooltip.
    /// </summary>
    [Reactive]
    public partial string? VapourSynthStatusTip { get; set; }

    /// <summary>
    /// Gets whether AviSynth was loaded.
    /// </summary>
    [Reactive]
    public partial bool AviSynthFound { get; set; }

    /// <summary>
    /// Gets the AviSynth probe caption shown beside the engine name.
    /// </summary>
    [Reactive]
    public partial string AviSynthStatus { get; set; } = "(Not Found)";

    /// <summary>
    /// Gets the AviSynth probe details shown as a tooltip.
    /// </summary>
    [Reactive]
    public partial string? AviSynthStatusTip { get; set; }

    /// <summary>
    /// Gets the detected VapourSynth library path.
    /// </summary>
    [Reactive]
    public partial string VapourSynthLibraryDisplay { get; set; } = "";

    /// <summary>
    /// Gets the detected VapourSynth plugin folders.
    /// </summary>
    [Reactive]
    public partial string VapourSynthPluginsDisplay { get; set; } = "";

    /// <summary>
    /// Gets the detected AviSynth library path.
    /// </summary>
    [Reactive]
    public partial string AviSynthLibraryDisplay { get; set; } = "";

    /// <summary>
    /// Gets the detected AviSynth plugin folders.
    /// </summary>
    [Reactive]
    public partial string AviSynthPluginsDisplay { get; set; } = "";

    /// <summary>
    /// Applies changes without closing the window.
    /// </summary>
    public RxCommandVoid Apply => field ??= ReactiveCommand.Create(SaveSettings);

    /// <summary>
    /// Saves changes and closes the window.
    /// </summary>
    public RxCommandVoid Ok => field ??= ReactiveCommand.Create(OkImpl);

    /// <summary>
    /// Closes the window without saving.
    /// </summary>
    public RxCommandVoid Cancel => field ??= ReactiveCommand.Create(CloseView);

    /// <summary>
    /// Restores default settings in the working copy.
    /// </summary>
    public RxCommandVoid RestoreDefault => field ??= ReactiveCommand.Create(RestoreDefaultImpl);

    // Writes a chosen theme back onto the setting. The list is the display of that setting.
    private void ApplyThemeChoice(AppTheme theme) => Theme = theme;

    // Writes a chosen interval back onto the setting.
    private void ApplyUpdateChoice(UpdateInterval interval) => CheckForUpdates = interval;

    private void OkImpl()
    {
        SaveSettings();
        DialogResult = true;
        CloseView();
    }

    private void SaveSettings()
    {
        _settingsProvider.Value.Theme = Theme;
        _settingsProvider.Value.VapourSynthThreads = Math.Max(1, VapourSynthThreads);
        _settingsProvider.Value.EnhanceEditorWithAutoComplete = EnhanceEditorWithAutoComplete;
        _settingsProvider.Value.CheckForUpdates = CheckForUpdates;
        _settingsProvider.Value.FFmpegPath = FFmpegPath.Trim();
        _appTheme.RequestedTheme = Theme.ToString();
        _frameworks.Apply(_settingsProvider.Value);
        ApplyDetection(_frameworks.VapourSynth, _frameworks.AviSynth);
        _settingsProvider.Save();
    }

    private void RestoreDefaultImpl()
    {
        Theme = AppTheme.Light;
        VapourSynthThreads = Environment.ProcessorCount;
        EnhanceEditorWithAutoComplete = true;
        CheckForUpdates = UpdateInterval.Biweekly;
        FFmpegPath = "";
    }

    private void ApplyDetection(FrameworkInstall vapourSynth, FrameworkInstall aviSynth)
    {
        VapourSynthFound = vapourSynth.Found;
        AviSynthFound = aviSynth.Found;
        VapourSynthStatus = FormatStatus(vapourSynth);
        VapourSynthStatusTip = FormatStatusTip(vapourSynth);
        AviSynthStatus = FormatStatus(aviSynth);
        AviSynthStatusTip = FormatStatusTip(aviSynth);
        VapourSynthLibraryDisplay = vapourSynth.LibraryPath ?? "";
        VapourSynthPluginsDisplay = FormatPlugins(vapourSynth);
        AviSynthLibraryDisplay = aviSynth.LibraryPath ?? "";
        AviSynthPluginsDisplay = FormatPlugins(aviSynth);
    }

    private static string FormatStatus(FrameworkInstall install) => install.Status switch
    {
        FrameworkStatus.Detected when install.Version.HasText() =>
            "(Detected " + install.Version + ")",
        FrameworkStatus.Detected => "(Detected)",
        FrameworkStatus.Error => "(Error)",
        _ => "(Not Found)"
    };

    private static string? FormatStatusTip(FrameworkInstall install)
    {
        if (install.Message.HasText())
        {
            return install.Message;
        }

        if (install.VersionDetail.HasText())
        {
            return install.VersionDetail;
        }

        return install.Status == FrameworkStatus.Error
            ? "The library could not be loaded or could not run a script."
            : null;
    }

    private static string FormatPlugins(FrameworkInstall install) =>
        install.PluginDirectories is { Count: > 0 } directories ? string.Join("; ", directories) : "";

    private static int EffectiveThreads(int threads) => threads > 0 ? threads : Environment.ProcessorCount;
}
