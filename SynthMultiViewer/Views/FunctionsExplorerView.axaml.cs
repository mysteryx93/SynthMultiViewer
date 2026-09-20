using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
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
        if (DataContext is FunctionsExplorerViewModel model &&
            ((ICommand)model.Insert).CanExecute(null))
        {
            ((ICommand)model.Insert).Execute(null);
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

        if (e.Key == Key.Enter && ((ICommand)model.Insert).CanExecute(null))
        {
            ((ICommand)model.Insert).Execute(null);
            e.Handled = true;
        }
    }
}
