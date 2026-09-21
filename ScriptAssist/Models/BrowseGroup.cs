namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// A named list of insertable functions for the Functions Explorer.
/// </summary>
/// <param name="Name">Plugin or package name shown in the left list.</param>
/// <param name="Functions">Insertable members of this group.</param>
/// <param name="Tip">Plugin description for the left-list hint, or null.</param>
public sealed record BrowseGroup(string Name, IReadOnlyList<BrowseFunction> Functions, string? Tip = null);
