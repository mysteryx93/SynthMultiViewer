using HanumanInstitute.ApiVapourSynth;
using HanumanInstitute.ScriptAssist;
using HanumanInstitute.ScriptAssist.Services;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Follows VapourSynth Python imports from the script directory, plugin folders, and site-packages.
/// </summary>
internal sealed class VapourSynthIncludeSource(IFileSystemService files) : IIncludeSource
{
    private static readonly Lock RootsGate = new();
    private static string[]? _vsRoots;
    private static string? _vsLibrary;

    /// <summary>
    /// Drops cached search roots so the next lookup rediscovers plugin and site-package directories.
    /// </summary>
    public static void Invalidate()
    {
        lock (RootsGate)
        {
            _vsRoots = null;
            _vsLibrary = null;
        }
    }

    /// <summary>
    /// Plugin and site-package directories used to resolve Python imports and list installed packages.
    /// </summary>
    public static IReadOnlyList<string> SearchRoots()
    {
        VsHelper.TryFindLibrary(out var libraryPath);
        return VsRoots(libraryPath);
    }

    /// <inheritdoc />
    public IncludeFile? Read(string specifier, string? fromPath) =>
        ScriptFiles.PythonModule(specifier, fromPath, SearchRoots(), files);

    private static string[] VsRoots(string? libraryPath)
    {
        lock (RootsGate)
        {
            if (_vsRoots != null && _vsLibrary == libraryPath)
            {
                return _vsRoots;
            }

            _vsLibrary = libraryPath;
            _vsRoots = VsHelper.GetPluginDirectories(libraryPath)
                .Concat(VsPathResolver.GetPythonModuleDirectories(libraryPath))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return _vsRoots;
        }
    }
}
