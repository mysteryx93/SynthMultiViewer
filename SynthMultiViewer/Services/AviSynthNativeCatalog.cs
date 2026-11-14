using HanumanInstitute.AviSynthApi;
using HanumanInstitute.ScriptAssist.AviSynth;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Copies AviSynth autoload metadata from the playback library.
/// </summary>
internal sealed class AviSynthNativeCatalog : IAviSynthNativeCatalog
{
    /// <inheritdoc />
    public IReadOnlyList<AviSynthFilter> Read() =>
        AvsCatalog.Read().Select(x => new AviSynthFilter(x.Name, x.Arguments, x.Category)).ToArray();
}
