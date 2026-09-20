using System.Text;
using HanumanInstitute.ScriptAssist.Services;

namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Lists installed VapourSynth Python import names under host-supplied roots.
/// </summary>
public static class ScriptPackages
{
    /// <summary>
    /// Import names treated as VapourSynth packages even without a <c>Requires-Dist</c> line.
    /// </summary>
    public static IReadOnlyList<string> Known { get; } = ["havsfunc", "vsutil", "mvsfunc", "vsjetpack"];

    private const int MentionLimit = 32 * 1024;
    private static readonly HashSet<string> KnownSet = new(Known, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns import names from <c>.dist-info</c> / <c>.egg-info</c> and loose root <c>*.py</c> files.
    /// </summary>
    public static IReadOnlyList<string> List(IReadOnlyList<string> roots, IFileSystemService files)
    {
        roots.CheckNotNull();
        files.CheckNotNull();
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            if (!root.HasText() || !DirectoryExists(files, root))
            {
                continue;
            }

            foreach (var directory in Directories(files, root))
            {
                var folder = files.Path.GetFileName(directory);
                if (folder.EndsWith(".dist-info", StringComparison.OrdinalIgnoreCase) ||
                    folder.EndsWith(".egg-info", StringComparison.OrdinalIgnoreCase))
                {
                    TryAddDistribution(directory, names, files);
                }
            }

            foreach (var file in Files(files, root, "*.egg-info"))
            {
                TryAddMetadata(file, names, files);
            }

            foreach (var file in Files(files, root, "*.py"))
            {
                TryAddLoose(file, names, files);
            }
        }

        if (names.Count > 1)
        {
            names.Remove("vsjetpack");
        }

        return [..names];
    }

    private static void TryAddDistribution(string directory, SortedSet<string> names, IFileSystemService files)
    {
        var metadata = files.Path.Combine(directory, "METADATA");
        if (!FileExists(files, metadata))
        {
            metadata = files.Path.Combine(directory, "PKG-INFO");
        }

        var text = TryRead(files, metadata);
        if (text == null || !TryPackage(text, out var distribution))
        {
            return;
        }

        if (NativeOnly(files, directory))
        {
            return;
        }

        foreach (var name in ImportNames(directory, distribution, files))
        {
            names.Add(name);
        }
    }

    private static void TryAddMetadata(string path, SortedSet<string> names, IFileSystemService files)
    {
        var text = TryRead(files, path);
        if (text == null || !TryPackage(text, out var name))
        {
            return;
        }

        names.Add(ImportName(name));
    }

