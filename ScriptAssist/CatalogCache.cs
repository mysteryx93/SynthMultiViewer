namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// One task per configuration, including failures. A forced refresh of the same key keeps the last
/// successful catalog when enumeration throws. Cancellation never restarts native enumeration.
/// </summary>
public sealed class CatalogCache(ISymbolSource source) : ISymbolCatalog
{
    private readonly Lock _gate = new();
    private Task<IReadOnlyList<Symbol>>? _task;
    private string? _key;
    private string? _lastKey;
    private IReadOnlyList<Symbol>? _last;

    /// <summary>
    /// Gets the configuration key last passed to <see cref="SetKey"/> or <see cref="Refresh"/>.
    /// </summary>
    internal string? Key
    {
        get
        {
            lock (_gate)
            {
                return _key;
            }
        }
    }

    /// <inheritdoc />
    public void Refresh(string key, bool force = false)
    {
        lock (_gate)
        {
            if (!force && _key == key && _task != null)
            {
                return;
            }

            _key = key;
            var previous = _lastKey == key ? _last : null;
            _task = Task.Run(() =>
            {
                try
                {
                    var result = source.Enumerate().ToArray();
                    lock (_gate)
                    {
                        if (_key == key)
                        {
                            _last = result;
                            _lastKey = key;
                        }
                    }

                    return (IReadOnlyList<Symbol>)result;
                }
                catch (Exception)
                {
                    return previous ?? [];
                }
            });
        }
    }

    /// <inheritdoc />
    public void SetKey(string key)
    {
        lock (_gate)
        {
            if (_key == key)
            {
                return;
            }

            _key = key;
            _task = null;
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Symbol>> GetAsync(CancellationToken token)
    {
        lock (_gate)
        {
            if (_task == null)
            {
                Refresh(_key ?? "");
            }

            return _task!.WaitAsync(token);
        }
    }
}
