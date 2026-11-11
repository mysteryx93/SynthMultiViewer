using System.Globalization;
using Avalonia.Threading;
using HanumanInstitute.FFmpeg;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.SynthMultiViewer.Helpers;
using HanumanInstitute.SynthMultiViewer.Services;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Shows the FFmpeg sample progress window for one encode.
/// The bar, percent, fps, and time left come from that sample.
/// </summary>
public partial class EncodeProgressViewModel : WorkspaceViewModel, IUserInterfaceWindow, IModalDialogViewModel, IViewClosed
{
    private FFmpegUserInterfaceManager? _manager;
    private IProcessWorker? _worker;
    private IProcessWorkerEncoder? _encoder;
    private ITimeLeftCalculator? _timeLeft;
    private long _frameCount;
    private bool _timeLeftToggle;
    private volatile bool _closed;

    /// <summary>
    /// Creates the progress window titled Encoding.
    /// The button starts as Cancel.
    /// </summary>
    public EncodeProgressViewModel()
    {
        DisplayName = "Encoding";
        this.WhenAnyValue(x => x.DisplayName, x => x.Status, FormatTitle).Subscribe(title => TitleWithStatus = title);
    }

    /// <summary>
    /// Gets or sets whether a finished encode closes the window.
    /// The FFmpeg UI manager supplies this.
    /// </summary>
    public bool AutoClose { get; set; }

    /// <inheritdoc />
    public bool? DialogResult => true;

    /// <summary>
    /// Gets the window title, including the percent while work is running.
    /// The view binds this as the title.
    /// </summary>
    [Reactive] public partial string TitleWithStatus { get; private set; } = "";

    /// <summary>
    /// Gets whether the process is still running.
    /// Cancel is available only while this is true.
    /// </summary>
    [Reactive] public partial bool IsRunning { get; private set; } = true;

    /// <summary>
    /// Gets the percent complete, or a failure message when the process never started.
    /// The sample shows this beside the fps.
    /// </summary>
    [Reactive] public partial string Status { get; private set; } = "";

    /// <summary>
    /// Gets the progress bar maximum, in frames.
    /// A known job length or the parsed file header sets it.
    /// </summary>
    [Reactive] public partial double ProgressBarMax { get; private set; } = 1;

    /// <summary>
    /// Gets how far the bar has advanced, in frames.
    /// Each progress event moves it.
    /// </summary>
    [Reactive] public partial double ProgressBarValue { get; private set; }

    /// <summary>
    /// Gets the label drawn on the bar for a secondary task.
    /// The main encode leaves this empty.
    /// </summary>
    [Reactive] public partial string TaskName { get; private set; } = "";

    /// <summary>
    /// Gets the reported frames per second.
    /// The sample prints this next to the percent.
    /// </summary>
    [Reactive] public partial string Fps { get; private set; } = "";

    /// <summary>
    /// Gets the estimated time remaining.
    /// It stays blank until the frame count is known.
    /// </summary>
    [Reactive] public partial string TimeLeft { get; private set; } = "";

    /// <summary>
    /// Gets the button caption.
    /// It changes to Close when the process ends.
    /// </summary>
    [Reactive] public partial string CancelText { get; private set; } = "_Cancel";

    /// <summary>
    /// Receives the running process and listens for its progress.
    /// A secondary task only supplies the bar label.
    /// </summary>
    public void DisplayTask(IProcessWorker host)
    {
        host.CheckNotNull();
        _manager?.Track(host, this);
        _encoder = host as IProcessWorkerEncoder;
        var options = _encoder?.Options ?? host.Options;
        if (options == null || options.IsMainTask)
        {
            _worker = host;
            if (_encoder != null)
            {
                _encoder.FileInfoUpdated += OnFileInfo;
                _encoder.ProgressReceived += OnProgress;
            }

            host.ProcessCompleted += OnCompleted;
            var count = options is ProcessOptionsEncoder encoder ? encoder.FrameCount + encoder.ResumePos : 0;
            SetLength(count);
            OnUi(() => Status = 0d.ToString("p1", CultureInfo.CurrentCulture));
        }
        else
        {
            OnUi(() => TaskName = host.Options.Title);
            host.ProcessCompleted += (_, _) => OnUi(() => TaskName = "");
        }

        if (_closed) { host.Cancel(); }
    }

    /// <summary>
    /// Associates this window with the manager that opens it, so a failed start can reach it.
    /// </summary>
    internal void Link(FFmpegUserInterfaceManager manager) => _manager = manager.CheckNotNull();

    /// <inheritdoc />
    void IUserInterfaceWindow.Close() => CloseView();

    /// <summary>
    /// Stops the active process when the window closes.
    /// </summary>
    public void OnClosed()
    {
        _closed = true;
        if (IsRunning) { _worker?.Cancel(); }
        CloseView();
    }

    /// <summary>
    /// Marks a process that failed before it could start.
    /// The button becomes Close.
    /// </summary>
    public void StartFailed(string message) => OnUi(() =>
    {
        if (_closed) { return; }
        IsRunning = false;
        Status = message;
        CancelText = "_Close";
    });

    // The parsed file header supplies the frame count the bar scales to.
    private void OnFileInfo(object? sender, EventArgs e)
    {
        if (_encoder?.FileInfo is not FileInfoFFmpeg info) { return; }
        SetLength(info.FrameCount + ResumePos);
    }

    // Keeps the bar scaled to a real frame count. Zero is an unknown length, not a full bar.
    private void SetLength(long count)
    {
        if (count <= 0) { return; }
        _frameCount = count;
        _timeLeft = new TimeLeftCalculator(count);
        OnUi(() =>
        {
            if (_closed) { return; }
            ProgressBarMax = count;
        });
    }

    // Moves the bar and refreshes the percent, fps, and time left.
    private void OnProgress(object? sender, ProgressReceivedEventArgs e)
    {
        if (_closed || e.Progress is not ProgressStatusFFmpeg progress) { return; }
        var value = progress.Frame + ResumePos;
        var count = _frameCount;
        var fps = progress.Fps.ToString(CultureInfo.CurrentCulture);
        var percent = count > 0
            ? ((double)value / count).ToString("p1", CultureInfo.CurrentCulture)
            : value.ToStringInvariant();
        _timeLeftToggle = !_timeLeftToggle;
        string? left = null;
        if (_timeLeftToggle && _timeLeft != null)
        {
            _timeLeft.Calculate(value);
            if (_timeLeft.ResultTimeLeft > TimeSpan.Zero)
            {
                left = _timeLeft.ResultTimeLeft.FormatDuration();
            }
        }

        OnUi(() =>
        {
            if (_closed) { return; }
            if (count > 0)
            {
                ProgressBarMax = count;
                ProgressBarValue = value;
            }
            Status = percent;
            Fps = fps;
            if (left != null) { TimeLeft = left; }
        });
    }

    // The process thread raises completion. The button then closes the window.
    private void OnCompleted(object? sender, ProcessCompletedEventArgs e) => OnUi(() =>
    {
        if (_closed) { return; }
        IsRunning = false;
        CancelText = "_Close";
        if (AutoClose) { CloseView(); }
    });

    // Compiled bindings write the control on the calling thread. The process callbacks are not that thread.
    private static void OnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    // Frames already encoded before this run stay ahead of the reported frame index.
    private long ResumePos => _encoder?.Options.ResumePos ?? 0;

    // Puts the percent in the title while a status string is present.
    private static string FormatTitle(string name, string status) =>
        string.IsNullOrEmpty(status) ? name : name + " (" + status + ")";
}
