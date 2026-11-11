namespace HanumanInstitute.SynthMultiViewer.Controls;

/// <summary>
/// One choice in a combo or list: the stored value and the text the row shows.
/// The control binds the row. The value stays on the model.
/// </summary>
/// <typeparam name="T">The type of the stored value.</typeparam>
public class ListItem<T>
{
    /// <summary>
    /// Creates a row for a value and the text that represents it.
    /// The text is what the control displays.
    /// </summary>
    /// <param name="value">The value this row selects.</param>
    /// <param name="text">The text shown for the row.</param>
    public ListItem(T value, string text)
    {
        Value = value;
        Text = text;
    }

    /// <summary>
    /// Gets or sets the value selected when this row is current.
    /// A null value is the blank default row.
    /// </summary>
    public T Value { get; set; }

    /// <summary>
    /// Gets or sets the text shown for this row.
    /// The control uses this text when it has no item template.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Returns the row text.
    /// A combo or list without a template shows this.
    /// </summary>
    public override string ToString() => Text;
}
