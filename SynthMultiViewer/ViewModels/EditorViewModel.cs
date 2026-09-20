using AvaloniaEdit.Document;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Describes an editable script and its optional file path.
/// </summary>
public interface IEditorViewModel : IScriptViewModel
{
    /// <summary>
    /// Gets or sets the script file path, or null for an unsaved script.
    /// </summary>
    string? FileName { get; set; }
    /// <summary>
    /// Gets or sets the script text.
    /// </summary>
    string Script { get; set; }
    /// <summary>
    /// Gets whether the text has changed since it was last saved or loaded.
    /// </summary>
    bool IsDirty { get; }
    /// <summary>
    /// Marks the current text as saved.
    /// </summary>
    void MarkSaved();
    /// <summary>
    /// Gets or sets the caret offset in <see cref="Script"/>.
    /// </summary>
    int CaretOffset { get; set; }
    /// <summary>
    /// Moves the caret to <paramref name="offset"/> and asks the editor to place that line at the top of the view.
    /// </summary>
    void Reveal(int offset);
    /// <summary>
    /// Inserts <paramref name="text"/> at <paramref name="offset"/> and shifts the caret when it is at or after that point.
    /// </summary>
    void Insert(int offset, string text);
    /// <summary>
    /// Inserts <paramref name="text"/> at <see cref="CaretOffset"/> and moves the caret to the end of the insert.
    /// </summary>
    void InsertAtCaret(string text);
    /// <summary>
    /// Starts an undo group so later inserts collapse into one undo step.
    /// </summary>
    void BeginUndoGroup();
    /// <summary>
    /// Ends the undo group started by <see cref="BeginUndoGroup"/>.
    /// </summary>
    void EndUndoGroup();
}

/// <summary>
/// Stores the text and file path of a script editor tab.
/// </summary>
public partial class EditorViewModel : ScriptViewModel, IEditorViewModel
{
    /// <summary>
    /// Creates a closable script editor tab.
    /// </summary>
    public EditorViewModel()
    {
        CanClose = true;
        DisplayName = "Script";
        this.WhenAnyValue(x => x.Kind)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(HighlightSource)));
    }

    /// <inheritdoc />
    [Reactive]
    public partial string? FileName { get; set; }

    /// <summary>
    /// Gets the live editor document. Text is materialized from this on save, run, and analysis.
    /// </summary>
    public TextDocument Document { get; } = new();

    /// <inheritdoc />
    public string Script
    {
        get => Document.Text;
        set
        {
            var text = value ?? string.Empty;
            if (Document.Text == text)
            {
                return;
            }

            Document.Text = text;
            this.RaisePropertyChanged(nameof(Script));
        }
    }

    /// <inheritdoc />
    public bool IsDirty => !Document.UndoStack.IsOriginalFile;

    /// <inheritdoc />
    public void MarkSaved() => Document.UndoStack.MarkAsOriginalFile();

    /// <inheritdoc />
    [Reactive]
    public partial int CaretOffset { get; set; }

    /// <summary>
    /// Gets a stamp the editor watches so <see cref="Reveal"/> still runs when the caret is unchanged.
    /// </summary>
    [Reactive]
    public partial int RevealRequest { get; set; }

    /// <inheritdoc />
    public void Reveal(int offset)
    {
        CaretOffset = offset.Clamp(0, Document.TextLength);
        RevealRequest++;
    }

    /// <inheritdoc />
    public void Insert(int offset, string text)
    {
        var value = text ?? "";
        offset = offset.Clamp(0, Document.TextLength);
        var caret = CaretOffset.Clamp(0, Document.TextLength);
        var anchor = Document.CreateAnchor(caret);
        anchor.MovementType = AnchorMovementType.AfterInsertion;
        Document.Insert(offset, value);
        CaretOffset = anchor.Offset;
        this.RaisePropertyChanged(nameof(Script));
    }

    /// <inheritdoc />
    public void InsertAtCaret(string text) => Insert(CaretOffset, text);

    /// <inheritdoc />
    public void BeginUndoGroup() => Document.UndoStack.StartUndoGroup();

    /// <inheritdoc />
    public void EndUndoGroup() => Document.UndoStack.EndUndoGroup();

    /// <summary>
    /// Gets the syntax highlighting asset for the current script kind.
    /// </summary>
    public string HighlightSource => Kind == ScriptKind.AviSynth ? "AviSynth.xshd" : "Python.xshd";
}
