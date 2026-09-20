namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Catalog-driven VapourSynth profile: bound plugins, vs/core/VideoNode members, same-file types.
/// </summary>
public sealed class VapourSynthLanguage : ILanguage, IPreparedLanguage, IRefreshableLanguage, IContextHover
{
    private readonly IIncludeSource? _read;
    /// <summary>
    /// Gets Python-like comment and string rules.
    /// </summary>
    private static LexerOptions LexerOptions { get; } = new()
    {
        HashLineComments = true,
        SingleQuotes = true,
        TripleQuotes = true,
        StringEscapes = true,
        PythonLineContinuations = true
    };

    /// <summary>
    /// Creates a profile that optionally follows Python imports through <paramref name="read"/>.
    /// </summary>
    public VapourSynthLanguage(IIncludeSource? read = null) => _read = read;

    /// <inheritdoc />
    public LexerOptions Lexer => LexerOptions;

    /// <inheritdoc />
    public StringComparison Comparison => StringComparison.Ordinal;

    /// <inheritdoc />
    public IReadOnlyList<Symbol> Keywords { get; } =
    [
        new("import", null, SymbolKind.Keyword),
        new("from", null, SymbolKind.Keyword),
        new("as", null, SymbolKind.Keyword),
        new("def", null, SymbolKind.Keyword),
        new("return", null, SymbolKind.Keyword),
        new("if", null, SymbolKind.Keyword),
        new("else", null, SymbolKind.Keyword),
        new("elif", null, SymbolKind.Keyword),
        new("for", null, SymbolKind.Keyword),
        new("while", null, SymbolKind.Keyword),
        new("in", null, SymbolKind.Keyword),
        new("True", null, SymbolKind.Keyword),
        new("False", null, SymbolKind.Keyword),
        new("None", null, SymbolKind.Keyword),
        new("and", null, SymbolKind.Keyword),
        new("or", null, SymbolKind.Keyword),
        new("not", null, SymbolKind.Keyword),
        new("vs", null, SymbolKind.Keyword),
        new("core", null, SymbolKind.Keyword)
    ];

    private IncludeCache Includes { get; } = new();

    void IRefreshableLanguage.Invalidate() => Includes.Clear();

    void IRefreshableLanguage.ReleaseDocument(string? documentPath) => Includes.Release(documentPath);

    /// <inheritdoc />
    public DocumentBindings Bind(string text, IReadOnlyList<Symbol> catalog, CancellationToken token,
        string? documentPath = null)
    {
        return VapourSynthBinder.Bind(PreparedDocument.Create(text, Lexer, token, this), catalog, Lexer, token,
            documentPath, _read, Includes);
    }

    DocumentBindings IPreparedLanguage.Bind(PreparedDocument prepared, IReadOnlyList<Symbol> catalog,
        CancellationToken token, string? documentPath) =>
        VapourSynthBinder.Bind(prepared, catalog, Lexer, token, documentPath, _read, Includes);

    /// <inheritdoc />
    public string? ParameterName(string parameter) => ParameterNames.OfPython(parameter);

    /// <inheritdoc />
    public IReadOnlyList<BrowseGroup> Browse(IReadOnlyList<Symbol> catalog, DocumentBindings bindings, string text,
        CancellationToken token = default, string? documentPath = null,
        IReadOnlyList<string>? extraPackages = null)
    {
        var groups = new List<BrowseGroup>();
        var index = VapourSynthCatalogIndex.Build(catalog);
        foreach (var ns in index.Namespaces)
        {
            token.ThrowIfCancellationRequested();
            var functions = Calls(index.Functions(ns.Name, false), ns.Name);
            if (functions.Count > 0)
            {
                groups.Add(new(ns.Name, functions));
            }
        }

        var imported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in bindings.Names)
        {
            token.ThrowIfCancellationRequested();
            if (pair.Key is "vs" or "vapoursynth")
            {
                continue;
            }

            var script = VapourSynthTypes.ScriptOf(pair.Value);
            if (script == null || !imported.Add(script))
            {
                continue;
            }

            var functions = ScriptCalls(pair.Key, script, bindings, token);
            if (functions.Count > 0)
            {
                groups.Add(new(pair.Key, functions));
            }
        }

