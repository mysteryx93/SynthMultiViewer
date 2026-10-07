namespace HanumanInstitute.ApiAviSynth;

/// <summary>
/// Names the AviSynth library the process loader opens, and the plugin directories that core already uses.
/// The library name is the bare file FFmpeg and avs2yuv pass to <c>dlopen</c> or <c>LoadLibrary</c>.
/// Plugin folders are the siblings of that library plus the paths the core itself reads.
/// </summary>
public static class AvsPathResolver
{
    private static readonly string? InheritedExtraPluginPath =
        Environment.GetEnvironmentVariable("AVISYNTH_PLUGIN_PATH");

    /// <summary>
    /// Gets the absolute path used when evaluating a script file.
    /// </summary>
    public static string ResolveScriptPath(string path) => Path.GetFullPath(path.CheckNotNullOrEmpty());

    /// <summary>
    /// Gets the bare library name the process loader opens.
    /// Linux is <c>libavisynth.so</c>, macOS is <c>libavisynth.dylib</c>, and Windows is <c>avisynth.dll</c>.
    /// </summary>
    public static IReadOnlyList<string> LibraryFileNames => OperatingSystem.IsWindows()
        ? ["avisynth.dll"]
        : OperatingSystem.IsMacOS()
            ? ["libavisynth.dylib"]
            : ["libavisynth.so"];

    /// <summary>
    /// Returns the path of a library the loader already opened.
    /// A bare name stays a bare name until the loaded module can be named.
    /// </summary>
    public static string ResolveExistingPath(string candidate, IntPtr loadedHandle = default)
    {
        candidate.CheckNotNullOrEmpty();
        if (File.Exists(candidate))
        {
            return Path.GetFullPath(candidate);
        }

        return ResolveFromProcessMaps(candidate, loadedHandle) ?? candidate;
    }

    /// <summary>
    /// Returns existing plugin directories, bundled first, then the directories the core reads.
    /// </summary>
    public static IReadOnlyList<string> GetPluginDirectories(string? libraryPath) =>
        GetBundledPluginDirectories(libraryPath)
            .Concat(GetSystemPluginDirectories())
            .Where(Directory.Exists)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Returns plugin directories shipped beside the loaded library.
    /// On Linux the core autoloads the <c>avisynth</c> directory next to <c>libavisynth.so</c>.
    /// </summary>
    public static IReadOnlyList<string> GetBundledPluginDirectories(string? libraryPath)
    {
        if (!libraryPath.HasValue() || !Path.IsPathRooted(libraryPath)) { return []; }

        var libDir = Path.GetDirectoryName(libraryPath);
        if (!libDir.HasValue()) { return []; }

        var beside = new[]
        {
            Path.Combine(libDir, "plugins+"),
            Path.Combine(libDir, "plugins"),
            Path.Combine(libDir, "avisynth")
        };
        var parent = Directory.GetParent(libDir)?.FullName;
        if (!parent.HasValue())
        {
            return beside;
        }

        return
        [
            ..beside,
            Path.Combine(parent, "plugins+"),
            Path.Combine(parent, "plugins"),
            Path.Combine(parent, "avisynth")
        ];
    }

    /// <summary>
    /// Returns plugin directories from <c>AVISYNTH_PLUGIN_PATH</c> and the user folders the core reads.
    /// A missing folder is dropped later, when the caller checks that it exists.
    /// </summary>
    public static IReadOnlyList<string> GetSystemPluginDirectories()
    {
        var directories = new List<string>();
        if (InheritedExtraPluginPath.HasValue())
        {
            directories.AddRange(InheritedExtraPluginPath.Split(
                Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (home.HasValue())
            {
                directories.Add(Path.Combine(home, ".avisynth"));
                directories.Add(Path.Combine(home, ".local", "lib", "avisynth"));
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            directories.Add(Path.Combine(programFiles, "AviSynth+", "plugins+"));
            directories.Add(Path.Combine(programFiles, "AviSynth+", "plugins"));
            directories.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AviSynth", "plugins"));
        }

        return directories;
    }

    private static IReadOnlyList<string> DistinctFileNames(string candidate)
    {
        var names = new List<string>();
        var fileName = Path.GetFileName(candidate);
        if (fileName.HasValue())
        {
            names.Add(fileName);
        }

        foreach (var name in LibraryFileNames)
        {
            if (!names.Contains(name, StringComparer.Ordinal))
            {
                names.Add(name);
            }
        }

        return names;
    }

    // Linux records the opened module in /proc/self/maps. Other systems keep the candidate.
    private static string? ResolveFromProcessMaps(string candidate, IntPtr loadedHandle)
    {
        if (loadedHandle == IntPtr.Zero || !OperatingSystem.IsLinux() || !File.Exists("/proc/self/maps"))
        {
            return null;
        }

        var needles = DistinctFileNames(candidate);
        foreach (var line in File.ReadLines("/proc/self/maps"))
        {
            var start = line.IndexOf('/');
            if (start < 0) { continue; }

            var path = line[start..];
            if (needles.Any(name => path.Contains(name, StringComparison.Ordinal)) && File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }

        return null;
    }
}
