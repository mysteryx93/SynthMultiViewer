using Avalonia.Headless;
using Avalonia.Input;

namespace HanumanInstitute.SynthMultiViewer.Tests;

/// <summary>
/// Drives headless windows and viewer setup for interaction tests.
/// </summary>
internal sealed class UiTestDriver
{
    /// <summary>
    /// Shows a headless window until the returned scope is disposed.
    /// </summary>
    public IDisposable Show(Window window)
    {
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return System.Reactive.Disposables.Disposable.Create(window.Close);
    }

    /// <summary>
    /// Sends a key gesture to a headless window.
    /// </summary>
    public void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null);
    }

    /// <summary>
    /// Opens a viewer from a new script tab.
    /// </summary>
    public async Task OpenViewerAsync(MainViewModel model)
    {
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Selects a viewer so its toolbar is available.
    /// </summary>
    public void ShowViewerToolbar(MainViewModel model)
    {
        model.SelectedItem = new ViewerViewModel { Kind = ScriptKind.VapourSynth };
        Dispatcher.UIThread.RunJobs();
    }
}
