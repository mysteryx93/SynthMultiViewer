using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Combines a cached catalog with a language profile and a document snapshot cache.
/// </summary>
public sealed class LanguageService : ILanguageService
{
    private readonly ILanguage _language;
    private readonly ISymbolCatalog _catalog;
    private readonly Lock _cacheGate = new();
    private readonly List<CachedSnapshot> _snapshots = [];
    private readonly Dictionary<SnapshotKey, InflightBuild> _inflight = [];
    private int _generation;
    private const int SnapshotLimit = 8;
    private const long SnapshotByteLimit = 16 * 1024 * 1024;

    /// <summary>
    /// Creates a service for <paramref name="language"/> using <paramref name="catalog"/>.
    /// </summary>
    public LanguageService(ILanguage language, ISymbolCatalog catalog)
    {
        _language = language.CheckNotNull();
        _catalog = catalog.CheckNotNull();
    }

    /// <inheritdoc />
    public Task<Reply> GetAsync(string text, int caret, CancellationToken cancellationToken,
        string? documentPath = null) =>
        GetAsync(text, caret, cancellationToken, documentPath, completions: true);

    /// <summary>
    /// Analyzes a snapshot, optionally skipping completion items.
    /// </summary>
    internal Task<Reply> GetAsync(string text, int caret, CancellationToken cancellationToken,
        string? documentPath, bool completions) =>
        GetCoreAsync(text, caret, cancellationToken, documentPath, completions);

