using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HanumanInstitute.ScriptAssist.Services;
using HanumanInstitute.SynthMultiViewer.Helpers;
using HanumanInstitute.SynthMultiViewer.Models;
using HanumanInstitute.SynthMultiViewer.Services;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Manages script tabs, shared viewer settings, and application commands.
/// </summary>
public partial class MainViewModel : WorkspaceViewModel, IViewLoaded, IViewClosed, IViewClosing
{
    private readonly IDialogService _dialogService;
    private readonly IEnvironmentService _environmentService;
    private readonly IDefaultScriptService _defaultScripts;
    private readonly ISettingsProvider<AppSettingsData> _settings;
    private readonly IAppUpdateService _appUpdate;
    private readonly IFileSystemService _files;
    private const int RecentFileLimit = 8;
    private double _scrollHorizontalOffset;
    private double _scrollVerticalOffset;
    private TimeSpan _playerPosition;
    private IScriptViewModel? _previousItem;
    private VideoPropertiesViewModel? _properties;
    private readonly VideoPropertiesPlacement _propertiesPlacement = new();
    private FunctionsExplorerViewModel? _explorer;
    private readonly FunctionsExplorerPlacement _explorerPlacement = new();
    private readonly HashSet<IScriptViewModel> _closing = [];
    private bool _loaded;

