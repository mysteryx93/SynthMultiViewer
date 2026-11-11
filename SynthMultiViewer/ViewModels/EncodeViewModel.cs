using System.ComponentModel;
using System.Reactive.Linq;
using HanumanInstitute.SynthMultiViewer.Controls;
using HanumanInstitute.FFmpeg;
using HanumanInstitute.MvvmDialogs;
using Microsoft.Extensions.Options;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using HanumanInstitute.ScriptAssist.Services;
using HanumanInstitute.SynthMultiViewer.Models;
using HanumanInstitute.SynthMultiViewer.Services;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Encodes the tab that was open when this window opened, with an argument string and saved presets.
/// </summary>
public partial class EncodeViewModel : WorkspaceViewModel, IModalDialogViewModel
{
    private readonly IEncoderService _encoder;
    private readonly IOptions<AppPaths> _paths;
    private readonly ISettingsProvider<AppSettingsData> _settings;
    private string _scriptText = "";
    private IScriptViewModel? _tab;
    private string? _scriptPath;
    private ScriptKind _kind = ScriptKind.VapourSynth;
    private int _clipFrames;
    private volatile IProcessWorker? _worker;

    private readonly IDialogService _dialogs;
    private INotifyPropertyChanged? _windowOwner;
    private bool _applyingPreset;

    /// <summary>
    /// Creates the encode window and loads saved presets, or the example list when none are saved.
    /// The blank row stays selected. The destination and arguments stay empty until the user sets them.
    /// </summary>
    /// <param name="encoder">The FFmpeg encoder service.</param>
    /// <param name="paths">The shared encoder paths. FFmpeg is taken from settings when a job starts.</param>
    /// <param name="settings">The provider for FFmpeg settings and encode presets.</param>
    /// <param name="files">The file system used for temporary scripts and destination paths.</param>
    /// <param name="dialogs">The service used to browse for an output file and to save a preset.</param>
    public EncodeViewModel(IEncoderService encoder, IOptions<AppPaths> paths, ISettingsProvider<AppSettingsData> settings,
        IFileSystemService files, IDialogService dialogs)
    {
        _encoder = encoder.CheckNotNull();
        _paths = paths.CheckNotNull();
        _settings = settings.CheckNotNull();
        Files = files.CheckNotNull();
        DisplayName = "Encode";
        _dialogs = dialogs.CheckNotNull();
        ReloadPresets();
        this.WhenAnyValue(x => x.Presets.SelectedValue).Subscribe(ApplyPreset);
        this.WhenAnyValue(x => x.Arguments).Subscribe(LeavePresetWhenEdited);
    }

    /// <inheritdoc />
    public bool? DialogResult { get; private set; }

    /// <summary>
    /// Gets the preset choices, with a blank row first.
    /// The rows reflect settings. Choosing the blank row leaves the arguments as typed.
    /// </summary>
    public ListItemCollectionView<EncodePreset?> Presets { get; } = new();

    /// <summary>
    /// Gets or sets the FFmpeg arguments between the input and the destination.
    /// </summary>
    [Reactive]
    public partial string Arguments { get; set; } = "";

    /// <summary>
    /// Gets or sets the output file.
    /// </summary>
    [Reactive]
    public partial string Destination { get; set; } = "";

    /// <summary>
    /// Asks for an output path.
    /// </summary>
    public RxCommandVoid Browse => field ??= ReactiveCommand.CreateFromTask(BrowseAsync);

    /// <summary>
    /// Asks for a name and stores the current arguments under it.
    /// </summary>
    public RxCommandVoid SavePreset => field ??= ReactiveCommand.CreateFromTask(SavePresetAsync);

    // Returns the arguments passed between FFmpeg's input and destination.
    private string? JobArguments()
    {
        var text = Arguments.Replace("\r\n", "\n").Replace('\n', ' ').Replace('\r', ' ').Trim();
        return text.HasText() ? text : null;
    }

    // Copies a named preset into the arguments box. The blank row leaves the text alone.
    private void ApplyPreset(EncodePreset? preset)
    {
        if (_applyingPreset || preset == null || !preset.Name.HasText())
        {
            return;
        }

        _applyingPreset = true;
        Arguments = preset.Arguments;
        _applyingPreset = false;
    }

