using HanumanInstitute.ApiAviSynth;
using HanumanInstitute.ScriptAssist.Services;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Follows AviSynth <c>Import</c> specifiers from plugin folders.
/// </summary>
internal sealed class AviSynthIncludeSource(IFileSystemService files) : IIncludeSource
{
    /// <inheritdoc />
    public IncludeFile? Read(string specifier, string? fromPath) =>
        ScriptFiles.AviSynth(specifier, fromPath, AvsScript.GetPluginDirectories(), files);
}
