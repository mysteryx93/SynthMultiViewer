namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Names and extra symbols collected from one buffer snapshot.
/// </summary>
public sealed class DocumentBindings
{
    private const int ViewLimit = 4;
    private readonly Lock _viewsGate = new();
    private List<(int Caret, DocumentBindings View)>? _views;
    private HashSet<string>? _functionNames;
    private long? _baseBytes;

    /// <summary>
    /// Gets assigned names and their inferred types.
    /// </summary>
    public IReadOnlyDictionary<string, TypeRef> Names { get; init; } =
        new Dictionary<string, TypeRef>(StringComparer.Ordinal);

    /// <summary>
    /// Gets functions declared in the buffer.
    /// </summary>
    public IReadOnlyList<Symbol> BufferSymbols { get; init; } = [];

    /// <summary>
    /// Gets parsed functions of imported script modules, keyed by module id.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Symbol>> ScriptModules { get; init; } =
        new Dictionary<string, IReadOnlyList<Symbol>>(StringComparer.Ordinal);

    /// <summary>
    /// Gets function scopes; empty when the language has no nested locals.
    /// </summary>
    public IReadOnlyList<BindingScope> Scopes { get; init; } = [];

    /// <summary>
    /// Gets module-level name writes in bind order. Empty when the language has no caret-relative names.
    /// </summary>
    internal IReadOnlyList<NameWrite> Writes { get; init; } = [];

    /// <summary>
    /// Returns this snapshot with names as of <paramref name="caret"/>, then scopes containing it.
    /// </summary>
    public DocumentBindings At(int caret)
    {
        var replay = ReplayNeeded(caret);
        if (!replay && (Scopes.Count == 0 || !Contains(caret)))
        {
            return this;
        }

        var inner = Innermost(caret);
        if (!replay && inner == null)
        {
            return this;
        }

        lock (_viewsGate)
        {
            if (_views != null)
            {
                for (var i = 0; i < _views.Count; i++)
                {
                    if (_views[i].Caret != caret)
                    {
                        continue;
                    }

                    var cached = _views[i].View;
                    if (i > 0)
                    {
                        _views.RemoveAt(i);
                        _views.Insert(0, (caret, cached));
                    }

                    return cached;
                }
            }

            var view = OverlayView(caret);
            _views ??= [];
            _views.Insert(0, (caret, view));
            if (_views.Count > ViewLimit)
            {
                _views.RemoveAt(_views.Count - 1);
            }

            return view;
        }
    }

    private BindingScope? Innermost(int caret)
    {
        BindingScope? inner = null;
        foreach (var scope in Scopes)
        {
            if (caret < scope.Start || caret > scope.End)
            {
                continue;
            }

            if (inner == null || scope.Start >= inner.Start)
            {
                inner = scope;
            }
        }

        return inner;
    }

    private bool ReplayNeeded(int caret)
    {
        foreach (var write in Writes)
        {
            if (write.Offset > caret)
            {
                return true;
            }
        }

        return false;
    }

    private Dictionary<string, TypeRef> Replay(int caret, IEqualityComparer<string> comparer)
    {
        if (Writes.Count == 0)
        {
            return new Dictionary<string, TypeRef>(Names, comparer);
        }

        var merged = new Dictionary<string, TypeRef>(comparer);
        foreach (var write in Writes)
        {
            if (write.Offset > caret || write.Name.Length == 0)
            {
                continue;
            }

            if (write.Remove)
            {
                merged.Remove(write.Name);
            }
            else
            {
                merged[write.Name] = write.Type;
            }
        }

        return merged;
    }

    private DocumentBindings OverlayView(int caret)
    {
        var comparer = Names is Dictionary<string, TypeRef> dictionary
            ? dictionary.Comparer
            : StringComparer.Ordinal;
        var merged = Replay(caret, comparer);
        if (!ShadowsFunctions(caret, comparer))
        {
            Overlay(caret, merged);
            return new()
            {
                Names = merged,
                BufferSymbols = BufferSymbols,
                ScriptModules = ScriptModules,
                Scopes = Scopes
            };
        }

        var functions = new Dictionary<string, List<Symbol>>(comparer);
        foreach (var symbol in BufferSymbols)
        {
            if (!functions.TryGetValue(symbol.Name, out var group))
            {
                group = [];
                functions[symbol.Name] = group;
            }

            group.Add(symbol);
        }

        Overlay(caret, merged, functions);
        return new()
        {
            Names = merged,
            BufferSymbols = SameSymbols(functions, BufferSymbols) ? BufferSymbols : Flatten(functions),
            ScriptModules = ScriptModules,
            Scopes = Scopes
        };
    }