    // Edited arguments are no longer the selected preset.
    private void LeavePresetWhenEdited(string arguments)
    {
        var preset = Presets.SelectedValue;
        if (_applyingPreset || preset == null || !preset.Name.HasText() || preset.Arguments == arguments)
        {
            return;
        }

        Presets.CurrentItem = Presets[0];
    }

    // Replaces the named rows from settings, keeping a blank row first.
    private void ReloadPresets()
    {
        var selected = Presets.SelectedValue?.Name ?? "";
        _applyingPreset = true;
        ShowPresets(EncodePreset.StoredOrExamples(Settings.Value.EncodePresets));
        if (selected.HasText())
        {
            Presets.CurrentItem = Presets.FirstOrDefault(item => item.Value?.Name == selected) ?? Presets[0];
        }

        _applyingPreset = false;
    }

    // Shows a blank row, then the catalog. The blank row is not a saved preset.
    private void ShowPresets(IReadOnlyList<EncodePreset> source)
    {
        Presets.Clear();
        Presets.Add(null, "");
        Presets.AddRange(source
            .Where(preset => preset.Name.HasText())
            .Select(preset => new ListItem<EncodePreset?>(preset.Copy(), preset.Name)));
        Presets.CurrentItem = Presets[0];
    }

    // Stores the arguments under the name from the dialog, then refreshes the combo.
    private async Task SavePresetAsync()
    {
        var dialog = _dialogs.CreateViewModel<SavePresetViewModel>().Load(Presets.SelectedValue?.Name ?? "");
        var accepted = await _dialogs.ShowDialogAsync(this, dialog);
        if (accepted == true)
        {
            Store(dialog.PresetName);
        }

        ReloadPresets();
        if (accepted == true)
        {
            _applyingPreset = true;
            Presets.CurrentItem = Presets.FirstOrDefault(item => item.Value?.Name == dialog.PresetName) ?? Presets[0];
            _applyingPreset = false;
        }
    }

    // Writes the accepted name into settings. An unsaved example list is stored with it.
    private void Store(string name)
    {
        var list = EncodePreset.StoredOrExamples(Settings.Value.EncodePresets)
            .Where(preset => preset.Name.HasText())
            .Select(preset => new EncodePreset
            {
                Name = preset.Name,
                Arguments = preset.Name == name ? Arguments : preset.Arguments
            })
            .ToList();
        if (!list.Any(preset => preset.Name == name))
        {
            list.Add(new EncodePreset { Name = name, Arguments = Arguments });
        }

        Settings.Value.EncodePresets = list;
        Settings.Save();
    }

    private async Task BrowseAsync()
    {
        var settings = new SaveFileDialogSettings
        {
            Title = "Encode to",
            SuggestedFileName = Files.Path.GetFileName(Destination),
            DefaultExtension = ".mkv",
            Filters = [new FileFilter("Matroska", "mkv"), new FileFilter("All files", "*")]
        };
        var file = await _dialogs.ShowSaveFileDialogAsync(this, settings);
        if (file == null)
        {
            return;
        }

        Destination = file.LocalPath;
    }

    private IFileSystemService Files { get; }

    private ISettingsProvider<AppSettingsData> Settings => _settings;

    /// <summary>
    /// Gets the file name of the attached tab.
    /// An unsaved tab shows its tab title.
    /// </summary>
    [Reactive]
    public partial string ScriptName { get; private set; } = "";

    /// <summary>
    /// Gets or sets the first frame to read.
    /// </summary>
    [RangeClusive(min: 0)]
    [Reactive]
    public partial int StartFrame { get; set; }

    /// <summary>
    /// Gets or sets how many frames to read. Empty reads the rest of the clip.
    /// </summary>
    [RangeClusive(min: 1)]
    [Reactive]
    public partial int? MaxLength { get; set; }

    /// <summary>
    /// Gets whether a job is running. Start stays disabled until it finishes.
    /// </summary>
    [Reactive]
    public partial bool IsRunning { get; private set; }

    /// <summary>
    /// Gets the job state.
    /// </summary>
    [Reactive]
    public partial string Status { get; private set; } = "";

    /// <summary>
    /// The window that stays open and owns the progress dialog.
    /// </summary>
    public void UseWindowOwner(INotifyPropertyChanged owner) => _windowOwner = owner.CheckNotNull();

