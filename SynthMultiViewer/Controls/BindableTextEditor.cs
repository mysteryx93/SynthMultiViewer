using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;

namespace HanumanInstitute.SynthMultiViewer.Controls;

/// <summary>
/// An AvaloniaEdit editor. Bind <see cref="TextEditor.Document"/>; do not two-way bind
/// <see cref="ScriptText"/>, which would recopy the whole buffer on every keystroke.
/// </summary>
public partial class BindableTextEditor : TextEditor
{
    private ScrollViewer? _scroll;

    /// <summary>
    /// Defines the bindable script text.
    /// </summary>
    public static readonly StyledProperty<string> ScriptTextProperty =
        AvaloniaProperty.Register<BindableTextEditor, string>(nameof(ScriptText), string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Defines the caret offset bound to the editor view-model.
    /// </summary>
    public static readonly StyledProperty<int> BindableCaretOffsetProperty =
        AvaloniaProperty.Register<BindableTextEditor, int>(nameof(BindableCaretOffset),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Defines a stamp that reveals the caret line at the top of the view.
    /// </summary>
    public static readonly StyledProperty<int> CaretRevealProperty =
        AvaloniaProperty.Register<BindableTextEditor, int>(nameof(CaretReveal));

    /// <summary>
    /// Creates an editor that keeps <see cref="TextEditor.Document"/> as the live buffer.
    /// </summary>
    public BindableTextEditor()
    {
        InitializeCompletion();
        TextArea.Caret.PositionChanged += (_, _) =>
        {
            if (BindableCaretOffset != CaretOffset)
            {
                SetCurrentValue(BindableCaretOffsetProperty, CaretOffset);
            }
        };
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_scroll != null)
        {
            _scroll.LayoutUpdated -= OnScrollLayoutUpdated;
        }

        base.OnApplyTemplate(e);
        _scroll = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        if (_scroll != null)
        {
            _scroll.LayoutUpdated += OnScrollLayoutUpdated;
        }
    }

    private void OnScrollLayoutUpdated(object? sender, EventArgs e)
    {
        if (_scroll == null)
        {
            return;
        }

        var viewport = _scroll.Viewport;
        foreach (var bar in _scroll.GetVisualDescendants().OfType<ScrollBar>())
        {
            if (bar.Orientation == Orientation.Horizontal && viewport.Width > 0
                && bar.LargeChange != viewport.Width)
            {
                bar.LargeChange = viewport.Width;
            }
            else if (bar.Orientation == Orientation.Vertical && viewport.Height > 0
                && bar.LargeChange != viewport.Height)
            {
                bar.LargeChange = viewport.Height;
            }
        }
    }

    /// <summary>
    /// Gets or sets the document text.
    /// </summary>
    public string ScriptText
    {
        get => GetValue(ScriptTextProperty);
        set => SetValue(ScriptTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the caret offset synchronized with the view-model.
    /// </summary>
    public int BindableCaretOffset
    {
        get => GetValue(BindableCaretOffsetProperty);
        set => SetValue(BindableCaretOffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets a stamp that places the caret line at the top of the view.
    /// </summary>
    public int CaretReveal
    {
        get => GetValue(CaretRevealProperty);
        set => SetValue(CaretRevealProperty, value);
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(TextEditor);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ScriptTextProperty && Text != ScriptText)
        {
            Text = ScriptText ?? string.Empty;
        }

        if (change.Property == BindableCaretOffsetProperty)
        {
            ApplyCaretOffset();
        }

        if (change.Property == CaretRevealProperty)
        {
            ApplyCaretOffset();
            RevealLineAtTop();
        }
    }

    private void ApplyCaretOffset()
    {
        var offset = BindableCaretOffset.Clamp(0, Document?.TextLength ?? 0);
        if (CaretOffset != offset)
        {
            CaretOffset = offset;
        }
    }

    private void RevealLineAtTop()
    {
        var document = Document;
        if (document == null)
        {
            return;
        }

        var line = document.GetLineByOffset(CaretOffset.Clamp(0, document.TextLength)).LineNumber;
        ScrollTo(line, 1, VisualYPosition.LineTop, 0, 0);
    }
}
