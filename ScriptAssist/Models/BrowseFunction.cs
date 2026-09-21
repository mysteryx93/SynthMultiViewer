namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// One insertable function in a <see cref="BrowseGroup"/>.
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="Signature">Hint text.</param>
/// <param name="InsertText">Canonical call written at the caret.</param>
/// <param name="Import">Unimported Python module to add as <c>import</c> before the call, or null.</param>
/// <param name="Offset">Header offset in the current buffer for This-file symbols, or null.</param>
/// <param name="Kind">Glyph kind. Functions stay <see cref="SymbolKind.Function"/>.</param>
public sealed record BrowseFunction(string Name, string Signature, string InsertText, string? Import = null,
    int? Offset = null, SymbolKind Kind = SymbolKind.Function);
