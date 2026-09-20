using HanumanInstitute.SynthMultiViewer.Views;
using HanumanInstitute.MvvmDialogs.Avalonia;

namespace HanumanInstitute.SynthMultiViewer;

/// <summary>
/// Maps application view models to their Avalonia views.
/// </summary>
public class ViewLocator : StrongViewLocator
{
    /// <summary>
    /// Registers the window and tab view mappings.
    /// </summary>
    public ViewLocator()
    {
        Register<MainViewModel, MainView>();
        Register<HelpViewModel, HelpView>();
        Register<InputViewModel, InputView>();
        Register<SettingsViewModel, SettingsView>();
        Register<VideoPropertiesViewModel, VideoPropertiesView>();
        Register<FunctionsExplorerViewModel, FunctionsExplorerView>();
        Register<TabColorViewModel, TabColorView>();
        Register<EditorViewModel, EditorView>();
        Register<ViewerViewModel, ViewerView>();
    }
}
