using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;

namespace HanumanInstitute.SynthMultiViewer.Views;

/// <summary>
/// Names an encode preset and changes the saved list.
/// </summary>
public partial class SavePresetView : Window
{
    /// <summary>
    /// Creates the view and loads its XAML.
    /// </summary>
    public SavePresetView() => InitializeComponent();

    // A double-click accepts the row, the same gesture as choosing a function.
    private void OnPresetDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not ViewModels.SavePresetViewModel model || model.Ok is not ICommand ok || !ok.CanExecute(null))
        {
            return;
        }

        ok.Execute(null);
    }
}
