namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// A search hit that keeps the plugin or group next to the function name.
/// </summary>
public sealed record FunctionHit(string Group, BrowseFunction Function)
{
    /// <summary>
    /// Gets the function name used for display and type-ahead.
    /// </summary>
    public string Name => Function.Name;

    /// <summary>
    /// Gets the function signature for the row tooltip.
    /// </summary>
    public string Signature => Function.Signature;
}
