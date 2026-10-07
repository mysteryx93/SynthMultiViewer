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
        ApplyDetection(_frameworks.VapourSynth, _frameworks.AviSynth);
    }

    /// <inheritdoc />
    public bool? DialogResult { get; private set; }

    /// <summary>
    /// Gets or sets the theme being edited.
    /// </summary>
    [Reactive]
    public partial AppTheme Theme { get; set; }

    /// <summary>
    /// Gets the list of themes for display.
    /// </summary>
    public IReadOnlyList<AppTheme> ThemeList { get; } = [AppTheme.Light, AppTheme.Dark];

    /// <summary>
    /// Gets or sets when to automatically check for updates.
    /// </summary>
    [Reactive]
    public partial UpdateInterval CheckForUpdates { get; set; }

    /// <summary>
    /// Gets the list of update intervals for display.
    /// </summary>
    public IReadOnlyList<UpdateInterval> CheckForUpdateList { get; } =
    [
        UpdateInterval.Daily,
        UpdateInterval.Biweekly,
        UpdateInterval.Weekly,
        UpdateInterval.Bimonthly,
        UpdateInterval.Monthly,
        UpdateInterval.Never
    ];

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