        AddInstalled(groups, bindings, imported, token, documentPath, extraPackages);
        var local = Calls(bindings.BufferSymbols, null);
        if (local.Count > 0)
        {
            groups.Add(new("This file", local));
        }

        groups.Sort(CompareGroups);
        return groups;
    }

    /// <inheritdoc />
    public TypeRef TypeOf(IReadOnlyList<PathSegment> segments, DocumentBindings bindings, IReadOnlyList<Symbol> catalog) =>
        VapourSynthTypeWalker.TypeOf(segments, bindings, VapourSynthCatalogIndex.Build(catalog));

    /// <inheritdoc />
    public IReadOnlyList<Symbol> Members(TypeRef type, IReadOnlyList<Symbol> catalog, DocumentBindings bindings) =>
        VapourSynthMembers.Of(type, Keywords, bindings, VapourSynthCatalogIndex.Build(catalog));

    /// <inheritdoc />
    public CallResolution? ResolveCall(IReadOnlyList<PathSegment> callee, DocumentBindings bindings, IReadOnlyList<Symbol> catalog)
    {
        if (callee.Count == 0) { return null; }

        var index = VapourSynthCatalogIndex.Build(catalog);
        var name = callee[^1].Name;
        IReadOnlyList<PathSegment> prefix = [];
        if (callee.Count > 1)
        {
            var copy = new PathSegment[callee.Count - 1];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = callee[i];
            }

            prefix = copy;
        }

        var receiver = VapourSynthTypeWalker.TypeOf(prefix, bindings, index);
        if (callee.Count > 1)
        {
            var member = VapourSynthMembers.Find(receiver, name, bindings, index);
            if (member?.Parameters == null)
            {
                return null;
            }

            return new()
            {
                Overloads = [VapourSynthTypes.ForDisplay(member)],
                ImplicitReceiver = VapourSynthTypes.IsBound(receiver)
            };
        }

        if (bindings.Names.TryGetValue(name, out var aliased))
        {
            var symbol = VapourSynthTypes.FunctionSymbol(aliased);
            if (symbol != null)
            {
                return new()
                {
                    Overloads = [VapourSynthTypes.ForDisplay(symbol)],
                    ImplicitReceiver = VapourSynthTypes.IsBoundFunction(aliased)
                };
            }
        }

        if (callee.Count == 1 && !bindings.Names.ContainsKey(name))
        {
            List<Symbol>? local = null;
            foreach (var symbol in bindings.BufferSymbols)
            {
                if (symbol.Name.Equals(name, StringComparison.Ordinal) && symbol.Parameters != null)
                {
                    local ??= [];
                    local.Add(VapourSynthTypes.ForDisplay(symbol));
                }
            }

            if (local != null)
            {
                return new() { Overloads = local };
            }
        }

        return null;
    }

    /// <inheritdoc />
    public HoverInfo? Hover(string code, CaretPath path, DocumentBindings bindings, IReadOnlyList<Symbol> catalog) =>
        HoverCore(code, path, bindings, catalog, null);

    HoverInfo? IContextHover.Hover(string code, CaretPath path, DocumentBindings bindings,
        IReadOnlyList<Symbol> catalog, HoverContext? context) =>
        HoverCore(code, path, bindings, catalog, context);

    private HoverInfo? HoverCore(string code, CaretPath path, DocumentBindings bindings, IReadOnlyList<Symbol> catalog,
        HoverContext? context)
    {
        if (path.Start >= path.End || path.End > code.Length)
        {
            return null;
        }

        var name = code[path.Start..path.End];
        if (name.Length == 0)
        {
            return null;
        }

        if (NamedArgumentHover.TryGet(code, path, name, this, Comparison, out var parameter, context))
        {
            return parameter == null ? null : TypeHover(name, path, ParameterType(parameter));
        }

        if (bindings.InFunctionHeader(path.Start) && path.Segments.Count == 0 &&
            IsParameterName(name, path.Start, bindings))
        {
            return null;
        }

        var index = VapourSynthCatalogIndex.Build(catalog);
        var memberType = VapourSynthTypeWalker.TypeOf(WithName(path.Segments, name), bindings, index);
        if (path.Segments.Count > 0)
        {
            var receiver = VapourSynthTypeWalker.TypeOf(path.Segments, bindings, index);
            var symbol = VapourSynthMembers.Find(receiver, name, bindings, index);
            if (symbol != null)
            {
                var shown = symbol.Kind == SymbolKind.Function
                    ? VapourSynthTypes.ForDisplay(symbol)
                    : symbol;
                return TypeHover(name, path, shown.Tip);
            }

            return TypeHover(name, path, VapourSynthTypes.Display(memberType));
        }

        if (bindings.InFunctionHeader(path.Start) && IsFunctionName(name, path.Start, bindings))
        {
            foreach (var symbol in bindings.BufferSymbols)
            {
                if (symbol.Name.Equals(name, StringComparison.Ordinal) && symbol.Parameters != null)
                {
                    return TypeHover(name, path, VapourSynthTypes.ForDisplay(symbol).Tip);
                }
            }
        }

        if (!bindings.Names.ContainsKey(name))
        {
            foreach (var symbol in bindings.BufferSymbols)
            {
                if (symbol.Name.Equals(name, StringComparison.Ordinal) && symbol.Parameters != null)
                {
                    return TypeHover(name, path, VapourSynthTypes.ForDisplay(symbol).Tip);
                }
            }
        }

        if (bindings.Names.TryGetValue(name, out var typed))
        {
            var aliased = VapourSynthTypes.FunctionSymbol(typed);
            if (aliased != null)
            {
                var display = VapourSynthTypes.ForDisplay(aliased);
                var shown = display.Name.Equals(name, StringComparison.Ordinal)
                    ? display
                    : display with { Name = name };
                return TypeHover(name, path, shown.Tip);
            }

            return TypeHover(name, path, new Symbol(name, null, SymbolKind.Local,
                ReturnType: VapourSynthTypes.Display(typed)).Tip);
        }

        return null;
    }

    private static HoverInfo? TypeHover(string name, CaretPath path, string? type)
    {
        if (!type.HasValue() || type.Equals(name, StringComparison.Ordinal))
        {
            return null;
        }

        return new(type, path.Start, path.End - path.Start);
    }

    private static string? ParameterType(string parameter)
    {
        var key = ParameterNames.PythonType(parameter);
        if (!key.HasValue())
        {
            return null;
        }

        var display = VapourSynthTypes.DisplayType(ParameterNames.OfPython(parameter), key);
        if (display.HasValue() && display != key)
        {
            return display;
        }

        var annotated = VapourSynthTypes.FromAnnotation(key);
        return annotated.IsUnknown ? display ?? key : VapourSynthTypes.Display(annotated);
    }

    private static bool IsParameterName(string name, int offset, DocumentBindings bindings)
    {
        foreach (var scope in bindings.Scopes)
        {
            if (offset >= scope.Start && offset < scope.HeaderEnd && scope.Names.ContainsKey(name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFunctionName(string name, int offset, DocumentBindings bindings)
    {
        foreach (var scope in bindings.Scopes)
        {
            if (offset >= scope.Start && offset < scope.HeaderEnd &&
                scope.Name.Equals(name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void AddInstalled(List<BrowseGroup> groups, DocumentBindings bindings, HashSet<string> imported,
        CancellationToken token, string? documentPath, IReadOnlyList<string>? extraPackages)
    {
        if (_read == null)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in bindings.Names)
        {
            if (VapourSynthTypes.ScriptOf(pair.Value) != null)
            {
                seen.Add(pair.Key);
            }
        }

        var session = new IncludeSession(Includes);
        foreach (var name in Packages(extraPackages))
        {
            token.ThrowIfCancellationRequested();
            if (name is "vs" or "vapoursynth" || !seen.Add(name))
            {
                continue;
            }

            var loaded = VapourSynthBinder.LoadPackage(name, documentPath, _read, Lexer, token, session);
            if (loaded == null || imported.Contains(loaded.Value.Id) || loaded.Value.Members.Count == 0)
            {
                continue;
            }

            imported.Add(loaded.Value.Id);
            var functions = Calls(loaded.Value.Members, import: name);
            if (functions.Count > 0)
            {
                groups.Add(new(name, functions));
            }
        }

        session.Finish(IncludeCache.BrowseWorkingSet);
    }

    private static IEnumerable<string> Packages(IReadOnlyList<string>? extraPackages)
    {
        foreach (var name in ScriptPackages.Known)
        {
            yield return name;
        }

        if (extraPackages == null)
        {
            yield break;
        }

        foreach (var name in extraPackages)
        {
            if (name.HasText())
            {
                yield return name;
            }
        }
    }

    private static IReadOnlyList<BrowseFunction> ScriptCalls(string qualifier, string script,
        DocumentBindings bindings, CancellationToken token)
    {
        var items = new List<BrowseFunction>();
        var walking = new HashSet<string>(StringComparer.Ordinal);
        CollectScript(qualifier, script, bindings, items, walking, token);
        items.Sort(CompareFunctions);
        return items;
    }

    private static void CollectScript(string qualifier, string script, DocumentBindings bindings,
        List<BrowseFunction> items, HashSet<string> walking, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!walking.Add(script) || !bindings.ScriptModules.TryGetValue(script, out var members))
        {
            return;
        }

        foreach (var symbol in members)
        {
            if (symbol.Kind == SymbolKind.Function)
            {
                var shown = VapourSynthTypes.ForDisplay(symbol);
                var name = shown.DisplayName;
                items.Add(new(name, shown.Signature, qualifier + "." + name + "()"));
            }

            var nested = symbol.ReturnType != null ? VapourSynthTypes.ScriptOf(new(symbol.ReturnType)) : null;
            if (nested != null)
            {
                CollectScript(qualifier + "." + symbol.Name, nested, bindings, items, walking, token);
            }
        }
    }

    private static IReadOnlyList<BrowseFunction> Calls(IReadOnlyList<Symbol> symbols, string? ns = null,
        string? import = null)
    {
        var items = new List<BrowseFunction>();
        foreach (var symbol in symbols)
        {
            if (symbol.Kind != SymbolKind.Function)
            {
                continue;
            }

            var shown = VapourSynthTypes.ForDisplay(symbol);
            var name = shown.DisplayName;
            var insert = import != null ? import + "." + name + "()"
                : ns == null ? name + "()"
                : "core." + ns + "." + name + "()";
            items.Add(new(name, shown.Signature, insert, import, shown.Offset));
        }

        items.Sort(CompareFunctions);
        return items;
    }

    private static int CompareGroups(BrowseGroup left, BrowseGroup right)
    {
        var leftLocal = left.Name == "This file";
        var rightLocal = right.Name == "This file";
        if (leftLocal != rightLocal)
        {
            return leftLocal ? -1 : 1;
        }

        return string.Compare(left.Name, right.Name, StringComparison.Ordinal);
    }

    private static int CompareFunctions(BrowseFunction left, BrowseFunction right) =>
        string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<PathSegment> WithName(IReadOnlyList<PathSegment> prefix, string name)
    {
        var segments = new PathSegment[prefix.Count + 1];
        for (var i = 0; i < prefix.Count; i++)
        {
            segments[i] = prefix[i];
        }

        segments[^1] = new() { Name = name, Kind = PathSegmentKind.Name };
        return segments;
    }
}
