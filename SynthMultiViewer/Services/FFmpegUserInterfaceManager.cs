using System.Collections.Concurrent;
using System.ComponentModel;
using HanumanInstitute.FFmpeg;
using HanumanInstitute.MvvmDialogs;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Connects FFmpeg process lifecycle events to the application's job windows.
/// </summary>
public sealed class FFmpegUserInterfaceManager : UserInterfaceManagerBase
{
    private readonly IDialogService _dialogs;
    private readonly ConcurrentDictionary<IProcessWorker, EncodeProgressViewModel> _progress = new();

    /// <summary>
    /// Creates the manager used to show process windows.
    /// </summary>
    public FFmpegUserInterfaceManager(IDialogService dialogs) => _dialogs = dialogs.CheckNotNull();

    /// <inheritdoc />
    public override IUserInterfaceWindow CreateUI(object? owner, string title, bool autoClose)
    {
        var viewModel = _dialogs.CreateViewModel<EncodeProgressViewModel>();
        viewModel.DisplayName = title;
        viewModel.AutoClose = autoClose;
        viewModel.Link(this);
        _dialogs.Show(owner as INotifyPropertyChanged, viewModel);
        return viewModel;
    }

    /// <inheritdoc />
    public override void DisplayError(object? owner, IProcessWorker host) =>
        ShowError(host.Output.HasText() ? host.Output : "FFmpeg failed without reporting an error.");

    /// <summary>
    /// Reports an exception raised before the worker can complete.
    /// </summary>
    public void ReportStartFailure(IProcessWorker? worker, string message)
    {
        if (worker != null && _progress.TryRemove(worker, out var viewModel))
        {
            worker.ProcessCompleted -= OnCompleted;
            viewModel.StartFailed(message);
        }
        ShowError(message);
    }

    /// <summary>
    /// Tracks a worker until completion so startup failures can update its window.
    /// </summary>
    internal void Track(IProcessWorker worker, EncodeProgressViewModel viewModel)
    {
        _progress[worker] = viewModel;
        worker.ProcessCompleted += OnCompleted;
    }

    // Releases the startup-failure association when the process finishes.
    private void OnCompleted(object sender, ProcessCompletedEventArgs e)
    {
        if (sender is IProcessWorker worker)
        {
            worker.ProcessCompleted -= OnCompleted;
            _progress.TryRemove(worker, out _);
        }
    }

    // Shows the process error independently of the settings window.
    private void ShowError(string message)
    {
        var error = _dialogs.CreateViewModel<EncodeErrorViewModel>();
        error.Message = message;
        _dialogs.Show(null, error);
    }
}
