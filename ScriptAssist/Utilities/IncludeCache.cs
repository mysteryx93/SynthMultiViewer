namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Remembers include resolutions and parsed exports until the next refresh,
/// with LRU bounds on path lookups and a byte cap on unpinned parsed files.
/// Lookups, publication, clearing, and version checks are synchronized; callers
/// keep file reading and parsing outside the lock.
/// </summary>
internal sealed class IncludeCache
{
    private const int PathLimit = 256;
    private const int WorkingSetLimit = 8;
    private const long EntryByteLimit = 4 * 1024 * 1024;
    private const long PinByteLimit = 8 * 1024 * 1024;
    internal const int ImportDepthLimit = 128;
    internal const int ImportWorkLimit = 256;
    private readonly Lock _gate = new();
    private int _version;
    private readonly Dictionary<(string Specifier, string? From), string?> _paths = [];
    private readonly LinkedList<(string Specifier, string? From)> _pathOrder = new();
    private readonly Dictionary<(string Specifier, string? From), LinkedListNode<(string Specifier, string? From)>>
        _pathNodes = [];
    private readonly Dictionary<string, IncludeEntry> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _entryOrder = new();
    private readonly Dictionary<string, LinkedListNode<string>> _entryNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorkingSet> _workingSets = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _workingOrder = new();
    private readonly HashSet<string> _pinnedEntries = new(StringComparer.Ordinal);
    private readonly HashSet<(string Specifier, string? From)> _pinnedPaths = [];
    private readonly Dictionary<string, long> _entryBytes = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets a stamp that changes whenever the cache is cleared.
    /// </summary>
    public int Version
    {
        get
        {
            lock (_gate)
            {
                return _version;
            }
        }
    }

    /// <summary>
    /// Gets a previously resolved path for <paramref name="specifier"/> loaded from <paramref name="fromPath"/>.
    /// </summary>
    public bool TryPath(string specifier, string? fromPath, out string? path)
    {
        lock (_gate)
        {
            var key = (specifier, fromPath);
            if (!_paths.TryGetValue(key, out path))
            {
                return false;
            }

            TouchPath(key);
            return true;
        }
    }

    /// <summary>
    /// Stores the resolved path, or null when the specifier did not load.
    /// </summary>
    public void SetPath(string specifier, string? fromPath, string? path, int version)
    {
        lock (_gate)
        {
            if (version != _version)
            {
                return;
            }

            var key = (specifier, fromPath);
            _paths[key] = path;
            TouchPath(key);
            EvictPaths();
        }
    }

