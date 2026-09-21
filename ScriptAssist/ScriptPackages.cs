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
    public static IReadOnlyList<string> List(IReadOnlyList<string> roots, IFileSystemService files,
        CancellationToken token = default)
    {
        roots.CheckNotNull();
        files.CheckNotNull();
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            token.ThrowIfCancellationRequested();
            if (!root.HasText() || !DirectoryExists(files, root))
            {
                continue;
            }

            foreach (var directory in Directories(files, root, token))
            {
                var folder = files.Path.GetFileName(directory);
                if (folder.EndsWith(".dist-info", StringComparison.OrdinalIgnoreCase) ||
                    folder.EndsWith(".egg-info", StringComparison.OrdinalIgnoreCase))
                {
                    TryAddDistribution(directory, names, files, token);
                }
            }

            foreach (var file in Files(files, root, "*.egg-info", token))
            {
                TryAddMetadata(file, names, files, token);
            }

            foreach (var file in Files(files, root, "*.py", token))
            {
                TryAddLoose(file, names, files, token);
            }
        }

        if (names.Count > 1)
        {
            names.Remove("vsjetpack");
        }

        return [..names];
    }

    private static void TryAddDistribution(string directory, SortedSet<string> names, IFileSystemService files,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var metadata = files.Path.Combine(directory, "METADATA");
        if (!FileExists(files, metadata))
        {
            metadata = files.Path.Combine(directory, "PKG-INFO");
        }

        var text = TryRead(files, metadata);
        token.ThrowIfCancellationRequested();
        if (text == null || !TryPackage(text, out var distribution))
        {
            return;
        }

        var record = TryRead(files, files.Path.Combine(directory, "RECORD"));
        token.ThrowIfCancellationRequested();
        var scan = ScanRecord(record, token);
        if (scan.NativeOnly)
        {
            return;
        }

        foreach (var name in ImportNames(directory, distribution, files, scan.Modules, token))
        {
            TryAddName(names, name);
        }
    }

    private static void TryAddMetadata(string path, SortedSet<string> names, IFileSystemService files,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var text = TryRead(files, path);
        if (text == null || !TryPackage(text, out var name))
        {
            return;
        }

        TryAddName(names, ImportName(name));
    }

    private static void TryAddLoose(string path, SortedSet<string> names, IFileSystemService files,
        CancellationToken token)
    {
        var file = files.Path.GetFileName(path);
        if (file.Equals("__init__.py", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var name = files.GetPathWithoutExtension(file);
        if (!ParameterNames.IsPythonImportName(name) ||
            name.Equals("vapoursynth", StringComparison.OrdinalIgnoreCase) ||
            names.Contains(name))
        {
            return;
        }

        token.ThrowIfCancellationRequested();
        var mention = TryReadPrefix(files, path, MentionLimit);
        if (mention != null && MentionsVapourSynthImport(mention))
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

    private static IReadOnlyList<string> ImportNames(string directory, string distribution, IFileSystemService files,
        IReadOnlyList<string> recordModules, CancellationToken token)
    {
        var top = TryRead(files, files.Path.Combine(directory, "top_level.txt"));
        token.ThrowIfCancellationRequested();
        if (top != null)
        {
            var fromTop = new List<string>();
            var start = 0;
            while (start < top.Length)
            {
                token.ThrowIfCancellationRequested();
                var end = top.IndexOf('\n', start);
                if (end < 0)
                {
                    end = top.Length;
                }

                var line = top.AsSpan(start, end - start).TrimEnd('\r').Trim().ToString();
                start = end + 1;
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

                if (ParameterNames.IsPythonImportName(module) &&
                    !module.Equals("vapoursynth", StringComparison.OrdinalIgnoreCase))
                {
                    fromTop.Add(module);
                }
            }

            if (fromTop.Count > 0)
            {
                return fromTop;
            }
        }

        if (recordModules.Count > 0)
        {
            return recordModules;
        }

        var fallback = ImportName(distribution);
        return ParameterNames.IsPythonImportName(fallback) ? [fallback] : [];
    }

    private readonly record struct RecordScan(bool NativeOnly, IReadOnlyList<string> Modules);

    private static RecordScan ScanRecord(string? record, CancellationToken token)
    {
        if (record == null)
        {
            return new(false, []);
        }

        var modules = new List<string>();
        var hasFile = false;
        var hasPython = false;
        var start = 0;
        while (start < record.Length)
        {
            token.ThrowIfCancellationRequested();
            var end = record.IndexOf('\n', start);
            if (end < 0)
            {
                end = record.Length;
            }

            var line = record.AsSpan(start, end - start);
            if (line.Length > 0 && line[^1] == '\r')
            {
                line = line[..^1];
            }

            start = end + 1;
            var comma = line.IndexOf(',');
            var path = (comma < 0 ? line : line[..comma]).Trim();
            if (path.Length == 0)
            {
                continue;
            }

            hasFile = true;
            var python = path.EndsWith(".py", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".pyi", StringComparison.OrdinalIgnoreCase);
            if (python)
            {
                hasPython = true;
            }

            if (!python)
            {
                continue;
            }

            var normalized = path.ToString().Replace('\\', '/');
            if (normalized.StartsWith("..", StringComparison.Ordinal))
            {
                continue;
            }

            var slash = normalized.IndexOf('/');
            var module = slash < 0 ? WithoutExtension(normalized) : normalized[..slash];
            if (module.EndsWith(".dist-info", StringComparison.OrdinalIgnoreCase) ||
                module.EndsWith(".egg-info", StringComparison.OrdinalIgnoreCase) ||
                module.Equals("vapoursynth", StringComparison.OrdinalIgnoreCase) ||
                !ParameterNames.IsPythonImportName(module) ||
                modules.Contains(module, StringComparer.Ordinal))
            {
                continue;
            }

            modules.Add(module);
        }

        return new(hasFile && !hasPython, modules);
    }

    private static string WithoutExtension(string path)
    {
        var slash = path.LastIndexOf('/');
        var name = slash < 0 ? path : path[(slash + 1)..];
        var dot = name.LastIndexOf('.');
        return dot < 0 ? name : name[..dot];
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

    private static void TryAddName(SortedSet<string> names, string name)
    {
        if (ParameterNames.IsPythonImportName(name) &&
            !name.Equals("vapoursynth", StringComparison.OrdinalIgnoreCase))
        {
            names.Add(name);
        }
    }

    private static readonly LexerOptions MentionLexer = new()
    {
        HashLineComments = true,
        SingleQuotes = true,
        TripleQuotes = true,
        StringEscapes = true
    };

    private static bool MentionsVapourSynthImport(string text)
    {
        var code = BufferLexer.Mask(text, MentionLexer, trackLiterals: false).Code;
        var i = 0;
        var statement = true;
        while (i < code.Length)
        {
            var c = code[i];
            if (c is ' ' or '\t' or '\f')
            {
                i++;
                continue;
            }

            if (c is '\n' or '\r' or ';')
            {
                statement = true;
                i++;
                continue;
            }

            if (statement && (IsWord(code, i, "import") || IsWord(code, i, "from")))
            {
                i += code[i] == 'i' ? 6 : 4;
                while (i < code.Length && code[i] is ' ' or '\t' or '\f')
                {
                    i++;
                }

                if (IsWord(code, i, "vapoursynth"))
                {
                    return true;
                }

                continue;
            }

            statement = false;
            i++;
        }

        return false;
    }

    private static bool IsWord(string text, int i, string word) =>
        i + word.Length <= text.Length &&
        text.AsSpan(i, word.Length).Equals(word, StringComparison.Ordinal) &&
        (i + word.Length == text.Length || !BufferLexer.IsIdentifier(text[i + word.Length])) &&
        (i == 0 || !BufferLexer.IsIdentifier(text[i - 1]));

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

    private static IEnumerable<string> Directories(IFileSystemService files, string root, CancellationToken token)
    {
        IEnumerable<string> items;
        try
        {
            items = files.Directory.EnumerateDirectories(root);
        }
        catch (System.IO.IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var item in Enumerate(items, token))
        {
            yield return item;
        }
    }

    private static IEnumerable<string> Files(IFileSystemService files, string root, string pattern,
        CancellationToken token)
    {
        IEnumerable<string> items;
        try
        {
            items = files.Directory.EnumerateFiles(root, pattern);
        }
        catch (System.IO.IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var item in Enumerate(items, token))
        {
            yield return item;
        }
    }

    private static IEnumerable<string> Enumerate(IEnumerable<string> items, CancellationToken token)
    {
        IEnumerator<string> enumerator;
        try
        {
            enumerator = items.GetEnumerator();
        }
        catch (System.IO.IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        using (enumerator)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                bool next;
                try
                {
                    next = enumerator.MoveNext();
                }
                catch (System.IO.IOException)
                {
                    yield break;
                }
                catch (UnauthorizedAccessException)
                {
                    yield break;
                }

                if (!next)
                {
                    yield break;
                }

                yield return enumerator.Current;
            }
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
