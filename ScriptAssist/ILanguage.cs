namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// A catalog-driven language profile.
/// </summary>
public interface ILanguage
{
    /// <summary>
    /// Gets comment and string rules.
    /// </summary>
    LexerOptions Lexer { get; }

    /// <summary>
    /// Gets identifier comparison for this language.
    /// </summary>
    StringComparison Comparison { get; }

    /// <summary>
    /// Gets static keywords offered at the statement root.
    /// </summary>
    IReadOnlyList<Symbol> Keywords { get; }

    /// <summary>
    /// Collects assignments, aliases, and buffer-declared functions from original editor text.
    /// </summary>
    DocumentBindings Bind(string text, IReadOnlyList<Symbol> catalog, CancellationToken token,
        string? documentPath = null);

    /// <summary>
    /// Types a dotted, called, or indexed expression.
    /// </summary>
    TypeRef TypeOf(IReadOnlyList<PathSegment> segments, DocumentBindings bindings, IReadOnlyList<Symbol> catalog);

    /// <summary>
    /// Lists members of a receiver, including catalog functions when the receiver is a namespace or bound plugin.
    /// </summary>
    IReadOnlyList<Symbol> Members(TypeRef type, IReadOnlyList<Symbol> catalog, DocumentBindings bindings);

    /// <summary>
    /// Resolves a callee expression to catalog overloads.
    /// </summary>
    CallResolution? ResolveCall(IReadOnlyList<PathSegment> callee, DocumentBindings bindings, IReadOnlyList<Symbol> catalog);

    /// <summary>
    /// Describes the identifier at the caret, or null when nothing useful is known.
    /// </summary>
    HoverInfo? Hover(string code, CaretPath path, DocumentBindings bindings, IReadOnlyList<Symbol> catalog);

    /// <summary>
    /// Ranks a member for completion on <paramref name="receiver"/>. Higher values sort first.
    /// </summary>
    double CompletionPriority(Symbol symbol, TypeRef receiver) => 0;

    /// <summary>
    /// Returns the argument name for a catalog or header parameter string.
    /// </summary>
    string? ParameterName(string parameter) => null;

    /// <summary>
    /// Projects catalog symbols and the current snapshot into explorer groups.
    /// Extra installed packages may be loaded through includes; they are not bound into
    /// <paramref name="bindings"/>.
    /// </summary>
    IReadOnlyList<BrowseGroup> Browse(IReadOnlyList<Symbol> catalog, DocumentBindings bindings, string text,
        CancellationToken token = default, string? documentPath = null,
        IReadOnlyList<string>? extraPackages = null) => [];
}
