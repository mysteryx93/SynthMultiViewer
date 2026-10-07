using System.Runtime.InteropServices;

namespace HanumanInstitute.ApiVapourSynth;

/// <summary>
/// Loads the VapourSynth script library and remembers the handle.
/// An existing <c>VSSCRIPT_PATH</c> file is opened alone. Otherwise the bare name is opened.
/// </summary>
internal static class NativeLibraryLocator
{
    private static readonly Dictionary<string, IntPtr> Loaded = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> LoadedPaths = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns whether <paramref name="libraryName"/> is this profile's import or file name.
    /// The import resolver uses this to ignore unrelated libraries.
    /// </summary>
    public static bool Matches(NativeLibraryProfile profile, string libraryName) =>
        string.Equals(libraryName, profile.ImportName, StringComparison.OrdinalIgnoreCase) ||
        profile.FileNames.Any(name => string.Equals(libraryName, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Loads the registered script library, or the bare name, and returns the native handle.
    /// A library that loads but lacks the required exports is released.
    /// </summary>
    public static IntPtr Resolve(NativeLibraryProfile profile)
    {
        if (Loaded.TryGetValue(profile.Id, out var existing) && existing != IntPtr.Zero)
        {
            return existing;
        }

        var skippedIncompatible = new List<string>();
        foreach (var candidate in LoadCandidates())
        {
            if (!NativeLibrary.TryLoad(candidate, out var handle))
            {
                continue;
            }

            if (!HasRequiredExports(handle, profile.RequiredExports))
            {
                skippedIncompatible.Add(candidate);
                NativeLibrary.Free(handle);
                continue;
            }

            Loaded[profile.Id] = handle;
            LoadedPaths[profile.Id] = VsPathResolver.ResolveExistingPath(candidate, handle);
            return handle;
        }

        if (skippedIncompatible.Count > 0)
        {
            throw new EntryPointNotFoundException(
                $"Found VapourSynth at '{skippedIncompatible[0]}' but it does not export {string.Join(", ", profile.RequiredExports)}.");
        }

        throw new DllNotFoundException("Could not load VapourSynth.");
    }

    /// <summary>
    /// Returns whether the script library can be loaded, and the path the loader opened.
    /// A missing library returns false instead of throwing.
    /// </summary>
    public static bool TryFindLibrary(out string? path)
    {
        try
        {
            Resolve(NativeScriptHosts.VapourSynthScript);
            return LoadedPaths.TryGetValue(NativeScriptHosts.VapourSynth, out path) && path.HasValue();
        }
        catch (DllNotFoundException)
        {
            path = null;
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            path = null;
            return false;
        }
    }

    /// <summary>
    /// Returns plugin directories for the library this process already loaded.
    /// An unloaded profile yields the system directories the core would use without a sibling path.
    /// </summary>
    public static IReadOnlyList<string> GetPluginDirectories(NativeLibraryProfile profile)
    {
        LoadedPaths.TryGetValue(profile.Id, out var libraryPath);
        return VsPathResolver.GetPluginDirectories(libraryPath);
    }

    /// <summary>
    /// Returns the text <c>getVSScriptAPILastError</c> wrote after a null script API.
    /// Libraries that do not export the symbol return null.
    /// </summary>
    public static string? TryReadScriptLastError()
    {
        if (!Loaded.TryGetValue(NativeScriptHosts.VapourSynth, out var handle) || handle == IntPtr.Zero)
        {
            return null;
        }

        if (!NativeLibrary.TryGetExport(handle, "getVSScriptAPILastError", out var export) || export == IntPtr.Zero)
        {
            return null;
        }

        var lastError = Marshal.GetDelegateForFunctionPointer<GetLastErrorDelegate>(export);
        return Utf8Ptr.FromUtf8Ptr(lastError());
    }

    // The registered file is exclusive so a symlink is never the first mapping of that library.
    private static IReadOnlyList<string> LoadCandidates()
    {
        var configured = Environment.GetEnvironmentVariable("VSSCRIPT_PATH");
        return VsPathResolver.LibraryCandidates(configured, configured.HasText() && File.Exists(configured));
    }

    private static bool HasRequiredExports(IntPtr handle, IReadOnlyList<string> exports) =>
        exports.All(name => NativeLibrary.TryGetExport(handle, name, out _));

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr GetLastErrorDelegate();
}
