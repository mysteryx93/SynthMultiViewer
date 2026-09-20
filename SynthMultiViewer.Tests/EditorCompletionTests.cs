using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using HanumanInstitute.ScriptAssist;
using HanumanInstitute.ScriptAssist.AvaloniaEdit;
using HanumanInstitute.ScriptAssist.VapourSynth;
using HanumanInstitute.SynthMultiViewer.Controls;
using Moq;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class EditorCompletionTests
{
    private static readonly Symbol[] Vs =
    [
        new("core.std.Crop", ["clip:vnode", "left:int:opt", "right:int:opt"], ReturnType: "clip:vnode"),
        new("core.std.BlankClip", ["width:int:opt", "height:int:opt"], ReturnType: "clip:vnode"),
        new("core.rife.RIFE", ["clip:vnode", "model:int:opt"], ReturnType: "clip:vnode"),
        new("core.std.SelectEvery", ["clip:vnode", "cycle:int", "offsets:int[]"], ReturnType: "clip:vnode")
    ];

    private static CatalogCache EmptyCatalog()
    {
        var source = new Mock<ISymbolSource>();
        source.Setup(s => s.Enumerate()).Returns([]);
        return new CatalogCache(source.Object);
    }

    private static LanguageService Service() =>
        new(new VapourSynthLanguage(), EmptyCatalog());

    private static void UseService(BindableTextEditor editor, ILanguageService service)
    {
        var factory = new Mock<IScriptLanguageFactory>();
        factory.Setup(f => f.IsEnabled).Returns(true);
        factory.Setup(f => f.Create(It.IsAny<string>())).Returns(service);
        editor.LanguageFactory = factory.Object;
    }

    [AvaloniaFact]
    public void Complete_UnicodeReplacement_UndoRestoresDocument()
    {
        const string text = "# 😀\n变量 = 1\n变suffix";
        var editor = new BindableTextEditor { Text = text };
        var caret = text.IndexOf("变suffix", StringComparison.Ordinal) + 1;
        var item = Assert.Single(Service().Analyze(text, caret, []).Items, x => x.InsertionText == "变量");

        new CompletionData(item).Complete(editor.TextArea, new SimpleSegment(item.Start, item.Length), EventArgs.Empty);
        var replaced = editor.Text;
        editor.Document.UndoStack.Undo();

        Assert.Equal("# 😀\n变量 = 1\n变量", replaced);
        Assert.Equal(text, editor.Text);
    }

    [AvaloniaTheory]
    [InlineData("document")]
    [InlineData("text")]
    [InlineData("caret")]
    [InlineData("editor")]
    [InlineData("escape")]
    [InlineData("kind")]
    public async Task RequestCompletion_StaleChange_DiscardsReply(string change)
    {
        var service = new DelayedService();
        var editor = new BindableTextEditor { Text = "co" };
        UseService(editor, service);
        var other = new TextBox();
        using var shown = TestSupport.Show(new() { Content = new StackPanel { Children = { editor, other } } });
        editor.TextArea.Focus();
        editor.CaretOffset = 2;
        var request = editor.RequestCompletionAsync(delay: TimeSpan.Zero);
        await service.Started.Task;
        switch (change)
        {
            case "text":
                editor.Text = "cor";
                break;
            case "document":
                editor.Document = new("co");
                break;
            case "caret":
                editor.CaretOffset = 1;
                break;
            case "editor":
                other.Focus();
                break;
            case "escape":
                editor.DismissCompletion();
                break;
            case "kind":
                editor.ScriptKind = MediaSynthUI.ScriptKind.AviSynth;
                break;
        }

        service.Reply.SetResult(new([new("core", 0, 2, SymbolKind.Keyword, "core")], null));
        await request;

        Assert.Null(editor.DisplayedReply);
        editor.DismissCompletion();
    }

    [AvaloniaTheory]
    [InlineData("service")]
    [InlineData("path")]
    [InlineData("enabled")]
    public async Task RequestAsync_AssistCallbackChanged_DiscardsReply(string change)
    {
        var delayed = new DelayedService();
        ILanguageService? current = delayed;
        string? path = "/tmp/a.vpy";
        var enabled = true;
        var editor = new TextEditor { Text = "co" };
        var session = new Mock<IAssistSession>();
        session.Setup(s => s.AssistanceEnabled).Returns(() => enabled);
        session.Setup(s => s.ResolveService()).Returns(() => current);
        session.Setup(s => s.DocumentPath).Returns(() => path);
        var assist = new EditorAssist(editor, session.Object);
        assist.Attach();
        using var shown = TestSupport.Show(new() { Content = editor, Width = 400, Height = 200 });
        editor.TextArea.Focus();
        editor.CaretOffset = 2;
        var request = assist.RequestAsync(delay: TimeSpan.Zero);
        await delayed.Started.Task;
        switch (change)
        {
            case "service":
                current = Service();
                break;
            case "path":
                path = "/tmp/b.vpy";
                break;
            case "enabled":
                enabled = false;
                break;
        }

        delayed.Reply.SetResult(new([new("core", 0, 2, SymbolKind.Keyword, "core")], null));
        await request;

        Assert.Null(assist.DisplayedReply);
        assist.Dispose();
    }

    [AvaloniaTheory]
    [InlineData("service")]
    [InlineData("path")]
    [InlineData("enabled")]
    public async Task RequestHover_AssistCallbackChanged_DiscardsHover(string change)
    {
        var delayed = new DelayedService();
        ILanguageService? current = delayed;
        string? path = "/tmp/a.vpy";
        var enabled = true;
        var editor = new TextEditor { Text = "clip" };
        var session = new Mock<IAssistSession>();
        session.Setup(s => s.AssistanceEnabled).Returns(() => enabled);
        session.Setup(s => s.ResolveService()).Returns(() => current);
        session.Setup(s => s.DocumentPath).Returns(() => path);
        var assist = new EditorAssist(editor, session.Object);
        assist.Attach();
        using var shown = TestSupport.Show(new() { Content = editor, Width = 400, Height = 200 });
        editor.TextArea.Focus();
        var request = assist.RequestHoverAsync(0);
        await delayed.Started.Task;
        switch (change)
        {
            case "service":
                current = Service();
                break;
            case "path":
                path = "/tmp/b.vpy";
                break;
            case "enabled":
                enabled = false;
                break;
        }

        delayed.Reply.SetResult(new([], null, new("VideoNode", 0, 4)));
        await request;

        Assert.Null(ToolTip.GetTip(editor.TextArea.TextView));
        assist.Dispose();
    }

    [AvaloniaFact]
    public async Task RequestCompletion_SpaceDuringDebounce_AdvancesInsightParameter()
    {
        var editor = OpenEditor("core.std.Crop(10,");
        using var shown = TestSupport.Show(new() { Content = editor });
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;
        _ = editor.RequestCompletionAsync(showCompletion: false, delay: TimeSpan.FromMilliseconds(80));

        editor.Document.Insert(editor.CaretOffset, " ");
        editor.CaretOffset = editor.Text.Length;
        await Task.Delay(160, TestContext.Current.CancellationToken);

        Assert.Equal(1, editor.DisplayedReply!.Insight!.ActiveParameter);
        editor.DismissCompletion();
    }

    [AvaloniaFact]
    public async Task RequestCompletion_SecondArg_ShowsInsightWithoutList()
    {
        var editor = OpenEditor("core.std.Crop(10, 20");
        using var shown = TestSupport.Show(new() { Content = editor });
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;

        await editor.RequestCompletionAsync(showCompletion: false, delay: TimeSpan.Zero);

        Assert.Null(editor.Completion);
        Assert.Equal(1, editor.DisplayedReply!.Insight!.ActiveParameter);
        editor.DismissCompletion();
    }

    [AvaloniaFact]
    public async Task RequestCompletion_CaretMoves_UpdatesInsightWithoutList()
    {
        var editor = OpenEditor("core.std.Crop(10, 20");
        using var shown = TestSupport.Show(new() { Content = editor });
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;
        await editor.RequestCompletionAsync(showCompletion: false, delay: TimeSpan.Zero);

        editor.CaretOffset = editor.Text.IndexOf('(') + 1;
        await Task.Delay(120, TestContext.Current.CancellationToken);

        Assert.Null(editor.Completion);
        Assert.Equal(0, editor.DisplayedReply!.Insight!.ActiveParameter);
        editor.DismissCompletion();
    }

    [AvaloniaFact]
    public async Task RequestCompletion_CommaRemoved_KeepsInsightWithoutList()
    {
        var editor = OpenEditor("core.std.Crop(10, 20");
        using var shown = TestSupport.Show(new() { Content = editor });
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;
        await editor.RequestCompletionAsync(showCompletion: false, delay: TimeSpan.Zero);
        var comma = editor.Text.IndexOf(',');

        editor.Document.Remove(comma, 1);
        editor.CaretOffset = comma;
        await Task.Delay(120, TestContext.Current.CancellationToken);

        Assert.Null(editor.Completion);
        Assert.Equal(0, editor.DisplayedReply!.Insight!.ActiveParameter);
        editor.DismissCompletion();
    }

    [AvaloniaFact]
    public async Task RequestCompletion_ForceInsightShortcut_ShowsInsightWithoutList()
    {
        var editor = OpenEditor("core.std.Crop(10, 20");
        var window = new Window { Content = editor };
        using var shown = TestSupport.Show(window);
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;
        await editor.RequestCompletionAsync(showCompletion: false, delay: TimeSpan.Zero);

        TestSupport.Press(window, Key.Space, RawInputModifiers.Control | RawInputModifiers.Shift);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Null(editor.Completion);
        Assert.NotNull(editor.DisplayedReply?.Insight);
        editor.DismissCompletion();
    }

    [AvaloniaFact]
    public async Task RequestCompletion_ReplyReady_ShowsPopupAndInsight()
    {
        var service = new DelayedService();
        var editor = new BindableTextEditor { Text = "core.std.Crop(co" };
        UseService(editor, service);
        using var shown = TestSupport.Show(new() { Content = editor });
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;
        service.Reply.SetResult(Service().Analyze(editor.Text, editor.CaretOffset, Vs));

        await editor.RequestCompletionAsync(delay: TimeSpan.Zero);

        Assert.NotNull(editor.DisplayedReply);
        Assert.NotNull(editor.Completion);
        Assert.NotNull(editor.Insight);
        editor.DismissCompletion();
    }

    [AvaloniaFact]
    public async Task KeyText_Equals_DoesNotCommitCompletion()
    {
        var editor = OpenEditor("core.std.Cr");
        var window = new Window { Content = editor };
        using var shown = TestSupport.Show(window);
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;
        await editor.RequestCompletionAsync(delay: TimeSpan.Zero);
        Assert.NotNull(editor.Completion);

        window.KeyTextInput("=");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("core.std.Cr=", editor.Text);
        editor.DismissCompletion();
    }

    [AvaloniaFact]
    public async Task RequestCompletion_MemberDot_ShowsListWithoutInsight()
    {
        var editor = OpenEditor("core.std.Crop(core.rife.");
        using var shown = TestSupport.Show(new() { Content = editor });
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;

        await editor.RequestCompletionAsync(delay: TimeSpan.Zero);

        Assert.NotNull(editor.Completion);
        Assert.Contains(editor.DisplayedReply!.Items, x => x.InsertionText == "RIFE");
        Assert.Null(editor.Insight);
        editor.DismissCompletion();
    }

    [AvaloniaFact]
    public async Task RequestCompletion_ShowCompletionFalse_ShowsInsightWithoutList()
    {
        var editor = OpenEditor("core.std.Crop(core.rife.");
        using var shown = TestSupport.Show(new() { Content = editor });
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;

        await editor.RequestCompletionAsync(showCompletion: false, delay: TimeSpan.Zero);

        Assert.Null(editor.Completion);
        Assert.NotNull(editor.Insight);
        editor.DismissCompletion();
    }

    [AvaloniaFact]
    public async Task Tab_CompletionSelected_InsertsAndOpensInsight()
    {
        var editor = OpenEditor("core.std.Cr");
        var window = new Window { Content = editor };
        using var shown = TestSupport.Show(window);
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;
        await editor.RequestCompletionAsync(delay: TimeSpan.Zero);

        TestSupport.Press(window, Key.Tab);
        window.KeyTextInput("(");
        await Task.Delay(250, TestContext.Current.CancellationToken);
        var text = editor.Text;
        var insight = editor.DisplayedReply?.Insight;
        var completion = editor.Completion;
        TestSupport.Press(window, Key.Escape);

        Assert.Equal("core.std.Crop(", text);
        Assert.NotNull(insight);
        Assert.Null(completion);
        Assert.Null(editor.DisplayedReply);
    }

    [AvaloniaFact]
    public async Task RequestInsertion_AfterLostFocus_InsertsText()
    {
        var editor = OpenEditor("core.std.Cr");
        var window = new Window { Content = editor, Width = 640, Height = 320 };
        using var shown = TestSupport.Show(window);
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;
        await editor.RequestCompletionAsync(delay: TimeSpan.Zero);
        Dispatcher.UIThread.RunJobs();
        var completion = editor.Completion;
        Assert.NotNull(completion);

        editor.RaiseEvent(new(InputElement.LostFocusEvent));
        completion.CompletionList.RequestInsertion(EventArgs.Empty);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("core.std.Crop", editor.Text);
    }

    [AvaloniaFact]
    public async Task Click_CompletionItem_InsertsText()
    {
        var editor = OpenEditor("core.std.Cr");
        var window = new Window { Content = editor, Width = 640, Height = 320 };
        using var shown = TestSupport.Show(window);
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;
        await editor.RequestCompletionAsync(delay: TimeSpan.Zero);
        Dispatcher.UIThread.RunJobs();
        var list = editor.Completion?.CompletionList.ListBox;
        Assert.NotNull(list);
        var item = list.ContainerFromIndex(Math.Max(0, list.SelectedIndex));
        Assert.NotNull(item);
        var root = TopLevel.GetTopLevel(item)!;
        var point = item.TranslatePoint(new(12, Math.Max(1, item.Bounds.Height / 2)), root)!.Value;

        root.MouseDown(point, MouseButton.Left);
        root.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("core.std.Crop", editor.Text);
    }

    [AvaloniaFact]
    public async Task DocumentChange_PendingRequest_CancelsAnalysis()
    {
        var service = new DelayedService();
        var editor = new BindableTextEditor { Text = "core.std.Crop(" };
        UseService(editor, service);
        using var shown = TestSupport.Show(new() { Content = editor, Width = 640, Height = 300 });
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Document.TextLength;
        var request = editor.RequestCompletionAsync(true, TimeSpan.Zero);
        await service.Started.Task;

        editor.Document.Remove(editor.Document.TextLength - 1, 1);
        Dispatcher.UIThread.RunJobs();
        service.Reply.TrySetCanceled();
        try
        {
            await request;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.True(service.Token.IsCancellationRequested);
        editor.DismissCompletion();
    }

    private static BindableTextEditor OpenEditor(string text)
    {
        var editor = new BindableTextEditor { Text = text };
        var source = new Mock<ISymbolSource>();
        source.Setup(s => s.Enumerate()).Returns(Vs);
        UseService(editor, new LanguageService(new VapourSynthLanguage(), new CatalogCache(source.Object)));
        return editor;
    }

    private sealed class DelayedService : ILanguageService
    {
        public TaskCompletionSource<bool> Started { get; } = new();
        public TaskCompletionSource<Reply> Reply { get; } = new();
        public CancellationToken Token { get; private set; }

        public Task<Reply> GetAsync(string text, int caret, CancellationToken cancellationToken,
            string? documentPath = null)
        {
            Token = cancellationToken;
            Started.TrySetResult(true);
            return Reply.Task;
        }

        public Task<IReadOnlyList<BrowseGroup>> BrowseAsync(string text, CancellationToken cancellationToken,
            string? documentPath = null, IReadOnlyList<string>? extraPackages = null) =>
            Task.FromResult<IReadOnlyList<BrowseGroup>>([]);

        public void Invalidate()
        {
        }
    }
}
