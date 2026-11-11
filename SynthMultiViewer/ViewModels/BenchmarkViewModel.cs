using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reactive.Linq;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.SynthMultiViewer.Helpers;
using HanumanInstitute.SynthMultiViewer.Models;
using HanumanInstitute.SynthMultiViewer.Services;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Benchmarks the source frames of the tab attached to this window.
/// </summary>
public partial class BenchmarkViewModel : WorkspaceViewModel, IViewClosed, IModalDialogViewModel
{
    private readonly ISettingsProvider<AppSettingsData> _settings;
    private IScriptViewModel? _tab;
    private CancellationTokenSource? _cancellation;
    private bool _closed;

    /// <summary>
    /// Creates the benchmark window.
    /// </summary>
    /// <param name="settings">The provider for the VapourSynth worker count.</param>
    public BenchmarkViewModel(ISettingsProvider<AppSettingsData> settings)
    {
        _settings = settings.CheckNotNull();
        DisplayName = "Benchmark";
    }

    /// <inheritdoc />
    public bool? DialogResult => null;

    /// <summary>
    /// Gets or sets whether VapourSynth uses the configured worker count.
    /// </summary>
    [Reactive] public partial bool IsMultiThreaded { get; set; } = true;

    /// <summary>
    /// Gets whether the attached script uses VapourSynth.
    /// </summary>
    [Reactive] public partial bool IsVapourSynth { get; private set; }

    /// <summary>
    /// Gets the attached tab's name.
    /// </summary>
    [Reactive] public partial string ScriptName { get; private set; } = "";

    /// <summary>
    /// Gets or sets the first frame to process.
    /// </summary>
    [RangeClusive(min: 0)]
    [Reactive] public partial int StartFrame { get; set; }

    /// <summary>
    /// Gets or sets the maximum frame count, or null for the rest of the clip.
    /// </summary>
    [RangeClusive(min: 1)]
    [Reactive] public partial int? MaxLength { get; set; }

    /// <summary>
    /// Gets whether a benchmark is active.
    /// </summary>
    [Reactive] public partial bool IsRunning { get; private set; }

    /// <summary>
    /// Gets the running or final average frames per second.
    /// </summary>
    [Reactive] public partial string AverageFps { get; private set; } = "—";

    /// <summary>
    /// Gets the completed and selected frame counts.
    /// </summary>
    [Reactive] public partial string Frames { get; private set; } = "—";

    /// <summary>
    /// Gets processing time.
    /// </summary>
    [Reactive] public partial string Elapsed { get; private set; } = "—";

    /// <summary>
    /// Gets the estimated time to finish.
    /// </summary>
    [Reactive] public partial string Remaining { get; private set; } = "—";

    /// <summary>
    /// Gets the benchmark state or error.
    /// </summary>
    [Reactive] public partial string Status { get; private set; } = "";

    /// <summary>
    /// Attaches a tab whose current script will be captured at each start.
    /// </summary>
    /// <param name="tab">The attached editor or viewer, or null to clear the attachment.</param>
    public void Bind(IScriptViewModel? tab)
    {
        _tab = tab;
        ScriptName = tab?.DisplayName ?? "Untitled";
        IsVapourSynth = tab?.Kind == ScriptKind.VapourSynth;
    }

    /// <summary>
    /// Starts processing the attached script.
    /// </summary>
    public RxCommandVoid Start => field ??= ReactiveCommand.CreateFromTask(StartAsync,
        this.WhenAnyValue(x => x.IsRunning).Select(running => !running));

    /// <summary>
    /// Stops requesting frames and waits for active requests to finish.
    /// </summary>
    public RxCommandVoid Abort => field ??= ReactiveCommand.Create(Stop,
        this.WhenAnyValue(x => x.IsRunning));

    /// <summary>
    /// Requests cancellation when the window closes.
    /// </summary>
    public void OnClosed()
    {
        _closed = true;
        Stop();
        CloseView();
    }

    // Captures the attached tab before entering the background worker.
    private async Task StartAsync()
    {
        if (IsRunning || !Validator.TryValidateObject(this, new ValidationContext(this), [], true))
        {
            return;
        }

        var (text, path) = _tab switch
        {
            IEditorViewModel editor => (editor.Script, editor.FileName),
            IViewerViewModel viewer => (viewer.Script, viewer.FileName),
            _ => (null, null)
        };
        if (!text.HasText() || _tab == null)
        {
            Status = "The tab has no script.";
            return;
        }

        var kind = _tab.Kind;
        var first = StartFrame;
        var length = MaxLength;
        var configuredThreads = _settings.Value.VapourSynthThreads;
        var threads = IsMultiThreaded && kind == ScriptKind.VapourSynth
            ? configuredThreads > 0 ? configuredThreads : Environment.ProcessorCount
            : 1;
        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsRunning = true;
        Status = "Running";
        AverageFps = Frames = Elapsed = Remaining = "—";
        var progress = new Progress<ScriptBenchmark.Sample>(sample =>
        {
            if (!_closed && IsRunning && ReferenceEquals(_cancellation, cancellation)) { ShowSample(sample); }
        });
        try
        {
            var sample = await Task.Run(() => new ScriptBenchmark().Run(text, path, kind, first, length,
                threads, progress, cancellation.Token));
            if (!_closed)
            {
                ShowSample(sample);
                Status = cancellation.IsCancellationRequested ? "Stopped" : "Done";
            }
        }
        catch (Exception ex)
        {
            if (!_closed) { Status = (ex is AggregateException aggregate ? aggregate.Flatten().InnerExceptions[0] : ex).Message; }
        }
        finally
        {
            _cancellation = null;
            cancellation.Dispose();
            IsRunning = false;
        }
    }

    // Displays throughput averaged over the entire frame-processing interval.
    private void ShowSample(ScriptBenchmark.Sample sample)
    {
        var seconds = sample.Elapsed.TotalSeconds;
        var fps = seconds > 0 ? sample.Frames / seconds : 0;
        AverageFps = fps.ToString("0.0", CultureInfo.InvariantCulture);
        Frames = $"{sample.Frames} / {sample.Total}";
        Elapsed = sample.Elapsed.FormatDuration();
        Remaining = fps > 0 ? TimeSpan.FromSeconds((sample.Total - sample.Frames) / fps).FormatDuration() : "—";
    }

    // Cancellation does not dispose the native session while frames are in flight.
    private void Stop() => _cancellation?.Cancel();
}
