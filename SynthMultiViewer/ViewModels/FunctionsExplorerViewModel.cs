using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.ScriptAssist.Services;
using HanumanInstitute.ScriptAssist.VapourSynth;
using HanumanInstitute.SynthMultiViewer.Helpers;
using HanumanInstitute.SynthMultiViewer.Services;
using Splat;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Modeless function browser over ScriptAssist catalogs and the current editor snapshot.
/// </summary>
public partial class FunctionsExplorerViewModel : WorkspaceViewModel, IViewClosed
{
    private int _load;
    private int _browseVersion = -1;
    private string _language = ScriptLanguageFactory.VapourSynth;
    private string _text = "";
    private string? _path;
    private IEditorViewModel? _loaded;
    private CancellationTokenSource? _browseCts;
    private IDisposable? _editorWatch;
    private string? _packageKey;
    private IReadOnlyList<string>? _packages;
    private FunctionHit[] _catalog = [];

    /// <summary>
    /// Creates an empty explorer window.
    /// </summary>
    public FunctionsExplorerViewModel()
    {
        DisplayName = "Functions Explorer";
        this.WhenAnyValue(x => x.SelectedGroup, x => x.Filter).Subscribe(_ => RebuildLists());
        this.WhenAnyValue(x => x.SelectedHit).Subscribe(hit =>
        {
            if (hit != null)
            {
                SelectedFunction = hit.Function;
            }
        });
        this.WhenAnyValue(x => x.Editor).Subscribe(OnEditorChanged);
        this.WhenAnyValue(x => x.Editor, x => x.SelectedFunction)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(CanInsert));
                this.RaisePropertyChanged(nameof(CanGoTo));
            });
    }

    /// <summary>
    /// Gets or sets the factory used to browse and refresh catalogs. The locator is used when null.
    /// </summary>
    public IScriptLanguageFactory? Languages { get; set; }

    /// <summary>
    /// Gets or sets the disk used to list installed Python packages. The locator is used when null.
    /// </summary>
    public IFileSystemService? Files { get; set; }

    /// <summary>
    /// Gets or sets the session placement bound by the explorer window.
    /// </summary>
    public FunctionsExplorerPlacement Placement { get; set; } = new();

    /// <summary>
    /// Gets or sets the editor whose snapshot is listed. Insert and browse follow this editor.
    /// </summary>
    [Reactive]
    public partial IEditorViewModel? Editor { get; set; }

    /// <summary>
    /// Gets or sets the in-memory function name filter.
    /// </summary>
    [Reactive]
    public partial string Filter { get; set; } = "";

    /// <summary>
    /// Gets or sets the selected group.
    /// </summary>
    [Reactive]
    public partial BrowseGroup? SelectedGroup { get; set; }

    /// <summary>
    /// Gets or sets the selected function.
    /// </summary>
    [Reactive]
    public partial BrowseFunction? SelectedFunction { get; set; }

    /// <summary>
    /// Gets or sets the selected search hit.
    /// </summary>
    [Reactive]
    public partial FunctionHit? SelectedHit { get; set; }

    /// <summary>
    /// Gets whether the search box is filtering every group into one list.
    /// </summary>
    [Reactive]
    public partial bool IsSearching { get; set; }

    /// <summary>
    /// Gets a short error from the last failed reload, or null.
    /// </summary>
    [Reactive]
    public partial string? Error { get; set; }

    /// <summary>
    /// Gets the groups from the last browse.
    /// </summary>
    public BatchList<BrowseGroup> Groups { get; } = [];

    /// <summary>
    /// Gets the functions of <see cref="SelectedGroup"/> when not searching.
    /// </summary>
    public BatchList<BrowseFunction> Functions { get; } = [];

    /// <summary>
    /// Gets matches across every group while searching.
    /// </summary>
    public BatchList<FunctionHit> Hits { get; } = [];

    /// <summary>
    /// Gets whether Insert can write into the current editor.
    /// </summary>
    public bool CanInsert =>
        Editor != null && SelectedFunction != null && ReferenceEquals(Editor, _loaded);

    /// <summary>
    /// Gets whether Go To can move the caret to a This-file header.
    /// </summary>
    public bool CanGoTo =>
        Editor != null && SelectedFunction?.Offset != null && ReferenceEquals(Editor, _loaded) &&
        Editor.DocumentVersion == _browseVersion;

    /// <summary>
    /// Reloads catalogs then rebuilds the list from the last editor snapshot.
    /// </summary>
    public RxCommandVoid Refresh => field ??= ReactiveCommand.CreateFromTask(RefreshAsync);

    /// <summary>
    /// Inserts the selected function at the editor caret.
    /// </summary>
    public RxCommandVoid Insert => field ??= ReactiveCommand.Create(InsertImpl,
        this.WhenAnyValue(x => x.CanInsert));

    /// <summary>
    /// Moves the editor caret to the selected This-file header.
    /// </summary>
    public RxCommandVoid GoTo => field ??= ReactiveCommand.Create(GoToImpl,
        this.WhenAnyValue(x => x.CanGoTo));

    /// <summary>
    /// Clears the search box and restores the two-pane list.
    /// </summary>
    public RxCommandVoid ClearFilter => field ??= ReactiveCommand.Create(() => { Filter = ""; },
        this.WhenAnyValue(x => x.IsSearching));

    /// <summary>
    /// Clears the window so it can be opened again after the view is closed.
    /// </summary>
    public void OnClosed()
    {
        CancelBrowse();
        _editorWatch?.Dispose();
        _editorWatch = null;
        _load++;
        _text = "";
        _path = null;
        _loaded = null;
        _browseVersion = -1;
        Error = null;
        Groups.Replace([]);
        _catalog = [];
        SelectedGroup = null;
        SelectedFunction = null;
        SelectedHit = null;
        this.RaisePropertyChanged(nameof(CanInsert));
        this.RaisePropertyChanged(nameof(CanGoTo));
        CloseView();
    }

    /// <summary>
    /// Reloads catalogs then browses the last editor snapshot.
    /// </summary>
    public async Task RefreshAsync()
    {
        _packages = null;
        _packageKey = null;
        ResolveLanguages()?.Refresh();
        await ReloadAsync();
    }

    /// <summary>
    /// Moves the current function or search hit by <paramref name="delta"/> items.
    /// </summary>
    public void MoveSelection(int delta)
    {
        if (IsSearching)
        {
            if (Hits.Count == 0)
            {
                return;
            }

            var index = SelectedHit == null ? -1 : Hits.IndexOf(SelectedHit);
            SelectedHit = Hits[Math.Clamp(index + delta, 0, Hits.Count - 1)];
            return;
        }

        if (Functions.Count == 0)
        {
            return;
        }

        var at = SelectedFunction == null ? -1 : Functions.IndexOf(SelectedFunction);
        SelectedFunction = Functions[Math.Clamp(at + delta, 0, Functions.Count - 1)];
    }

    /// <summary>
    /// Browses the last editor snapshot into <see cref="Groups"/>.
    /// </summary>
    public async Task ReloadAsync()
    {
        CancelBrowse();
        var cts = new CancellationTokenSource();
        _browseCts = cts;
        var token = cts.Token;
        var editor = Editor;
        CaptureEditor();
        var languages = ResolveLanguages();
        var load = ++_load;
        _loaded = null;
        this.RaisePropertyChanged(nameof(CanInsert));
        this.RaisePropertyChanged(nameof(CanGoTo));
        if (languages == null || editor == null)
        {
            return;
        }

        try
        {
            Error = null;
            IReadOnlyList<string>? extra = null;
            if (_language == ScriptLanguageFactory.VapourSynth)
            {
                extra = await LoadPackagesAsync(token);
            }

            var groups = await languages.BrowseAsync(_language, _text, token, _path, extra);
            if (load != _load || token.IsCancellationRequested)
            {
                return;
            }

            Groups.Replace(groups);
            IndexCatalog();
            SelectedGroup = Groups.ElementAtOrDefault(0);
            _loaded = editor;
            this.RaisePropertyChanged(nameof(CanInsert));
            this.RaisePropertyChanged(nameof(CanGoTo));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (load == _load)
            {
                Error = ex.Message;
            }
        }
    }

    private async Task<IReadOnlyList<string>?> LoadPackagesAsync(CancellationToken token)
    {
        var files = ResolveFiles();
        if (files == null)
        {
            return null;
        }

        var cachedKey = _packageKey;
        var cached = _packages;
        var extra = await Task.Run(() =>
        {
            var found = VapourSynthIncludeSource.SearchRoots();
            var current = string.Join('\0', found);
            if (cached != null && cachedKey == current)
            {
                return (Key: current, Names: cached);
            }

            return (Key: current, Names: ScriptPackages.List(found, files, token));
        }, token);
        if (!token.IsCancellationRequested)
        {
            _packageKey = extra.Key;
            _packages = extra.Names;
        }

        return extra.Names;
    }

    private void OnEditorChanged(IEditorViewModel? editor)
    {
        _editorWatch?.Dispose();
        _editorWatch = null;
        _loaded = null;
        this.RaisePropertyChanged(nameof(CanInsert));
        this.RaisePropertyChanged(nameof(CanGoTo));
        if (editor == null)
        {
            return;
        }

        _editorWatch = new CompositeDisposable(
            editor.WhenAnyValue(x => x.Kind, x => x.FileName)
                .Skip(1)
                .Subscribe(pair =>
                {
                    _ = ReloadAsync();
                }),
            editor.WhenAnyValue(x => x.DocumentVersion)
                .Skip(1)
                .Subscribe(_ =>
                {
                    this.RaisePropertyChanged(nameof(CanInsert));
                    this.RaisePropertyChanged(nameof(CanGoTo));
                }));
        _ = ReloadAsync();
    }

    private void CancelBrowse()
    {
        _browseCts?.Cancel();
        _browseCts?.Dispose();
        _browseCts = null;
    }

    private void IndexCatalog()
    {
        var hits = new List<FunctionHit>();
        foreach (var group in Groups)
        {
            foreach (var function in group.Functions)
            {
                hits.Add(new FunctionHit(group.Name, function));
            }
        }

        hits.Sort(CompareHits);
        _catalog = [..hits];
    }

    private void CaptureEditor()
    {
        if (Editor == null)
        {
            return;
        }

        _language = Editor.Kind.ToString();
        _text = Editor.Script;
        _path = Editor.FileName;
        _browseVersion = Editor.DocumentVersion;
    }

    private void RebuildLists()
    {
        var keep = SelectedFunction;
        var keepGroup = SelectedHit?.Group ?? SelectedGroup?.Name;
        IsSearching = Filter.HasText();
        if (IsSearching)
        {
            var hits = new List<FunctionHit>();
            foreach (var hit in _catalog)
            {
                if (hit.Function.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase) ||
                    hit.Group.Contains(Filter, StringComparison.OrdinalIgnoreCase))
                {
                    hits.Add(hit);
                }
            }

            Hits.Replace(hits);
            Functions.Replace([]);
            SelectedHit = keep != null
                ? Hits.FirstOrDefault(item => SameFunction(item.Function, keep) &&
                                              (keepGroup == null || item.Group == keepGroup)) ??
                  Hits.ElementAtOrDefault(0)
                : Hits.ElementAtOrDefault(0);
            SelectedFunction = SelectedHit?.Function;
            return;
        }

        SelectedHit = null;
        Hits.Replace([]);
        if (SelectedGroup == null)
        {
            Functions.Replace([]);
            SelectedFunction = null;
            return;
        }

        Functions.Replace(SelectedGroup.Functions);
        SelectedFunction = keep != null
            ? Functions.FirstOrDefault(item => SameFunction(item, keep)) ?? Functions.ElementAtOrDefault(0)
            : Functions.ElementAtOrDefault(0);
    }

    private static bool SameFunction(BrowseFunction left, BrowseFunction right) =>
        left.InsertText == right.InsertText && left.Import == right.Import &&
        left.Signature == right.Signature && left.Offset == right.Offset;

    private static int CompareHits(FunctionHit left, FunctionHit right)
    {
        var names = string.Compare(left.Function.Name, right.Function.Name, StringComparison.OrdinalIgnoreCase);
        return names != 0
            ? names
            : string.Compare(left.Group, right.Group, StringComparison.OrdinalIgnoreCase);
    }

    private void InsertImpl()
    {
        if (!CanInsert || Editor == null || SelectedFunction == null)
        {
            return;
        }

        var function = SelectedFunction;
        Editor.BeginUndoGroup();
        try
        {
            if (function.Import.HasText())
            {
                var plan = VapourSynthImports.Plan(Editor.Script, function.Import);
                if (plan.Needed)
                {
                    Editor.Insert(plan.Offset, plan.Text);
                }
            }

            var text = function.InsertText;
            var caret = Editor.CaretOffset;
            Editor.InsertAtCaret(text);
            if (text.EndsWith("()", StringComparison.Ordinal))
            {
                Editor.CaretOffset = caret + text.Length - 1;
            }
        }
        finally
        {
            Editor.EndUndoGroup();
        }
    }

    private void GoToImpl()
    {
        if (!CanGoTo || Editor == null || SelectedFunction?.Offset is not { } offset)
        {
            return;
        }

        Editor.Reveal(offset);
    }

    /// <summary>
    /// Replaces items with one collection reset.
    /// </summary>
    public sealed class BatchList<T> : ObservableCollection<T>
    {
        /// <summary>
        /// Replaces the contents and raises a single reset.
        /// </summary>
        public void Replace(IReadOnlyList<T> items)
        {
            CheckReentrancy();
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    private IScriptLanguageFactory? ResolveLanguages() =>
        Languages ?? Locator.Current.GetService<IScriptLanguageFactory>();

    private IFileSystemService? ResolveFiles() =>
        Files ?? Locator.Current.GetService<IFileSystemService>();
}
