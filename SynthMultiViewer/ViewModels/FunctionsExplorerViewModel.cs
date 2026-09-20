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
    public ObservableCollection<BrowseGroup> Groups { get; } = [];

    /// <summary>
    /// Gets the functions of <see cref="SelectedGroup"/> when not searching.
    /// </summary>
    public ObservableCollection<BrowseFunction> Functions { get; } = [];

    /// <summary>
    /// Gets matches across every group while searching.
    /// </summary>
    public ObservableCollection<FunctionHit> Hits { get; } = [];

    /// <summary>
    /// Gets whether Insert can write into the current editor.
    /// </summary>
    public bool CanInsert => Editor != null && SelectedFunction != null;

    /// <summary>
    /// Reloads catalogs then rebuilds the list from the last editor snapshot.
    /// </summary>
    public RxCommandVoid Refresh => field ??= ReactiveCommand.CreateFromTask(RefreshAsync);

    /// <summary>
    /// Inserts the selected function at the editor caret.
    /// </summary>
    public RxCommandVoid Insert => field ??= ReactiveCommand.Create(InsertImpl,
        this.WhenAnyValue(x => x.Editor, x => x.SelectedFunction, (editor, function) =>
            editor != null && function != null));

    /// <summary>
    /// Clears the search box and restores the two-pane list.
    /// </summary>
    public RxCommandVoid ClearFilter => field ??= ReactiveCommand.Create(() => { Filter = ""; },
        this.WhenAnyValue(x => x.IsSearching));

    /// <summary>
    /// Clears the window so it can be opened again after the view is closed.
    /// </summary>
    public void OnClosed() => CloseView();

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
        CaptureEditor();
        var languages = ResolveLanguages();
        if (languages == null)
        {
            return;
        }

        var load = ++_load;
        IReadOnlyList<string>? extra = null;
        if (_language == ScriptLanguageFactory.VapourSynth)
        {
            var files = ResolveFiles();
            extra = files == null ? null : ScriptPackages.List(VapourSynthIncludeSource.SearchRoots(), files);
        }

        var groups = await languages.BrowseAsync(_language, _text, CancellationToken.None, _path, extra);
        if (load != _load)
        {
            return;
        }

        Groups.Clear();
        foreach (var group in groups)
        {
            Groups.Add(group);
        }

        SelectedGroup = Groups.Count == 0 ? null : Groups[0];
    }

    private void OnEditorChanged(IEditorViewModel? editor)
    {
        if (editor == null)
        {
            return;
        }

        _ = ReloadAsync();
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
        Functions.Clear();
        Hits.Clear();
        IsSearching = Filter.HasText();
        if (IsSearching)
        {
            var hits = new List<FunctionHit>();
            foreach (var group in Groups)
            {
                foreach (var function in group.Functions)
                {
                    if (function.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase) ||
                        group.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase))
                    {
                        hits.Add(new FunctionHit(group.Name, function));
                    }
                }
            }

            hits.Sort(CompareHits);
            foreach (var hit in hits)
            {
                Hits.Add(hit);
            }

            SelectedHit = keepName != null
                ? Hits.FirstOrDefault(item => item.Function.Name == keepName &&
                                              (keepGroup == null || item.Group == keepGroup)) ??
                  Hits.ElementAtOrDefault(0)
                : Hits.ElementAtOrDefault(0);
            SelectedFunction = SelectedHit?.Function;
            return;
        }

        SelectedHit = null;
        if (SelectedGroup == null)
        {
            SelectedFunction = null;
            return;
        }

        foreach (var function in SelectedGroup.Functions)
        {
            Functions.Add(function);
        }

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
        if (Editor == null || SelectedFunction == null)
        {
            return;
        }

        var function = SelectedFunction;
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

    private IScriptLanguageFactory? ResolveLanguages() =>
        Languages ?? Locator.Current.GetService<IScriptLanguageFactory>();

    private IFileSystemService? ResolveFiles() =>
        Files ?? Locator.Current.GetService<IFileSystemService>();
}