    /// <summary>
    /// Gets parsed exports for a resolved include path.
    /// </summary>
    public bool TryMembers(string path, out IReadOnlyList<Symbol> members)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(path, out var entry))
            {
                TouchEntry(path);
                members = entry.Members;
                return true;
            }
        }

        members = null!;
        return false;
    }

    /// <summary>
    /// Gets cached declarations and dependencies for a resolved include path.
    /// </summary>
    public bool TryEntry(string path, out IncludeEntry entry)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(path, out entry!))
            {
                return false;
            }

            TouchEntry(path);
            return true;
        }
    }

    /// <summary>
    /// Stores parsed exports for a resolved include path.
    /// </summary>
    public void SetMembers(string path, IReadOnlyList<Symbol> members, int version) =>
        SetEntry(path, new(Copy(members), []), version);

    /// <summary>
    /// Stores declarations and import dependencies for a resolved include path.
    /// </summary>
    public void SetEntry(string path, IncludeEntry entry, int version)
    {
        lock (_gate)
        {
            if (version != _version)
            {
                return;
            }

            var copy = new IncludeEntry(Copy(entry.Members), Copy(entry.Dependencies));
            var bytes = EntryBytes(copy);
            if (bytes > EntryByteLimit)
            {
                return;
            }

            _entries[path] = copy;
            _entryBytes[path] = bytes;
            TouchEntry(path);
            EvictEntries();
        }
    }

    /// <summary>
    /// Pins <paramref name="entries"/> and path keys for <paramref name="documentPath"/> so a later
    /// bind of that document does not cascade-reread after LRU eviction.
    /// Restores any working-set rows the LRU dropped during the bind.
    /// </summary>
    public void Retain(string? documentPath, IReadOnlyDictionary<string, IncludeEntry> entries,
        IReadOnlyDictionary<(string Specifier, string? From), string?> paths, int version)
    {
        lock (_gate)
        {
            if (version != _version)
            {
                return;
            }

            var key = documentPath ?? "";
            var stored = new HashSet<string>(StringComparer.Ordinal);
            var pinned = 0L;
            foreach (var pair in entries)
            {
                var copy = new IncludeEntry(Copy(pair.Value.Members), Copy(pair.Value.Dependencies));
                var bytes = EntryBytes(copy);
                if (bytes > EntryByteLimit || pinned + bytes > PinByteLimit)
                {
                    continue;
                }

                pinned += bytes;
                stored.Add(pair.Key);
                _entries[pair.Key] = copy;
                _entryBytes[pair.Key] = bytes;
            }

            _workingSets[key] = new WorkingSet(stored, [..paths.Keys]);
            Touch(key, _workingOrder);
            while (_workingSets.Count > WorkingSetLimit && _workingOrder.Last != null)
            {
                var last = _workingOrder.Last.Value;
                _workingOrder.RemoveLast();
                _workingSets.Remove(last);
            }

            foreach (var pair in paths)
            {
                _paths[pair.Key] = pair.Value;
            }

            RebuildPins();
            EvictEntries();
            EvictPaths();
        }
    }

    /// <summary>
    /// Drops the working set for <paramref name="documentPath"/> so its imports can age out.
    /// </summary>
    public void Release(string? documentPath)
    {
        lock (_gate)
        {
            var key = documentPath ?? "";
            if (!_workingSets.Remove(key))
            {
                return;
            }

            var node = _workingOrder.Find(key);
            if (node != null)
            {
                _workingOrder.Remove(node);
            }

            RebuildPins();
            EvictEntries();
            EvictPaths();
        }
    }

    /// <summary>
    /// Drops every remembered include.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _version++;
            _paths.Clear();
            _pathOrder.Clear();
            _pathNodes.Clear();
            _entries.Clear();
            _entryBytes.Clear();
            _entryOrder.Clear();
            _entryNodes.Clear();
            _workingSets.Clear();
            _workingOrder.Clear();
            _pinnedEntries.Clear();
            _pinnedPaths.Clear();
        }
    }

    private static void Touch<T>(T key, Dictionary<T, LinkedListNode<T>> nodes, LinkedList<T> order)
        where T : notnull
    {
        if (nodes.TryGetValue(key, out var node))
        {
            order.Remove(node);
            order.AddFirst(node);
            return;
        }

        nodes[key] = order.AddFirst(key);
    }

    private static void Touch(string key, LinkedList<string> order)
    {
        var node = order.Find(key);
        if (node != null)
        {
            order.Remove(node);
            order.AddFirst(node);
            return;
        }

        order.AddFirst(key);
    }

    private void TouchEntry(string path)
    {
        if (_pinnedEntries.Contains(path))
        {
            UnpinNode(path, _entryNodes, _entryOrder);
            return;
        }

        Touch(path, _entryNodes, _entryOrder);
    }

    private void TouchPath((string Specifier, string? From) key)
    {
        if (_pinnedPaths.Contains(key))
        {
            UnpinNode(key, _pathNodes, _pathOrder);
            return;
        }

        Touch(key, _pathNodes, _pathOrder);
    }

    private void RebuildPins()
    {
        _pinnedEntries.Clear();
        _pinnedPaths.Clear();
        foreach (var set in _workingSets.Values)
        {
            foreach (var path in set.Entries)
            {
                _pinnedEntries.Add(path);
            }

            foreach (var key in set.Paths)
            {
                _pinnedPaths.Add(key);
            }
        }

        foreach (var path in _entries.Keys)
        {
            if (_pinnedEntries.Contains(path))
            {
                UnpinNode(path, _entryNodes, _entryOrder);
            }
            else if (!_entryNodes.ContainsKey(path))
            {
                _entryNodes[path] = _entryOrder.AddFirst(path);
            }
        }

        foreach (var key in _paths.Keys)
        {
            if (_pinnedPaths.Contains(key))
            {
                UnpinNode(key, _pathNodes, _pathOrder);
            }
            else if (!_pathNodes.ContainsKey(key))
            {
                _pathNodes[key] = _pathOrder.AddFirst(key);
            }
        }
    }

    private static void UnpinNode<T>(T key, Dictionary<T, LinkedListNode<T>> nodes, LinkedList<T> order)
        where T : notnull
    {
        if (!nodes.TryGetValue(key, out var node))
        {
            return;
        }

        order.Remove(node);
        nodes.Remove(key);
    }

    private void EvictEntries()
    {
        while (_entryOrder.Last != null && UnpinnedBytes() > EntryByteLimit)
        {
            var path = _entryOrder.Last.Value;
            _entryOrder.RemoveLast();
            _entryNodes.Remove(path);
            _entries.Remove(path);
            _entryBytes.Remove(path);
        }
    }

    private long UnpinnedBytes()
    {
        var bytes = 0L;
        foreach (var path in _entryOrder)
        {
            if (_entryBytes.TryGetValue(path, out var size))
            {
                bytes += size;
            }
        }

        return bytes;
    }

    private static long EntryBytes(IncludeEntry entry)
    {
        var bytes = (long)entry.Members.Count * 32;
        foreach (var symbol in entry.Members)
        {
            bytes += (long)symbol.Name.Length * sizeof(char);
            if (symbol.ReturnType != null)
            {
                bytes += (long)symbol.ReturnType.Length * sizeof(char);
            }

            if (symbol.Parameters == null)
            {
                continue;
            }

            foreach (var parameter in symbol.Parameters)
            {
                bytes += (long)parameter.Length * sizeof(char);
            }
        }

        foreach (var dependency in entry.Dependencies)
        {
            bytes += (long)dependency.Length * sizeof(char);
        }

        return bytes;
    }

    private void EvictPaths()
    {
        while (_paths.Count > PathLimit && _pathOrder.Last != null)
        {
            var key = _pathOrder.Last.Value;
            _pathOrder.RemoveLast();
            _pathNodes.Remove(key);
            _paths.Remove(key);
        }
    }

    private static IReadOnlyList<Symbol> Copy(IReadOnlyList<Symbol> members) =>
        members as Symbol[] ?? [..members];

    private static IReadOnlyList<string> Copy(IReadOnlyList<string> items) =>
        items as string[] ?? [..items];

    private sealed record WorkingSet(
        HashSet<string> Entries,
        HashSet<(string Specifier, string? From)> Paths);
}

