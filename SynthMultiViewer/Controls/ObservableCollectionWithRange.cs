using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace HanumanInstitute.SynthMultiViewer.Controls;

/// <summary>
/// An observable list that can append many items in one notification.
/// The view refills from this list. Callers do not persist the control.
/// </summary>
public class ObservableCollectionWithRange<T> : ObservableCollection<T>
{
    /// <summary>
    /// Appends every item, then raises one reset.
    /// A refill should not notify once per row.
    /// </summary>
    /// <param name="items">The items to append.</param>
    public void AddRange(IEnumerable<T> items)
    {
        CheckReentrancy();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
