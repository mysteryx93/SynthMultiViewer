namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Creates language services and refreshes injected catalogs.
/// </summary>
public interface IScriptLanguageFactory
{
    /// <summary>
    /// Gets or sets whether <see cref="Create"/> may return a service.
    /// Catalog <c>Refresh</c> still enumerates while this is false.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Returns the shared service for <paramref name="language"/>, or null when the id is unknown
    /// or <see cref="IsEnabled"/> is false.
    /// </summary>
    ILanguageService? Create(string language);

    /// <summary>
    /// Updates the catalog key for <paramref name="language"/>. Enumeration runs only when
    /// <see cref="IsEnabled"/> is true.
    /// </summary>
    void Configure(string language, string catalogKey);

    /// <summary>
    /// Forces every registered catalog to enumerate again, including while
    /// <see cref="IsEnabled"/> is false.
    /// </summary>
    void Refresh();

    /// <summary>
    /// Builds explorer groups for <paramref name="language"/>. Works while
    /// <see cref="IsEnabled"/> is false; <see cref="Create"/> still returns null.
    /// </summary>
    Task<IReadOnlyList<BrowseGroup>> BrowseAsync(string language, string text,
        CancellationToken cancellationToken, string? documentPath = null,
        IReadOnlyList<string>? extraPackages = null);
}
