namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Overlay of enclosing-scope names and symbols for the innermost function being bound.
/// </summary>
internal sealed class VisibleCache
{
    public OverlayNames? Names;
    public List<NameWrite>? History;
    public int Offset = -1;
    private BindingScope? _scope;
    private HashSet<string>? _moduleNames;
    private Dictionary<string, Symbol>? _local;
    private HashSet<string>? _hidden;
    private List<Symbol>? _symbols;

    public void Reset()
    {
        Names = null;
        _scope = null;
        _local = null;
        _hidden = null;
        _symbols = null;
    }

    public void Ensure(BindingScope inner, Dictionary<string, TypeRef> globals, SymbolList buffer)
    {
        if (ReferenceEquals(_scope, inner) && Names != null)
        {
            return;
        }

        Remember(buffer);
        var overlay = new OverlayNames(globals);
        _local = null;
        _hidden = null;
        OverlayChain(inner, overlay);
        Names = overlay;
        _scope = inner;
        _symbols = null;
    }

    public IReadOnlyList<Symbol> Symbols(SymbolList buffer)
    {
        if ((_local == null || _local.Count == 0) && (_hidden == null || _hidden.Count == 0))
        {
            return buffer;
        }

        if (_symbols != null)
        {
            return _symbols;
        }

        var merged = new List<Symbol>(buffer.Count + (_local?.Count ?? 0));
        foreach (var symbol in buffer)
        {
            if (_hidden != null && _hidden.Contains(symbol.Name) ||
                _local != null && _local.ContainsKey(symbol.Name))
            {
                continue;
            }

            merged.Add(symbol);
        }

        if (_local != null)
        {
            merged.AddRange(_local.Values);
        }

        _symbols = merged;
        return _symbols;
    }

    public void NoteName(BindingScope? changed, string name, TypeRef type)
    {
        if (changed == null)
        {
            History?.Add(new(Offset, name, type));
        }

        if (!Tracks(changed) || Names == null)
        {
            return;
        }

        if (Shadowed(changed, name))
        {
            return;
        }

        Names.Set(name, type);
        var listChanged = _local != null && _local.Remove(name);
        if (_moduleNames != null && _moduleNames.Contains(name))
        {
            _hidden ??= new(StringComparer.Ordinal);
            listChanged |= _hidden.Add(name);
        }

        if (listChanged)
        {
            _symbols = null;
        }
    }

    public void NoteFunction(BindingScope? changed, Symbol symbol)
    {
        if (changed == null)
        {
            _moduleNames ??= new(StringComparer.Ordinal);
            _moduleNames.Add(symbol.Name);
            History?.Add(new(Offset, symbol.Name, default, Remove: true));
        }

        if (!Tracks(changed) || Names == null)
        {
            return;
        }

        if (Shadowed(changed, symbol.Name))
        {
            if (_moduleNames != null && _moduleNames.Contains(symbol.Name))
            {
                _hidden ??= new(StringComparer.Ordinal);
                if (_hidden.Add(symbol.Name))
                {
                    _symbols = null;
                }
            }

            return;
        }

        Names.RemoveName(symbol.Name);
        if (changed == null)
        {
            _hidden?.Remove(symbol.Name);
            _local?.Remove(symbol.Name);
            if (_local != null || _hidden != null)
            {
                _symbols = null;
            }

            return;
        }

        _local ??= new(StringComparer.Ordinal);
        _local[symbol.Name] = symbol;
        _hidden?.Remove(symbol.Name);
        _symbols = null;
    }

    private void Remember(SymbolList buffer)
    {
        if (_moduleNames != null)
        {
            return;
        }

        _moduleNames = new(StringComparer.Ordinal);
        foreach (var symbol in buffer)
        {
            _moduleNames.Add(symbol.Name);
        }
    }

    private void OverlayChain(BindingScope inner, OverlayNames names)
    {
        var chain = new List<BindingScope>();
        for (var scope = inner; scope != null; scope = scope.Enclosing)
        {
            chain.Add(scope);
        }

        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var scope = chain[i];
            foreach (var pair in scope.Names)
            {
                names.Set(pair.Key, pair.Value);
                if (_moduleNames == null || !_moduleNames.Contains(pair.Key))
                {
                    continue;
                }

                _hidden ??= new(StringComparer.Ordinal);
                _hidden.Add(pair.Key);
                _local?.Remove(pair.Key);
            }

            foreach (var symbol in scope.Symbols)
            {
                _local ??= new(StringComparer.Ordinal);
                _local[symbol.Name] = symbol;
                _hidden?.Remove(symbol.Name);
                if (symbol.Parameters != null)
                {
                    names.RemoveName(symbol.Name);
                }
            }
        }
    }

    private bool Shadowed(BindingScope? changed, string name) =>
        _scope != null && !ReferenceEquals(changed, _scope) && _scope.Names.ContainsKey(name);

    private bool Tracks(BindingScope? changed)
    {
        if (Names == null)
        {
            return false;
        }

        if (_scope == null)
        {
            return changed == null;
        }

        return changed == null || changed.Start <= _scope.Start && changed.End >= _scope.End;
    }

    internal sealed class OverlayNames(Dictionary<string, TypeRef> globals) : IReadOnlyDictionary<string, TypeRef>
    {
        private readonly Dictionary<string, TypeRef> _overlay = new(StringComparer.Ordinal);
        private readonly HashSet<string> _removed = new(StringComparer.Ordinal);

        public void Set(string name, TypeRef type)
        {
            _removed.Remove(name);
            _overlay[name] = type;
        }

        public void RemoveName(string name)
        {
            _overlay.Remove(name);
            if (globals.ContainsKey(name))
            {
                _removed.Add(name);
            }
        }

        public int Count
        {
            get
            {
                var n = _overlay.Count;
                foreach (var key in globals.Keys)
                {
                    if (!_overlay.ContainsKey(key) && !_removed.Contains(key))
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        public TypeRef this[string key] =>
            TryGetValue(key, out var value) ? value : throw new KeyNotFoundException(key);

        public IEnumerable<string> Keys
        {
            get
            {
                foreach (var pair in this)
                {
                    yield return pair.Key;
                }
            }
        }

        public IEnumerable<TypeRef> Values
        {
            get
            {
                foreach (var pair in this)
                {
                    yield return pair.Value;
                }
            }
        }

        public bool ContainsKey(string key) => TryGetValue(key, out _);

        public bool TryGetValue(string key, out TypeRef value)
        {
            if (_overlay.TryGetValue(key, out value))
            {
                return true;
            }

            if (_removed.Contains(key))
            {
                value = default!;
                return false;
            }

            return globals.TryGetValue(key, out value);
        }

        public IEnumerator<KeyValuePair<string, TypeRef>> GetEnumerator()
        {
            foreach (var pair in _overlay)
            {
                yield return pair;
            }

            foreach (var pair in globals)
            {
                if (!_overlay.ContainsKey(pair.Key) && !_removed.Contains(pair.Key))
                {
                    yield return pair;
                }
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