/// <summary>
/// Cached declarations of one include file, plus resolved files it imports.
/// </summary>
internal sealed record IncludeEntry(IReadOnlyList<Symbol> Members, IReadOnlyList<string> Dependencies);

/// <summary>
/// Writes include entries using the cache version captured when a bind started.
/// </summary>
internal readonly struct IncludeSession
{
    /// <summary>
    /// Captures <paramref name="cache"/> at the current version so in-flight stores
    /// after <see cref="IncludeCache.Clear"/> are ignored.
    /// </summary>
    public IncludeSession(IncludeCache? cache)
    {
        Cache = cache;
        Version = cache?.Version ?? 0;
        Complete = new(StringComparer.Ordinal);
        Entries = new(StringComparer.Ordinal);
        Paths = [];
        Work = new();
        Depth = new();
        Limit = new();
    }

    public IncludeCache? Cache { get; }
    public int Version { get; }
    private Counter Work { get; }
    private Counter Depth { get; }
    private Counter Limit { get; }

    /// <summary>
    /// Paths whose cached graphs were already verified complete during this bind.
    /// </summary>
    public HashSet<string> Complete { get; }

    /// <summary>
    /// Entries loaded during this bind, independent of shared LRU eviction.
    /// </summary>
    public Dictionary<string, IncludeEntry> Entries { get; }

    /// <summary>
    /// Path resolutions loaded during this bind, independent of shared LRU eviction.
    /// </summary>
    public Dictionary<(string Specifier, string? From), string?> Paths { get; }

    public bool TryPath(string specifier, string? fromPath, out string? path)
    {
        var key = (specifier, fromPath);
        if (Paths.TryGetValue(key, out path))
        {
            return true;
        }

        if (Cache == null || !Cache.TryPath(specifier, fromPath, out path))
        {
            path = null;
            return false;
        }

        Paths[key] = path;
        return true;
    }

    public void SetPath(string specifier, string? fromPath, string? path)
    {
        Paths[(specifier, fromPath)] = path;
        Cache?.SetPath(specifier, fromPath, path, Version);
    }

    public bool TryMembers(string path, out IReadOnlyList<Symbol> members)
    {
        if (TryEntry(path, out var entry))
        {
            members = entry.Members;
            return true;
        }

        members = null!;
        return false;
    }

    public bool TryEntry(string path, out IncludeEntry entry)
    {
        if (Entries.TryGetValue(path, out entry!))
        {
            return true;
        }

        if (Cache == null || !Cache.TryEntry(path, out entry))
        {
            entry = null!;
            return false;
        }

        Entries[path] = entry;
        return true;
    }

    public void SetMembers(string path, IReadOnlyList<Symbol> members) =>
        SetEntry(path, new(members, []));

    public void SetEntry(string path, IncludeEntry entry)
    {
        var copy = new IncludeEntry(Copy(entry.Members), Copy(entry.Dependencies));
        Entries[path] = copy;
        Cache?.SetEntry(path, copy, Version);
    }

    /// <summary>
    /// Pins this bind's import graph on the shared cache as the document working set.
    /// </summary>
    public void Finish(string? documentPath) =>
        Cache?.Retain(documentPath, Entries, Paths, Version);

    /// <summary>
    /// Gets whether this bind hit the import work limit and left graphs incomplete.
    /// </summary>
    public bool Limited => Limit.Value != 0;

    /// <summary>
    /// Returns whether another file can still be reserved under the import work limit.
    /// A failed check records the graph as incomplete so it is not published.
    /// </summary>
    public bool CanImport()
    {
        if (Work.Value < IncludeCache.ImportWorkLimit && Limit.Value == 0)
        {
            return true;
        }

        Limit.Value = 1;
        return false;
    }

    public bool TryImport()
    {
        if (!CanImport())
        {
            return false;
        }

        Work.Value++;
        return true;
    }

    public bool TryDepth()
    {
        if (Depth.Value >= IncludeCache.ImportDepthLimit)
        {
            return false;
        }

        Depth.Value++;
        return true;
    }

    public void LeaveDepth()
    {
        if (Depth.Value > 0)
        {
            Depth.Value--;
        }
    }

    private static IReadOnlyList<Symbol> Copy(IReadOnlyList<Symbol> members) =>
        members as Symbol[] ?? [..members];

    private static IReadOnlyList<string> Copy(IReadOnlyList<string> items) =>
        items as string[] ?? [..items];

    private sealed class Counter
    {
        public int Value;
    }
}
