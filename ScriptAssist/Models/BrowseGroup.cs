namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// A named list of insertable functions for the Functions Explorer.
/// </summary>
public sealed record BrowseGroup(string Name, IReadOnlyList<BrowseFunction> Functions);
