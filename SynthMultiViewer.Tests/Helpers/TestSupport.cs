using System.ComponentModel;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using HanumanInstitute.MediaSynthUI;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.Avalonia;
using HanumanInstitute.MvvmDialogs.FileSystem;
using HanumanInstitute.ScriptAssist.Services;
using HanumanInstitute.SynthMultiViewer.Models;
using HanumanInstitute.SynthMultiViewer.Services;
using HanumanInstitute.SynthMultiViewer.ViewModels;

namespace HanumanInstitute.SynthMultiViewer.Tests;

internal static class TestSupport
{
    public static IDisposable Show(Window window)
    {
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return System.Reactive.Disposables.Disposable.Create(window.Close);
    }

    public static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null);
    }

    public static MainViewModel CreateMain(
        IEnvironmentService? environment = null, ISettingsProvider<AppSettingsData>? settings = null,
        IDialogManager? manager = null, IAppUpdateService? updates = null,
        IFileSystemService? files = null) =>
        new(CreateDialogs(settings: settings, manager: manager), environment ?? new TestEnvironment(),
            new MemoryDefaultScripts(), settings ?? new MemorySettingsProvider(),
            updates ?? new MemoryAppUpdateService(), files ?? new FakeFileSystemService());

    public static async Task OpenViewerAsync(MainViewModel model)
    {
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
    }

    public static void ShowViewerToolbar(MainViewModel model)
    {
        model.SelectedItem = new ViewerViewModel { Kind = ScriptKind.VapourSynth };
        Dispatcher.UIThread.RunJobs();
    }

    public static DialogService CreateDialogs(
        ISettingsProvider<AppSettingsData>? settings = null, IAppTheme? theme = null,
        IDialogManager? manager = null) => new(
        manager ?? new DialogManager(viewLocator: new ViewLocator()),
        viewModelFactory: type => CreateViewModel(type, settings, theme));

    public static HelpViewModel CreateHelp(
        IEnvironmentService? environment = null, IAppVersionClient? versions = null) =>
        new(environment ?? new TestEnvironment(), versions ?? new MemoryAppVersionClient());

    public static SettingsViewModel CreateSettings(
        ISettingsProvider<AppSettingsData>? settings = null,
        IAppTheme? theme = null,
        IFrameworkDetectionService? detection = null,
        IDialogService? dialogs = null,
        IFileSystemService? files = null) =>
        new(
            settings ?? new MemorySettingsProvider(),
            theme ?? new MemoryAppTheme(),
            detection ?? new MemoryFrameworkDetection(),
            dialogs ?? CreateDialogs(),
            files ?? new FakeFileSystemService());

    public static object CreateViewModel(
        Type type, ISettingsProvider<AppSettingsData>? settings = null, IAppTheme? theme = null)
    {
        if (type == typeof(SettingsViewModel))
        {
            return CreateSettings(settings, theme);
        }

        if (type == typeof(HelpViewModel))
        {
            return CreateHelp();
        }

        return Activator.CreateInstance(type)!;
    }

    public sealed class ScriptedDialogManager : DialogManager
    {
        public ScriptedDialogManager() : base(viewLocator: new ViewLocator()) { }

        public IModalDialogViewModel? LastDialog { get; private set; }
        public INotifyPropertyChanged? LastShown { get; private set; }
        public Action<IModalDialogViewModel>? OnShow { get; set; }

        public override void Show(INotifyPropertyChanged? ownerViewModel, INotifyPropertyChanged viewModel)
        {
            LastShown = viewModel;
            LastDialog = viewModel as IModalDialogViewModel;
        }

        public override Task ShowDialogAsync(
            INotifyPropertyChanged ownerViewModel, IModalDialogViewModel viewModel)
        {
            LastDialog = viewModel;
            OnShow?.Invoke(viewModel);
            return Task.CompletedTask;
        }
    }

    public sealed class FakeDialogManager : DialogManager
    {
        public FakeDialogManager() : base(viewLocator: new ViewLocator()) { }

        private object? NextFrameworkResult { get; set; }
        private readonly Queue<object?> _queued = new();
        public object? LastFrameworkSettings { get; private set; }
        public int FrameworkDialogCount { get; private set; }

        public void ReturnFile(string? path) =>
            NextFrameworkResult = path == null
                ? Array.Empty<IDialogStorageFile>()
                : new IDialogStorageFile[] { new DesktopDialogStorageFile(path) };

        public void ReturnFolder(string? path) =>
            NextFrameworkResult = path == null
                ? Array.Empty<IDialogStorageFolder>()
                : new IDialogStorageFolder[] { new DesktopDialogStorageFolder(path) };

        public void QueueFrameworkResult(object? result) => _queued.Enqueue(result);

        public override Task<object?> ShowFrameworkDialogAsync<TSettings>(
            INotifyPropertyChanged? ownerViewModel, TSettings settings, Func<object?, string>? resultToString = null)
        {
            LastFrameworkSettings = settings;
            FrameworkDialogCount++;
            return Task.FromResult(_queued.Count > 0 ? _queued.Dequeue() : NextFrameworkResult);
        }
    }

    public sealed class OwnerDialogManager(Window owner) : DialogManager(viewLocator: new ViewLocator())
    {
        public override IView? FindViewByViewModel(INotifyPropertyChanged model) =>
            ReferenceEquals(owner.DataContext, model) ? owner.AsWrapper() : base.FindViewByViewModel(model);
    }

    public sealed class MemoryAppVersionClient : IAppVersionClient
    {
        public AppVersionInfo? Result { get; set; }
        public int QueryCount { get; private set; }

        public Task<AppVersionInfo?> QueryVersionAsync()
        {
            QueryCount++;
            return Task.FromResult(Result);
        }
    }

    public sealed class MemoryAppUpdateService : IAppUpdateService
    {
        public int CheckCount { get; private set; }

        public Task CheckForUpdatesAsync(INotifyPropertyChanged owner)
        {
            CheckCount++;
            return Task.CompletedTask;
        }
    }

    public sealed class MemoryProcessService : IProcessService
    {
        public string? LastUrl { get; private set; }

        public void OpenBrowserUrl(string url) => LastUrl = url;
    }

    public sealed class TestEnvironment(IReadOnlyList<string>? arguments = null) : IEnvironmentService
    {
        public IReadOnlyList<string> CommandLineArguments { get; } = arguments ?? ["viewer"];
        public Version AppVersion { get; set; } = new(1, 2, 3);
        public string ApplicationDataPath { get; } = "/appdata";
        public DateTime Now { get; set; } = new(2026, 1, 15);
    }

    public sealed class MemorySettingsProvider : ISettingsProvider<AppSettingsData>
    {
        public AppSettingsData Value { get; set; } = new();
        public int SaveCount { get; private set; }
#pragma warning disable 67
        public event EventHandler? Changed;
        public event EventHandler? Saving;
#pragma warning restore 67
        public AppSettingsData Load() => Value;
        public AppSettingsData Load(string path) => Value;
        public void Save()
        {
            Saving?.Invoke(this, EventArgs.Empty);
            SaveCount++;
        }
        public void Save(string path) => Save();
    }

    public sealed class MemoryAppTheme : IAppTheme
    {
        public string RequestedTheme { get; set; } = "Light";
    }

    public sealed class MemoryFrameworkDetection : IFrameworkDetectionService
    {
        public FrameworkInstall VapourSynth { get; init; } = new(false);
        public FrameworkInstall AviSynth { get; init; } = new(false);
        public AppSettingsData? LastApplied { get; private set; }

        public void Apply(AppSettingsData settings) => LastApplied = settings;
    }

    public sealed class MemoryDefaultScripts : IDefaultScriptService
    {
        public string VapourSynth { get; set; } = "import vapoursynth as vs\ncore = vs.core\nclip = core.std.BlankClip()\nclip.set_output()\n";
        public string AviSynth { get; set; } = "BlankClip()\n";
    }

}
