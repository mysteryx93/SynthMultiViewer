using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media;
using System.IO.Abstractions;
using HanumanInstitute.MediaSynthUI;
using HanumanInstitute.ScriptAssist.Services;
using HanumanInstitute.SynthMultiViewer.Controls;
using HanumanInstitute.SynthMultiViewer.Helpers;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using HanumanInstitute.SynthMultiViewer.Views;
using ReactiveUI;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class BehaviorTests
{
    [AvaloniaFact]
    public async Task HeaderEditDone_EnterPressed_CommitsBoundName()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();

        view.KeyTextInput("Renamed tab");
        TestSupport.Press(view, Key.Enter);

        Assert.Equal("Renamed tab", model.SelectedItem!.DisplayName);
        Assert.False(model.SelectedItem.IsEditingHeader);
    }

    [AvaloniaFact]
    public async Task HeaderEditCancel_EscapePressed_RestoresName()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();
        var original = model.SelectedItem!.DisplayName;
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();

        view.KeyTextInput("Renamed tab");
        TestSupport.Press(view, Key.Escape);

        Assert.Equal(original, model.SelectedItem.DisplayName);
        Assert.False(model.SelectedItem.IsEditingHeader);
    }

    [AvaloniaFact]
    public async Task HeaderEditDone_BlankName_RestoresName()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();
        var original = model.SelectedItem!.DisplayName;
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();
        var box = view.GetVisualDescendants().OfType<TextBox>()
            .Single(x => x.Classes.Contains("tab-header-edit"));

        box.Text = "";
        TestSupport.Press(view, Key.Enter);

        Assert.False(model.SelectedItem.IsEditingHeader);
        Assert.Equal(original, model.SelectedItem.DisplayName);
    }

    [AvaloniaFact]
    public async Task HeaderEditDone_EmptyNameClickAway_RestoresName()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();
        var original = model.SelectedItem!.DisplayName;
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();
        var box = view.GetVisualDescendants().OfType<TextBox>()
            .Single(x => x.Classes.Contains("tab-header-edit"));

        box.Text = "   ";
        view.MouseDown(new(20, view.Bounds.Height - 20), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.False(model.SelectedItem.IsEditingHeader);
        Assert.Equal(original, model.SelectedItem.DisplayName);
    }

    [AvaloniaFact]
    public async Task HeaderEditDone_ViewerSurfaceClicked_CommitsBoundName()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();

        view.KeyTextInput("Viewer name");
        var host = view.GetVisualDescendants().OfType<SynthPlayerHost>().Single();
        var point = host.TranslatePoint(new(20, 20), view)!.Value;
        view.MouseDown(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        var viewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);
        Assert.False(viewer.IsEditingHeader);
        Assert.Equal("Viewer name", viewer.DisplayName);
    }

    [AvaloniaFact]
    public void HasCapture_ClickOutside_ReleasesCapture()
    {
        var box = new TextBox { Width = 80, Height = 24 };
        var outside = new Border
        {
            Width = 100,
            Height = 80,
            Background = Brushes.Gray
        };
        var view = new Window
        {
            Width = 400,
            Height = 200,
            Content = new StackPanel { Children = { box, outside } }
        };
        using var window = TestSupport.Show(view);
        MouseCapture.SetHasCapture(box, true);
        box.Focus();
        Dispatcher.UIThread.RunJobs();

        var point = outside.TranslatePoint(new(10, 10), view)!.Value;
        view.MouseDown(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.False(MouseCapture.GetHasCapture(box));
    }

    [AvaloniaFact]
    public async Task WhenVisible_HeaderEditorShown_FocusesAndSelectsText()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();

        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();

        var box = view.GetVisualDescendants().OfType<TextBox>()
            .Single(x => x.Classes.Contains("tab-header-edit"));
        Assert.True(box.IsFocused);
        Assert.Equal(box.Text!.Length, Math.Abs(box.SelectionEnd - box.SelectionStart));
    }

    [AvaloniaFact]
    public async Task New_Executed_FocusesScriptEditor()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.Load.Execute();
        Dispatcher.UIThread.RunJobs();

        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();

        Assert.True(VisibleEditor(view).TextArea.IsFocused);
    }

    [AvaloniaFact]
    public async Task NewAviSynth_Executed_FocusesScriptEditor()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.Load.Execute();
        Dispatcher.UIThread.RunJobs();

        await model.NewAviSynth.Execute();
        Dispatcher.UIThread.RunJobs();

        Assert.True(VisibleEditor(view).TextArea.IsFocused);
        Assert.Equal(ScriptKind.AviSynth, Assert.IsType<EditorViewModel>(model.SelectedItem).Kind);
    }

    [AvaloniaFact]
    public async Task ReadScriptFileAsync_OpensDocument_FocusesScriptEditor()
    {
        const string path = "/scripts/clip.vpy";
        var files = new FakeFileSystemService().Add(path, "clip = core.std.BlankClip()");
        var model = TestSupport.CreateMain(files: files);
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.Load.Execute();
        Dispatcher.UIThread.RunJobs();

        await model.ReadScriptFileAsync(path);
        Dispatcher.UIThread.RunJobs();

        Assert.True(VisibleEditor(view).TextArea.IsFocused);
        Assert.Equal(path, Assert.IsType<EditorViewModel>(model.SelectedItem).FileName);
    }

    [AvaloniaFact]
    public void LostFocus_CommandReplaced_DetachesOldHandler()
    {
        var control = new TextBox();
        var firstCalls = 0;
        var secondCalls = 0;
        using var first = ReactiveCommand.Create(() => firstCalls++);
        using var second = ReactiveCommand.Create(() => secondCalls++);
        EventCommands.SetLostFocus(control, first);

        EventCommands.SetLostFocus(control, second);
        control.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));
        EventCommands.SetLostFocus(control, null);
        control.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));

        Assert.Equal(0, firstCalls);
        Assert.Equal(1, secondCalls);
    }

    [AvaloniaFact]
    public async Task SeekKeys_HeaderEditorFocused_LeavesPositionUnchanged()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        var viewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);
        viewer.Duration = TimeSpan.FromSeconds(239);
        viewer.Position = TimeSpan.FromSeconds(20);
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();
        view.KeyTextInput("Renamed");

        TestSupport.Press(view, Key.Left);

        Assert.Equal(TimeSpan.FromSeconds(20), viewer.Position);
        Assert.True(viewer.IsEditingHeader);
        Assert.Equal("Renamed", viewer.DisplayName);
    }

    [AvaloniaFact]
    public async Task PlayPauseKeys_HeaderEditorFocused_LeavesIsPlayingUnchanged()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        var viewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();
        view.KeyTextInput("Name");

        TestSupport.Press(view, Key.Space);

        Assert.False(viewer.IsPlaying);
        Assert.True(viewer.IsEditingHeader);
        Assert.Equal("Name", viewer.DisplayName);
    }

    [AvaloniaFact]
    public void DropFiles_TextDropped_IgnoresDrop()
    {
        var paths = new List<string>();
        using var command = ReactiveCommand.Create<IEnumerable<string>>(files => paths.AddRange(files));
        var view = new Window { Width = 200, Height = 200, Background = Brushes.White };
        DragDrop.SetAllowDrop(view, true);
        FileDropBehavior.SetCommand(view, command);
        using var window = TestSupport.Show(view);
        using var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText("not a file"));

        view.DragDrop(new(20, 80), RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        view.DragDrop(new(20, 80), RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
        view.DragDrop(new(20, 80), RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);

        Assert.Empty(paths);
    }

    [AvaloniaFact]
    public async Task DropFiles_FileDroppedOnEditor_OpensScriptWithoutInsertingPath()
    {
        var files = new FileSystemService(new FileSystem());
        var path = files.Path.Combine(files.Path.GetTempPath(), files.Path.GetRandomFileName() + ".vpy");
        files.File.WriteAllText(path, "clip = core.std.BlankClip()");
        try
        {
            var model = TestSupport.CreateMain(files: files);
            var view = new MainView { DataContext = model };
            using var window = TestSupport.Show(view);
            await model.New.Execute();
            Dispatcher.UIThread.RunJobs();
            var editor = view.GetVisualDescendants().OfType<BindableTextEditor>().Single();
            var original = editor.Text;
            var storage = await view.StorageProvider.TryGetFileFromPathAsync(new(path));
            Assert.NotNull(storage);
            using var data = new DataTransfer();
            data.Add(DataTransferItem.CreateFile(storage));
            data.Add(DataTransferItem.CreateText(path));
            var point = editor.TranslatePoint(new(20, 20), view)!.Value;

            view.DragDrop(point, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
            view.DragDrop(point, RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
            view.DragDrop(point, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);
            for (var i = 0; i < 50 && (model.SelectedItem as EditorViewModel)?.FileName != path; i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Yield();
            }

            var opened = Assert.IsType<EditorViewModel>(model.SelectedItem);
            Assert.Equal(path, opened.FileName);
            Assert.Equal("clip = core.std.BlankClip()", opened.Script);
            Assert.Equal(original, editor.Text);
            Assert.DoesNotContain(path, editor.Text);
            Assert.True(VisibleEditor(view).TextArea.IsFocused);
        }
        finally
        {
            files.DeleteFileSilent(path);
        }
    }

    [AvaloniaFact]
    public async Task ZoomKeys_EditorFocused_DoNotStealEqualsOrMinus()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model, Width = 640, Height = 320 };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();
        var editor = VisibleEditor(view);
        editor.Focus();
        editor.Text = "clip";
        editor.CaretOffset = 4;
        Dispatcher.UIThread.RunJobs();
        var zoom = model.Zoom;

        TestSupport.Press(view, Key.OemPlus);
        view.KeyTextInput("=");
        TestSupport.Press(view, Key.OemMinus);
        view.KeyTextInput("-");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("clip=-", editor.Text);
        Assert.Equal(zoom, model.Zoom);
    }

    [AvaloniaFact]
    public async Task CopyFrameKeys_EditorFocused_LeavesClipboardUnchanged()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        view.GetVisualDescendants().OfType<SynthPlayerHost>().Single().PresentTestFrame(CreateFrame());
        Dispatcher.UIThread.RunJobs();
        model.SelectedItem = model.ScriptList.OfType<EditorViewModel>().Last();
        Dispatcher.UIThread.RunJobs();
        var editor = view.GetVisualDescendants().OfType<BindableTextEditor>()
            .First(x => x.IsEffectivelyVisible);
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        var clipboard = view.Clipboard!;
        await clipboard.SetTextAsync("keep");

        TestSupport.Press(view, Key.C, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(await clipboard.TryGetBitmapAsync());
    }

    [AvaloniaFact]
    public async Task Viewer_Tab_DoesNotFocusToolbarOrTabClose()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model, Width = 800, Height = 480 };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        var viewer = view.GetVisualDescendants().OfType<ViewerView>().First(x => x.IsEffectivelyVisible);
        viewer.Focus();
        Dispatcher.UIThread.RunJobs();

        TestSupport.Press(view, Key.Tab);
        Dispatcher.UIThread.RunJobs();

        var focused = TopLevel.GetTopLevel(view)!.FocusManager!.GetFocusedElement() as Visual;
        Assert.NotNull(focused);
        Assert.False(IsToolbarOrTabClose(focused));
    }

    [AvaloniaFact]
    public void ListBoxTextSearch_RepeatedLetter_SelectsNextMatch()
    {
        var list = new ListBox
        {
            Width = 200,
            Height = 160,
            ItemsSource = new[] { "Crop", "CropAbs", "BlankClip" }
        };
        ListBoxTextSearch.SetEnabled(list, true);
        var host = new Window { Width = 240, Height = 200, Content = list };
        using var shown = TestSupport.Show(host);
        list.Focus();
        Dispatcher.UIThread.RunJobs();

        Type(list, "c");
        var first = list.SelectedItem;
        Type(list, "c");

        Assert.Equal("Crop", first);
        Assert.Equal("CropAbs", list.SelectedItem);
    }

    [AvaloniaFact]
    public void ListBoxTextSearch_DifferentLetter_StartsNewSearch()
    {
        var list = new ListBox
        {
            Width = 200,
            Height = 160,
            ItemsSource = new[] { "Crop", "BlankClip" }
        };
        ListBoxTextSearch.SetEnabled(list, true);
        var host = new Window { Width = 240, Height = 200, Content = list };
        using var shown = TestSupport.Show(host);
        list.Focus();
        Dispatcher.UIThread.RunJobs();

        Type(list, "c");
        Type(list, "b");

        Assert.Equal("BlankClip", list.SelectedItem);
    }

    [AvaloniaFact]
    public void ListBoxTextSearch_MissThenLetter_SelectsMatch()
    {
        var list = new ListBox
        {
            Width = 200,
            Height = 160,
            ItemsSource = new[] { "Crop", "BlankClip" }
        };
        ListBoxTextSearch.SetEnabled(list, true);
        var host = new Window { Width = 240, Height = 200, Content = list };
        using var shown = TestSupport.Show(host);
        list.Focus();
        Dispatcher.UIThread.RunJobs();

        Type(list, "z");
        Type(list, "c");

        Assert.Equal("Crop", list.SelectedItem);
    }

    private static bool IsToolbarOrTabClose(Visual focused)
    {
        for (var visual = focused; visual != null; visual = visual.GetVisualParent())
        {
            if (visual is Button button && button.Classes.Contains("tab-close"))
            {
                return true;
            }

            if (visual is Control control &&
                control.GetVisualParent() is StackPanel { Classes: var classes } &&
                classes.Contains("toolbar"))
            {
                return true;
            }
        }

        return false;
    }

    private static void Type(ListBox list, string text) =>
        list.RaiseEvent(new TextInputEventArgs
        {
            RoutedEvent = InputElement.TextInputEvent,
            Source = list,
            Text = text
        });

    private static WriteableBitmap CreateFrame() =>
        new(new(8, 8), new(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

    private static BindableTextEditor VisibleEditor(Visual root) =>
        root.GetVisualDescendants().OfType<BindableTextEditor>().First(x => x.IsEffectivelyVisible);
}