    private static void TryAddLoose(string path, SortedSet<string> names, IFileSystemService files)
    {
        var file = files.Path.GetFileName(path);
        if (file.Equals("__init__.py", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var name = files.GetPathWithoutExtension(file);
        if (!name.HasText() || name.Equals("vapoursynth", StringComparison.OrdinalIgnoreCase) ||
            names.Contains(name))
        {
            return;
        }

        var mention = TryReadPrefix(files, path, MentionLimit);
        if (mention != null && mention.Contains("vapoursynth", StringComparison.OrdinalIgnoreCase))
        {
            names.Add(name);
        }
    }

    private static bool TryPackage(string metadata, out string name)
    {
        name = "";
        var requiresVapourSynth = false;
        foreach (var raw in metadata.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
            {
                name = line[5..].Trim();
            }
            else if (line.StartsWith("Requires-Dist:", StringComparison.OrdinalIgnoreCase) &&
                DependencyName(line[14..]).Equals("vapoursynth", StringComparison.OrdinalIgnoreCase))
            {
                requiresVapourSynth = true;
            }
        }

        if (!name.HasText() || name.Equals("vapoursynth", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return KnownSet.Contains(name) || KnownSet.Contains(ImportName(name)) || requiresVapourSynth;
    }

    private static IReadOnlyList<string> ImportNames(string directory, string distribution, IFileSystemService files)
    {
        var top = TryRead(files, files.Path.Combine(directory, "top_level.txt"));
        if (top != null)
        {
            var fromTop = new List<string>();
            foreach (var raw in top.Split('\n'))
            {
                var line = raw.Trim().TrimEnd('\r');
                if (!line.HasText() || line[0] == '#')
                {
                    continue;
                }

                var module = line.Replace('\\', '/');
                var slash = module.IndexOf('/');
                if (slash >= 0)
                {
                    module = module[..slash];
                }

                if (module.HasText() && !module.Equals("vapoursynth", StringComparison.OrdinalIgnoreCase))
                {
                    fromTop.Add(module);
                }
            }

            if (fromTop.Count > 0)
            {
                return fromTop;
            }
        }

        var fromRecord = RecordModules(files, directory);
        if (fromRecord.Count > 0)
        {
            return fromRecord;
        }

        var fallback = ImportName(distribution);
        return fallback.HasText() ? [fallback] : [];
    }

    private static IReadOnlyList<string> RecordModules(IFileSystemService files, string directory)
    {
        var record = TryRead(files, files.Path.Combine(directory, "RECORD"));
        if (record == null)
        {
            return [];
        }

        var modules = new List<string>();
        foreach (var raw in record.Split('\n'))
        {
            if (modules.Count >= 8)
            {
                break;
            }

            var path = raw.Split(',')[0].Trim().Replace('\\', '/');
            if (path.Length == 0 || path.StartsWith("..", StringComparison.Ordinal) ||
                !path.EndsWith(".py", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(".pyi", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var slash = path.IndexOf('/');
            var module = slash < 0
                ? files.GetPathWithoutExtension(path)
                : path[..slash];
            if (module.EndsWith(".dist-info", StringComparison.OrdinalIgnoreCase) ||
                module.EndsWith(".egg-info", StringComparison.OrdinalIgnoreCase) ||
                module.Equals("vapoursynth", StringComparison.OrdinalIgnoreCase) ||
                !module.HasText())
            {
                continue;
            }

            if (!modules.Contains(module, StringComparer.Ordinal))
            {
                modules.Add(module);
            }
        }

        return modules;
    }

    private static string ImportName(string name) => name.Replace('-', '_');

    private static string DependencyName(string rest)
    {
        var text = rest.Trim();
        var end = text.Length;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is ' ' or ';' or '[' or '(' or '<' or '>' or '=' or '!' or '~')
            {
                end = i;
                break;
            }
        }

        return ImportName(text[..end]);
    }

    private static bool NativeOnly(IFileSystemService files, string directory)
    {
        var record = TryRead(files, files.Path.Combine(directory, "RECORD"));
        if (record == null)
        {
            return false;
        }

        var hasFile = false;
        foreach (var raw in record.Split('\n'))
        {
            var path = raw.Split(',')[0].Trim();
            if (path.Length == 0)
            {
                continue;
            }

            hasFile = true;
            if (path.EndsWith(".py", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".pyi", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return hasFile;
    }

    private static bool DirectoryExists(IFileSystemService files, string path)
    {
        try
        {
            return files.Directory.Exists(path);
        }
        catch (System.IO.IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> Directories(IFileSystemService files, string root)
    {
        try
        {
            return [..files.Directory.EnumerateDirectories(root)];
        }
        catch (System.IO.IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> Files(IFileSystemService files, string root, string pattern)
    {
        try
        {
            return [..files.Directory.EnumerateFiles(root, pattern)];
        }
        catch (System.IO.IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool FileExists(IFileSystemService files, string path)
    {
        try
        {
            return files.File.Exists(path);
        }
        catch (System.IO.IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? TryRead(IFileSystemService files, string path)
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

    private static string? TryReadPrefix(IFileSystemService files, string path, int limit)
    {
        try
        {
            if (!files.File.Exists(path))
            {
                return null;
            }

            using var stream = files.File.OpenRead(path);
            var buffer = new byte[limit];
            var read = stream.Read(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer, 0, read);
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
