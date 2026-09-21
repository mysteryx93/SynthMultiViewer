namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Describes the source of a completion item.
/// </summary>
public enum SymbolKind
{
    /// <summary>
    /// A static language keyword.
    /// </summary>
    Keyword,

    /// <summary>
    /// An assigned buffer variable.
    /// </summary>
    Local,

    /// <summary>
    /// A plugin namespace or module member.
    /// </summary>
    Namespace,

    /// <summary>
    /// A type-name with named members, not called.
    /// </summary>
    Enum,

    /// <summary>
    /// A native filter, buffer function, or typed member.
    /// </summary>
    Function,

    /// <summary>
    /// A typed attribute that is not called.
    /// </summary>
    Property
}
