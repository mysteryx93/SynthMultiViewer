using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Media;
using HanumanInstitute.FFmpeg;
using HanumanInstitute.MvvmDialogs;
using Microsoft.Extensions.Options;
using HanumanInstitute.MvvmDialogs.Avalonia;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using HanumanInstitute.ScriptAssist;
using ReactiveUI.Builder;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class MainViewModelTests : ViewerTestsBase
{
    private readonly UiTestDriver _ui = new();

    static MainViewModelTests() =>
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();

    [AvaloniaFact]
    public async Task Load_FileUriArgument_OpensScript()
    {
        const string path = "/scripts/opened.vpy";
        var files = FilesFake.Add(path, "opened from uri");
        EnvironmentFake.CommandLineArguments = ["viewer", new Uri("file://" + path).AbsoluteUri];
        var model = Workspace;

        await model.Load.Execute();

        var editor = Assert.IsType<EditorViewModel>(Assert.Single(model.ScriptList));
        Assert.Equal(path, editor.FileName);
        Assert.Equal("opened from uri", editor.Script);
    }

    [AvaloniaFact]
    public async Task Load_ExecutedTwice_OpensArgumentsOnce()
    {
        const string path = "/scripts/from-file.vpy";
        var files = FilesFake.Add(path, "script from file");
        EnvironmentFake.CommandLineArguments = ["viewer", path];
        var model = Workspace;

        await model.Load.Execute();
        await model.Load.Execute();

        var editor = Assert.IsType<EditorViewModel>(Assert.Single(model.ScriptList));
        Assert.Equal(path, editor.FileName);
        Assert.Equal("script from file", editor.Script);
        Assert.True(editor.IsActive);
    }

    [Fact]
    public async Task Load_FirstRun_ChecksForUpdates()
    {
        var updates = UpdatesMock;
        var model = Workspace;

        await model.Load.Execute();

        updates.Verify(x => x.CheckForUpdatesAsync(It.IsAny<INotifyPropertyChanged>()), Times.Once);
    }

    [Fact]
    public async Task Load_ExecutedTwice_ChecksForUpdatesOnce()
    {
        var updates = UpdatesMock;
        var model = Workspace;

        await model.Load.Execute();
        await model.Load.Execute();

        updates.Verify(x => x.CheckForUpdatesAsync(It.IsAny<INotifyPropertyChanged>()), Times.Once);
    }

    [AvaloniaTheory]
    [InlineData(-1)]
    [InlineData("-1")]
    [InlineData(10)]
    [InlineData("invalid")]
    public async Task SelectTab_InvalidIndex_LeavesSelectionUnchanged(object index)
    {
        var model = Workspace;
        await model.New.Execute();
        var editor = model.SelectedItem;

        await model.SelectTab.Execute(index);

        Assert.Same(editor, model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task NextTab_FromFirst_SelectsFollowingTab()
    {
        var model = Workspace;
        await model.New.Execute();
        var first = model.SelectedItem;
        await model.New.Execute();
        var second = model.SelectedItem;
        model.SelectedItem = first;

        await model.NextTab.Execute();

        Assert.Same(second, model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task NextTab_FromLast_WrapsToFirst()
    {
        var model = Workspace;
        await model.New.Execute();
        var first = model.SelectedItem;
        await model.New.Execute();

        await model.NextTab.Execute();

        Assert.Same(first, model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task PreviousTab_FromFirst_WrapsToLast()
    {
        var model = Workspace;
        await model.New.Execute();
        var first = model.SelectedItem;
        await model.New.Execute();
        var last = model.SelectedItem;
        model.SelectedItem = first;

        await model.PreviousTab.Execute();

        Assert.Same(last, model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task MoveTabRight_FromFirst_SwapsWithNeighborAndKeepsSelection()
    {
        var model = Workspace;
        await model.New.Execute();
        var first = model.SelectedItem;
        await model.New.Execute();
        var second = model.SelectedItem;
        model.SelectedItem = first;

        await model.MoveTabRight.Execute();

        Assert.Same(second, model.ScriptList[0]);
        Assert.Same(first, model.ScriptList[1]);
        Assert.Same(first, model.SelectedItem);
        Assert.True(first!.IsActive);
    }

    [AvaloniaFact]
    public async Task MoveTabLeft_AtStart_LeavesOrderUnchanged()
    {
        var model = Workspace;
        await model.New.Execute();
        var first = model.SelectedItem;
        await model.New.Execute();
        model.SelectedItem = first;

        await model.MoveTabLeft.Execute();

        Assert.Same(first, model.ScriptList[0]);
        Assert.Same(first, model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task SelectTab_ByStripIndex_SelectsMixedTabs()
    {
        var model = Workspace;
        await model.New.Execute();
        var editor = model.SelectedItem;
        await model.Run.Execute();
        var viewer = model.SelectedItem;
        model.SelectedItem = editor;

        await model.SelectTab.Execute(1);

        Assert.Same(viewer, model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task SelectTab_ByStringIndex_SelectsEditor()
    {
        var model = Workspace;
        await model.New.Execute();
        var editor = model.SelectedItem;
        await model.Run.Execute();
        model.SelectedItem = editor;
        await model.SelectTab.Execute(1);

        await model.SelectTab.Execute("0");

        Assert.Same(editor, model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task Run_FromEditor_AppendsViewerAtEnd()
    {
        var model = Workspace;
        await model.New.Execute();
        var first = model.SelectedItem;
        await model.New.Execute();
        var second = model.SelectedItem;
        model.SelectedItem = first;

        await model.Run.Execute();

        Assert.Equal(3, model.ScriptList.Count);
        Assert.Same(first, model.ScriptList[0]);
        Assert.Same(second, model.ScriptList[1]);
        Assert.IsType<ViewerViewModel>(model.ScriptList[2]);
        Assert.Same(model.ScriptList[2], model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task New_AfterClose_ContinuesAfterHighestScriptNumber()
    {
        var model = Workspace;
        await model.New.Execute();
        var first = model.SelectedItem!;
        await model.New.Execute();
        var second = model.SelectedItem!;
        await first.Close.Execute();

        await model.New.Execute();

        Assert.Equal(["Script 2", "Script 3"], model.ScriptList.Select(x => x.DisplayName));
        Assert.Equal("Script 3", model.SelectedItem!.DisplayName);
        Assert.Same(second, model.ScriptList[0]);
    }

    [AvaloniaFact]
    public async Task Run_AfterClose_ContinuesAfterHighestViewerNumber()
    {
        var model = Workspace;
        await model.New.Execute();
        var editor = model.SelectedItem;
        await model.Run.Execute();
        var first = model.SelectedItem!;
        model.SelectedItem = editor;
        await model.Run.Execute();
        await first.Close.Execute();
        model.SelectedItem = editor;

        await model.Run.Execute();

        Assert.Equal(["Script 1", "Viewer 2", "Viewer 3"], model.ScriptList.Select(x => x.DisplayName));
    }

    [AvaloniaFact]
    public async Task New_AfterOpenFile_DoesNotConsumeScriptNumbers()
    {
        const string path = "/scripts/opened.vpy";
        var files = FilesFake.Add(path, "opened");
        var model = Workspace;
        await model.New.Execute();

        await model.ReadScriptFileAsync(path);
        await model.New.Execute();

        Assert.Equal(["Script 1", "opened.vpy", "Script 2"],
            model.ScriptList.Select(x => x.DisplayName));
    }

    [AvaloniaFact]
    public async Task New_AfterViewer_AppendsEditorAtEnd()
    {
        var model = Workspace;
        await model.New.Execute();
        await model.Run.Execute();
        var viewer = model.SelectedItem;

        await model.NewAviSynth.Execute();

        Assert.Equal(3, model.ScriptList.Count);
        Assert.Same(viewer, model.ScriptList[1]);
        var editor = Assert.IsType<EditorViewModel>(model.ScriptList[2]);
        Assert.Equal(ScriptKind.AviSynth, editor.Kind);
        Assert.Same(editor, model.SelectedItem);
    }

    [AvaloniaTheory]
    [InlineData(1, 11)]
    [InlineData(-1, 9)]
    [InlineData(10, 20)]
    [InlineData(-10, 0)]
    [InlineData(100, 50)]
    public async Task Seek_ViewerSelected_MovesPositionByFrames(int frames, int expected)
    {
        var model = Workspace;
        await model.New.Execute();
        await model.Run.Execute();
        var viewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);
        viewer.Duration = TimeSpan.FromSeconds(50);
        viewer.Position = TimeSpan.FromSeconds(10);

        await model.Seek.Execute(frames);

        Assert.Equal(TimeSpan.FromSeconds(expected), viewer.Position);
    }

    [AvaloniaTheory]
    [InlineData("invalid")]
    [InlineData(null)]
    public async Task Seek_InvalidParameter_LeavesPositionUnchanged(object? frames)
    {
        var model = Workspace;
        await model.New.Execute();
        await model.Run.Execute();
        var viewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);
        viewer.Duration = TimeSpan.FromSeconds(50);
        viewer.Position = TimeSpan.FromSeconds(10);

        await model.Seek.Execute(frames);

        Assert.Equal(TimeSpan.FromSeconds(10), viewer.Position);
    }

    [AvaloniaFact]
    public async Task Seek_EditorSelected_CanExecuteIsFalse()
    {
        var model = Workspace;
        await model.New.Execute();

        Assert.False(((ICommand)model.Seek).CanExecute(1));
    }

    [AvaloniaFact]
    public async Task PlayPause_ViewerSelected_TogglesIsPlaying()
    {
        var model = Workspace;
        await model.New.Execute();
        await model.Run.Execute();
        var viewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);

        await model.PlayPause.Execute();
        var playing = viewer.IsPlaying;
        await model.PlayPause.Execute();

        Assert.True(playing);
        Assert.False(viewer.IsPlaying);
    }

    [AvaloniaFact]
    public async Task PlayPause_EditorSelected_CanExecuteIsFalse()
    {
        var model = Workspace;
        await model.New.Execute();

        Assert.False(((ICommand)model.PlayPause).CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Close_ViewerSelected_ReleasesScriptAndSelectsRemainingTab()
    {
        var model = Workspace;
        await model.New.Execute();
        var editor = model.SelectedItem;
        await model.Run.Execute();
        var viewer = (ViewerViewModel)model.SelectedItem!;

        await viewer.Close.Execute();

        Assert.Null(viewer.Script);
        Assert.False(viewer.IsActive);
        Assert.Same(editor, model.SelectedItem);
        Assert.True(editor!.IsActive);
    }

    [AvaloniaFact]
    public async Task Close_LastTabClosed_ClearsSelection()
    {
        var model = Workspace;
        await model.New.Execute();

        await model.SelectedItem!.Close.Execute();

        Assert.Empty(model.ScriptList);
        Assert.Null(model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task Close_UnmodifiedEditor_DoesNotPrompt()
    {
        var manager = DialogsFake;
        var model = Workspace;
        await model.New.Execute();

        await model.SelectedItem!.Close.Execute();

        Assert.Equal(0, manager.FrameworkDialogCount);
        Assert.Empty(model.ScriptList);
    }

    [AvaloniaFact]
    public async Task Close_DirtyEditor_Cancel_KeepsTab()
    {
        var manager = DialogsFake;
        manager.QueueFrameworkResult(null);
        var model = Workspace;
        await model.New.Execute();
        var editor = Assert.IsType<EditorViewModel>(model.SelectedItem);
        editor.Script += " extra";

        await editor.Close.Execute();

        Assert.Same(editor, model.SelectedItem);
        Assert.Contains(editor, model.ScriptList);
        Assert.True(editor.IsDirty);
        var prompt = Assert.IsType<MessageBoxSettings>(manager.LastFrameworkSettings);
        Assert.Null(prompt.DefaultValue);
        Assert.Equal(MessageBoxButton.YesNoCancel, prompt.Button);
    }

    [AvaloniaFact]
    public async Task Close_DirtyEditor_Discard_ClosesWithoutSaving()
    {
        const string path = "/scripts/original.vpy";
        var files = FilesFake.Add(path, "original");
        var manager = DialogsFake;
        manager.QueueFrameworkResult(false);
        var model = Workspace;
        Assert.True(await model.ReadScriptFileAsync(path));
        var editor = Assert.IsType<EditorViewModel>(model.SelectedItem);
        editor.Script = "changed";

        await editor.Close.Execute();

        Assert.Empty(model.ScriptList);
        Assert.Equal("original", files.File.ReadAllText(path));
    }

    [AvaloniaFact]
    public async Task Close_DirtyEditor_Save_WritesAndCloses()
    {
        const string path = "/scripts/original.vpy";
        var files = FilesFake.Add(path, "original");
        var manager = DialogsFake;
        manager.QueueFrameworkResult(true);
        var model = Workspace;
        Assert.True(await model.ReadScriptFileAsync(path));
        var editor = Assert.IsType<EditorViewModel>(model.SelectedItem);
        editor.Script = "changed";

        await editor.Close.Execute();
        for (var i = 0; i < 50 && model.ScriptList.Count > 0; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }

        Assert.Empty(model.ScriptList);
        Assert.Equal("changed", files.File.ReadAllText(path));
    }

    [AvaloniaFact]
    public async Task Close_DirtyUntitled_SaveDialogCancelled_KeepsTab()
    {
        var manager = DialogsFake;
        manager.QueueFrameworkResult(true);
        manager.QueueFrameworkResult(null);
        var model = Workspace;
        await model.New.Execute();
        var editor = Assert.IsType<EditorViewModel>(model.SelectedItem);
        editor.Script += " extra";

        await editor.Close.Execute();

        Assert.Same(editor, model.SelectedItem);
        Assert.True(editor.IsDirty);
    }

    [AvaloniaFact]
    public async Task OnClosing_DirtyEditor_CancelKeepsWindowOpen()
    {
        var manager = DialogsFake;
        manager.QueueFrameworkResult(null);
        var model = Workspace;
        await model.New.Execute();
        Assert.IsType<EditorViewModel>(model.SelectedItem).Script += "x";
        var args = new CancelEventArgs();

        await model.OnClosingAsync(args);
        var syncCancel = args.Cancel;
        await model.OnClosingAsync(args);

        Assert.True(syncCancel);
        Assert.True(args.Cancel);
        Assert.Single(model.ScriptList);
    }

    [AvaloniaFact]
    public async Task SaveAs_AviSynth_SelectsAviSynthFilter()
    {
        var manager = DialogsFake;
        manager.QueueFrameworkResult(null);
        var model = Workspace;
        await model.NewAviSynth.Execute();

        await model.SaveAs.Execute();

        var settings = Assert.IsType<SaveFileDialogSettings>(manager.LastFrameworkSettings);
        Assert.Equal(".avs", settings.DefaultExtension);
        Assert.Equal("AviSynth Script", settings.Filters[0].Name);
        Assert.Equal(["avs", "avsi"], settings.Filters[0].Extensions);
    }

    [AvaloniaFact]
    public async Task SaveAs_VapourSynth_SelectsVapourSynthFilter()
    {
        var manager = DialogsFake;
        manager.QueueFrameworkResult(null);
        var model = Workspace;
        await model.New.Execute();

        await model.SaveAs.Execute();

        var settings = Assert.IsType<SaveFileDialogSettings>(manager.LastFrameworkSettings);
        Assert.Equal(".vpy", settings.DefaultExtension);
        Assert.Equal("VapourSynth Script", settings.Filters[0].Name);
        Assert.Equal(["vpy"], settings.Filters[0].Extensions);
    }

    [AvaloniaFact]
    public async Task Close_MiddleTabClosed_SelectsFollowingTab()
    {
        var model = Workspace;
        await model.New.Execute();
        await model.New.Execute();
        var second = model.SelectedItem;
        await model.New.Execute();
        var third = model.SelectedItem;
        model.SelectedItem = second;

        await second!.Close.Execute();

        Assert.Same(third, model.SelectedItem);
        Assert.True(third!.IsActive);
    }

    [AvaloniaFact]
    public async Task Close_LastOfSeveralClosed_SelectsPreviousTab()
    {
        var model = Workspace;
        await model.New.Execute();
        await model.New.Execute();
        var second = model.SelectedItem;
        await model.New.Execute();

        await model.SelectedItem!.Close.Execute();

        Assert.Same(second, model.SelectedItem);
        Assert.True(second!.IsActive);
    }

    [AvaloniaFact]
    public async Task UpdateAll_SingleViewer_RemainsEnabled()
    {
        var model = Workspace;
        var command = (ICommand)model.UpdateAll;
        await model.New.Execute();
        await model.Run.Execute();
        var viewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);
        viewer.Position = TimeSpan.FromSeconds(1.2);

        var canExecute = command.CanExecute(null);
        await model.UpdateAll.Execute();

        Assert.True(canExecute);
        Assert.Equal(TimeSpan.FromSeconds(1.2), viewer.Position);
    }

    [AvaloniaFact]
    public async Task UpdateAll_EditorSelected_CanExecuteIsFalse()
    {
        var model = Workspace;
        var command = (ICommand)model.UpdateAll;
        await model.New.Execute();
        var editor = model.SelectedItem;
        await model.Run.Execute();
        model.SelectedItem = editor;

        var canExecute = command.CanExecute(null);

        Assert.False(canExecute);
    }

    [AvaloniaFact]
    public async Task Properties_ViewerSelected_ShowsModelessWindow()
    {
        var manager = DialogsFake;
        var model = Workspace;
        await model.New.Execute();
        await model.Run.Execute();

        await model.Properties.Execute();

        var properties = Assert.IsType<VideoPropertiesViewModel>(manager.LastShown);
        Assert.Same(model.SelectedItem, properties.Viewer);
        Assert.True(model.IsPropertiesOpen);
    }

    [AvaloniaFact]
    public async Task Properties_AlreadyOpen_ClosesWindow()
    {
        var manager = DialogsFake;
        var model = Workspace;
        await model.New.Execute();
        await model.Run.Execute();
        await model.Properties.Execute();
        var first = manager.LastShown;
        var opened = model.IsPropertiesOpen;

        await model.Properties.Execute();

        Assert.True(opened);
        Assert.False(model.IsPropertiesOpen);
        Assert.Same(first, manager.LastShown);
    }

    [AvaloniaFact]
    public async Task Properties_ToggledClosed_ReopenShowsNewWindow()
    {
        var manager = DialogsFake;
        var model = Workspace;
        await model.New.Execute();
        await model.Run.Execute();
        await model.Properties.Execute();
        var first = manager.LastShown;
        await model.Properties.Execute();

        await model.Properties.Execute();

        Assert.True(model.IsPropertiesOpen);
        Assert.NotSame(first, manager.LastShown);
    }

    [AvaloniaFact]
    public async Task Properties_Closed_ShowsNewWindow()
    {
        var manager = DialogsFake;
        var model = Workspace;
        await model.New.Execute();
        await model.Run.Execute();
        await model.Properties.Execute();
        var first = Assert.IsType<VideoPropertiesViewModel>(manager.LastShown);

        first.OnClosed();
        await model.Properties.Execute();

        var second = Assert.IsType<VideoPropertiesViewModel>(manager.LastShown);
        Assert.NotSame(first, second);
        Assert.Same(model.SelectedItem, second.Viewer);
    }

    [AvaloniaFact]
    public async Task Properties_Reopen_RestoresPlacementOnNewWindow()
    {
        var manager = DialogsFake;
        var model = Workspace;
        await model.New.Execute();
        await model.Run.Execute();
        await model.Properties.Execute();
        var first = Assert.IsType<VideoPropertiesViewModel>(manager.LastShown);
        first.Placement.Left = 40;
        first.Placement.Top = 80;
        first.Placement.Width = 420;
        first.Placement.Height = 500;

        first.OnClosed();
        await model.Properties.Execute();

        var second = Assert.IsType<VideoPropertiesViewModel>(manager.LastShown);
        Assert.NotSame(first, second);
        Assert.Same(first.Placement, second.Placement);
        Assert.Equal(40, second.Placement.Left);
        Assert.Equal(80, second.Placement.Top);
        Assert.Equal(420, second.Placement.Width);
        Assert.Equal(500, second.Placement.Height);
    }

    [AvaloniaFact]
    public async Task Properties_ToggleClose_RestoresPlacementOnReopen()
    {
        var manager = DialogsFake;
        var model = Workspace;
        await model.New.Execute();
        await model.Run.Execute();
        await model.Properties.Execute();
        var first = Assert.IsType<VideoPropertiesViewModel>(manager.LastShown);
        first.Placement.Left = 12;
        first.Placement.Top = 24;
        first.Placement.Width = 360;
        first.Placement.Height = 480;

        await model.Properties.Execute();
        await model.Properties.Execute();

        var second = Assert.IsType<VideoPropertiesViewModel>(manager.LastShown);
        Assert.NotSame(first, second);
        Assert.Same(first.Placement, second.Placement);
        Assert.Equal(12, second.Placement.Left);
        Assert.Equal(24, second.Placement.Top);
        Assert.Equal(360, second.Placement.Width);
        Assert.Equal(480, second.Placement.Height);
    }

    [AvaloniaFact]
    public async Task Properties_EditorSelected_CannotExecute()
    {
        var model = Workspace;
        await model.New.Execute();

        Assert.False(((ICommand)model.Properties).CanExecute(null));
    }

    [AvaloniaFact]
    public async Task FunctionsExplorer_EditorSelected_ShowsModelessWindow()
    {
        var manager = DialogsFake;
        var model = Workspace;
        await model.New.Execute();

        await model.FunctionsExplorer.Execute();

        var explorer = Assert.IsType<FunctionsExplorerViewModel>(manager.LastShown);
        Assert.Same(model.SelectedItem, explorer.Editor);
        Assert.True(model.IsFunctionsExplorerOpen);
    }

    [AvaloniaFact]
    public async Task Encode_EditorSelected_ShowsSettingsDialog()
    {
        var manager = DialogsFake;
        var model = Workspace;
        await model.New.Execute();

        await model.Encode.Execute();

        var encode = Assert.IsType<EncodeViewModel>(manager.LastDialog);
        Assert.Equal("Encode", encode.DisplayName);
    }

    [AvaloniaFact]
    public async Task Encode_StartAccepted_ClosesSettingsDialog()
    {
        var encoder = new Mock<IEncoderService>();
        encoder.SetupGet(x => x.Processes).Returns(new EncoderService().Processes);
        encoder.Setup(x => x.GetMediaEncoder(It.IsAny<object>())).Returns(new Mock<IMediaEncoder>().Object);
        var encode = new EncodeViewModel(encoder.Object, Options.Create(new AppPaths()), SettingsFake, FilesFake, Dialogs);
        var model = Workspace;
        await model.New.Execute();
        encode.Bind(model.SelectedItem);
        var closed = false;
        encode.RequestClose += OnClose;
        void OnClose(object? sender, EventArgs e) => closed = true;

        await encode.Start.Execute();

        Assert.True(encode.DialogResult);
        Assert.True(closed);
    }

    [AvaloniaFact]
    public async Task Benchmark_PlayingViewer_PausesUntilDialogCloses()
    {
        var manager = DialogsFake;
        var model = Workspace;
        var viewer = new ViewerViewModel { IsPlaying = true };
        model.ScriptList.Add(viewer);
        model.SelectedItem = viewer;
        var paused = false;
        manager.OnShow = Observe;
        void Observe(IModalDialogViewModel dialog) => paused = !viewer.IsPlaying;

        await model.Benchmark.Execute();

        Assert.IsType<BenchmarkViewModel>(manager.LastDialog);
        Assert.True(paused);
        Assert.True(viewer.IsPlaying);
    }

    [AvaloniaFact]
    public async Task FunctionsExplorer_AlreadyOpen_ClosesWindow()
    {
        var manager = DialogsFake;
        var model = Workspace;
        await model.New.Execute();
        await model.FunctionsExplorer.Execute();
        var opened = model.IsFunctionsExplorerOpen;

        await model.FunctionsExplorer.Execute();

        Assert.True(opened);
        Assert.False(model.IsFunctionsExplorerOpen);
    }

    [AvaloniaFact]
    public async Task FunctionsExplorer_ViewerSelected_CannotExecute()
    {
        var model = Workspace;
        await model.New.Execute();
        await model.Run.Execute();

        Assert.False(((ICommand)model.FunctionsExplorer).CanExecute(null));
    }

    [AvaloniaFact]
    public async Task FunctionsExplorer_ViewerTab_ClosesWindow()
    {
        var manager = DialogsFake;
        var model = Workspace;
        await model.New.Execute();
        await model.FunctionsExplorer.Execute();

        await model.Run.Execute();

        Assert.False(model.IsFunctionsExplorerOpen);
    }

    [AvaloniaFact]
    public async Task FunctionsExplorer_SwitchEditor_BrowsesNewEditor()
    {
        var manager = DialogsFake;
        var model = Workspace;
        await model.New.Execute();
        await model.FunctionsExplorer.Execute();
        var explorer = Assert.IsType<FunctionsExplorerViewModel>(manager.LastShown);
        var n = 0;
        var languages = new Mock<IScriptLanguageFactory>();
        languages.Setup(f => f.BrowseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
            .Returns(() =>
            {
                n++;
                IReadOnlyList<BrowseGroup> groups =
                    [new BrowseGroup(n.ToString(), [new BrowseFunction("F", "F()", "F()")])];
                return Task.FromResult(groups);
            });
        explorer.Languages = languages.Object;
        await explorer.ReloadAsync();

        await model.New.Execute();
        await explorer.ReloadAsync();

        Assert.True(model.IsFunctionsExplorerOpen);
        Assert.Same(model.SelectedItem, explorer.Editor);
        Assert.NotEqual("1", Assert.Single(explorer.Groups).Name);
    }

    [AvaloniaTheory(Timeout = 10000)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Close_InputDialogConfirmedOrCancelled_ReturnsDialogResult(bool accept)
    {
        var ownerModel = Workspace;
        var owner = new Window { DataContext = ownerModel };
        using var window = _ui.Show(owner);
        var manager = new Mock<DialogManager>(new ViewLocator(), null!, null!, null!, null!) { CallBase = true };
        manager.Setup(x => x.FindViewByViewModel(ownerModel)).Returns(owner.AsWrapper());
        var dialogs = new DialogService(manager.Object);
        var input = new InputViewModel { Value = "12", Validate = text => int.TryParse(text, out _) };
        input.Reset();
        var result = dialogs.ShowDialogAsync(ownerModel, input);

        ((ICommand)(accept ? input.Ok : input.Close)).Execute(null);
        var actual = await result.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(accept ? true : null, actual);
    }

    [AvaloniaFact(Timeout = 10000)]
    public async Task Close_HelpDialogClosed_CompletesDialog()
    {
        var ownerModel = Workspace;
        var owner = new Window { DataContext = ownerModel };
        using var window = _ui.Show(owner);
        var manager = new Mock<DialogManager>(new ViewLocator(), null!, null!, null!, null!) { CallBase = true };
        manager.Setup(x => x.FindViewByViewModel(ownerModel)).Returns(owner.AsWrapper());
        var dialogs = new DialogService(manager.Object);
        var help = HelpModel;
        var result = dialogs.ShowDialogAsync(ownerModel, help);

        ((ICommand)help.Close).Execute(null);
        var actual = await result.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(actual);
    }

    [AvaloniaFact]
    public void Threads_MultiThreadingOff_ReturnsOne()
    {
        var settings = SettingsFake;
        settings.Value = new AppSettingsData { VapourSynthThreads = 8 };
        var model = Workspace;

        Assert.Equal(1, model.Threads);
    }

    [AvaloniaFact]
    public void Threads_MultiThreadingOn_UsesVapourSynthThreads()
    {
        var settings = SettingsFake;
        settings.Value = new AppSettingsData { VapourSynthThreads = 8 };
        var model = Workspace;
        model.IsMultiThreaded = true;

        Assert.Equal(8, model.Threads);
    }

    [AvaloniaFact]
    public void Threads_SettingsSavedWhileMultiThreaded_RaisesThreads()
    {
        var settings = SettingsFake;
        settings.Value = new AppSettingsData { VapourSynthThreads = 4 };
        var model = Workspace;
        model.IsMultiThreaded = true;
        var raised = false;
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Threads))
            {
                raised = true;
            }
        };

        settings.Value.VapourSynthThreads = 12;
        settings.Save();

        Assert.True(raised);
        Assert.Equal(12, model.Threads);
    }

    [AvaloniaFact]
    public void OnClosed_SavesSettings()
    {
        var settings = SettingsFake;
        var model = Workspace;

        model.OnClosed();

        Assert.Equal(1, settings.SaveCount);
    }

    [AvaloniaFact]
    public void Zoom_SetToZero_EnablesScaleToFit()
    {
        var model = Workspace;

        model.Zoom = 0;

        Assert.True(model.ZoomScaleToFit);
        Assert.Equal(0, model.Zoom);
    }

    [AvaloniaFact]
    public async Task Toolbar_EditorSelected_ShowsEditorCommands()
    {
        var model = Workspace;

        await model.New.Execute();

        Assert.True(model.IsEditorSelected);
        Assert.False(model.IsViewerSelected);
        Assert.False(model.IsVapourSynthViewerSelected);
    }

    [AvaloniaFact]
    public async Task Toolbar_VapourSynthViewerSelected_ShowsViewerCommands()
    {
        var model = Workspace;

        await _ui.OpenViewerAsync(model);

        Assert.False(model.IsEditorSelected);
        Assert.True(model.IsViewerSelected);
        Assert.True(model.IsVapourSynthViewerSelected);
    }

    [AvaloniaFact]
    public async Task Toolbar_AviSynthViewerSelected_HidesMultiThreading()
    {
        var model = Workspace;
        await model.NewAviSynth.Execute();

        await model.Run.Execute();

        Assert.False(model.IsEditorSelected);
        Assert.True(model.IsViewerSelected);
        Assert.False(model.IsVapourSynthViewerSelected);
    }

    [AvaloniaFact]
    public async Task TabBackground_DefaultSettings_UsesEngineTypeColors()
    {
        var model = Workspace;
        await model.New.Execute();
        await model.NewAviSynth.Execute();
        var avs = Assert.IsType<EditorViewModel>(model.SelectedItem);
        model.SelectedItem = model.ScriptList[0];
        await model.Run.Execute();
        var vsViewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);

        Assert.Equal(TabColors.For(ScriptKind.VapourSynth, false, AppTheme.Light),
            BrushColor(model.ScriptList[0].TabBackground));
        Assert.Equal(TabColors.For(ScriptKind.VapourSynth, true, AppTheme.Light),
            BrushColor(vsViewer.TabBackground));
        Assert.Equal(TabColors.For(ScriptKind.AviSynth, false, AppTheme.Light),
            BrushColor(avs.TabBackground));
    }

    [AvaloniaFact]
    public async Task TabBackground_ThemeChanged_RecalculatesFill()
    {
        var settings = SettingsFake;
        var model = Workspace;
        await model.New.Execute();
        settings.Value.Theme = AppTheme.Dark;
        settings.Save();

        Assert.Equal(TabColors.For(ScriptKind.VapourSynth, false, AppTheme.Dark),
            BrushColor(model.ScriptList[0].TabBackground));
    }

    [AvaloniaFact]
    public async Task TabBackground_TabColorOverride_UsesCustomColor()
    {
        var model = Workspace;
        await model.New.Execute();
        model.SelectedItem!.TabColor = Colors.HotPink;

        Assert.Equal(Colors.HotPink, BrushColor(model.SelectedItem.TabBackground));
    }

    [AvaloniaFact]
    public async Task ChangeTabColor_Ok_AppliesSelectedHue()
    {
        var manager = DialogsFake;
        manager.OnShow = OnShow;
        var model = Workspace;
        await model.New.Execute();
        void OnShow(IModalDialogViewModel dialog)
        {
            var picker = Assert.IsType<TabColorViewModel>(dialog);
            Assert.Equal(TabColors.For(ScriptKind.VapourSynth, false, AppTheme.Light), picker.Color);
            picker.Color = Colors.HotPink;
            ((ICommand)picker.Ok).Execute(null);
        }

        await model.ChangeTabColor.Execute();

        Assert.Equal(Colors.HotPink, model.SelectedItem!.TabColor);
        Assert.Equal(Colors.HotPink, BrushColor(model.SelectedItem.TabBackground));
    }

    [AvaloniaFact]
    public async Task ChangeTabColor_Cancel_LeavesExistingHue()
    {
        var manager = DialogsFake;
        manager.OnShow = OnShow;
        var model = Workspace;
        await model.New.Execute();
        model.SelectedItem!.TabColor = Colors.Orange;
        void OnShow(IModalDialogViewModel dialog) => ((ICommand)((TabColorViewModel)dialog).Close).Execute(null);

        await model.ChangeTabColor.Execute();

        Assert.Equal(Colors.Orange, model.SelectedItem.TabColor);
    }

    [AvaloniaFact]
    public async Task ChangeTabColor_Default_ClearsOverride()
    {
        var manager = DialogsFake;
        manager.OnShow = OnShow;
        var model = Workspace;
        await model.New.Execute();
        model.SelectedItem!.TabColor = Colors.HotPink;
        void OnShow(IModalDialogViewModel dialog)
        {
            var picker = Assert.IsType<TabColorViewModel>(dialog);
            ((ICommand)picker.RestoreDefault).Execute(null);
            ((ICommand)picker.Ok).Execute(null);
        }

        await model.ChangeTabColor.Execute();

        Assert.Null(model.SelectedItem.TabColor);
        Assert.Equal(TabColors.For(ScriptKind.VapourSynth, false, AppTheme.Light),
            BrushColor(model.SelectedItem.TabBackground));
    }

    private static Color BrushColor(IBrush brush) => Assert.IsType<SolidColorBrush>(brush).Color;

    [AvaloniaFact]
    public async Task Run_AviSynthEditor_CopiesKindToViewer()
    {
        var model = Workspace;
        await model.NewAviSynth.Execute();
        var editor = Assert.IsType<EditorViewModel>(model.SelectedItem);
        editor.FileName = "/scripts/clip.avs";

        await model.Run.Execute();

        var viewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);
        Assert.Equal(ScriptKind.AviSynth, viewer.Kind);
        Assert.Equal(editor.Script, viewer.Script);
        Assert.Equal(editor.FileName, viewer.FileName);
    }

    [AvaloniaFact]
    public async Task Run_UnsavedPastedScript_CopiesTextWithoutInventingAPath()
    {
        var model = Workspace;
        await model.New.Execute();
        var editor = Assert.IsType<EditorViewModel>(model.SelectedItem);
        const string pasted = """
            import vapoursynth as vs
            clip = vs.core.std.BlankClip(width=16, height=16, length=1, format=vs.RGB24)
            clip.set_output()
            """;
        editor.Script = pasted;

        await model.Run.Execute();

        var viewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);
        Assert.Null(viewer.FileName);
        Assert.Equal(pasted, viewer.Script);
        Assert.Equal(ScriptKind.VapourSynth, viewer.Kind);
    }

    [AvaloniaFact]
    public async Task ReadScriptFileAsync_AvsExtension_SetsAviSynthKind()
    {
        const string path = "/scripts/clip.avs";
        const string script = "BlankClip()\n";
        var files = FilesFake.Add(path, script);
        var model = Workspace;

        var loaded = await model.ReadScriptFileAsync(path);

        var editor = Assert.IsType<EditorViewModel>(Assert.Single(model.ScriptList));
        Assert.True(loaded);
        Assert.Equal(path, editor.FileName);
        Assert.Equal(ScriptKind.AviSynth, editor.Kind);
        Assert.Equal(script, editor.Script);
    }

    [AvaloniaFact]
    public async Task Load_NoArguments_LeavesEmptyStart()
    {
        var model = Workspace;

        await model.Load.Execute();

        Assert.Empty(model.ScriptList);
        Assert.True(model.IsStartVisible);
        Assert.Null(model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task New_HidesStart()
    {
        var model = Workspace;
        await model.Load.Execute();

        await model.New.Execute();

        Assert.False(model.IsStartVisible);
        Assert.Single(model.ScriptList);
    }

    [AvaloniaFact]
    public async Task Close_LastTab_ShowsStart()
    {
        var model = Workspace;
        await model.Load.Execute();
        await model.New.Execute();

        await model.SelectedItem!.Close.Execute();

        Assert.True(model.IsStartVisible);
        Assert.Empty(model.ScriptList);
        Assert.Null(model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task New_DoesNotRememberUntitled()
    {
        var settings = SettingsFake;
        var model = Workspace;

        await model.New.Execute();
        await model.SelectedItem!.Close.Execute();

        Assert.Empty(settings.Value.RecentFiles);
        Assert.Empty(model.Recents);
    }

    [AvaloniaFact]
    public async Task ReadScriptFileAsync_RemembersRecent()
    {
        const string path = "/scripts/clip.vpy";
        var files = FilesFake.Add(path, "clip");
        var settings = SettingsFake;
        var model = Workspace;

        Assert.True(await model.ReadScriptFileAsync(path));

        Assert.Equal([path], settings.Value.RecentFiles);
        Assert.False(model.IsStartVisible);
    }

    [AvaloniaFact]
    public async Task Close_RecentFile_ShowsRecentOnStart()
    {
        const string path = "/scripts/clip.vpy";
        var files = FilesFake.Add(path, "clip");
        var settings = SettingsFake;
        var model = Workspace;
        Assert.True(await model.ReadScriptFileAsync(path));

        await model.SelectedItem!.Close.Execute();

        Assert.True(model.IsStartVisible);
        Assert.Equal(path, Assert.Single(model.Recents).Path);
        Assert.Equal("clip.vpy", model.Recents[0].Name);
    }

    [AvaloniaFact]
    public async Task Recents_Reopen_MovesToFront()
    {
        const string first = "/scripts/a.vpy";
        const string second = "/scripts/b.vpy";
        var files = FilesFake.Add(first, "a").Add(second, "b");
        var settings = SettingsFake;
        var model = Workspace;
        Assert.True(await model.ReadScriptFileAsync(first));
        Assert.True(await model.ReadScriptFileAsync(second));
        Assert.Equal([second, first], settings.Value.RecentFiles);

        Assert.True(await model.ReadScriptFileAsync(first));

        Assert.Equal([first, second], settings.Value.RecentFiles);
    }

    [AvaloniaFact]
    public async Task Recents_Cap8_DropsOldest()
    {
        var disk = FilesFake;
        var paths = new string[9];
        for (var i = 0; i < 9; i++)
        {
            paths[i] = "/scripts/" + i + ".vpy";
            disk.Add(paths[i], i.ToString());
        }
        var settings = SettingsFake;
        var model = Workspace;

        for (var i = 0; i < 9; i++)
        {
            Assert.True(await model.ReadScriptFileAsync(paths[i]));
        }

        Assert.Equal(8, settings.Value.RecentFiles.Count);
        Assert.Equal(paths[8], settings.Value.RecentFiles[0]);
        Assert.DoesNotContain(paths[0], settings.Value.RecentFiles);
    }

    [AvaloniaFact]
    public void Recents_MissingFile_PrunedOnStart()
    {
        var settings = SettingsFake;
        settings.Value.RecentFiles.Add("/scripts/missing.vpy");
        var model = Workspace;

        Assert.Empty(model.Recents);
        Assert.Empty(settings.Value.RecentFiles);
        Assert.True(settings.SaveCount > 0);
    }

    [AvaloniaFact]
    public async Task OpenRecent_Missing_PrunesAndShowsError()
    {
        var manager = DialogsFake;
        var settings = SettingsFake;
        const string missing = "/scripts/missing.vpy";
        var files = FilesFake.Add(missing, "clip");
        settings.Value.RecentFiles.Add(missing);
        var model = Workspace;
        Assert.Single(model.Recents);
        files.File.Delete(missing);

        await model.OpenRecent.Execute(missing);

        Assert.Empty(model.Recents);
        Assert.Empty(settings.Value.RecentFiles);
        Assert.IsType<MessageBoxSettings>(manager.LastFrameworkSettings);
        Assert.True(model.IsStartVisible);
    }

    [AvaloniaFact]
    public async Task Save_ExistingPath_RemembersFile()
    {
        const string path = "/scripts/original.vpy";
        var files = FilesFake.Add(path, "original");
        var settings = SettingsFake;
        var model = Workspace;
        await model.New.Execute();
        var editor = Assert.IsType<EditorViewModel>(model.SelectedItem);
        editor.FileName = path;
        editor.Script = "saved";

        await model.Save.Execute();

        Assert.Equal([path], settings.Value.RecentFiles);
        Assert.Equal("saved", files.File.ReadAllText(path));
    }
}
