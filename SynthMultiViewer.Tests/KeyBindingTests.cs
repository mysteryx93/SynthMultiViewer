using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform;
using Avalonia.VisualTree;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FileSystem;
using HanumanInstitute.ScriptAssist;
using HanumanInstitute.ScriptAssist.VapourSynth;
using HanumanInstitute.SynthMultiViewer.Controls;
using HanumanInstitute.SynthMultiViewer.Views;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class KeyBindingTests : ViewerTestsBase
{
    private readonly UiTestDriver _ui = new();

    [AvaloniaFact]
    public async Task WindowBindings_FileTabsAndDialogs_Execute()
    {
        const string path = "/scripts/open.vpy";
        const string savePath = "/scripts/keybind-save.vpy";
        var files = FilesFake.Add(path, "BlankClip()\n");
        var manager = DialogsFake;
        manager.QueueFrameworkResult(new IDialogStorageFile[] { new DesktopDialogStorageFile(path) });
        manager.QueueFrameworkResult(null);
        var model = Workspace;
        var view = new MainView { DataContext = model, Width = 640, Height = 400 };
        using var shown = _ui.Show(view);
        view.Focus();
        Dispatcher.UIThread.RunJobs();

        _ui.Press(view, Key.N, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var vs = Assert.IsType<EditorViewModel>(model.SelectedItem);
        var vsKind = vs.Kind;
        vs.MarkSaved();
        _ui.Press(view, Key.M, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var avs = Assert.IsType<EditorViewModel>(model.SelectedItem);
        var avsKind = avs.Kind;
        avs.MarkSaved();
        _ui.Press(view, Key.O, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var openedPath = Assert.IsType<EditorViewModel>(model.SelectedItem).FileName;
        _ui.Press(view, Key.D1, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var afterCtrl1 = model.SelectedItem;
        _ui.Press(view, Key.Tab, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var afterCtrlTab = model.SelectedItem;
        _ui.Press(view, Key.Tab, RawInputModifiers.Control | RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();
        var afterShiftTab = model.SelectedItem;
        _ui.Press(view, Key.Right, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        var afterAltRight = model.ScriptList[1];
        _ui.Press(view, Key.Left, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        var afterAltLeft = model.ScriptList[0];
        _ui.Press(view, Key.F2);
        Dispatcher.UIThread.RunJobs();
        var editing = vs.IsEditingHeader;
        vs.IsEditingHeader = false;
        Dispatcher.UIThread.RunJobs();
        _ui.Press(view, Key.T, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var tabColor = manager.LastDialog;
        _ui.Press(view, Key.F1);
        Dispatcher.UIThread.RunJobs();
        var help = manager.LastDialog;
        _ui.Press(view, Key.OemComma, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var settings = manager.LastDialog;
        _ui.Press(view, Key.S, RawInputModifiers.Control | RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();
        var saveAs = manager.LastFrameworkSettings;
        manager.QueueFrameworkResult(new DesktopDialogStorageFile(savePath));
        _ui.Press(view, Key.S, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var savedPath = vs.FileName;
        _ui.Press(view, Key.F5);
        Dispatcher.UIThread.RunJobs();
        var afterF5 = model.SelectedItem;
        var beforeClose = model.ScriptList.Count;
        _ui.Press(view, Key.F4, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var afterClose = model.ScriptList.Count;

        Assert.Equal(ScriptKind.VapourSynth, vsKind);
        Assert.Equal(ScriptKind.AviSynth, avsKind);
        Assert.Equal(path, openedPath);
        Assert.Same(vs, afterCtrl1);
        Assert.Same(avs, afterCtrlTab);
        Assert.Same(vs, afterShiftTab);
        Assert.Same(vs, afterAltRight);
        Assert.Same(vs, afterAltLeft);
        Assert.True(editing);
        Assert.IsType<TabColorViewModel>(tabColor);
        Assert.IsType<HelpViewModel>(help);
        Assert.IsType<SettingsViewModel>(settings);
        Assert.IsType<MvvmDialogs.FrameworkDialogs.SaveFileDialogSettings>(saveAs);
        Assert.Equal(savePath, savedPath);
        Assert.IsType<ViewerViewModel>(afterF5);
        Assert.Equal(beforeClose - 1, afterClose);
    }

    [AvaloniaFact]
    public async Task ViewerBindings_SeekPlayZoomPropertiesAndCopy_Execute()
    {
        var manager = DialogsFake;
        manager.OnShow = Respond;
        void Respond(IModalDialogViewModel dialog)
        {
            var input = (InputViewModel)dialog;
            input.Value = "30";
            ((System.Windows.Input.ICommand)input.Ok).Execute(null);
        }
        var model = Workspace;
        var view = new MainView { DataContext = model, Width = 640, Height = 400 };
        using var shown = _ui.Show(view);
        await model.New.Execute();
        await model.Run.Execute();
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        var first = model.ScriptList.OfType<ViewerViewModel>().First();
        var second = (ViewerViewModel)model.SelectedItem!;
        first.Duration = TimeSpan.FromSeconds(239);
        second.Duration = TimeSpan.FromSeconds(239);
        second.Position = TimeSpan.FromSeconds(20);
        view.Focus();
        Dispatcher.UIThread.RunJobs();

        _ui.Press(view, Key.Left);
        Dispatcher.UIThread.RunJobs();
        var afterLeft = second.Position;
        _ui.Press(view, Key.Right);
        Dispatcher.UIThread.RunJobs();
        var afterRight = second.Position;
        _ui.Press(view, Key.Left, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var afterCtrlLeft = second.Position;
        _ui.Press(view, Key.Right, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var afterCtrlRight = second.Position;
        _ui.Press(view, Key.Space);
        Dispatcher.UIThread.RunJobs();
        var playing = second.IsPlaying;
        _ui.Press(view, Key.Space);
        Dispatcher.UIThread.RunJobs();
        var paused = second.IsPlaying;
        var startZoom = model.Zoom;
        _ui.Press(view, Key.OemPlus);
        Dispatcher.UIThread.RunJobs();
        var zoomed = model.Zoom;
        _ui.Press(view, Key.OemMinus);
        Dispatcher.UIThread.RunJobs();
        var zoomRestored = model.Zoom;
        _ui.Press(view, Key.G, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var afterGoTo = second.Position;
        _ui.Press(view, Key.I, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var propertiesOpen = model.IsPropertiesOpen;
        _ui.Press(view, Key.I, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var propertiesClosed = model.IsPropertiesOpen;
        first.Position = TimeSpan.FromSeconds(5);
        _ui.Press(view, Key.F6, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var synced = first.Position;
        var threaded = model.IsMultiThreaded;
        _ui.Press(view, Key.F8);
        Dispatcher.UIThread.RunJobs();
        var afterThreads = model.IsMultiThreaded;
        var square = model.SquarePixels;
        _ui.Press(view, Key.F9);
        Dispatcher.UIThread.RunJobs();
        var afterSquare = model.SquarePixels;
        view.GetVisualDescendants().OfType<SynthPlayerHost>().First(x => x.IsEffectivelyVisible)
            .PresentTestFrame(new(new(8, 8), new(96, 96),
                PixelFormat.Bgra8888, AlphaFormat.Opaque));
        Dispatcher.UIThread.RunJobs();
        _ui.Press(view, Key.C, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var copied = await view.Clipboard!.TryGetBitmapAsync();

        Assert.Equal(TimeSpan.FromSeconds(19), afterLeft);
        Assert.Equal(TimeSpan.FromSeconds(20), afterRight);
        Assert.Equal(TimeSpan.FromSeconds(10), afterCtrlLeft);
        Assert.Equal(TimeSpan.FromSeconds(20), afterCtrlRight);
        Assert.True(playing);
        Assert.False(paused);
        Assert.NotEqual(startZoom, zoomed);
        Assert.Equal(startZoom, zoomRestored);
        Assert.Equal(TimeSpan.FromSeconds(29), afterGoTo);
        Assert.True(propertiesOpen);
        Assert.False(propertiesClosed);
        Assert.Equal(second.Position, synced);
        Assert.NotEqual(threaded, afterThreads);
        Assert.NotEqual(square, afterSquare);
        Assert.NotNull(copied);
        Assert.Equal(new(8, 8), copied.PixelSize);
    }

    [AvaloniaFact]
    public async Task EditorBindings_AvaloniaEditAndAssist_Execute()
    {
        var factory = new CountingFactory();
        var editor = new BindableTextEditor
        {
            Text = "alpha\nbeta\n",
            LanguageFactory = factory
        };
        var window = new Window { Content = editor, Width = 480, Height = 240 };
        using var shown = _ui.Show(window);
        editor.TextArea.Focus();
        editor.CaretOffset = 0;
        Dispatcher.UIThread.RunJobs();

        _ui.Press(window, Key.D, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var afterDeleteLine = editor.Text;
        _ui.Press(window, Key.Z, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var afterUndoDelete = editor.Text;
        _ui.Press(window, Key.A, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var selectedAll = editor.SelectionLength;
        var selectAllLength = editor.Text.Length;
        editor.SelectionLength = 0;
        editor.CaretOffset = 0;
        _ui.Press(window, Key.Tab);
        Dispatcher.UIThread.RunJobs();
        var afterTab = editor.Text;
        _ui.Press(window, Key.Z, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var afterUndoTab = editor.Text;
        _ui.Press(window, Key.Y, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var afterRedoTab = editor.Text;
        _ui.Press(window, Key.Tab, RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();
        var afterUnindent = editor.Text;
        editor.Text = "hello world\nsecond";
        editor.CaretOffset = 5;
        Dispatcher.UIThread.RunJobs();
        _ui.Press(window, Key.Home);
        Dispatcher.UIThread.RunJobs();
        var afterHome = editor.CaretOffset;
        _ui.Press(window, Key.End);
        Dispatcher.UIThread.RunJobs();
        var afterEnd = editor.CaretOffset;
        _ui.Press(window, Key.End, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var afterCtrlEnd = editor.CaretOffset;
        var documentLength = editor.Text.Length;
        _ui.Press(window, Key.Home, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var afterCtrlHome = editor.CaretOffset;
        _ui.Press(window, Key.Right, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var afterWordRight = editor.CaretOffset;
        _ui.Press(window, Key.Left, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var afterWordLeft = editor.CaretOffset;
        editor.CaretOffset = 11;
        _ui.Press(window, Key.Back, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var afterCtrlBack = editor.Text;
        editor.CaretOffset = 0;
        _ui.Press(window, Key.Delete, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var afterCtrlDelete = editor.Text;
        editor.Text = "beta one\nbeta two\n";
        editor.CaretOffset = 0;
        _ui.Press(window, Key.F, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var findClosed = editor.SearchPanel.IsClosed;
        _ui.Press(window, Key.H, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var replaceMode = editor.SearchPanel.IsReplaceMode;
        editor.SearchPanel.SearchPattern = "beta";
        _ui.Press(window, Key.F3);
        Dispatcher.UIThread.RunJobs();
        var firstFindText = editor.SelectedText;
        var firstMatch = editor.SelectionStart;
        _ui.Press(window, Key.F3);
        Dispatcher.UIThread.RunJobs();
        var secondFindText = editor.SelectedText;
        var secondMatch = editor.SelectionStart;
        _ui.Press(window, Key.F3, RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();
        var previousMatch = editor.SelectionStart;
        editor.SearchPanel.ReplacePattern = "gamma";
        editor.CaretOffset = 0;
        _ui.Press(window, Key.R, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        var afterReplace = editor.Text;
        _ui.Press(window, Key.A, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        var afterReplaceAll = editor.Text;
        _ui.Press(window, Key.Escape);
        Dispatcher.UIThread.RunJobs();
        var searchClosed = editor.SearchPanel.IsClosed;
        editor.Text = "core.std.Cr";
        editor.CaretOffset = editor.Text.Length;
        _ui.Press(window, Key.Space, RawInputModifiers.Control);
        await Task.Delay(80, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        var completion = editor.Completion;
        _ui.Press(window, Key.Escape);
        Dispatcher.UIThread.RunJobs();
        var dismissed = editor.DisplayedReply;
        editor.Text = "core.std.Crop(";
        editor.CaretOffset = editor.Text.Length;
        _ui.Press(window, Key.Space, RawInputModifiers.Control | RawInputModifiers.Shift);
        await Task.Delay(80, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        var insight = editor.DisplayedReply?.Insight;
        _ui.Press(window, Key.R, RawInputModifiers.Control | RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();
        var refreshCount = factory.RefreshCount;
        editor.DismissCompletion();

        Assert.StartsWith("beta", afterDeleteLine, StringComparison.Ordinal);
        Assert.StartsWith("alpha", afterUndoDelete, StringComparison.Ordinal);
        Assert.Equal(selectAllLength, selectedAll);
        Assert.StartsWith("\t", afterTab, StringComparison.Ordinal);
        Assert.StartsWith("alpha", afterUndoTab, StringComparison.Ordinal);
        Assert.StartsWith("\t", afterRedoTab, StringComparison.Ordinal);
        Assert.StartsWith("alpha", afterUnindent, StringComparison.Ordinal);
        Assert.Equal(0, afterHome);
        Assert.Equal(11, afterEnd);
        Assert.Equal(documentLength, afterCtrlEnd);
        Assert.Equal(0, afterCtrlHome);
        Assert.True(afterWordRight > 0);
        Assert.Equal(0, afterWordLeft);
        Assert.StartsWith("hello ", afterCtrlBack, StringComparison.Ordinal);
        Assert.DoesNotContain("hello", afterCtrlDelete, StringComparison.Ordinal);
        Assert.False(findClosed);
        Assert.True(replaceMode);
        Assert.Equal("beta", firstFindText);
        Assert.Equal("beta", secondFindText);
        Assert.NotEqual(firstMatch, secondMatch);
        Assert.Equal(firstMatch, previousMatch);
        Assert.StartsWith("gamma", afterReplace, StringComparison.Ordinal);
        Assert.Contains("gamma two", afterReplaceAll, StringComparison.Ordinal);
        Assert.DoesNotContain("beta", afterReplaceAll, StringComparison.Ordinal);
        Assert.True(searchClosed);
        Assert.NotNull(completion);
        Assert.Null(dismissed);
        Assert.NotNull(insight);
        Assert.Equal(1, refreshCount);
    }

    private sealed class CountingFactory : IScriptLanguageFactory
    {
        public int RefreshCount { get; private set; }
        public bool IsEnabled { get; set; } = true;
        private readonly ILanguageService _service;

        public CountingFactory()
        {
            var source = new Mock<ISymbolSource>();
            source.Setup(s => s.Enumerate()).Returns(
            [
                new Symbol("core.std.Crop", ["clip:vnode", "left:int:opt"], ReturnType: "clip:vnode")
            ]);
            _service = new LanguageService(new VapourSynthLanguage(), new CatalogCache(source.Object));
        }

        public ILanguageService? Create(string language) => IsEnabled ? _service : null;
        public void Configure(string language, string catalogKey) { }
        public void Refresh() => RefreshCount++;
        public Task<IReadOnlyList<BrowseGroup>> BrowseAsync(string language, string text,
            CancellationToken cancellationToken, string? documentPath = null,
            IReadOnlyList<string>? extraPackages = null) =>
            _service.BrowseAsync(text, cancellationToken, documentPath, extraPackages);
    }

}