    private bool ShadowsFunctions(int caret, IEqualityComparer<string> comparer)
    {
        HashSet<string>? functionNames = null;
        foreach (var scope in Scopes)
        {
            if (caret < scope.Start || caret > scope.End)
            {
                continue;
            }

            if (scope.Symbols.Count > 0)
            {
                return true;
            }

            functionNames ??= FunctionNames(comparer);
            foreach (var name in scope.Names.Keys)
            {
                if (functionNames.Contains(name))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private HashSet<string> FunctionNames(IEqualityComparer<string> comparer)
    {
        if (_functionNames != null)
        {
            return _functionNames;
        }

        _functionNames = new(comparer);
        foreach (var symbol in BufferSymbols)
        {
            _functionNames.Add(symbol.Name);
        }

        return _functionNames;
    }

    /// <summary>
    /// Approximate retained size of names, function lists, and cached scope views.
    /// </summary>
    internal long RetainedBytes()
    {
        lock (_viewsGate)
        {
            var bytes = BaseBytes();
            if (_views == null)
            {
                return bytes;
            }

            foreach (var (_, view) in _views)
            {
                bytes += NamesBytes(view.Names);
                if (!ReferenceEquals(view.BufferSymbols, BufferSymbols))
                {
                    bytes += (long)view.BufferSymbols.Count * 32;
                }
            }

            return bytes;
        }
    }

    private long BaseBytes()
    {
        if (_baseBytes is { } cached)
        {
            return cached;
        }

        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var bytes = NamesBytes(Names);
        foreach (var write in Writes)
        {
            bytes += sizeof(int) + sizeof(bool);
            bytes += (long)(write.Name.Length + write.Type.Id.Length) * sizeof(char);
        }

        bytes += SymbolsBytes(BufferSymbols, seen);
        foreach (var pair in ScriptModules)
        {
            bytes += (long)pair.Key.Length * sizeof(char);
            bytes += SymbolsBytes(pair.Value, seen);
        }

        foreach (var scope in Scopes)
        {
            bytes += (long)scope.Name.Length * sizeof(char);
            bytes += NamesBytes(scope.Names);
            bytes += SymbolsBytes(scope.Symbols, seen);
            foreach (var parameter in scope.Parameters)
            {
                bytes += (long)parameter.Length * sizeof(char);
            }
        }

        _baseBytes = bytes;
        return bytes;
    }

    private static bool SameSymbols(Dictionary<string, List<Symbol>> functions, IReadOnlyList<Symbol> original)
    {
        var count = 0;
        foreach (var group in functions.Values)
        {
            count += group.Count;
        }

        if (count != original.Count)
        {
            return false;
        }

        foreach (var symbol in original)
        {
            if (!functions.TryGetValue(symbol.Name, out var group) || !Contains(group, symbol))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Contains(List<Symbol> group, Symbol symbol)
    {
        foreach (var item in group)
        {
            if (ReferenceEquals(item, symbol))
            {
                return true;
            }
        }

        return false;
    }

    private static List<Symbol> Flatten(Dictionary<string, List<Symbol>> functions)
    {
        var items = new List<Symbol>();
        foreach (var group in functions.Values)
        {
            items.AddRange(group);
        }

        return items;
    }

    private static long NamesBytes(IReadOnlyDictionary<string, TypeRef> names)
    {
        var bytes = 0L;
        foreach (var pair in names)
        {
            bytes += (long)(pair.Key.Length + pair.Value.Id.Length) * sizeof(char);
        }

        return bytes;
    }

    private static long SymbolsBytes(IReadOnlyList<Symbol> symbols, HashSet<object> seen)
    {
        if (!seen.Add(symbols))
        {
            return 0;
        }

        var bytes = (long)symbols.Count * 32;
        foreach (var symbol in symbols)
        {
            if (!seen.Add(symbol))
            {
                continue;
            }

            bytes += (long)symbol.Name.Length * sizeof(char);
            if (symbol.ReturnType != null)
            {
                bytes += (long)symbol.ReturnType.Length * sizeof(char);
            }

            if (symbol.Parameters == null || !seen.Add(symbol.Parameters))
            {
                continue;
            }

            foreach (var parameter in symbol.Parameters)
            {
                bytes += (long)parameter.Length * sizeof(char);
            }
        }

        return bytes;
    }

    /// <summary>
    /// Gets whether any function scope contains <paramref name="caret"/>.
    /// </summary>
    internal bool Contains(int caret)
    {
        foreach (var scope in Scopes)
        {
            if (caret >= scope.Start && caret <= scope.End)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Overlays containing scope names onto <paramref name="names"/> in source order.
    /// </summary>
    internal void Overlay(int caret, Dictionary<string, TypeRef> names,
        Dictionary<string, List<Symbol>>? functions = null) =>
        Overlay(Scopes, caret, names, functions);

    /// <summary>
    /// Overlays containing scope names onto <paramref name="names"/> in source order.
    /// A function symbol with parameters removes a same-named value binding.
    /// </summary>
    internal static void Overlay(IReadOnlyList<BindingScope> scopes, int caret, Dictionary<string, TypeRef> names,
        Dictionary<string, List<Symbol>>? functions = null)
    {
        foreach (var scope in scopes)
        {
            if (caret < scope.Start || caret > scope.End)
            {
                continue;
            }

            foreach (var pair in scope.Names)
            {
                names[pair.Key] = pair.Value;
                functions?.Remove(pair.Key);
            }

            if (functions == null)
            {
                continue;
            }

            var added = new HashSet<string>(functions.Comparer);
            foreach (var symbol in scope.Symbols)
            {
                if (added.Add(symbol.Name))
                {
                    functions[symbol.Name] = [symbol];
                }
                else
                {
                    functions[symbol.Name].Add(symbol);
                }

                if (symbol.Parameters != null)
                {
                    names.Remove(symbol.Name);
                }
            }
        }
    }

    /// <summary>
    /// Gets whether <paramref name="caret"/> is inside a function parameter list.
    /// </summary>
    public bool InFunctionHeader(int caret)
    {
        foreach (var scope in Scopes)
        {
            if (caret < scope.Start || caret > scope.End)
            {
                continue;
            }

            if (caret >= scope.Start && caret < scope.HeaderEnd)
            {
                return true;
            }

            if (caret == scope.HeaderEnd && scope.HeaderEnd == scope.End)
            {
                return true;
            }
        }

        return false;
    }
}
