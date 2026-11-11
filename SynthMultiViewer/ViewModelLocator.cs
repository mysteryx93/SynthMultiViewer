using System.IO.Abstractions;
using HanumanInstitute.FFmpeg;
using HanumanInstitute.ScriptAssist.Services;
using HanumanInstitute.SynthMultiViewer.Models;
using HanumanInstitute.SynthMultiViewer.Services;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.Avalonia;
using Microsoft.Extensions.Options;
using Splat;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Registers application services and resolves view models.
/// </summary>
public static class ViewModelLocator
{
    static ViewModelLocator()
    {
        var container = Locator.CurrentMutable;

        container.Register<IDialogService>(() => new DialogService(
            new DialogManager(
                viewLocator: new ViewLocator(),
                dialogFactory: new DialogFactory().AddMessageBox()),
            viewModelFactory: t => Locator.Current.GetService(t)));

        SplatRegistrations.RegisterLazySingleton<IEnvironmentService, EnvironmentService>();
        SplatRegistrations.RegisterLazySingleton<IFileSystem, FileSystem>();
        SplatRegistrations.RegisterLazySingleton<IFileSystemService, FileSystemService>();
        SplatRegistrations.RegisterLazySingleton<ISerializationService, SerializationService>();
        SplatRegistrations.RegisterLazySingleton<IAppPathService, AppPathService>();
        SplatRegistrations.RegisterLazySingleton<ISettingsProvider<AppSettingsData>, AppSettingsProvider>();
        SplatRegistrations.RegisterLazySingleton<IAppTheme, AppThemeService>();
        SplatRegistrations.RegisterLazySingleton<IDefaultScriptService, DefaultScriptService>();
        SplatRegistrations.RegisterLazySingleton<IScriptLanguageFactory, ScriptAssistService>();
        SplatRegistrations.RegisterLazySingleton<IFrameworkDetectionService, FrameworkDetectionService>();
        container.Register(() => new HttpClient { Timeout = TimeSpan.FromSeconds(10) });
        SplatRegistrations.RegisterLazySingleton<IAppVersionClient, AppVersionClient>();
        SplatRegistrations.RegisterLazySingleton<IProcessService, ProcessService>();
        SplatRegistrations.RegisterLazySingleton<IAppUpdateService, AppUpdateService>();
        SplatRegistrations.Register<MainViewModel>();
        SplatRegistrations.Register<HelpViewModel>();
        SplatRegistrations.Register<InputViewModel>();
        SplatRegistrations.Register<SettingsViewModel>();
        SplatRegistrations.Register<VideoPropertiesViewModel>();
        SplatRegistrations.Register<FunctionsExplorerViewModel>();
        SplatRegistrations.Register<EncodeViewModel>();
        SplatRegistrations.Register<SavePresetViewModel>();
        SplatRegistrations.Register<EncodeProgressViewModel>();
        SplatRegistrations.Register<EncodeErrorViewModel>();
        SplatRegistrations.Register<BenchmarkViewModel>();
        SplatRegistrations.Register<TabColorViewModel>();
        SplatRegistrations.Register<EditorViewModel>();
        SplatRegistrations.Register<ViewerViewModel>();

        // FFmpeg
        container.RegisterLazySingleton<IEncoderService>(() => new EncoderService(
            Locator.Current.GetService<IProcessManager>().CheckNotNull(),
            Locator.Current.GetService<IUserInterfaceManager>().CheckNotNull()));
        SplatRegistrations.RegisterConstant(Options.Create(new AppPaths()));
        SplatRegistrations.RegisterLazySingleton<IProcessManager, ProcessManager>();
        SplatRegistrations.RegisterLazySingleton<IUserInterfaceManager, FFmpegUserInterfaceManager>();
        SplatRegistrations.Register<IMediaEncoder, MediaEncoder>();
        SplatRegistrations.Register<IMediaMuxer, MediaMuxer>();
        SplatRegistrations.Register<IMediaInfoReader, MediaInfoReader>();
        SplatRegistrations.Register<IMediaScript, MediaScript>();

        SplatRegistrations.SetupIOC();
    }

    /// <summary>
    /// Creates the main workspace.
    /// </summary>
    public static MainViewModel Main => Locator.Current.GetService<MainViewModel>()!;
    /// <summary>
    /// Creates the application information dialog model.
    /// </summary>
    public static HelpViewModel Help => Locator.Current.GetService<HelpViewModel>()!;
    /// <summary>
    /// Creates a text input dialog model.
    /// </summary>
    public static InputViewModel Input => Locator.Current.GetService<InputViewModel>()!;
    /// <summary>
    /// Creates the settings dialog model.
    /// </summary>
    public static SettingsViewModel Settings => Locator.Current.GetService<SettingsViewModel>()!;
}
