namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Provides completions, insight, and hover without evaluating editor text.
/// </summary>
public interface ILanguageService
{
    /// <summary>
    /// Analyzes a snapshot; callers must discard replies after editor state changes.
    /// </summary>
    Task<Reply> GetAsync(string text, int caret, CancellationToken cancellationToken, string? documentPath = null);

    /// <summary>
    /// Drops cached document analysis so the next request rebinds includes and assignments.
    /// </summary>
    void Invalidate();

    /// <summary>
    /// Builds explorer groups from the catalog and the same snapshot <see cref="GetAsync"/> uses.
    /// Does not load unimported packages into completion.
    /// </summary>
    Task<IReadOnlyList<BrowseGroup>> BrowseAsync(string text, CancellationToken cancellationToken,
        string? documentPath = null, IReadOnlyList<string>? extraPackages = null);
}
