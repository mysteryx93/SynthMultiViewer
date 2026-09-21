namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Left-list role in the Functions Explorer.
/// </summary>
public enum BrowseGroupKind
{
    /// <summary>
    /// Symbols defined in the current buffer.
    /// </summary>
    Local,

    /// <summary>
    /// A native plugin namespace.
    /// </summary>
    Plugin,

    /// <summary>
    /// An imported or installed script module.
    /// </summary>
    Module,

    /// <summary>
    /// AviSynth core internals. Uses the namespace glyph.
    /// </summary>
    Internal
}
