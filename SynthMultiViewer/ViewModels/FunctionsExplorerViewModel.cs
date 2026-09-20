using System.Collections.Specialized;
using System.ComponentModel;
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
    private string _language = ScriptLanguageFactory.VapourSynth;
    private string _text = "";
    private string? _path;
    private IEditorViewModel? _loaded;
    private CancellationTokenSource? _browseCts;
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
            .Subscribe(_ => this.RaisePropertyChanged(nameof(CanInsert)));
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
    public bool CanInsert => Editor != null && SelectedFunction != null && ReferenceEquals(Editor, _loaded);

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
        _load++;
        _text = "";
        _path = null;
        _loaded = null;
        Groups.Replace([]);
        _catalog = [];
        SelectedGroup = null;
        SelectedFunction = null;
        SelectedHit = null;
        this.RaisePropertyChanged(nameof(CanInsert));
        CloseView();
    }

    /// <summary>
    /// Reloads catalogs then browses the last editor snapshot.
    /// </summary>
    public async Task RefreshAsync()
    {
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
        if (languages == null || editor == null)
        {
            return;
        }

        try
        {
            IReadOnlyList<string>? extra = null;
            if (_language == ScriptLanguageFactory.VapourSynth)
            {
                var files = ResolveFiles();
                if (files != null)
                {
                    extra = await Task.Run(() =>
                    {
                        token.ThrowIfCancellationRequested();
                        return ScriptPackages.List(VapourSynthIncludeSource.SearchRoots(), files);
                    }, token);
                }
            }

            var groups = await languages.BrowseAsync(_language, _text, token, _path, extra);
            if (load != _load || token.IsCancellationRequested)
            {
                return;
            }

            Groups.Replace(groups);
            IndexCatalog();
            SelectedGroup = Groups.Count == 0 ? null : Groups[0];
            _loaded = editor;
            this.RaisePropertyChanged(nameof(CanInsert));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnEditorChanged(IEditorViewModel? editor)
    {
        _loaded = null;
        this.RaisePropertyChanged(nameof(CanInsert));
        if (editor == null)
        {
            return;
        }

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
    }

    private void RebuildLists()
    {
        var keepName = SelectedFunction?.Name;
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
            SelectedHit = keepName != null
                ? Hits.FirstOrDefault(item => item.Function.Name == keepName &&
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
        SelectedFunction = keepName != null
            ? Functions.FirstOrDefault(item => item.Name == keepName) ?? Functions.ElementAtOrDefault(0)
            : Functions.ElementAtOrDefault(0);
    }

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
            if (function.Import.HasText() && !VapourSynthImports.Contains(Editor.Script, function.Import))
            {
                var at = VapourSynthImports.InsertionOffset(Editor.Script);
                Editor.Insert(at, VapourSynthImports.Statement(Editor.Script, at, function.Import));
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
