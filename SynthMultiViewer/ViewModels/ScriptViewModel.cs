using System.ComponentModel;
using System.Reactive.Linq;
using Avalonia.Media;
using HanumanInstitute.SynthMultiViewer.Models;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Describes a tab and its editable header.
/// </summary>
public interface IScriptViewModel : IWorkspaceViewModel, INotifyPropertyChanged
{
    /// <summary>
    /// Gets whether the tab can be renamed.
    /// </summary>
    bool CanEditHeader { get; }
    /// <summary>
    /// Gets or sets whether the header editor is visible.
    /// </summary>
    bool IsEditingHeader { get; set; }
    /// <summary>
    /// Gets the command that begins renaming an active tab.
    /// </summary>
    RxCommandVoid BeginHeaderEdit { get; }
    /// <summary>
    /// Gets the command that finishes editing the tab name.
    /// </summary>
    RxCommandVoid HeaderEditDone { get; }
    /// <summary>
    /// Gets the command that discards the header edit.
    /// </summary>
    RxCommandVoid HeaderEditCancel { get; }
    /// <summary>
    /// Gets or sets the creation index used for default tab titles.
    /// </summary>
    int Index { get; set; }
    /// <summary>
    /// Gets or sets whether this tab is selected.
    /// </summary>
    bool IsActive { get; set; }
    /// <summary>
    /// Gets or sets which native engine evaluates the script.
    /// </summary>
    ScriptKind Kind { get; set; }
    /// <summary>
    /// Gets or sets an optional per-tab hue; null uses the VapourSynth or AviSynth hue.
    /// </summary>
    Color? TabColor { get; set; }
    /// <summary>
    /// Gets the brush painted on the tab.
    /// </summary>
    IBrush TabBackground { get; }
    /// <summary>
    /// Updates <see cref="TabBackground"/> from the current override or settings.
    /// </summary>
    void ApplyTabColor(AppSettingsData settings);
}

/// <summary>
/// Provides selection, ordering, and header editing for script tabs.
/// </summary>
public partial class ScriptViewModel : WorkspaceViewModel, IScriptViewModel
{
    private string _headerName = string.Empty;
    private bool _isEditingHeader;
    private bool _applyingHeader;

    /// <summary>
    /// Creates an editable tab.
    /// </summary>
    public ScriptViewModel()
    {
        this.WhenAnyValue(x => x.Kind, x => x.TabColor)
            .Subscribe(_ => RefreshTabBackground());
        RefreshTabBackground();
    }

    /// <summary>
    /// Creates an editable tab with the specified title and close permission.
    /// </summary>
    public ScriptViewModel(string displayName, bool canClose) : base(displayName, canClose) { }

    /// <inheritdoc />
    public bool CanEditHeader { get; protected set; } = true;

    /// <inheritdoc />
    public bool IsEditingHeader
    {
        get => _isEditingHeader;
        set
        {
            if (_isEditingHeader == value) { return; }

            if (_isEditingHeader && !value && !_applyingHeader)
            {
                EndHeaderEdit(true);
                return;
            }

            this.RaiseAndSetIfChanged(ref _isEditingHeader, value);
        }
    }

    /// <inheritdoc />
    [Reactive]
    public partial int Index { get; set; }

    /// <inheritdoc />
    [Reactive]
    public partial bool IsActive { get; set; }

    /// <inheritdoc />
    [Reactive]
    public partial ScriptKind Kind { get; set; }

    /// <inheritdoc />
    [Reactive]
    public partial Color? TabColor { get; set; }

    /// <inheritdoc />
    [Reactive]
    public partial IBrush TabBackground { get; private set; } = Brushes.Transparent;

    private AppSettingsData? _tabColorSettings;

    /// <summary>
    /// Begins renaming an active tab when its header is editable.
    /// </summary>
    public RxCommandVoid BeginHeaderEdit => field ??= ReactiveCommand.Create(
        StartHeaderEdit,
        this.WhenAnyValue(
            x => x.IsActive,
            x => x.IsEditingHeader,
            (active, editing) => active && !editing && CanEditHeader));

    /// <inheritdoc />
    public RxCommandVoid HeaderEditDone => field ??= ReactiveCommand.Create(
        () => EndHeaderEdit(true),
        this.WhenAnyValue(x => x.IsEditingHeader));

    /// <inheritdoc />
    public RxCommandVoid HeaderEditCancel => field ??= ReactiveCommand.Create(
        () => EndHeaderEdit(false),
        this.WhenAnyValue(x => x.IsEditingHeader));

    private void StartHeaderEdit()
    {
        _headerName = DisplayName;
        IsEditingHeader = true;
    }

    private void EndHeaderEdit(bool commit)
    {
        if (!_isEditingHeader) { return; }

        var name = DisplayName.Trim();
        var result = commit && name.Length > 0 ? name : _headerName;
        DisplayName = result;
        _applyingHeader = true;
        IsEditingHeader = false;
        _applyingHeader = false;
    }

    /// <inheritdoc />
    public void ApplyTabColor(AppSettingsData settings)
    {
        _tabColorSettings = settings;
        RefreshTabBackground();
    }

    private void RefreshTabBackground()
    {
        var viewer = this is IViewerViewModel;
        var theme = _tabColorSettings?.Theme ?? AppTheme.Light;
        var color = TabColors.For(Kind, viewer, theme, TabColor);
        if (TabBackground is SolidColorBrush brush && brush.Color == color)
        {
            return;
        }

        TabBackground = new SolidColorBrush(color);
    }
}