    /// <summary>
    /// Attaches a tab. Each job captures its current script when started.
    /// </summary>
    /// <param name="tab">The attached editor or viewer, or null to clear the attachment.</param>
    public void Bind(IScriptViewModel? tab)
    {
        if (IsRunning)
        {
            return;
        }

        _tab = tab;
        switch (tab)
        {
            case IEditorViewModel editor:
                SetScript(editor.Script, editor.FileName, editor.Kind, editor.DisplayName);
                _clipFrames = 0;
                break;
            case IViewerViewModel viewer:
                SetScript(viewer.Script ?? "", viewer.FileName, viewer.Kind, viewer.DisplayName);
                _clipFrames = viewer.ClipInfo?.FrameCount ?? 0;
                break;
            default:
                SetScript("", null, ScriptKind.VapourSynth, null);
                _clipFrames = 0;
                break;
        }
    }

    /// <summary>
    /// Starts the job. Disabled while one is already running.
    /// </summary>
    public RxCommandVoid Start => field ??= ReactiveCommand.CreateFromTask(
        StartAsync,
        this.WhenAnyValue(x => x.IsRunning).Select(running => !running));

    // Stores the script the next Start call will write.
    private void SetScript(string text, string? path, ScriptKind kind, string? displayName)
    {
        _scriptText = text;
        _scriptPath = path;
        _kind = kind;
        ScriptName = path.HasText()
            ? Files.Path.GetFileName(path)
            : displayName.HasText() ? displayName : "Untitled";
    }

    private async Task StartAsync()
    {
        if (!TryRange(out var first, out var last))
        {
            return;
        }

        var destination = Destination;
        if (!destination.HasText())
        {
            Status = "Choose an output file.";
            return;
        }

        ScriptJobFile? job = null;
        try
        {
            job = new ScriptJobFile(Files, _scriptPath, _scriptText, _kind, first, last);
            Files.EnsureDirectoryExists(destination);
            var arguments = JobArguments();
            _paths.Value.FFmpeg = _settings.Value.FFmpegPath.HasText() ? _settings.Value.FFmpegPath : "ffmpeg";
            var length = last.HasValue ? (long)last.Value - first + 1 : 0L;
            if (length <= 0 && _clipFrames > first)
            {
                length = _clipFrames - first;
            }

            var options = new ProcessOptionsEncoder(ProcessDisplayMode.Interface, "Encoding")
            {
                WorkingDirectory = job.WorkingDirectory,
                FrameCount = length
            };
            var library = Environment.GetEnvironmentVariable("VSSCRIPT_PATH");
            if (_kind == ScriptKind.VapourSynth && OperatingSystem.IsLinux() && library.HasText())
            {
                options.EnvironmentVariables["LD_PRELOAD"] = library;
            }

            var source = job.Path;
            var kind = _kind;
            IsRunning = true;
            DialogResult = true;
            CloseView();
            await Task.Run(() => RunEncode(kind, source, destination, arguments, options));
        }
        catch (Exception ex)
        {
            if (DialogResult == true && _encoder.UiManager is FFmpegUserInterfaceManager manager)
            {
                manager.ReportStartFailure(_worker, ex.Message);
            }
            else
            {
                Status = ex.Message;
            }
        }
        finally
        {
            job?.Dispose();
            IsRunning = false;
        }
    }

    // Runs the selected script through FFmpeg and lets its UI manager display the worker.
    private CompletionStatus RunEncode(ScriptKind kind, string source, string? destination, string? arguments,
        ProcessOptionsEncoder options)
    {
        var encoder = _encoder.GetMediaEncoder(_windowOwner ?? this);
        return kind == ScriptKind.AviSynth
            ? encoder.EncodeFFmpegAvisynth(source, destination, arguments, options, OnStarted)
            : encoder.EncodeFFmpegVapourSynth(source, destination, arguments, options, OnStarted);
    }

    // Retains the worker so a failed process start can update its progress window.
    private void OnStarted(object? sender, ProcessStartedEventArgs e) => _worker = e.ProcessWorker;

    private bool TryRange(out int first, out int? last)
    {
        first = StartFrame;
        last = null;
        if (!Check.IsInRange(first, 0, true, null, false))
        {
            return false;
        }

        if (!MaxLength.HasValue)
        {
            return true;
        }

        var length = MaxLength.Value;
        if (!Check.IsInRange(length, 1, true, int.MaxValue - first, true))
        {
            return false;
        }

        last = first + length - 1;
        return true;
    }
}
