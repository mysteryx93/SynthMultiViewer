using System.ComponentModel;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.Avalonia;
using HanumanInstitute.MvvmDialogs.FileSystem;

namespace HanumanInstitute.SynthMultiViewer.Tests;

/// <summary>
/// Records dialogs and supplies scripted results without showing views.
/// </summary>
internal sealed class FakeDialogManager : DialogManager
{
    public FakeDialogManager() : base(viewLocator: new ViewLocator()) { }

    private object? NextFrameworkResult { get; set; }
    private readonly Queue<object?> _queued = new();
    public object? LastFrameworkSettings { get; private set; }
    public int FrameworkDialogCount { get; private set; }
    /// <summary>
    /// Gets the most recently shown modeless view model.
    /// </summary>
    public INotifyPropertyChanged? LastShown { get; private set; }
    /// <summary>
    /// Gets the most recently shown modal view model.
    /// </summary>
    public IModalDialogViewModel? LastDialog { get; private set; }
    /// <summary>
    /// Gets or sets the scripted response to a modal view.
    /// </summary>
    public Action<IModalDialogViewModel>? OnShow { get; set; }

    /// <summary>
    /// Records a modeless view shown by the service.
    /// </summary>
    public override void Show(INotifyPropertyChanged? ownerViewModel, INotifyPropertyChanged viewModel)
    {
        LastShown = viewModel;
    }

    /// <summary>
    /// Records a modal view and applies the scripted response.
    /// </summary>
    public override Task ShowDialogAsync(INotifyPropertyChanged ownerViewModel, IModalDialogViewModel viewModel)
    {
        LastDialog = viewModel;
        OnShow?.Invoke(viewModel);
        return Task.CompletedTask;
    }

    public void ReturnFile(string? path) =>
        NextFrameworkResult = path == null
            ? Array.Empty<IDialogStorageFile>()
            : new IDialogStorageFile[] { new DesktopDialogStorageFile(path) };

    public void ReturnFolder(string? path) =>
        NextFrameworkResult = path == null
            ? Array.Empty<IDialogStorageFolder>()
            : new IDialogStorageFolder[] { new DesktopDialogStorageFolder(path) };

    public void QueueFrameworkResult(object? result) => _queued.Enqueue(result);

    public override Task<object?> ShowFrameworkDialogAsync<TSettings>(
        INotifyPropertyChanged? ownerViewModel, TSettings settings, Func<object?, string>? resultToString = null)
    {
        LastFrameworkSettings = settings;
        FrameworkDialogCount++;
        return Task.FromResult(_queued.Count > 0 ? _queued.Dequeue() : NextFrameworkResult);
    }
}
