namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Provides native image-copy utilities and the plugin directories the loaded core already uses.
/// </summary>
public static class VsHelper
{
    /// <summary>
    /// Returns plugin directories the loaded core already uses.
    /// </summary>
    public static IReadOnlyList<string> GetPluginDirectories(string? libraryPath = null) =>
        VsPathResolver.GetPluginDirectories(libraryPath ?? (TryFindLibrary(out var path) ? path : null));

    /// <summary>
    /// Returns whether a VapourSynth script library can be loaded, and its path when found.
    /// </summary>
    public static bool TryFindLibrary(out string? path) => NativeLibraryLocator.TryFindLibrary(out path);

    /// <summary>
    /// Returns whether the loaded VapourSynth can evaluate a script, and the error when it cannot.
    /// </summary>
    public static bool TryEvaluate(out string? error) => VsScript.TryEvaluate(out error);

    /// <summary>
    /// Returns the loaded core's release label, such as R79.
    /// </summary>
    public static bool TryReadVersion(out string? version, out string? detail) =>
        VsScript.TryReadVersion(out version, out detail);

    /// <summary>
    /// Returns the version requested from the VapourSynth 4 core API.
    /// </summary>
    public static int GetApiVersion() => VsInvoke.ApiVersion;

    /// <summary>
    /// Copies frame data from one memory location to another.
    /// </summary>
    public static unsafe void BitBlt(IntPtr dstp, int dstStride, IntPtr srcp, int srcStride, int rowSize, int height)
    {
        for (var row = 0; row < height; row++)
        {
            Buffer.MemoryCopy((byte*)srcp + (nint)row * srcStride, (byte*)dstp + (nint)row * dstStride, rowSize, rowSize);
        }
    }
}
