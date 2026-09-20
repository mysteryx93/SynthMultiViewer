using System.ComponentModel;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using HanumanInstitute.MediaSynthUI;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using HanumanInstitute.ScriptAssist;
using HanumanInstitute.SynthMultiViewer.Models;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using Moq;
using ReactiveUI.Builder;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class MainViewModelTests
{
    static MainViewModelTests() =>
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();

    [AvaloniaFact]
    public async Task Load_FileUriArgument_OpensScript()
    {
        const string path = "/scripts/opened.vpy";
        var files = new FakeFileSystemService().Add(path, "opened from uri");
        var model = TestSupport.CreateMain(new TestSupport.TestEnvironment(["viewer", new Uri("file://" + path).AbsoluteUri]),
            files: files);

        await model.Load.Execute();

        var editor = Assert.IsType<EditorViewModel>(Assert.Single(model.ScriptList));
        Assert.Equal(path, editor.FileName);
        Assert.Equal("opened from uri", editor.Script);
    }

    [AvaloniaFact]
    public async Task Load_ExecutedTwice_OpensArgumentsOnce()
    {
        const string path = "/scripts/from-file.vpy";
        var files = new FakeFileSystemService().Add(path, "script from file");
        var model = TestSupport.CreateMain(new TestSupport.TestEnvironment(["viewer", path]), files: files);

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
        var updates = new TestSupport.MemoryAppUpdateService();
        var model = TestSupport.CreateMain(updates: updates);

        await model.Load.Execute();

        Assert.Equal(1, updates.CheckCount);
    }

    [Fact]
    public async Task Load_ExecutedTwice_ChecksForUpdatesOnce()
    {
        var updates = new TestSupport.MemoryAppUpdateService();
        var model = TestSupport.CreateMain(updates: updates);

        await model.Load.Execute();
        await model.Load.Execute();

        Assert.Equal(1, updates.CheckCount);
    }

    [AvaloniaTheory]
    [InlineData(-1)]
    [InlineData("-1")]
    [InlineData(10)]
    [InlineData("invalid")]
    public async Task SelectTab_InvalidIndex_LeavesSelectionUnchanged(object index)
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        var editor = model.SelectedItem;

        await model.SelectTab.Execute(index);

        Assert.Same(editor, model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task NextTab_FromFirst_SelectsFollowingTab()
    {
        var model = TestSupport.CreateMain();
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
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        var first = model.SelectedItem;
        await model.New.Execute();

        await model.NextTab.Execute();

        Assert.Same(first, model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task PreviousTab_FromFirst_WrapsToLast()
    {
        var model = TestSupport.CreateMain();
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
        var model = TestSupport.CreateMain();
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
        var model = TestSupport.CreateMain();
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
        var model = TestSupport.CreateMain();
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
        var model = TestSupport.CreateMain();
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
        var model = TestSupport.CreateMain();
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
        var model = TestSupport.CreateMain();
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
        var model = TestSupport.CreateMain();
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
        var files = new FakeFileSystemService().Add(path, "opened");
        var model = TestSupport.CreateMain(files: files);
        await model.New.Execute();

        await model.ReadScriptFileAsync(path);
        await model.New.Execute();

        Assert.Equal(["Script 1", "opened.vpy", "Script 2"],
            model.ScriptList.Select(x => x.DisplayName));
    }

    [AvaloniaFact]
    public async Task New_AfterViewer_AppendsEditorAtEnd()
    {
        var model = TestSupport.CreateMain();
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
        var model = TestSupport.CreateMain();
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
        var model = TestSupport.CreateMain();
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
        var model = TestSupport.CreateMain();
        await model.New.Execute();

        Assert.False(((ICommand)model.Seek).CanExecute(1));
    }

    [AvaloniaFact]
    public async Task PlayPause_ViewerSelected_TogglesIsPlaying()
    {
        var model = TestSupport.CreateMain();
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
        var model = TestSupport.CreateMain();
        await model.New.Execute();

        Assert.False(((ICommand)model.PlayPause).CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Close_ViewerSelected_ReleasesScriptAndSelectsRemainingTab()
    {
        var model = TestSupport.CreateMain();
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
        var model = TestSupport.CreateMain();
        await model.New.Execute();

        await model.SelectedItem!.Close.Execute();

        Assert.Empty(model.ScriptList);
        Assert.Null(model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task Close_UnmodifiedEditor_DoesNotPrompt()
    {
        var manager = new TestSupport.FakeDialogManager();
        var model = TestSupport.CreateMain(manager: manager);
        await model.New.Execute();

        await model.SelectedItem!.Close.Execute();

        Assert.Equal(0, manager.FrameworkDialogCount);
        Assert.Empty(model.ScriptList);
    }

    [AvaloniaFact]
    public async Task Close_DirtyEditor_Cancel_KeepsTab()
    {
        var manager = new TestSupport.FakeDialogManager();
        manager.QueueFrameworkResult(null);
        var model = TestSupport.CreateMain(manager: manager);
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
        var files = new FakeFileSystemService().Add(path, "original");
        var manager = new TestSupport.FakeDialogManager();
        manager.QueueFrameworkResult(false);
        var model = TestSupport.CreateMain(manager: manager, files: files);
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
        var files = new FakeFileSystemService().Add(path, "original");
        var manager = new TestSupport.FakeDialogManager();
        manager.QueueFrameworkResult(true);
        var model = TestSupport.CreateMain(manager: manager, files: files);
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
        var manager = new TestSupport.FakeDialogManager();
        manager.QueueFrameworkResult(true);
        manager.QueueFrameworkResult(null);
        var model = TestSupport.CreateMain(manager: manager);
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
        var manager = new TestSupport.FakeDialogManager();
        manager.QueueFrameworkResult(null);
        var model = TestSupport.CreateMain(manager: manager);
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
        var manager = new TestSupport.FakeDialogManager();
        manager.QueueFrameworkResult(null);
        var model = TestSupport.CreateMain(manager: manager);
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
        var manager = new TestSupport.FakeDialogManager();
        manager.QueueFrameworkResult(null);
        var model = TestSupport.CreateMain(manager: manager);
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
        var model = TestSupport.CreateMain();
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
        var model = TestSupport.CreateMain();
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
        var model = TestSupport.CreateMain();
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
        var model = TestSupport.CreateMain();
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
        var manager = new TestSupport.ScriptedDialogManager();
        var model = TestSupport.CreateMain(manager: manager);
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
        var manager = new TestSupport.ScriptedDialogManager();
        var model = TestSupport.CreateMain(manager: manager);
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
        var manager = new TestSupport.ScriptedDialogManager();
        var model = TestSupport.CreateMain(manager: manager);
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
        var manager = new TestSupport.ScriptedDialogManager();
        var model = TestSupport.CreateMain(manager: manager);
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
        var manager = new TestSupport.ScriptedDialogManager();
        var model = TestSupport.CreateMain(manager: manager);
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
        var manager = new TestSupport.ScriptedDialogManager();
        var model = TestSupport.CreateMain(manager: manager);
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
        var model = TestSupport.CreateMain();
        await model.New.Execute();

        Assert.False(((ICommand)model.Properties).CanExecute(null));
    }

    [AvaloniaFact]
    public async Task FunctionsExplorer_EditorSelected_ShowsModelessWindow()
    {
        var manager = new TestSupport.ScriptedDialogManager();
        var model = TestSupport.CreateMain(manager: manager);
        await model.New.Execute();

        await model.FunctionsExplorer.Execute();

        var explorer = Assert.IsType<FunctionsExplorerViewModel>(manager.LastShown);
        Assert.Same(model.SelectedItem, explorer.Editor);
        Assert.True(model.IsFunctionsExplorerOpen);
    }

    [AvaloniaFact]
    public async Task FunctionsExplorer_AlreadyOpen_ClosesWindow()
    {
        var manager = new TestSupport.ScriptedDialogManager();
        var model = TestSupport.CreateMain(manager: manager);
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
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        await model.Run.Execute();

        Assert.False(((ICommand)model.FunctionsExplorer).CanExecute(null));
    }

    [AvaloniaFact]
    public async Task FunctionsExplorer_ViewerTab_ClosesWindow()
    {
        var manager = new TestSupport.ScriptedDialogManager();
        var model = TestSupport.CreateMain(manager: manager);
        await model.New.Execute();
        await model.FunctionsExplorer.Execute();

        await model.Run.Execute();

        Assert.False(model.IsFunctionsExplorerOpen);
    }

    [AvaloniaFact]
    public async Task FunctionsExplorer_SwitchEditor_BrowsesNewEditor()
    {
        var manager = new TestSupport.ScriptedDialogManager();
        var model = TestSupport.CreateMain(manager: manager);
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

        Assert.True(model.IsFunctionsExplorerOpen);
        Assert.Same(model.SelectedItem, explorer.Editor);
        Assert.Equal("2", Assert.Single(explorer.Groups).Name);
    }

    [AvaloniaTheory(Timeout = 10000)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Close_InputDialogConfirmedOrCancelled_ReturnsDialogResult(bool accept)
    {
        var ownerModel = TestSupport.CreateMain();
        var owner = new Window { DataContext = ownerModel };
        using var window = TestSupport.Show(owner);
        var dialogs = new HanumanInstitute.MvvmDialogs.Avalonia.DialogService(new TestSupport.OwnerDialogManager(owner));
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
        var ownerModel = TestSupport.CreateMain();
        var owner = new Window { DataContext = ownerModel };
        using var window = TestSupport.Show(owner);
        var dialogs = new HanumanInstitute.MvvmDialogs.Avalonia.DialogService(new TestSupport.OwnerDialogManager(owner));
        var help = TestSupport.CreateHelp();
        var result = dialogs.ShowDialogAsync(ownerModel, help);

        ((ICommand)help.Close).Execute(null);
        var actual = await result.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(actual);
    }

    [AvaloniaFact]
    public void Threads_MultiThreadingOff_ReturnsOne()
    {
        var settings = new TestSupport.MemorySettingsProvider { Value = { VapourSynthThreads = 8 } };
        var model = TestSupport.CreateMain(settings: settings);

        Assert.Equal(1, model.Threads);
    }

    [AvaloniaFact]
    public void Threads_MultiThreadingOn_UsesVapourSynthThreads()
    {
        var settings = new TestSupport.MemorySettingsProvider { Value = { VapourSynthThreads = 8 } };
        var model = TestSupport.CreateMain(settings: settings);
        model.IsMultiThreaded = true;

        Assert.Equal(8, model.Threads);
    }

    [AvaloniaFact]
    public void Threads_SettingsSavedWhileMultiThreaded_RaisesThreads()
    {
        var settings = new TestSupport.MemorySettingsProvider { Value = { VapourSynthThreads = 4 } };
        var model = TestSupport.CreateMain(settings: settings);
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
        var settings = new TestSupport.MemorySettingsProvider();
        var model = TestSupport.CreateMain(settings: settings);

        model.OnClosed();

        Assert.Equal(1, settings.SaveCount);
    }

    [AvaloniaFact]
    public void Zoom_SetToZero_EnablesScaleToFit()
    {
        var model = TestSupport.CreateMain();

        model.Zoom = 0;

        Assert.True(model.ZoomScaleToFit);
        Assert.Equal(0, model.Zoom);
    }

    [AvaloniaFact]
    public async Task Toolbar_EditorSelected_ShowsEditorCommands()
    {
        var model = TestSupport.CreateMain();

        await model.New.Execute();

        Assert.True(model.IsEditorSelected);
        Assert.False(model.IsViewerSelected);
        Assert.False(model.IsVapourSynthViewerSelected);
    }

    [AvaloniaFact]
    public async Task Toolbar_VapourSynthViewerSelected_ShowsViewerCommands()
    {
        var model = TestSupport.CreateMain();

        await TestSupport.OpenViewerAsync(model);

        Assert.False(model.IsEditorSelected);
        Assert.True(model.IsViewerSelected);
        Assert.True(model.IsVapourSynthViewerSelected);
    }

    [AvaloniaFact]
    public async Task Toolbar_AviSynthViewerSelected_HidesMultiThreading()
    {
        var model = TestSupport.CreateMain();
        await model.NewAviSynth.Execute();

        await model.Run.Execute();

        Assert.False(model.IsEditorSelected);
        Assert.True(model.IsViewerSelected);
        Assert.False(model.IsVapourSynthViewerSelected);
    }

    [AvaloniaFact]
    public async Task TabBackground_DefaultSettings_UsesEngineTypeColors()
    {
        var model = TestSupport.CreateMain();
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
        var settings = new TestSupport.MemorySettingsProvider();
        var model = TestSupport.CreateMain(settings: settings);
        await model.New.Execute();
        settings.Value.Theme = AppTheme.Dark;
        settings.Save();

        Assert.Equal(TabColors.For(ScriptKind.VapourSynth, false, AppTheme.Dark),
            BrushColor(model.ScriptList[0].TabBackground));
    }

    [AvaloniaFact]
    public async Task TabBackground_TabColorOverride_UsesCustomColor()
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        model.SelectedItem!.TabColor = Colors.HotPink;

        Assert.Equal(Colors.HotPink, BrushColor(model.SelectedItem.TabBackground));
    }

    [AvaloniaFact]
    public async Task ChangeTabColor_Ok_AppliesSelectedHue()
    {
        var manager = new TestSupport.ScriptedDialogManager
        {
            OnShow = dialog =>
            {
                var picker = Assert.IsType<TabColorViewModel>(dialog);
                Assert.Equal(TabColors.For(ScriptKind.VapourSynth, false, AppTheme.Light), picker.Color);
                picker.Color = Colors.HotPink;
                ((ICommand)picker.Ok).Execute(null);
            }
        };
        var model = TestSupport.CreateMain(manager: manager);
        await model.New.Execute();

        await model.ChangeTabColor.Execute();

        Assert.Equal(Colors.HotPink, model.SelectedItem!.TabColor);
        Assert.Equal(Colors.HotPink, BrushColor(model.SelectedItem.TabBackground));
    }

    [AvaloniaFact]
    public async Task ChangeTabColor_Cancel_LeavesExistingHue()
    {
        var manager = new TestSupport.ScriptedDialogManager
        {
            OnShow = dialog => ((ICommand)((TabColorViewModel)dialog).Close).Execute(null)
        };
        var model = TestSupport.CreateMain(manager: manager);
        await model.New.Execute();
        model.SelectedItem!.TabColor = Colors.Orange;

        await model.ChangeTabColor.Execute();

        Assert.Equal(Colors.Orange, model.SelectedItem.TabColor);
    }

    [AvaloniaFact]
    public async Task ChangeTabColor_Default_ClearsOverride()
    {
        var manager = new TestSupport.ScriptedDialogManager
        {
            OnShow = dialog =>
            {
                var picker = Assert.IsType<TabColorViewModel>(dialog);
                ((ICommand)picker.RestoreDefault).Execute(null);
                ((ICommand)picker.Ok).Execute(null);
            }
        };
        var model = TestSupport.CreateMain(manager: manager);
        await model.New.Execute();
        model.SelectedItem!.TabColor = Colors.HotPink;

        await model.ChangeTabColor.Execute();

        Assert.Null(model.SelectedItem.TabColor);
        Assert.Equal(TabColors.For(ScriptKind.VapourSynth, false, AppTheme.Light),
            BrushColor(model.SelectedItem.TabBackground));
    }

    private static Color BrushColor(IBrush brush) => Assert.IsType<SolidColorBrush>(brush).Color;

    [AvaloniaFact]
    public async Task Run_AviSynthEditor_CopiesKindToViewer()
    {
        var model = TestSupport.CreateMain();
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
        var model = TestSupport.CreateMain();
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
        var files = new FakeFileSystemService().Add(path, script);
        var model = TestSupport.CreateMain(files: files);

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
        var model = TestSupport.CreateMain();

        await model.Load.Execute();

        Assert.Empty(model.ScriptList);
        Assert.True(model.IsStartVisible);
        Assert.Null(model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task New_HidesStart()
    {
        var model = TestSupport.CreateMain();
        await model.Load.Execute();

        await model.New.Execute();

        Assert.False(model.IsStartVisible);
        Assert.Single(model.ScriptList);
    }

    [AvaloniaFact]
    public async Task Close_LastTab_ShowsStart()
    {
        var model = TestSupport.CreateMain();
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
        var settings = new TestSupport.MemorySettingsProvider();
        var model = TestSupport.CreateMain(settings: settings);

        await model.New.Execute();
        await model.SelectedItem!.Close.Execute();

        Assert.Empty(settings.Value.RecentFiles);
        Assert.Empty(model.Recents);
    }

    [AvaloniaFact]
    public async Task ReadScriptFileAsync_RemembersRecent()
    {
        const string path = "/scripts/clip.vpy";
        var files = new FakeFileSystemService().Add(path, "clip");
        var settings = new TestSupport.MemorySettingsProvider();
        var model = TestSupport.CreateMain(settings: settings, files: files);

        Assert.True(await model.ReadScriptFileAsync(path));

        Assert.Equal([path], settings.Value.RecentFiles);
        Assert.False(model.IsStartVisible);
    }

    [AvaloniaFact]
    public async Task Close_RecentFile_ShowsRecentOnStart()
    {
        const string path = "/scripts/clip.vpy";
        var files = new FakeFileSystemService().Add(path, "clip");
        var settings = new TestSupport.MemorySettingsProvider();
        var model = TestSupport.CreateMain(settings: settings, files: files);
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
        var files = new FakeFileSystemService().Add(first, "a").Add(second, "b");
        var settings = new TestSupport.MemorySettingsProvider();
        var model = TestSupport.CreateMain(settings: settings, files: files);
        Assert.True(await model.ReadScriptFileAsync(first));
        Assert.True(await model.ReadScriptFileAsync(second));
        Assert.Equal([second, first], settings.Value.RecentFiles);

        Assert.True(await model.ReadScriptFileAsync(first));

        Assert.Equal([first, second], settings.Value.RecentFiles);
    }

    [AvaloniaFact]
    public async Task Recents_Cap8_DropsOldest()
    {
        var disk = new FakeFileSystemService();
        var paths = new string[9];
        for (var i = 0; i < 9; i++)
        {
            paths[i] = "/scripts/" + i + ".vpy";
            disk.Add(paths[i], i.ToString());
        }
        var settings = new TestSupport.MemorySettingsProvider();
        var model = TestSupport.CreateMain(settings: settings, files: disk);

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
        var settings = new TestSupport.MemorySettingsProvider();
        settings.Value.RecentFiles.Add("/scripts/missing.vpy");

        var model = TestSupport.CreateMain(settings: settings);

        Assert.Empty(model.Recents);
        Assert.Empty(settings.Value.RecentFiles);
        Assert.True(settings.SaveCount > 0);
    }

    [AvaloniaFact]
    public async Task OpenRecent_Missing_PrunesAndShowsError()
    {
        var manager = new TestSupport.FakeDialogManager();
        var settings = new TestSupport.MemorySettingsProvider();
        const string missing = "/scripts/missing.vpy";
        var files = new FakeFileSystemService().Add(missing, "clip");
        settings.Value.RecentFiles.Add(missing);
        var model = TestSupport.CreateMain(settings: settings, manager: manager, files: files);
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
        var files = new FakeFileSystemService().Add(path, "original");
        var settings = new TestSupport.MemorySettingsProvider();
        var model = TestSupport.CreateMain(settings: settings, files: files);
        await model.New.Execute();
        var editor = Assert.IsType<EditorViewModel>(model.SelectedItem);
        editor.FileName = path;
        editor.Script = "saved";

        await model.Save.Execute();

        Assert.Equal([path], settings.Value.RecentFiles);
        Assert.Equal("saved", files.File.ReadAllText(path));
    }
}
