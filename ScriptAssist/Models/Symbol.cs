namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// A catalog or buffer symbol with optional parameters and return type.
/// </summary>
public sealed record Symbol(
    string Name,
    string[]? Parameters,
    SymbolKind Kind = SymbolKind.Function,
    bool ImplicitLast = false,
    string? ReturnType = null,
    string? Group = null,
    string? Title = null,
    int? Offset = null)
{
    /// <summary>
    /// Gets the last dotted segment of <see cref="Name"/>. Catalog identity stays on
    /// <see cref="Name"/>; hover, insight, and completion insert this.
    /// </summary>
    public string DisplayName
    {
        get
        {
            var last = Name.LastIndexOf('.');
            return last < 0 ? Name : Name[(last + 1)..];
        }
    }

    /// <summary>
    /// Gets the display signature.
    /// </summary>
    public string Signature
    {
        get
        {
            if (Kind is SymbolKind.Property or SymbolKind.Local)
            {
                return ReturnType.HasValue() ? Name + ": " + ReturnType : Name;
            }

            if (Kind == SymbolKind.Namespace)
            {
                if (ReturnType.HasValue() &&
                    !ReturnType.StartsWith("script:", StringComparison.Ordinal))
                {
                    return Name + ": " + ReturnType;
                }

                if (Parameters is { Length: > 0 } && !ReturnType.HasValue())
                {
                    return Name + ": " + string.Join(" | ", Parameters);
                }

                return Name;
            }

            if (Kind != SymbolKind.Function)
            {
                return Name;
            }

            var inside = Parameters == null ? "parameters unknown" : string.Join(", ", Parameters);
            var suffix = ImplicitLast ? " [implicit last]" : "";
            var call = DisplayName + "(" + inside + ")" + suffix;
            if (!ReturnType.HasValue())
            {
                return call;
            }

            var ret = ReturnType.TrimEnd(';').Trim();
            return ret.Length == 0 ? call : call + " -> " + ret;
        }
    }

    /// <summary>
    /// Hover and completion-hint text. The list and caret already show the name, so
    /// locals, properties, and namespaces are the type only; functions are the signature.
    /// </summary>
    internal string? Tip => TipOf(Kind, DisplayName, Signature);

    /// <summary>
    /// Same rules as <see cref="Tip"/> for a completion item or other kind/signature pair.
    /// </summary>
    internal static string? TipOf(SymbolKind kind, string name, string signature,
        StringComparison comparison = StringComparison.Ordinal)
    {
        if (kind is SymbolKind.Property or SymbolKind.Local or SymbolKind.Namespace)
        {
            var colon = signature.IndexOf(':');
            if (colon < 0 || colon + 1 >= signature.Length)
            {
                return null;
            }

            var type = signature[(colon + 1)..].Trim();
            if (type.Length == 0 || type.Equals(name, comparison))
            {
                return null;
            }

            return type;
        }

        return kind == SymbolKind.Function && signature.HasValue() ? signature : null;
    }
}
