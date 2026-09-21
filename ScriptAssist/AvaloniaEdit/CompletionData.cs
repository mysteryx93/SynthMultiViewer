using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace HanumanInstitute.ScriptAssist.AvaloniaEdit;

/// <summary>
/// Adapts a snapshot completion to AvaloniaEdit's live replacement segment.
/// </summary>
public sealed class CompletionData : ICompletionData
{
    private readonly CompletionItem _item;
    private readonly AssistTipSize _size;

    /// <summary>
    /// Creates completion data using <see cref="AssistTipSize.Hint"/>.
    /// </summary>
    public CompletionData(CompletionItem item) : this(item, AssistTipSize.Hint)
    {
    }

    /// <summary>
    /// Creates completion data that wraps the side-panel hint to <paramref name="size"/>.
    /// </summary>
    public CompletionData(CompletionItem item, AssistTipSize size)
    {
        _item = item;
        _size = size;
    }

    /// <inheritdoc />
    public IImage Image => KindImages.Of(_item.Kind)!;

    /// <inheritdoc />
    public string Text => _item.InsertionText;

    /// <inheritdoc />
    public object Content => Text;

    /// <inheritdoc />
    public object Description
    {
        get
        {
            var text = HintText(_item, _size);
            return text.HasValue() ? HintBlock(text, _size) : null!;
        }
    }

    /// <inheritdoc />
    public double Priority => _item.Priority;

    /// <inheritdoc />
    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) =>
        textArea.Document.Replace(completionSegment, _item.InsertionText);

    /// <summary>
    /// Function signature, or property/local type. The list already shows the name.
    /// </summary>
    internal static string? HintText(CompletionItem item, AssistTipSize? size = null)
    {
        size ??= AssistTipSize.Hint;
        var text = Symbol.TipOf(item.Kind, item.InsertionText, item.Signature);
        return text == null ? null : TruncateHint(text, size.MaxCharacters);
    }

    /// <summary>
    /// Wraps hint or hover text to <paramref name="size"/>. The box sizes to the text
    /// so short signatures are not padded to a fixed width.
    /// </summary>
    internal static TextBlock HintBlock(string text, AssistTipSize? size = null)
    {
        size ??= AssistTipSize.Hint;
        return new TextBlock
        {
            Text = TruncateHint(text, size.MaxCharacters),
            MaxWidth = Math.Max(1, size.MaxWidth),
            MaxLines = Math.Max(1, size.MaxLines),
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }

    /// <summary>
    /// Caps extreme native signatures; wrapping and line limit handle ordinary length.
    /// </summary>
    internal static string TruncateHint(string text, int max)
    {
        max = Math.Max(1, max);
        if (!text.HasValue() || text.Length <= max)
        {
            return text;
        }

        return text[..(max - 1)] + "…";
    }
}
