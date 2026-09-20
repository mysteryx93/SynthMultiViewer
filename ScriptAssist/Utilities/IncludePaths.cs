using System.IO.Abstractions;

namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Builds candidate paths for script includes without reading the disk.
/// </summary>
internal static class IncludePaths
{
    /// <summary>
    /// AviSynth <c>Import</c> specifiers: rooted paths first, then next to the buffer, then plugin roots.
    /// </summary>
    public static IEnumerable<string> AviSynth(string specifier, string? fromPath, IReadOnlyList<string> roots,
        IPath paths) =>
        Candidates(specifier, fromPath, roots, false, paths);

    /// <summary>
    /// Python module specifiers: <c>name.py</c>, <c>name.pyi</c>, <c>name/__init__.py</c>, and
    /// <c>name/__init__.pyi</c> under the buffer and plugin roots.
    /// </summary>
    public static IEnumerable<string> PythonModule(string specifier, string? fromPath, IReadOnlyList<string> roots,
        IPath paths) =>
        Candidates(specifier, fromPath, roots, true, paths);

    private static IEnumerable<string> Candidates(string specifier, string? fromPath, IReadOnlyList<string> roots,
        bool pythonModule, IPath paths)
    {
        paths.CheckNotNull();
        specifier = specifier.Trim().Trim('"', '\'');
        if (specifier.Length == 0)
        {
            yield break;
        }

        specifier = specifier.Replace('\\', paths.DirectorySeparatorChar);
        if (pythonModule && specifier[0] == '.')
        {
            foreach (var path in RelativePython(specifier, fromPath, paths))
            {
                yield return path;
            }

            yield break;
        }

        if (paths.IsPathRooted(specifier))
        {
            foreach (var path in Expand(specifier, pythonModule, paths))
            {
                yield return path;
            }

            yield break;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var fromDir = fromPath.HasText() ? paths.GetDirectoryName(fromPath) : null;
        foreach (var directory in Roots(fromDir, roots))
        {
            if (!seen.Add(directory))
            {
                continue;
            }

            var combined = pythonModule
                ? paths.Combine(directory, specifier.Replace('.', paths.DirectorySeparatorChar))
                : paths.Combine(directory, specifier);
            foreach (var path in Expand(combined, pythonModule, paths))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> Roots(string? fromDir, IReadOnlyList<string> roots)
    {
        if (fromDir.HasText())
        {
            yield return fromDir;
        }

        foreach (var root in roots)
        {
            if (root.HasText())
            {
                yield return root;
            }
        }
    }

    private static IEnumerable<string> RelativePython(string specifier, string? fromPath, IPath paths)
    {
        if (!fromPath.HasText())
        {
            yield break;
        }

        var directory = paths.GetDirectoryName(fromPath);
        var dots = 0;
        while (dots < specifier.Length && specifier[dots] == '.')
        {
            dots++;
        }

        for (var i = 1; i < dots && directory.HasText(); i++)
        {
            directory = paths.GetDirectoryName(directory);
        }

        if (!directory.HasText())
        {
            yield break;
        }

        var rest = specifier[dots..].Replace('.', paths.DirectorySeparatorChar);
        if (rest.Length == 0)
        {
            yield return paths.Combine(directory, "__init__.py");
            yield return paths.Combine(directory, "__init__.pyi");
            yield break;
        }

        foreach (var path in Expand(paths.Combine(directory, rest), true, paths))
        {
            yield return path;
        }
    }

    private static IEnumerable<string> Expand(string path, bool pythonModule, IPath paths)
    {
        if (!pythonModule)
        {
            yield return path;
            yield break;
        }

        yield return path + ".py";
        yield return path + ".pyi";
        yield return paths.Combine(path, "__init__.py");
        yield return paths.Combine(path, "__init__.pyi");
    }
}