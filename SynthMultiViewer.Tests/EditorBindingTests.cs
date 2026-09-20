using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using HanumanInstitute.MediaSynthUI;
using HanumanInstitute.SynthMultiViewer.Controls;
using HanumanInstitute.SynthMultiViewer.Helpers;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using HanumanInstitute.SynthMultiViewer.Views;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class EditorBindingTests
{
    [AvaloniaFact]
    public void ScriptText_DocumentEdited_UpdatesViewModel()
    {
        var model = new EditorViewModel { Script = "original" };
        var view = new EditorView { DataContext = model };
        using var window = TestSupport.Show(new() { Content = view });
        var editor = view.FindControl<BindableTextEditor>("Editor")!;

        editor.Document.Insert(editor.Document.TextLength, " edit");

        Assert.Equal("original edit", model.Script);
    }

    [AvaloniaFact]
    public void ScriptText_EditUndone_RestoresViewModelText()
    {
        var model = new EditorViewModel { Script = "original" };
        var view = new EditorView { DataContext = model };
        using var window = TestSupport.Show(new() { Content = view });
        var editor = view.FindControl<BindableTextEditor>("Editor")!;
        editor.Document.Insert(editor.Document.TextLength, " edit");

        editor.Undo();

        Assert.Equal("original", editor.Text);
        Assert.Equal("original", model.Script);
    }

    [AvaloniaFact]
    public void ScriptText_DataContextReplaced_StopsUpdatingPreviousModel()
    {
        var first = new EditorViewModel { Script = "first" };
        var second = new EditorViewModel { Script = "second" };
        var view = new EditorView { DataContext = first };
        using var window = TestSupport.Show(new() { Content = view });
        var editor = view.FindControl<BindableTextEditor>("Editor")!;

        view.DataContext = second;
        first.Script = "detached";
        editor.Document.Insert(editor.Document.TextLength, " edit");

        Assert.Equal("second edit", editor.Text);
        Assert.Equal("second edit", second.Script);
        Assert.Equal("detached", first.Script);
    }

    [AvaloniaFact]
    public void ScriptText_ViewModelChanges_UpdatesDocument()
    {
        var model = new EditorViewModel { Script = "original" };
        var view = new EditorView { DataContext = model };
        using var window = TestSupport.Show(new() { Content = view });
        var editor = view.FindControl<BindableTextEditor>("Editor")!;

        model.Script = "replacement";

        Assert.Equal("replacement", editor.Text);
    }

    [AvaloniaFact]
    public void HighlightSource_KindAviSynth_LoadsAviSynthDefinition()
    {
        var model = new EditorViewModel { Kind = ScriptKind.AviSynth, Script = "BlankClip()" };
        var view = new EditorView { DataContext = model };

        using var window = TestSupport.Show(new() { Content = view });
        var editor = view.FindControl<BindableTextEditor>("Editor")!;

        Assert.Equal("HighlightAviSynth.xshd", SyntaxHighlight.GetSource(editor));
        Assert.NotNull(editor.SyntaxHighlighting);
        Assert.Equal("AviSynth", editor.SyntaxHighlighting.Name);
    }

    [AvaloniaFact]
    public void HighlightSource_KindVapourSynth_LoadsPythonDefinition()
    {
        var model = new EditorViewModel { Kind = ScriptKind.VapourSynth, Script = "clip = core.std.BlankClip()" };
        var view = new EditorView { DataContext = model };

        using var window = TestSupport.Show(new() { Content = view });
        var editor = view.FindControl<BindableTextEditor>("Editor")!;

        Assert.Equal("HighlightVapourSynth.xshd", SyntaxHighlight.GetSource(editor));
        Assert.NotNull(editor.SyntaxHighlighting);
        Assert.Equal("Python", editor.SyntaxHighlighting.Name);
    }
}
