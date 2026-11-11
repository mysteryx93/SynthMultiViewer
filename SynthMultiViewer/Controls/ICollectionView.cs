using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace HanumanInstitute.SynthMultiViewer.Controls;

/// <summary>
/// A list that also tracks which row is current.
/// The current row is display selection. It is not a second copy of the model.
/// </summary>
/// <typeparam name="T">The type of the list.</typeparam>
public interface ICollectionView<T> : INotifyPropertyChanged, INotifyCollectionChanged, IList<T>, IList
{
    /// <summary>
    /// Gets the rows as an observable list that can append a range at once.
    /// Bind the control to the view. Change this list when the model changes.
    /// </summary>
    ObservableCollectionWithRange<T> Source { get; }

    /// <summary>
    /// Gets or sets the current row.
    /// Null means nothing is selected.
    /// </summary>
    T? CurrentItem { get; set; }

    /// <summary>
    /// Gets or sets the index of the current row, or -1 when none is selected.
    /// The index follows <see cref="CurrentItem"/>.
    /// </summary>
    int CurrentPosition { get; set; }

    /// <summary>
    /// Moves the current row to the first item.
    /// An empty list clears the current row.
    /// </summary>
    void MoveCurrentToFirst();

    /// <summary>
    /// Moves the current row to the last item.
    /// An empty list clears the current row.
    /// </summary>
    void MoveCurrentToLast();
}
