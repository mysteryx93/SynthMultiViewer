using System.Globalization;

namespace HanumanInstitute.SynthMultiViewer.Controls;

/// <summary>
/// Adds value-and-text rows to a list of <see cref="ListItem{T}"/>.
/// The row is display. The value is what the model stores.
/// </summary>
public static class ListItemExtensions
{
    /// <summary>
    /// Appends a row with an explicit label.
    /// The label is independent of the value's own text.
    /// </summary>
    /// <typeparam name="T">The type of the stored value.</typeparam>
    /// <param name="list">The list that receives the row.</param>
    /// <param name="value">The value the row selects.</param>
    /// <param name="text">The text the row shows.</param>
    /// <returns>The row that was appended.</returns>
    public static ListItem<T> Add<T>(this IList<ListItem<T>> list, T value, string text)
    {
        list.CheckNotNull();
        var item = new ListItem<T>(value, text);
        list.Add(item);
        return item;
    }

    /// <summary>
    /// Appends a row whose label is the value's invariant text.
    /// Use this when the value already reads the way the row should.
    /// </summary>
    /// <typeparam name="T">The type of the stored value.</typeparam>
    /// <param name="list">The list that receives the row.</param>
    /// <param name="value">The value the row selects.</param>
    /// <returns>The row that was appended.</returns>
    public static ListItem<T> Add<T>(this IList<ListItem<T>> list, T value) =>
        Add(list, value, Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
}
