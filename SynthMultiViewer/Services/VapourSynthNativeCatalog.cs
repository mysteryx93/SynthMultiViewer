using HanumanInstitute.VapourSynthApi;
using HanumanInstitute.ScriptAssist.VapourSynth;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Copies VapourSynth API4 plugin metadata from the playback library.
/// </summary>
internal sealed class VapourSynthNativeCatalog : IVapourSynthNativeCatalog
{
    /// <inheritdoc />
    public IReadOnlyList<VapourSynthFunction> Read() =>
        VsCatalog.Read().Select(x => new VapourSynthFunction(x.Namespace, x.Name, x.Arguments, x.ReturnType, x.PluginName)).ToArray();
}
