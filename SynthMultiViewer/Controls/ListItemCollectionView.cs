using System.Globalization;

namespace HanumanInstitute.SynthMultiViewer.Controls;

/// <summary>
/// A combo or list of values, each shown with its own text.
/// The current row reflects the model. The model is not read back from the control.
/// </summary>
/// <typeparam name="T">The type of the stored value.</typeparam>
public class ListItemCollectionView<T> : CollectionView<ListItem<T>>
{
    /// <summary>
    /// Creates an empty list of choices.
    /// The caller adds the default row first, then the real values.
    /// </summary>
    public ListItemCollectionView() : base(new List<ListItem<T>>())
    {
    }

    /// <summary>
    /// Creates a list of choices from the supplied rows.
    /// The first row starts current when the list is not empty.
    /// </summary>
    /// <param name="list">The rows to show.</param>
    public ListItemCollectionView(IList<ListItem<T>> list) : base(list)
    {
    }

    /// <summary>
    /// Gets or sets the value of the current row.
    /// A null value selects the blank row, whose stored value is null.
    /// </summary>
    public T? SelectedValue
    {
        get => CurrentItem != null ? CurrentItem.Value : default;
        set => CurrentItem = value != null
            ? Source.FirstOrDefault(item => item.Value?.Equals(value) == true)
            : Source.FirstOrDefault(item => item.Value == null);
    }

    /// <summary>
    /// Appends a choice with an explicit label.
    /// Put the default row on first so it sits at the top.
    /// </summary>
    /// <param name="value">The value this row selects.</param>
    /// <param name="text">The text the row shows.</param>
    /// <returns>The row that was appended.</returns>
    public ListItem<T> Add(T value, string text)
    {
        var item = new ListItem<T>(value, text);
        Source.Add(item);
        return item;
    }

    /// <summary>
    /// Appends a choice whose label is the value's invariant text.
    /// Use this when that text is already the label.
    /// </summary>
    /// <param name="value">The value this row selects.</param>
    /// <returns>The row that was appended.</returns>
    public ListItem<T> Add(T value) => Add(value, Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");

    /// <summary>
    /// Raises property change, and also raises <see cref="SelectedValue"/> when the current row changes.
    /// Bindings on the value follow the row.
    /// </summary>
    /// <param name="propertyName">The property that changed.</param>
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(CurrentItem))
        {
            OnPropertyChanged(nameof(SelectedValue));
        }
    }
}
