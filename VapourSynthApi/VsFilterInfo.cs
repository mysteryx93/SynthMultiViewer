namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Metadata copied from a native plugin function.
/// </summary>
public sealed record VsFilterInfo(string Namespace, string Name, string? Arguments, string? ReturnType = null,
    string? PluginName = null);