    private async Task<Reply> GetCoreAsync(string text, int caret, CancellationToken cancellationToken,
        string? documentPath, bool completions)
    {
        var native = await _catalog.GetAsync(cancellationToken).ConfigureAwait(false);
        if (caret < 0 || caret > text.Length)
        {
            return new([], null);
        }

        return await Task.Run(() => Analyze(text, caret, native, cancellationToken, documentPath, completions),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Computes completions, nested call insight, and hover from an immutable snapshot.
    /// </summary>
    internal Reply Analyze(string text, int caret, IReadOnlyList<Symbol> native, CancellationToken token = default,
        string? documentPath = null, bool completions = true)
    {
        if (caret < 0 || caret > text.Length) { return new([], null); }

        return ReplyFrom(Snapshot(text, native, token, documentPath), caret, token, completions);
    }

    private Reply ReplyFrom(DocumentSnapshot snapshot, int caret, CancellationToken token, bool completions)
    {
        if (snapshot.Masked.IsLiteral(caret))
        {
            return new([], null);
        }

        token.ThrowIfCancellationRequested();
        var bindings = snapshot.Bindings.At(caret);
        var path = ExpressionReader.Read(snapshot.Masked.Code, caret, _language, token, snapshot.Joins);
        var receiver = _language.TypeOf(path.Segments, bindings, snapshot.Catalog);
        var walk = CallScanner.Walk(snapshot.Masked.Code, _language, bindings, snapshot.Catalog, token,
            snapshot.Quoted.Code, caret, snapshot.Joins);
        var scan = bindings.InFunctionHeader(caret) ? null : walk.Scan;
        var insight = scan?.Insight;
        var unclosed = walk.Unclosed;
        var items = !completions || unclosed == '[' && path.Segments.Count == 0
            ? new()
            : Complete(path, receiver, snapshot, bindings, token);
        if (completions)
        {
            AddParameterNames(items, path, scan, snapshot.Masked.Code);
        }

        var hoverContext = new HoverContext(scan, unclosed);
        var hover = _language is IContextHover contextual
            ? contextual.Hover(snapshot.Masked.Code, path, bindings, snapshot.Catalog, hoverContext)
            : _language.Hover(snapshot.Masked.Code, path, bindings, snapshot.Catalog);
        var comparison = _language.Comparison;
        var comparer = comparison == StringComparison.OrdinalIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        return new(items.DistinctBy(x => x.InsertionText, comparer).OrderByDescending(x => x.Priority)
                .ThenBy(x => x.InsertionText, comparer).ToArray(), insight, hover);
    }

    private List<CompletionItem> Complete(CaretPath path, TypeRef receiver, DocumentSnapshot snapshot,
        DocumentBindings bindings, CancellationToken token)
    {
        var members = _language.Members(receiver, snapshot.Catalog, bindings);
        var comparison = _language.Comparison;
        var items = new List<CompletionItem>();
        foreach (var symbol in members)
        {
            token.ThrowIfCancellationRequested();
            var name = symbol.DisplayName;
            if (name.Length == 0 || !name.StartsWith(path.Typed, comparison))
            {
                continue;
            }

            items.Add(new(name, path.Start, path.End - path.Start, symbol.Kind, symbol.Signature,
                _language.CompletionPriority(symbol, receiver)));
        }
        return items;
    }

    private static string ParameterInsertion(string name, string code, int end)
    {
        var i = end;
        while (i < code.Length && char.IsWhiteSpace(code[i]))
        {
            i++;
        }

        return i < code.Length && code[i] == '=' ? name : name + "=";
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BrowseGroup>> BrowseAsync(string text, CancellationToken cancellationToken,
        string? documentPath = null, IReadOnlyList<string>? extraPackages = null)
    {
        var native = await _catalog.GetAsync(cancellationToken).ConfigureAwait(false);
        return await Task.Run(() =>
        {
            var snapshot = Snapshot(text, native, cancellationToken, documentPath);
            return _language.Browse(native, snapshot.Bindings, text, cancellationToken, documentPath,
                extraPackages);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Invalidate()
    {
        lock (_cacheGate)
        {
            _generation++;
            _snapshots.Clear();
            foreach (var build in _inflight.Values)
            {
                build.Cts.Cancel();
            }

            _inflight.Clear();
            if (_language is IRefreshableLanguage refreshable)
            {
                refreshable.Invalidate();
            }
        }
    }

    private void AddParameterNames(List<CompletionItem> items, CaretPath path, CallScan? scan, string code)
    {
        if (scan == null || path.Segments.Count > 0 || scan.InNestedDelimiter ||
            !ParameterNames.AtArgumentStart(scan.CurrentArgument))
        {
            return;
        }

        var used = scan.UsedNames;
        var seen = new HashSet<string>(_language.Comparison == StringComparison.OrdinalIgnoreCase
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
        var consumed = scan.PositionalConsumed;
        var indexOverload = 0;
        foreach (var overload in scan.Overloads)
        {
            var skip = SkipFirst(scan, indexOverload++);
            if (overload.Parameters == null)
            {
                continue;
            }

            var hasSlash = false;
            foreach (var parameter in overload.Parameters)
            {
                if (parameter.Trim() == "/")
                {
                    hasSlash = true;
                    break;
                }
            }

            var seenSlash = false;
            var keywordOnly = false;
            var index = 0;
            foreach (var parameter in overload.Parameters)
            {
                var kind = ParameterNames.Classify(parameter, ref keywordOnly);
                if (kind is ParameterKind.Separator or ParameterKind.Kwargs or ParameterKind.Varargs)
                {
                    if (parameter.Trim() == "/")
                    {
                        seenSlash = true;
                    }

                    continue;
                }

                if (kind != ParameterKind.KeywordOnly)
                {
                    var slot = index++;
                    if (slot < skip)
                    {
                        continue;
                    }

                    if (slot - skip < consumed || hasSlash && !seenSlash)
                    {
                        continue;
                    }
                }

                var name = _language.ParameterName(parameter);
                if (name == null || used.Contains(name) || !seen.Add(name) ||
                    !name.StartsWith(path.Typed, _language.Comparison))
                {
                    continue;
                }

                items.Add(new(ParameterInsertion(name, code, path.End), path.Start, path.End - path.Start,
                    SymbolKind.Keyword, parameter, 2));
            }
        }
    }

    private static int SkipFirst(CallScan scan, int overload)
    {
        if (scan.OverloadSkips != null && (uint)overload < (uint)scan.OverloadSkips.Length)
        {
            return scan.OverloadSkips[overload] ? 1 : 0;
        }

        return scan.ImplicitClip ? 1 : 0;
    }

    private DocumentSnapshot Snapshot(string text, IReadOnlyList<Symbol> native, CancellationToken token,
        string? documentPath)
    {
        var key = new SnapshotKey(text, documentPath, native);
        InflightBuild inflight;
        lock (_cacheGate)
        {
            for (var i = 0; i < _snapshots.Count; i++)
            {
                var item = _snapshots[i];
                if (item.Text == text && item.Path == documentPath && ReferenceEquals(item.Catalog, native))
                {
                    if (i > 0)
                    {
                        _snapshots.RemoveAt(i);
                        _snapshots.Insert(0, item);
                    }

                    return item.Snapshot;
                }
            }

            if (_inflight.TryGetValue(key, out inflight!) && !inflight.Cts.IsCancellationRequested)
            {
                inflight.Waiters++;
            }
            else
            {
                inflight = new InflightBuild(_generation, new CancellationTokenSource());
                inflight.Work = Task.Run(() => Build(text, native, documentPath, inflight.Cts.Token),
                    inflight.Cts.Token);
                _inflight[key] = inflight;
            }
        }

        DocumentSnapshot? snapshot = null;
        try
        {
            snapshot = WaitSnapshot(inflight.Work, token);
            return snapshot;
        }
        finally
        {
            lock (_cacheGate)
            {
                inflight.Waiters--;
                if (token.IsCancellationRequested && inflight.Waiters == 0 && !inflight.Work.IsCompleted)
                {
                    inflight.Cts.Cancel();
                    if (_inflight.TryGetValue(key, out var abandoned) && ReferenceEquals(abandoned, inflight))
                    {
                        _inflight.Remove(key);
                    }
                }
                else if (inflight.Work.IsCompleted && _inflight.TryGetValue(key, out var current) &&
                    ReferenceEquals(current, inflight))
                {
                    _inflight.Remove(key);
                }

                if (snapshot != null && _generation == inflight.Generation && !inflight.Published)
                {
                    inflight.Published = true;
                    Publish(text, documentPath, native, snapshot);
                }
            }
        }
    }

    private static DocumentSnapshot WaitSnapshot(Task<DocumentSnapshot> work, CancellationToken token)
    {
        try
        {
            work.Wait(token);
        }
        catch (AggregateException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }

        return work.GetAwaiter().GetResult();
    }

    private DocumentSnapshot Build(string text, IReadOnlyList<Symbol> native, string? documentPath,
        CancellationToken token)
    {
        var prepared = PreparedDocument.Create(text, _language.Lexer, token, _language);
        var bindings = _language is IPreparedLanguage preparedLanguage
            ? preparedLanguage.Bind(prepared, native, token, documentPath)
            : _language.Bind(text, native, token, documentPath);
        return new DocumentSnapshot(prepared, bindings, native);
    }

    private void Publish(string text, string? documentPath, IReadOnlyList<Symbol> native, DocumentSnapshot snapshot)
    {
        for (var i = 0; i < _snapshots.Count; i++)
        {
            if (!ReferenceEquals(_snapshots[i].Snapshot, snapshot))
            {
                continue;
            }

            if (i > 0)
            {
                var item = _snapshots[i];
                _snapshots.RemoveAt(i);
                _snapshots.Insert(0, item);
            }

            return;
        }

        if (documentPath != null)
        {
            _snapshots.RemoveAll(item => item.Path == documentPath &&
                ReferenceEquals(item.Catalog, native));
        }
        else
        {
            _snapshots.RemoveAll(item => item.Text == text && item.Path == null &&
                ReferenceEquals(item.Catalog, native));
        }

        _snapshots.Insert(0, new(text, documentPath, native, snapshot));
        var total = 0L;
        foreach (var item in _snapshots)
        {
            total += item.Bytes;
        }

        while (_snapshots.Count > SnapshotLimit || total > SnapshotByteLimit && _snapshots.Count > 1)
        {
            var last = _snapshots[^1];
            total -= last.Bytes;
            _snapshots.RemoveAt(_snapshots.Count - 1);
            if (_language is IRefreshableLanguage refreshable)
            {
                refreshable.ReleaseDocument(last.Path);
            }
        }
    }

    private static long SnapshotBytes(string text, DocumentSnapshot snapshot) =>
        (long)text.Length * sizeof(char) * 3 + snapshot.Joins.Length + snapshot.Bindings.RetainedBytes();

    private sealed class InflightBuild(int generation, CancellationTokenSource cts)
    {
        public int Generation { get; } = generation;
        public CancellationTokenSource Cts { get; } = cts;
        public int Waiters { get; set; } = 1;
        public bool Published { get; set; }
        public Task<DocumentSnapshot> Work { get; set; } = null!;
    }

    private sealed class SnapshotKey : IEquatable<SnapshotKey>
    {
        public SnapshotKey(string text, string? path, IReadOnlyList<Symbol> catalog)
        {
            Text = text;
            Path = path;
            Catalog = catalog;
        }

        public string Text { get; }
        public string? Path { get; }
        public IReadOnlyList<Symbol> Catalog { get; }

        public bool Equals(SnapshotKey? other) =>
            other != null && Text == other.Text && Path == other.Path &&
            ReferenceEquals(Catalog, other.Catalog);

        public override bool Equals(object? obj) => obj is SnapshotKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Text, Path, RuntimeHelpers.GetHashCode(Catalog));
    }

    private sealed record CachedSnapshot(
        string Text,
        string? Path,
        IReadOnlyList<Symbol> Catalog,
        DocumentSnapshot Snapshot)
    {
        public long Bytes => SnapshotBytes(Text, Snapshot);
    }

    private sealed record DocumentSnapshot(
        PreparedDocument Prepared,
        DocumentBindings Bindings,
        IReadOnlyList<Symbol> Catalog)
    {
        public LexedBuffer Masked => Prepared.Masked;
        public LexedBuffer Quoted => Prepared.Quoted;
        public bool[] Joins => Prepared.Joins;
    }
}
