using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HanumanInstitute.SynthMultiViewer.Helpers;

namespace HanumanInstitute.SynthMultiViewer.Views;

/// <summary>
/// Modeless function browser for the current editor snapshot.
/// </summary>
public partial class FunctionsExplorerView : Window
{
    /// <summary>
    /// Creates the view and loads its XAML.
    /// </summary>
    public FunctionsExplorerView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerWheelChangedEvent, OnExplorerWheel, RoutingStrategies.Bubble);
        AddHandler(ScrollViewer.ScrollChangedEvent, OnExplorerScrolled, RoutingStrategies.Bubble);
    }

    private void OnExplorerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.Handled || e.Source is not Visual source)
        {
            return;
        }

        if (source is OverlayPopupHost || source.FindAncestorOfType<OverlayPopupHost>() is not null)
        {
            ScrollHintList(e);
        }
    }

    private void OnExplorerScrolled(object? sender, ScrollChangedEventArgs e)
    {
        if (e.OffsetDelta == default || e.Source is not Visual source)
        {
            return;
        }

        var list = source as ListBox ?? source.FindAncestorOfType<ListBox>();
        if (list == GroupsList || list == FunctionsList || list == HitsList)
        {
            HideTips(list);
        }
    }

    private void ScrollHintList(PointerWheelEventArgs e)
    {
        var list = HitsList.IsVisible ? HitsList : FunctionsList;
        if (!ScrollByWheel(list, e.Delta))
        {
            return;
        }

        HideTips(list);
        e.Handled = true;
    }

    private static bool ScrollByWheel(ListBox list, Vector delta)
    {
        var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scroll is null)
        {
            return false;
        }

        var offset = scroll.Offset;
        scroll.Offset = new Vector(offset.X - (delta.X * 50), offset.Y - (delta.Y * 50));
        return scroll.Offset != offset;
    }

    private static void HideTips(Visual visual)
    {
        if (visual is Control control && ToolTip.GetIsOpen(control))
        {
            ToolTip.SetIsOpen(control, false);
        }

        foreach (var child in visual.GetVisualChildren())
        {
            HideTips(child);
        }
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        WindowBounds.ApplyPosition(this);
        Dispatcher.UIThread.Post(() => WindowBounds.ApplyPosition(this), DispatcherPriority.Loaded);
        Dispatcher.UIThread.Post(() => WindowBounds.WatchPosition(this), DispatcherPriority.Background);
        Dispatcher.UIThread.Post(() => Search.Focus(), DispatcherPriority.Loaded);
    }

    private void OnFunctionDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is FunctionsExplorerViewModel model)
        {
            ActivateSelection(model);
        }
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None ||
            DataContext is not FunctionsExplorerViewModel model)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (((ICommand)model.ClearFilter).CanExecute(null))
            {
                ((ICommand)model.ClearFilter).Execute(null);
            }
            else if (((ICommand)model.Close).CanExecute(null))
            {
                ((ICommand)model.Close).Execute(null);
            }

            e.Handled = true;
            return;
        }

        if (e.Key is Key.Up or Key.Down && Search.IsFocused)
        {
            model.MoveSelection(e.Key == Key.Down ? 1 : -1);
            Search.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && ActivateSelection(model))
        {
            e.Handled = true;
        }
    }

    private static bool ActivateSelection(FunctionsExplorerViewModel model)
    {
        if (((ICommand)model.GoTo).CanExecute(null))
        {
            ((ICommand)model.GoTo).Execute(null);
            return true;
        }

        if (((ICommand)model.Insert).CanExecute(null))
        {
            ((ICommand)model.Insert).Execute(null);
            return true;
        }

        return false;
    }
}
