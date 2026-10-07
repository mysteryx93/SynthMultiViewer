namespace HanumanInstitute.ApiVapourSynth;

/// <summary>
/// Names the VapourSynth script library the process loader opens, and the plugin directories that core already uses.
/// The registered <c>VSSCRIPT_PATH</c> file is the path <c>vapoursynth config</c> recorded.
/// A bare name is the fallback FFmpeg opens, and it is not a second search through library directories.
/// </summary>
public static class VsPathResolver
{
    private const string ExtraPluginPathVariable = "VAPOURSYNTH_EXTRA_PLUGIN_PATH";

    private static readonly string? InheritedExtraPluginPath =
        Environment.GetEnvironmentVariable(ExtraPluginPathVariable);

    /// <summary>
    /// Gets the absolute path used when evaluating a script file.
    /// </summary>
    public static string ResolveScriptPath(string path) => Path.GetFullPath(path.CheckNotNullOrEmpty());

    /// <summary>
    /// Gets the bare library name the process loader opens.
    /// Linux is <c>libvapoursynth-script.so</c>, macOS is <c>libvapoursynth-script.dylib</c>, and Windows is <c>VSScript.dll</c>.
    /// </summary>
    public static IReadOnlyList<string> LibraryFileNames => NativeScriptHosts.VapourSynthScript.FileNames;

    /// <summary>
    /// Returns the library to open.
    /// An existing <paramref name="configuredPath"/> replaces the bare name, because that file is the config key.
    /// A missing path leaves the bare name FFmpeg opens.
    /// </summary>
    public static IReadOnlyList<string> LibraryCandidates(string? configuredPath, bool configuredFileExists)
    {
        if (configuredPath.HasText() && configuredFileExists)
        {
            return [configuredPath];
        }

        return LibraryFileNames;
    }

    /// <summary>
    /// Returns the path of a library the loader already opened.
    /// A bare name stays a bare name until the loaded module can be named.
    /// </summary>
    public static string ResolveExistingPath(string candidate, IntPtr loadedHandle = 0)
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
    /// Returns directories that contain Python script plugins such as havsfunc, not native <c>.so</c> plugins.
    /// The walk starts at the loaded library and stops after six ancestors.
    /// </summary>
    public static IReadOnlyList<string> GetPythonModuleDirectories(string? libraryPath = null)
    {
        var directories = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (libraryPath.HasText())
        {
            var directory = File.Exists(libraryPath) || Path.GetExtension(libraryPath).HasValue()
                ? Path.GetDirectoryName(libraryPath)
                : libraryPath;
            for (var i = 0; i < 6 && directory.HasText(); i++)
            {
                AddPythonSites(directory, directories, seen);
                directory = Path.GetDirectoryName(directory);
            }
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (home.HasText())
            {
                AddPythonSites(Path.Combine(home, ".local", "lib"), directories, seen);
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            AddPythonSites(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python"),
                directories, seen);
            AddPythonSites(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VapourSynth"), directories, seen);
            AddPythonSites(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python"), directories, seen);
        }

        return directories;
    }

    /// <summary>
    /// Returns plugin directories shipped beside the loaded library.
    /// R74 and later autoload <c>vapoursynth/plugins</c> under the package that holds the library.
    /// </summary>
    public static IReadOnlyList<string> GetBundledPluginDirectories(string? libraryPath)
    {
        if (!libraryPath.HasValue() || !Path.IsPathRooted(libraryPath)) { return []; }

        var libDir = Path.GetDirectoryName(libraryPath);
        if (!libDir.HasValue()) { return []; }

        var beside = new[]
        {
            Path.Combine(libDir, "vapoursynth", "plugins"),
            Path.Combine(libDir, "plugins"),
            Path.Combine(libDir, "core", "plugins"),
            Path.Combine(libDir, "vapoursynth")
        };
        var parent = Directory.GetParent(libDir)?.FullName;
        if (!parent.HasValue())
        {
            return beside;
        }

        return
        [
            ..beside,
            Path.Combine(parent, "vapoursynth", "plugins"),
            Path.Combine(parent, "plugins"),
            Path.Combine(parent, "vapoursynth")
        ];
    }

    /// <summary>
    /// Returns plugin directories from the extra-path variable, vapoursynth.conf, and the user folders the core reads.
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

        directories.AddRange(ReadConfiguredPluginDirectories());
        if (OperatingSystem.IsLinux())
        {
            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (dataHome.HasValue())
            {
                directories.Add(Path.Combine(dataHome, "vapoursynth", "plugins"));
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (home.HasValue())
            {
                directories.Add(Path.Combine(home, ".local", "lib", "vapoursynth"));
                directories.Add(Path.Combine(home, ".local", "share", "vapoursynth", "plugins"));
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            directories.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VapourSynth",
                Environment.Is64BitProcess ? "plugins64" : "plugins32"));
        }

        return directories;
    }

    private static void AddPythonSites(string? root, List<string> directories, HashSet<string> seen)
    {
        if (!root.HasText() || !Directory.Exists(root))
        {
            return;
        }

        var name = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (name is "site-packages" or "dist-packages")
        {
            AddExisting(root, directories, seen);
        }

        AddExisting(Path.Combine(root, "site-packages"), directories, seen);
        AddExisting(Path.Combine(root, "dist-packages"), directories, seen);
        AddExisting(Path.Combine(root, "Lib", "site-packages"), directories, seen);
        try
        {
            foreach (var python in Directory.EnumerateDirectories(root, "python3.*"))
            {
                AddExisting(Path.Combine(python, "site-packages"), directories, seen);
                AddExisting(Path.Combine(python, "dist-packages"), directories, seen);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void AddExisting(string path, List<string> directories, HashSet<string> seen)
    {
        if (!Directory.Exists(path) || !seen.Add(path))
        {
            return;
        }

        directories.Add(path);
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

    private static IReadOnlyList<string> ReadConfiguredPluginDirectories()
    {
        var confPath = Environment.GetEnvironmentVariable("VAPOURSYNTH_CONF_PATH");
        if (!confPath.HasValue())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (OperatingSystem.IsMacOS())
            {
                confPath = Path.Combine(home, "Library", "Application Support", "VapourSynth", "vapoursynth.conf");
            }
            else if (OperatingSystem.IsLinux())
            {
                var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                confPath = configHome.HasValue()
                    ? Path.Combine(configHome, "vapoursynth", "vapoursynth.conf")
                    : Path.Combine(home, ".config", "vapoursynth", "vapoursynth.conf");
            }
        }

        if (!confPath.HasValue() || !File.Exists(confPath)) { return []; }

        var directories = new List<string>();
        foreach (var line in File.ReadLines(confPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || !trimmed.Contains('='))
            {
                continue;
            }

            var parts = trimmed.Split('=', 2);
            var key = parts[0].Trim();
            var value = parts[1].Trim();
            if (key is "UserPluginDir" or "SystemPluginDir" && value.HasValue())
            {
                directories.Add(value);
            }
        }

        return directories;
    }
}
