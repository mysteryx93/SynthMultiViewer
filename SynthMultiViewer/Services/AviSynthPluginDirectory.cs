using HanumanInstitute.AviSynthApi;
using HanumanInstitute.ScriptAssist.Services;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// AviSynth plugin folders and the script files in them.
/// </summary>
internal sealed class AviSynthPluginDirectory(IFileSystemService files) : IScriptDirectory
{
    /// <inheritdoc />
    public IReadOnlyList<string> Roots() => AvsScript.GetPluginDirectories();

    /// <inheritdoc />
    public IEnumerable<string> Files(string directory, IReadOnlyList<string> extensions) =>
        files.GetFilesByExtensions(directory, extensions);

    /// <inheritdoc />
    public string? TryRead(string path)
    {
        try
        {
            return files.File.Exists(path) ? files.File.ReadAllText(path) : null;
        }
        catch (System.IO.IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
