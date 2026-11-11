using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HanumanInstitute.SynthMultiViewer.Controls;

/// <summary>
/// A list view that keeps one current row while the source changes.
/// Controls bind here. The source is replaced when the model changes.
/// </summary>
/// <typeparam name="T">The type of each row.</typeparam>
public class CollectionView<T> : ICollectionView<T>
{
    /// <summary>
    /// Raised when the rows change.
    /// The control listens to this, not to a separate collection on the view-model.
    /// </summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>
    /// Raised when the current row or its index changes.
    /// Selection bindings listen to this.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Creates an empty view with no current row.
    /// Rows are added after construction.
    /// </summary>
    public CollectionView()
    {
        Source.CollectionChanged += OnSourceChanged;
    }

    /// <summary>
    /// Creates a view filled from the supplied rows.
    /// The first row starts current when the list is not empty.
    /// </summary>
    /// <param name="list">The rows to copy into the view.</param>
    public CollectionView(IEnumerable<T> list) : this()
    {
        Source.AddRange(list.ToList());
        CurrentPosition = Source.Any() ? 0 : -1;
    }

    /// <summary>
    /// Gets the rows the control displays.
    /// Clear and refill this when the model changes.
    /// </summary>
    public ObservableCollectionWithRange<T> Source { get; } = [];

    /// <summary>
    /// Gets or sets the index of the current row.
    /// Values outside the list are coerced. An empty list uses -1.
    /// </summary>
    public int CurrentPosition
    {
        get => _currentPosition;
        set => SetPosition(value);
    }

    /// <summary>
    /// Gets or sets the current row.
    /// Setting null clears the selection.
    /// </summary>
    public T? CurrentItem
    {
        get => _currentPosition > -1 ? Source.ElementAtOrDefault(_currentPosition) : default;
        set => SetPosition(value != null ? Source.IndexOf(value) : -1);
    }

    /// <summary>
    /// Gets the number of rows.
    /// This follows the source.
    /// </summary>
    public int Count => Source.Count;

    /// <summary>
    /// Gets whether the view rejects edits.
    /// This view accepts edits.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets whether the view has a fixed size.
    /// Rows can be added and removed.
    /// </summary>
    public bool IsFixedSize => false;

    /// <inheritdoc />
    bool ICollection.IsSynchronized => ((ICollection)Source).IsSynchronized;

    /// <inheritdoc />
    object ICollection.SyncRoot => ((ICollection)Source).SyncRoot;

    /// <summary>
    /// Gets or sets the row at an index.
    /// The index is the display position.
    /// </summary>
    public T this[int index]
    {
        get => Source[index];
        set => Source[index] = value;
    }

    /// <inheritdoc />
    object? IList.this[int index]
    {
        get => this[index];
        set => this[index] = (T)value!;
    }

    /// <summary>
    /// Moves the current row to the first item.
    /// An empty list clears the current row.
    /// </summary>
    public void MoveCurrentToFirst() => CurrentPosition = Source.Any() ? 0 : -1;

    /// <summary>
    /// Moves the current row to the last item.
    /// An empty list clears the current row.
    /// </summary>
    public void MoveCurrentToLast() => CurrentPosition = Source.Any() ? Source.Count - 1 : -1;

    /// <summary>
    /// Appends one row.
    /// The first row becomes current when nothing was selected.
    /// </summary>
    public void Add(T item) => Source.Add(item);

    /// <summary>
    /// Appends many rows and notifies once.
    /// Use this when refilling the display from the model.
    /// </summary>
    /// <param name="items">The rows to append.</param>
    public void AddRange(IEnumerable<T> items) => Source.AddRange(items);

    /// <summary>
    /// Removes every row.
    /// The current row is cleared with them.
    /// </summary>
    public void Clear() => Source.Clear();

    /// <summary>
    /// Returns whether the view contains the row.
    /// The match is the source's match.
    /// </summary>
    public bool Contains(T item) => Source.Contains(item);

    /// <summary>
    /// Copies the rows into an array.
    /// The copy starts at the given index.
    /// </summary>
    public void CopyTo(T[] array, int arrayIndex) => Source.CopyTo(array, arrayIndex);

    /// <summary>
    /// Removes one row.
    /// Returns whether the row was present.
    /// </summary>
    public bool Remove(T item) => Source.Remove(item);

    /// <summary>
    /// Removes the row at an index.
    /// Later rows move up.
    /// </summary>
    public void RemoveAt(int index) => Source.RemoveAt(index);

    /// <summary>
    /// Returns the index of a row, or -1.
    /// The search uses the source.
    /// </summary>
    public int IndexOf(T item) => Source.IndexOf(item);

    /// <summary>
    /// Inserts a row at an index.
    /// Later rows move down.
    /// </summary>
    public void Insert(int index, T item) => Source.Insert(index, item);

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => Enumerate();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => Enumerate();

    /// <inheritdoc />
    int IList.Add(object? value)
    {
        try
        {
            Source.Add((T)value!);
            return Count - 1;
        }
        catch
        {
            return -1;
        }
    }

    /// <inheritdoc />
    bool IList.Contains(object? value) => Source.Contains((T)value!);

    /// <inheritdoc />
    void IList.Insert(int index, object? value) => Source.Insert(index, (T)value!);

    /// <inheritdoc />
    void IList.Remove(object? value) => Source.Remove((T)value!);

    /// <inheritdoc />
    int IList.IndexOf(object? value) => Source.IndexOf((T)value!);

    /// <inheritdoc />
    void ICollection.CopyTo(Array array, int index) => Source.CopyTo((T[])array, index);

    /// <summary>
    /// Raises <see cref="PropertyChanged"/>.
    /// Selection properties call this after they change.
    /// </summary>
    /// <param name="propertyName">The property that changed.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private int _currentPosition = -1;

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        CollectionChanged?.Invoke(this, e);
        if (CurrentPosition > -1)
        {
            var position = CurrentPosition;
            _currentPosition = -1;
            OnPropertyChanged(nameof(CurrentPosition));
            OnPropertyChanged(nameof(CurrentItem));
            SetPosition(position);
            return;
        }

        if (Source.Any())
        {
            SetPosition(0);
        }
    }

    private void SetPosition(int position)
    {
        if (position <= -1 && _currentPosition <= -1)
        {
            return;
        }

        _currentPosition = Math.Max(-1, Math.Min(Source.Count - 1, position));
        OnPropertyChanged(nameof(CurrentPosition));
        OnPropertyChanged(nameof(CurrentItem));
    }

    private IEnumerator<T> Enumerate()
    {
        using var iterator = Source.GetEnumerator();
        while (iterator.MoveNext())
        {
            yield return iterator.Current!;
        }
    }
}
