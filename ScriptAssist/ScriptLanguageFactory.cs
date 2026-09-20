using HanumanInstitute.ScriptAssist.AviSynth;
using HanumanInstitute.ScriptAssist.VapourSynth;

namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Holds the VapourSynth and AviSynth language services and their catalogs.
/// </summary>
public class ScriptLanguageFactory : IScriptLanguageFactory
{
    /// <summary>
    /// Host-facing id for VapourSynth. Matches <c>nameof(ScriptKind.VapourSynth)</c> in Synth Multi-Viewer.
    /// </summary>
    public const string VapourSynth = "VapourSynth";

    /// <summary>
    /// Host-facing id for AviSynth. Matches <c>nameof(ScriptKind.AviSynth)</c> in Synth Multi-Viewer.
    /// </summary>
    public const string AviSynth = "AviSynth";

    private readonly Dictionary<string, LanguageProfile> _profiles;

    /// <summary>
    /// Creates the built-in VapourSynth and AviSynth profiles.
    /// </summary>
    public ScriptLanguageFactory(
        IVapourSynthNativeCatalog vapoursynth,
        IAviSynthNativeCatalog avisynth,
        IScriptDirectory avisynthAutoload,
        IIncludeSource? vapoursynthIncludes = null,
        IIncludeSource? avisynthIncludes = null)
    {
        vapoursynth.CheckNotNull();
        avisynth.CheckNotNull();
        avisynthAutoload.CheckNotNull();
        _profiles = new(StringComparer.Ordinal)
        {
            [VapourSynth] = new(VapourSynth, new VapourSynthLanguage(vapoursynthIncludes),
                new CatalogCache(new VapourSynthSymbolSource(vapoursynth))),
            [AviSynth] = new(AviSynth, new AviSynthLanguage(avisynthIncludes),
                new CatalogCache(new AviSynthSymbolSource(avisynth, avisynthAutoload, avisynthIncludes)))
        };
    }

    /// <inheritdoc />
    public bool IsEnabled { get; set; } = true;

    /// <inheritdoc />
    public ILanguageService? Create(string language)
    {
        language.CheckNotNull();
        if (!IsEnabled)
        {
            return null;
        }

        return _profiles.TryGetValue(language, out var profile) ? profile.Service : null;
    }

    /// <inheritdoc />
    public void Configure(string language, string catalogKey)
    {
        language.CheckNotNull();
        catalogKey.CheckNotNull();
        if (!_profiles.TryGetValue(language, out var profile))
        {
            return;
        }

        var previous = profile.CatalogKey;
        if (!IsEnabled)
        {
            profile.Catalog.SetKey(catalogKey);
        }
        else
        {
            profile.Catalog.Refresh(catalogKey);
        }

        profile.CatalogKey = catalogKey;
        if (previous != catalogKey)
        {
            profile.Service.Invalidate();
        }
    }

    /// <inheritdoc />
    public virtual void Refresh()
    {
        foreach (var profile in _profiles.Values)
        {
            profile.Catalog.Refresh(profile.CatalogKey ?? "", true);
            profile.Service.Invalidate();
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BrowseGroup>> BrowseAsync(string language, string text,
        CancellationToken cancellationToken, string? documentPath = null,
        IReadOnlyList<string>? extraPackages = null)
    {
        language.CheckNotNull();
        return _profiles.TryGetValue(language, out var profile)
            ? profile.Service.BrowseAsync(text, cancellationToken, documentPath, extraPackages)
            : Task.FromResult<IReadOnlyList<BrowseGroup>>([]);
    }
}