    /// <summary>
    /// Creates the workspace with dialog and environment services.
    /// </summary>
    public MainViewModel(
        IDialogService dialogService,
        IEnvironmentService environmentService,
        IDefaultScriptService defaultScripts,
        ISettingsProvider<AppSettingsData> settings,
        IAppUpdateService appUpdate,
        IFileSystemService files)
    {
        _dialogService = dialogService;
        _environmentService = environmentService;
        _defaultScripts = defaultScripts;
        _settings = settings;
        _appUpdate = appUpdate;
        _files = files.CheckNotNull();
        DisplayName = "Synth Multi-Viewer";
        CanClose = false;

        _settings.Saving += (_, _) => OnSettingsChanged();
        _settings.Changed += (_, _) => OnSettingsChanged();
        ScriptList.CollectionChanged += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(IsStartVisible));
            if (ScriptList.Count == 0)
            {
                RefreshRecents();
            }
        };
        RefreshRecents();
        this.WhenAnyValue(x => x.IsMultiThreaded)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(Threads)));

        this.WhenAnyValue(x => x.Zoom)
            .Subscribe(value =>
            {
                ZoomScaleToFit = value == 0.0;
                if (value == 0.0)
                {
                    return;
                }

                var coerced = value.Clamp(MinZoom, MaxZoom);
                if (Math.Abs(coerced - value) > double.Epsilon)
                {
                    Zoom = coerced;
                }
            });

        this.WhenAnyValue(x => x.SelectedItem)
            .Subscribe(OnSelectedItemChanged);
        this.WhenAnyValue(x => x.IsPropertiesOpen)
            .Subscribe(OnPropertiesOpenChanged);
        this.WhenAnyValue(x => x.IsFunctionsExplorerOpen)
            .Subscribe(OnFunctionsExplorerOpenChanged);
    }

    /// <summary>
    /// Gets the current application settings, including restored window bounds.
    /// </summary>
    public AppSettingsData AppSettings => _settings.Value;

    /// <summary>
    /// Gets the open tabs in strip order.
    /// </summary>
    public ObservableCollection<IScriptViewModel> ScriptList { get; } = [];

    /// <summary>
    /// Gets whether the empty-canvas start surface is shown.
    /// </summary>
    public bool IsStartVisible => ScriptList.Count == 0;

    /// <summary>
    /// Gets recently opened scripts for the start surface.
    /// </summary>
    public ObservableCollection<RecentFileItem> Recents { get; } = [];

    /// <summary>
    /// Gets whether the start surface has recent files to show.
    /// </summary>
    public bool HasRecents => Recents.Count > 0;

    /// <summary>
    /// Gets the minimum zoom factor.
    /// </summary>
    public double MinZoom { get; } = 0.1;
    /// <summary>
    /// Gets the maximum zoom factor.
    /// </summary>
    public double MaxZoom { get; } = 10;
    /// <summary>
    /// Gets the multiplier used for each zoom step.
    /// </summary>
    public double ZoomIncrement { get; } = 1.2;
    /// <summary>
    /// Gets the zoom choices shown in the toolbar.
    /// </summary>
    public IReadOnlyList<string> ZoomList { get; } =
        ["Scale to Fit", "20%", "50%", "70%", "100%", "150%", "200%", "400%"];

    /// <summary>
    /// Gets or sets the active tab.
    /// </summary>
    [Reactive]
    public partial IScriptViewModel? SelectedItem { get; set; }

    /// <summary>
    /// Gets whether the selected tab is a script editor.
    /// </summary>
    public bool IsEditorSelected => SelectedItem is IEditorViewModel;

    /// <summary>
    /// Gets whether the selected tab is a script viewer.
    /// </summary>
    public bool IsViewerSelected => SelectedItem is IViewerViewModel;

    /// <summary>
    /// Gets whether the selected tab is a VapourSynth viewer.
    /// </summary>
    public bool IsVapourSynthViewerSelected =>
        SelectedItem is IViewerViewModel { Kind: ScriptKind.VapourSynth };

    /// <summary>
    /// Gets or sets the zoom factor; zero selects scale-to-fit mode.
    /// </summary>
    [Reactive]
    public partial double Zoom { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether frames fit the available viewer area.
    /// </summary>
    [Reactive]
    public partial bool ZoomScaleToFit { get; set; }

    /// <summary>
    /// Gets or sets whether frame requests use multiple worker threads.
    /// </summary>
    [Reactive]
    public partial bool IsMultiThreaded { get; set; }

    /// <summary>
    /// Gets or sets whether frames use nearest-neighbor interpolation.
    /// </summary>
    [Reactive]
    public partial bool SquarePixels { get; set; }

    /// <summary>
    /// Gets or sets whether the video properties window is open.
    /// </summary>
    [Reactive]
    public partial bool IsPropertiesOpen { get; set; }

    /// <summary>
    /// Gets or sets whether the functions explorer window is open.
    /// </summary>
    [Reactive]
    public partial bool IsFunctionsExplorerOpen { get; set; }

    /// <summary>
    /// Gets the requested worker count; one when multi-threading is off.
    /// </summary>
    public int Threads
    {
        get
        {
            if (!IsMultiThreaded)
            {
                return 1;
            }

            var count = _settings.Value.VapourSynthThreads;
            return count > 0 ? count : Environment.ProcessorCount;
        }
    }

    /// <summary>
    /// Loads command-line scripts once; an empty workspace shows the start surface.
    /// </summary>
    public RxCommandVoid Load => field ??= ReactiveCommand.CreateFromTask(LoadedAsync);
    /// <summary>
    /// Opens the local file paths supplied by a drop operation.
    /// </summary>
    public ReactiveCommand<IEnumerable<string>, RxVoid> DropFiles => field ??= ReactiveCommand.CreateFromTask<IEnumerable<string>>(DropFilesAsync);
    /// <summary>
    /// Restores the shared position in the viewer that loaded its media.
    /// </summary>
    public ReactiveCommand<IViewerViewModel, RxVoid> MediaLoaded => field ??= ReactiveCommand.Create<IViewerViewModel>(ViewerMediaLoaded);

    /// <summary>
    /// Creates a VapourSynth editor with the default script.
    /// </summary>
    public RxCommandVoid New => field ??= ReactiveCommand.Create(NewImpl);
    /// <summary>
    /// Creates an AviSynth editor with the default script.
    /// </summary>
    public RxCommandVoid NewAviSynth => field ??= ReactiveCommand.Create(NewAviSynthImpl);
    /// <summary>
    /// Prompts for a script file and opens it in an editor.
    /// </summary>
    public RxCommandVoid Open => field ??= ReactiveCommand.CreateFromTask(OpenImplAsync);
    /// <summary>
    /// Opens a recent script from the start surface.
    /// </summary>
    public ReactiveCommand<string, RxVoid> OpenRecent =>
        field ??= ReactiveCommand.CreateFromTask<string>(OpenRecentAsync);
    /// <summary>
    /// Saves the selected editor, prompting for a path if needed.
    /// </summary>
    public RxCommandVoid Save => field ??= ReactiveCommand.CreateFromTask(SaveImplAsync, WhenEditorSelected);
    /// <summary>
    /// Saves the selected editor to a chosen path.
    /// </summary>
    public RxCommandVoid SaveAs => field ??= ReactiveCommand.CreateFromTask(SaveAsImplAsync, WhenEditorSelected);
    /// <summary>
    /// Opens the selected script in a new viewer.
    /// </summary>
    public RxCommandVoid Run => field ??= ReactiveCommand.Create(RunImpl, WhenEditorSelected);
    /// <summary>
    /// Prompts for a frame index and seeks the selected viewer.
    /// </summary>
    public RxCommandVoid GoTo => field ??= ReactiveCommand.CreateFromTask(GoToImplAsync, WhenViewerSelected);
    /// <summary>
    /// Copies the selected viewer's current frame to the clipboard.
    /// </summary>
    public ReactiveCommand<object?, RxVoid> CopyFrame => field ??= ReactiveCommand.CreateFromTask<object?>(CopyFrameImplAsync, WhenViewerSelected);
    /// <summary>
    /// Toggles the modeless video properties window.
    /// </summary>
    public RxCommandVoid Properties => field ??= ReactiveCommand.Create(PropertiesImpl, WhenViewerSelected);
    /// <summary>
    /// Toggles the modeless functions explorer window.
    /// </summary>
    public RxCommandVoid FunctionsExplorer =>
        field ??= ReactiveCommand.Create(FunctionsExplorerImpl, WhenEditorSelected);
    /// <summary>
    /// Moves the selected viewer by the supplied frame count.
    /// </summary>
    public ReactiveCommand<object?, RxVoid> Seek => field ??= ReactiveCommand.Create<object?>(SeekImpl, WhenViewerSelected);
    /// <summary>
    /// Toggles play and pause on the selected viewer.
    /// </summary>
    public RxCommandVoid PlayPause => field ??= ReactiveCommand.Create(PlayPauseImpl, WhenViewerSelected);
    /// <summary>
    /// Toggles parallel frame requests.
    /// </summary>
    public RxCommandVoid ToggleMultiThreaded => field ??= ReactiveCommand.Create(() => { IsMultiThreaded = !IsMultiThreaded; });
    /// <summary>
    /// Toggles nearest-neighbor interpolation.
    /// </summary>
    public RxCommandVoid ToggleSquarePixels => field ??= ReactiveCommand.Create(() => { SquarePixels = !SquarePixels; });
    /// <summary>
    /// Copies the shared frame position to the other viewers.
    /// </summary>
    public RxCommandVoid UpdateAll => field ??= ReactiveCommand.Create(UpdateAllImpl, WhenViewerSelected);
    /// <summary>
    /// Increases the zoom factor by one step.
    /// </summary>
    public RxCommandVoid ZoomIn => field ??= ReactiveCommand.Create(ZoomInImpl);
    /// <summary>
    /// Decreases the zoom factor by one step.
    /// </summary>
    public RxCommandVoid ZoomOut => field ??= ReactiveCommand.Create(ZoomOutImpl);
    /// <summary>
    /// Opens application information and keyboard shortcuts.
    /// </summary>
    public RxCommandVoid Help => field ??= ReactiveCommand.CreateFromTask(HelpImplAsync);
    /// <summary>
    /// Opens the settings dialog.
    /// </summary>
    public RxCommandVoid Settings => field ??= ReactiveCommand.CreateFromTask(SettingsImplAsync);
    /// <summary>
    /// Begins renaming the selected tab.
    /// </summary>
    public RxCommandVoid Rename => field ??= ReactiveCommand.Create(RenameImpl,
        this.WhenAnyValue(x => x.SelectedItem).Select(x => x?.CanEditHeader == true));
    /// <summary>
    /// Opens a color picker for the selected tab.
    /// </summary>
    public RxCommandVoid ChangeTabColor => field ??= ReactiveCommand.CreateFromTask(
        ChangeTabColorImplAsync,
        this.WhenAnyValue(x => x.SelectedItem).Select(_ => ScriptList.Count > 0));
    /// <summary>
    /// Selects a tab by its zero-based strip index, supplied as an integer or string.
    /// </summary>
    public ReactiveCommand<object?, RxVoid> SelectTab => field ??= ReactiveCommand.Create<object?>(SelectTabImpl);
    /// <summary>
    /// Selects the next tab, wrapping from last to first.
    /// </summary>
    public RxCommandVoid NextTab => field ??= ReactiveCommand.Create(NextTabImpl,
        this.WhenAnyValue(x => x.SelectedItem).Select(_ => ScriptList.Count > 0));
    /// <summary>
    /// Selects the previous tab, wrapping from first to last.
    /// </summary>
    public RxCommandVoid PreviousTab => field ??= ReactiveCommand.Create(PreviousTabImpl,
        this.WhenAnyValue(x => x.SelectedItem).Select(_ => ScriptList.Count > 0));
    /// <summary>
    /// Moves the selected tab one place left.
    /// </summary>
    public RxCommandVoid MoveTabLeft => field ??= ReactiveCommand.Create(MoveTabLeftImpl, WhenMultipleTabs);
    /// <summary>
    /// Moves the selected tab one place right.
    /// </summary>
    public RxCommandVoid MoveTabRight => field ??= ReactiveCommand.Create(MoveTabRightImpl, WhenMultipleTabs);

    private IObservable<bool> WhenEditorSelected =>
        this.WhenAnyValue(x => x.SelectedItem).Select(x => x is IEditorViewModel);

    private IObservable<bool> WhenViewerSelected =>
        this.WhenAnyValue(x => x.SelectedItem).Select(x => x is IViewerViewModel);

    private IObservable<bool> WhenMultipleTabs =>
        Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                h => ScriptList.CollectionChanged += h,
                h => ScriptList.CollectionChanged -= h)
            .Select(_ => RxVoid.Default)
            .StartWith(RxVoid.Default)
            .Select(_ => ScriptList.Count > 1);

    private void OnSelectedItemChanged(IScriptViewModel? value)
    {
        if (_previousItem is IViewerViewModel { ErrorMessage: null } oldViewer)
        {
            _playerPosition = oldViewer.Position;
            _scrollHorizontalOffset = oldViewer.ScrollHorizontalOffset;
            _scrollVerticalOffset = oldViewer.ScrollVerticalOffset;
        }

        _previousItem = value;

        foreach (var item in ScriptList)
        {
            item.IsActive = item == value;
        }

        if (value is IViewerViewModel newViewer)
        {
            if (newViewer.ErrorMessage == null)
            {
                newViewer.Position = _playerPosition;
                newViewer.ScrollHorizontalOffset = _scrollHorizontalOffset;
                newViewer.ScrollVerticalOffset = _scrollVerticalOffset;
            }
            else
            {
                newViewer.Position = TimeSpan.Zero;
            }
        }

        this.RaisePropertyChanged(nameof(IsEditorSelected));
        this.RaisePropertyChanged(nameof(IsViewerSelected));
        this.RaisePropertyChanged(nameof(IsVapourSynthViewerSelected));
        if (_properties != null)
        {
            _properties.Viewer = value as IViewerViewModel;
        }

        if (_explorer == null)
        {
            return;
        }

        if (value is IEditorViewModel editor)
        {
            _explorer.Editor = editor;
            return;
        }

        IsFunctionsExplorerOpen = false;
    }

    private void NewImpl() => AddEditor(_defaultScripts.VapourSynth, ScriptKind.VapourSynth);

    private void NewAviSynthImpl() => AddEditor(_defaultScripts.AviSynth, ScriptKind.AviSynth);

    private void AddEditor(string script, ScriptKind kind)
    {
        var editor = _dialogService.CreateViewModel<EditorViewModel>();
        editor.Kind = kind;
        editor.Script = script;
        editor.MarkSaved();
        var index = TabAutoNumber.Next(ScriptList.Select(x => x.DisplayName), "Script");
        editor.Index = index;
        AddTab(editor, "Script " + index);
    }

    private async Task OpenImplAsync()
    {
        var settings = new OpenFileDialogSettings
        {
            Filters =
            {
                new("VapourSynth Script", ScriptKindLookup.FileFilterExtensions(ScriptKind.VapourSynth)),
                new("AviSynth Script", ScriptKindLookup.FileFilterExtensions(ScriptKind.AviSynth)),
                new("All files", "*")
            }
        };
        var file = await _dialogService.ShowOpenFileDialogAsync(this, settings);
        if (file != null)
        {
            await ReadScriptFileAsync(file.LocalPath);
        }
    }

    private async Task SaveImplAsync()
    {
        if (SelectedItem is IEditorViewModel item)
        {
            await SaveEditorAsync(item);
        }
    }

    private async Task SaveAsImplAsync()
    {
        if (SelectedItem is IEditorViewModel item)
        {
            await SaveAsEditorAsync(item);
        }
    }

    private async Task SaveEditorAsync(IEditorViewModel item)
    {
        if (item.FileName == null)
        {
            await SaveAsEditorAsync(item);
            return;
        }

        await _files.File.WriteAllTextAsync(item.FileName, item.Script);
        item.MarkSaved();
        RememberFile(item.FileName);
    }

    private async Task SaveAsEditorAsync(IEditorViewModel item)
    {
        var vapoursynth = new FileFilter("VapourSynth Script",
            ScriptKindLookup.FileFilterExtensions(ScriptKind.VapourSynth));
        var avisynth = new FileFilter("AviSynth Script",
            ScriptKindLookup.FileFilterExtensions(ScriptKind.AviSynth));
        var all = new FileFilter("All files", "*");
        var settings = new SaveFileDialogSettings
        {
            DefaultExtension = ScriptKindLookup.DefaultExtension(item.Kind),
            Filters = item.Kind == ScriptKind.AviSynth
                ? [avisynth, vapoursynth, all]
                : [vapoursynth, avisynth, all]
        };
        var file = await _dialogService.ShowSaveFileDialogAsync(this, settings);
        if (file == null)
        {
            return;
        }

        await _files.File.WriteAllTextAsync(file.LocalPath, item.Script);
        item.FileName = file.LocalPath;
        item.DisplayName = _files.Path.GetFileName(item.FileName);
        item.Kind = ScriptKindLookup.FromPath(item.FileName) ?? item.Kind;
        item.MarkSaved();
        RememberFile(item.FileName);
    }

    private void RunImpl()
    {
        if (SelectedItem is not IEditorViewModel editor) { return; }

        var viewer = _dialogService.CreateViewModel<ViewerViewModel>();
        viewer.Kind = editor.Kind;
        viewer.FileName = editor.FileName;
        viewer.Script = editor.Script;
        var index = TabAutoNumber.Next(ScriptList.Select(x => x.DisplayName), "Viewer");
        viewer.Index = index;
        AddTab(viewer, "Viewer " + index);
    }

    private async Task GoToImplAsync()
    {
        if (SelectedItem is not IViewerViewModel viewer) { return; }

        var last = (int)viewer.Duration.TotalSeconds + 1;
        var result = await RequestInputAsync(
            "Go To Frame...",
            "Enter frame number:",
            ((int)viewer.Position.TotalSeconds + 1).ToString(CultureInfo.InvariantCulture),
            text => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frame) &&
                    frame >= 1 && frame <= last);
        if (result != null && int.TryParse(result, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) &&
            number >= 1 && number <= last)
        {
            viewer.Position = TimeSpan.FromSeconds(number - 1);
        }
    }

    private void PropertiesImpl() => IsPropertiesOpen = !IsPropertiesOpen;

    private void FunctionsExplorerImpl() => IsFunctionsExplorerOpen = !IsFunctionsExplorerOpen;

    private void OnFunctionsExplorerOpenChanged(bool open)
    {
        if (open)
        {
            OpenFunctionsExplorerWindow();
            return;
        }

        CloseFunctionsExplorerWindow();
    }

    private void OpenFunctionsExplorerWindow()
    {
        if (_explorer != null)
        {
            _explorer.Editor = SelectedItem as IEditorViewModel;
            _dialogService.Activate(_explorer);
            return;
        }

        _explorer = _dialogService.CreateViewModel<FunctionsExplorerViewModel>();
        _explorer.Placement = _explorerPlacement;
        _explorer.Editor = SelectedItem as IEditorViewModel;
        ApplyExplorerDefaultPlacement();
        _explorer.RequestClose += FunctionsExplorerOnRequestClose;
        _dialogService.Show(this, _explorer);
    }

    private void ApplyExplorerDefaultPlacement()
    {
        if (_explorerPlacement.Left is not null || _explorerPlacement.Top is not null)
        {
            return;
        }

        if (_dialogService.DialogManager.FindViewByViewModel(this)?.RefObj is not Window owner)
        {
            return;
        }

        var position = VideoPropertiesPlacement.AlignToOwnerRight(
            owner.Position, VideoPropertiesPlacement.OwnerFrameSize(owner),
            VideoPropertiesPlacement.ChildFrameSize(
                owner, _explorerPlacement.Width, _explorerPlacement.Height));
        _explorerPlacement.Left = position.X;
        _explorerPlacement.Top = position.Y;
    }

    private void CloseFunctionsExplorerWindow()
    {
        if (_explorer == null)
        {
            return;
        }

        var explorer = _explorer;
        explorer.RequestClose -= FunctionsExplorerOnRequestClose;
        _dialogService.Close(explorer);
        explorer.Editor = null;
        _explorer = null;
    }

    private void FunctionsExplorerOnRequestClose(object? sender, EventArgs e)
    {
        if (_explorer == null)
        {
            return;
        }

        _explorer.RequestClose -= FunctionsExplorerOnRequestClose;
        _explorer.Editor = null;
        _explorer = null;
        IsFunctionsExplorerOpen = false;
    }

    private void OnPropertiesOpenChanged(bool open)
    {
        if (open)
        {
            OpenPropertiesWindow();
            return;
        }

        ClosePropertiesWindow();
    }

    private void OpenPropertiesWindow()
    {
        if (_properties != null)
        {
            _properties.Viewer = SelectedItem as IViewerViewModel;
            _dialogService.Activate(_properties);
            return;
        }

        _properties = _dialogService.CreateViewModel<VideoPropertiesViewModel>();
        _properties.Placement = _propertiesPlacement;
        _properties.Viewer = SelectedItem as IViewerViewModel;
        ApplyDefaultPlacement();
        _properties.RequestClose += PropertiesOnRequestClose;
        _dialogService.Show(this, _properties);
    }

    private void ApplyDefaultPlacement()
    {
        if (_propertiesPlacement.Left is not null || _propertiesPlacement.Top is not null)
        {
            return;
        }

        if (_dialogService.DialogManager.FindViewByViewModel(this)?.RefObj is not Window owner)
        {
            return;
        }

        var position = VideoPropertiesPlacement.AlignToOwnerRight(
            owner.Position, VideoPropertiesPlacement.OwnerFrameSize(owner),
            VideoPropertiesPlacement.ChildFrameSize(
                owner, _propertiesPlacement.Width, _propertiesPlacement.Height));
        _propertiesPlacement.Left = position.X;
        _propertiesPlacement.Top = position.Y;
    }

    private void ClosePropertiesWindow()
    {
        if (_properties == null)
        {
            return;
        }

        var properties = _properties;
        properties.RequestClose -= PropertiesOnRequestClose;
        _dialogService.Close(properties);
        properties.Viewer = null;
        _properties = null;
    }

    private void PropertiesOnRequestClose(object? sender, EventArgs e)
    {
        if (_properties == null)
        {
            return;
        }

        _properties.RequestClose -= PropertiesOnRequestClose;
        _properties.Viewer = null;
        _properties = null;
        IsPropertiesOpen = false;
    }

    private async Task CopyFrameImplAsync(object? parameter)
    {
        if (SelectedItem is not IViewerViewModel) { return; }

        var host = (parameter as Visual)?.GetVisualDescendants().OfType<SynthPlayerHost>()
            .FirstOrDefault(x => x.IsEffectivelyVisible);
        if (host is null) { return; }

        await host.CopyFrameToClipboardAsync();
    }

    private void SeekImpl(object? parameter)
    {
        if (SelectedItem is not IViewerViewModel viewer) { return; }
        if (!TryIndex(parameter, out var frames)) { return; }

        var last = Math.Max(0, (int)viewer.Duration.TotalSeconds);
        var next = ((int)viewer.Position.TotalSeconds + frames).Clamp(0, last);
        viewer.Position = TimeSpan.FromSeconds(next);
    }

    private void PlayPauseImpl()
    {
        if (SelectedItem is IViewerViewModel viewer)
        {
            viewer.IsPlaying = !viewer.IsPlaying;
        }
    }

    private void UpdateAllImpl()
    {
        if (SelectedItem is IViewerViewModel viewer)
        {
            _playerPosition = viewer.Position;
        }

        foreach (var item in ScriptList.OfType<IViewerViewModel>())
        {
            if (item != SelectedItem)
            {
                item.Position = _playerPosition;
            }
        }
    }

    private static bool TryIndex(object? parameter, out int index)
    {
        switch (parameter)
        {
            case int i:
                index = i;
                return true;
            case string s when int.TryParse(s, out var parsed):
                index = parsed;
                return true;
            default:
                index = -1;
                return false;
        }
    }

    private void SelectTabImpl(object? parameter)
    {
        if (!TryIndex(parameter, out var index)) { return; }

        if (index >= 0 && index < ScriptList.Count)
        {
            SelectedItem = ScriptList[index];
        }
    }

    private void NextTabImpl()
    {
        if (ScriptList.Count == 0) { return; }

        var pos = SelectedItem == null ? -1 : ScriptList.IndexOf(SelectedItem);
        SelectedItem = ScriptList[(pos + 1) % ScriptList.Count];
    }

    private void PreviousTabImpl()
    {
        if (ScriptList.Count == 0) { return; }

        var pos = SelectedItem == null ? 0 : ScriptList.IndexOf(SelectedItem);
        if (pos < 0)
        {
            pos = 0;
        }

        SelectedItem = ScriptList[(pos - 1 + ScriptList.Count) % ScriptList.Count];
    }

    private void MoveTabLeftImpl() => MoveSelectedTab(-1);

    private void MoveTabRightImpl() => MoveSelectedTab(1);

    private void MoveSelectedTab(int delta)
    {
        var item = SelectedItem;
        if (!TabOrder.TryMove(ScriptList, item, delta) || item is null)
        {
            return;
        }

        SelectedItem = item;
    }

    private void ZoomInImpl()
    {
        if (ZoomScaleToFit)
        {
            Zoom = 1;
            ZoomScaleToFit = false;
        }
        else
        {
            Zoom *= ZoomIncrement;
        }
    }

    private void ZoomOutImpl()
    {
        if (ZoomScaleToFit)
        {
            Zoom = 1;
            ZoomScaleToFit = false;
        }
        else
        {
            Zoom /= ZoomIncrement;
        }
    }

    private async Task HelpImplAsync() =>
        await _dialogService.ShowDialogAsync(this, _dialogService.CreateViewModel<HelpViewModel>());

    private async Task SettingsImplAsync() =>
        await _dialogService.ShowDialogAsync(this, _dialogService.CreateViewModel<SettingsViewModel>());

    private void RenameImpl()
    {
        if (SelectedItem is not { } item) { return; }

        ((ICommand)item.BeginHeaderEdit).Execute(null);
    }

    private async Task ChangeTabColorImplAsync()
    {
        var tab = SelectedItem;
        if (tab is null)
        {
            return;
        }

        var picker = _dialogService.CreateViewModel<TabColorViewModel>();
        var viewer = tab is IViewerViewModel;
        var theme = _settings.Value.Theme;
        picker.Load(
            TabColors.For(tab.Kind, viewer, theme, tab.TabColor),
            tab.TabColor is null,
            TabColors.For(tab.Kind, viewer, theme));
        if (await _dialogService.ShowDialogAsync(this, picker) == true)
        {
            tab.TabColor = picker.Result;
        }
    }

    /// <inheritdoc />
    public async void OnLoaded() => await LoadedAsync();

    /// <inheritdoc />
    public void OnClosed()
    {
        IsPropertiesOpen = false;
        IsFunctionsExplorerOpen = false;
        _settings.Save();
    }

    /// <summary>
    /// Opens command-line scripts once. An empty workspace keeps the start surface.
    /// </summary>
    public async Task LoadedAsync()
    {
        if (_loaded) { return; }

        _loaded = true;
        foreach (var arg in CommandLineScripts.FromArguments(_environmentService.CommandLineArguments))
        {
            await ReadScriptFileAsync(arg);
        }

        await _appUpdate.CheckForUpdatesAsync(this);
    }

    private async Task OpenRecentAsync(string path)
    {
        if (!path.HasText())
        {
            return;
        }

        await ReadScriptFileAsync(path);
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private void RememberFile(string path)
    {
        if (!path.HasText())
        {
            return;
        }

        var files = _settings.Value.RecentFiles;
        files.RemoveAll(x => PathComparer.Equals(x, path));
        files.Insert(0, path);
        while (files.Count > RecentFileLimit)
        {
            files.RemoveAt(files.Count - 1);
        }

        _settings.Save();
        if (IsStartVisible)
        {
            RefreshRecents();
        }
    }

    private bool ForgetFile(string path)
    {
        var removed = _settings.Value.RecentFiles.RemoveAll(x => PathComparer.Equals(x, path));
        if (removed == 0)
        {
            return false;
        }

        _settings.Save();
        return true;
    }

    private void RefreshRecents()
    {
        var files = _settings.Value.RecentFiles;
        var changed = false;
        for (var i = files.Count - 1; i >= 0; i--)
        {
            if (!_files.File.Exists(files[i]))
            {
                files.RemoveAt(i);
                changed = true;
            }
        }

        while (files.Count > RecentFileLimit)
        {
            files.RemoveAt(files.Count - 1);
            changed = true;
        }

        if (changed)
        {
            _settings.Save();
        }

        Recents.Clear();
        foreach (var path in files)
        {
            Recents.Add(new(path, _files.Path.GetFileName(path)));
        }

        this.RaisePropertyChanged(nameof(HasRecents));
    }

    /// <summary>
    /// Opens each dropped file in a separate editor.
    /// </summary>
    public async Task DropFilesAsync(IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            await ReadScriptFileAsync(file);
        }
    }

    private void ViewerMediaLoaded(IViewerViewModel viewer)
    {
        if (ScriptList.Contains(viewer))
        {
            viewer.Position = _playerPosition;
        }
    }

    /// <summary>
    /// Opens a script file and returns whether it loaded; failures are shown in a dialog.
    /// </summary>
    public async Task<bool> ReadScriptFileAsync(string file)
    {
        try
        {
            var content = await _files.File.ReadAllTextAsync(file);
            var editor = _dialogService.CreateViewModel<EditorViewModel>();
            editor.FileName = file;
            if (ScriptKindLookup.FromPath(file) is { } kind)
            {
                editor.Kind = kind;
            }
            editor.Script = content;
            editor.MarkSaved();
            AddTab(editor, _files.Path.GetFileName(file));
            RememberFile(file);
            return true;
        }
        catch (Exception ex)
        {
            if (ForgetFile(file))
            {
                RefreshRecents();
            }

            await _dialogService.ShowMessageBoxAsync(this, ex.Message, "Error loading file");
            return false;
        }
    }

    private void AddTab(IScriptViewModel viewModel, string title)
    {
        viewModel.DisplayName = title;
        viewModel.RequestClose += ScriptOnRequestClose;
        viewModel.ApplyTabColor(_settings.Value);
        ScriptList.Add(viewModel);
        SelectedItem = viewModel;
    }

    private void OnSettingsChanged()
    {
        this.RaisePropertyChanged(nameof(Threads));
        foreach (var tab in ScriptList)
        {
            tab.ApplyTabColor(_settings.Value);
        }
    }

    /// <inheritdoc />
    public void OnClosing(CancelEventArgs e)
    {
        if (ScriptList.OfType<IEditorViewModel>().Any(x => x.IsDirty))
        {
            e.Cancel = true;
        }
    }

    /// <inheritdoc />
    public async Task OnClosingAsync(CancelEventArgs e)
    {
        foreach (var editor in ScriptList.OfType<IEditorViewModel>().ToList())
        {
            if (!editor.IsDirty)
            {
                continue;
            }

            if (!await ConfirmCloseAsync(editor))
            {
                e.Cancel = true;
                return;
            }
        }

        e.Cancel = false;
    }

    private async void ScriptOnRequestClose(object? sender, EventArgs e)
    {
        if (sender is not IScriptViewModel model) { return; }

        if (model is IEditorViewModel editor && editor.IsDirty)
        {
            if (!_closing.Add(model))
            {
                return;
            }

            try
            {
                if (!await ConfirmCloseAsync(editor))
                {
                    return;
                }
            }
            finally
            {
                _closing.Remove(model);
            }
        }

        RemoveTab(model);
    }

    private async Task<bool> ConfirmCloseAsync(IEditorViewModel editor)
    {
        SelectedItem = editor;
        var result = await _dialogService.ShowMessageBoxAsync(
            this,
            "Save changes to '" + editor.DisplayName + "'?",
            "Unsaved document",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            defaultResult: null);
        if (result == true)
        {
            await SaveEditorAsync(editor);
            return !editor.IsDirty;
        }

        return result == false;
    }

    private void RemoveTab(IScriptViewModel model)
    {
        model.RequestClose -= ScriptOnRequestClose;

        if (model is IViewerViewModel viewer)
        {
            viewer.Script = null;
        }

        var pos = ScriptList.IndexOf(model);
        if (pos < 0) { return; }

        var wasSelected = SelectedItem == model;
        ScriptList.RemoveAt(pos);
        model.IsActive = false;
        if (wasSelected)
        {
            SelectedItem = ScriptList.ElementAtOrDefault(Math.Min(pos, ScriptList.Count - 1));
        }
    }

    private async Task<string?> RequestInputAsync(string displayName, string text, string value, Func<string, bool> validate)
    {
        var input = _dialogService.CreateViewModel<InputViewModel>();
        input.DisplayName = displayName;
        input.Text = text;
        input.Validate = validate;
        input.Value = value;
        input.Reset();
        return await _dialogService.ShowDialogAsync(this, input) == true ? input.Value : null;
    }
}
