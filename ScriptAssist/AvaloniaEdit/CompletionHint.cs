using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit.CodeCompletion;

namespace HanumanInstitute.ScriptAssist.AvaloniaEdit;

/// <summary>
/// Pins the nested completion hint to the list overlay, not a virtualized row.
/// Overlay completion otherwise places against the main window (far-right edge).
/// </summary>
internal sealed class CompletionHint
{
    private readonly CompletionWindow _window;
    private readonly ListBox? _list;
    private Popup? _tip;
    private Point? _pointer;
    private bool _detached;

    public CompletionHint(CompletionWindow window)
    {
        _window = window;
        _list = window.CompletionList.ListBox;
        _tip = window.GetLogicalChildren().OfType<Popup>().FirstOrDefault();
        window.CompletionList.SelectionChanged += OnSelectionChanged;
        if (_list != null)
        {
            _list.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Bubble);
            _list.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheel, RoutingStrategies.Bubble, true);
            _list.AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged, RoutingStrategies.Bubble);
        }

        Place(true);
    }

    public void Detach()
    {
        _detached = true;
        _window.CompletionList.SelectionChanged -= OnSelectionChanged;
        if (_list != null)
        {
            _list.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
            _list.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheel);
            _list.RemoveHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged);
        }

        if (_tip != null)
        {
            _tip.IsOpen = false;
            _tip.PlacementTarget = null;
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) => Place(true);

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        SelectAtPointer();
        Place(false);
    }

    private void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (_list == null)
        {
            return;
        }

        _pointer = e.GetPosition(_list);
        Dispatcher.UIThread.Post(() =>
        {
            SelectAtPointer();
            Place(false);
        }, DispatcherPriority.Render);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_list == null || e.Pointer.Captured != null)
        {
            return;
        }

        _pointer = e.GetPosition(_list);
        SelectAt(e.Source as Visual);
    }

    private void SelectAtPointer()
    {
        if (_list == null || _pointer is not { } point)
        {
            return;
        }

        SelectAt(_list.InputHitTest(point) as Visual);
    }

    private void SelectAt(Visual? source)
    {
        if (_list == null || source == null || source is ScrollBar ||
            source.FindAncestorOfType<ScrollBar>() != null)
        {
            return;
        }

        var row = source as ListBoxItem ?? source.FindAncestorOfType<ListBoxItem>();
        if (row?.DataContext is not ICompletionData data ||
            ReferenceEquals(_window.CompletionList.SelectedItem, data))
        {
            return;
        }

        _window.CompletionList.SelectedItem = data;
    }

    private void Place(bool retry)
    {
        if (_detached)
        {
            return;
        }

        _tip ??= _window.GetLogicalChildren().OfType<Popup>().FirstOrDefault();
        if (_tip == null)
        {
            return;
        }

        var description = _window.CompletionList.SelectedItem?.Description;
        var empty = description == null || description is string text && !text.HasValue();
        var host = _list;
        var row = SelectedRow();
        if (empty || host == null || !host.IsAttachedToVisualTree() ||
            row == null || !row.IsAttachedToVisualTree())
        {
            _tip.IsOpen = false;
            if (retry && !empty)
            {
                Dispatcher.UIThread.Post(() => Place(false), DispatcherPriority.Loaded);
            }

            return;
        }

        var y = row.TranslatePoint(new Point(0, 0), host)?.Y ?? 0;
        if (y + row.Bounds.Height <= 0 || y >= host.Bounds.Height)
        {
            _tip.IsOpen = false;
            return;
        }

        _tip.ShouldUseOverlayLayer = true;
        _tip.TakesFocusFromNativeControl = false;
        _tip.IsHitTestVisible = false;
        if (_tip.Child is Control child)
        {
            child.IsHitTestVisible = false;
        }

        _tip.PlacementTarget = host;
        _tip.Placement = PlacementMode.RightEdgeAlignedTop;
        _tip.HorizontalOffset = 2;
        _tip.VerticalOffset = y;
        _tip.IsOpen = true;
    }

    private Control? SelectedRow()
    {
        if (_list == null)
        {
            return null;
        }

        var index = _list.SelectedIndex;
        return index < 0 ? null : _list.ContainerFromIndex(index) as Control;
    }
}
