using System.Text.RegularExpressions;

namespace HanumanInstitute.ScriptAssist.AviSynth;

/// <summary>
/// Parses AviSynth <c>function Name(...)</c> headers from script text.
/// </summary>
public static class AviSynthFunctions
{
    /// <summary>
    /// Returns declared functions; comments are ignored.
    /// </summary>
    public static IReadOnlyList<Symbol> Parse(string text, LexerOptions lexer, CancellationToken token = default)
    {
        var spans = new List<Symbol>();
        foreach (var span in Spans(text, lexer, token))
        {
            spans.Add(span.Symbol);
        }

        return spans;
    }

    /// <summary>
    /// Returns declared functions with header offsets; comments and line continuations are ignored.
    /// </summary>
    internal static IReadOnlyList<AviSynthFunctionSpan> Spans(string text, LexerOptions lexer,
        CancellationToken token = default)
    {
        var clean = AviSynthPatterns.Clean(text, lexer, token: token);
        var quoted = AviSynthPatterns.Clean(text, lexer, maskStrings: false, token: token);
        return Spans(clean, quoted, token);
    }

    internal static IReadOnlyList<AviSynthFunctionSpan> Spans(string clean, string quoted,
        CancellationToken token = default)
    {
        var buffer = new List<AviSynthFunctionSpan>();
        var matches = AviSynthPatterns.Functions().Matches(clean);
        for (var i = 0; i < matches.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var match = matches[i];
            var open = match.Index + match.Length - 1;
            var limit = i + 1 < matches.Count ? matches[i + 1].Index : clean.Length;
            var close = FunctionHeaders.MatchingClose(clean, open, limit, token);
            if (close < 0)
            {
                close = -1;
            }

            var end = close < 0 ? limit : close;
            if (end <= open)
            {
                continue;
            }

            var inside = AviSynthPatterns.Whitespace().Replace(quoted[(open + 1)..end], " ");
            buffer.Add(new(new(match.Groups[1].Value, ParameterNames.Split(inside)),
                match.Index, close < 0 ? Math.Max(open, end - 1) : close));
        }

        return buffer;
    }

    /// <summary>
    /// Follows <c>Import</c> specifiers with <paramref name="read"/> and returns parsed functions.
    /// </summary>
    public static IReadOnlyList<Symbol> LoadImports(string text, string? documentPath, IIncludeSource? read,
        LexerOptions lexer, CancellationToken token = default)
    {
        var buffer = new List<Symbol>();
        AddImports(text, documentPath, read, buffer, new(StringComparer.Ordinal), lexer, token);
        return buffer;
    }


    /// <summary>
    /// Follows <c>Import</c> specifiers with <paramref name="read"/> and appends parsed functions.
    /// </summary>
    public static void AddImports(string text, string? documentPath, IIncludeSource? read, List<Symbol> buffer,
        HashSet<string> visited, LexerOptions lexer, CancellationToken token) =>
        AddImports(text, documentPath, read, buffer, visited, lexer, token, new IncludeCache());

    internal static void AddImports(string text, string? documentPath, IIncludeSource? read, List<Symbol> buffer,
        HashSet<string> visited, LexerOptions lexer, CancellationToken token, IncludeCache? includes) =>
        AddImports(text, documentPath, read, buffer, visited, lexer, token, new IncludeSession(includes));

    internal static void AddImports(string clean, string quoted, string? documentPath, IIncludeSource? read,
        List<Symbol> buffer, HashSet<string> visited, LexerOptions lexer, CancellationToken token,
        IncludeCache? includes)
    {
        if (read == null)
        {
            return;
        }

        var ensuring = new HashSet<string>(StringComparer.Ordinal);
        var session = new IncludeSession(includes);
        foreach (var specifier in ImportSpecifiers(clean, quoted, token))
        {
            LoadSpecifier(specifier, documentPath, read, buffer, visited, ensuring, lexer, token, session);
        }

        session.Finish(documentPath);
    }

    private static void AddImports(string text, string? documentPath, IIncludeSource? read, List<Symbol> buffer,
        HashSet<string> visited, LexerOptions lexer, CancellationToken token, IncludeSession includes)
    {
        if (read == null)
        {
            return;
        }

        var ensuring = new HashSet<string>(StringComparer.Ordinal);
        foreach (var specifier in ImportSpecifiers(text, lexer, token))
        {
            LoadSpecifier(specifier, documentPath, read, buffer, visited, ensuring, lexer, token, includes);
        }

        includes.Finish(documentPath);
    }

    private static void LoadSpecifier(string specifier, string? fromPath, IIncludeSource read, List<Symbol> buffer,
        HashSet<string> visited, HashSet<string> ensuring, LexerOptions lexer, CancellationToken token,
        IncludeSession includes)
    {
        token.ThrowIfCancellationRequested();
        if (includes.TryPath(specifier, fromPath, out var path) &&
            (path == null || EntryComplete(path, includes, token)))
        {
            if (path != null)
            {
                Expand(path, buffer, visited, includes);
            }

            return;
        }

        if (!includes.TryImport())
        {
            return;
        }

        var file = read.Read(specifier, fromPath);
        if (file == null)
        {
            includes.SetPath(specifier, fromPath, null);
            return;
        }

        EnsureCached(file.Value.Path, file.Value.Text, file.Value.Path, read, ensuring, lexer, token, includes, 0);
        includes.SetPath(specifier, fromPath, file.Value.Path);
        Expand(file.Value.Path, buffer, visited, includes);
    }

    private static void EnsureCached(string path, string text, string fromPath, IIncludeSource read,
        HashSet<string> ensuring, LexerOptions lexer, CancellationToken token, IncludeSession includes,
        int depth)
    {
        if (EntryComplete(path, includes, token) || !ensuring.Add(path))
        {
            return;
        }

        if (depth >= IncludeCache.ImportDepthLimit)
        {
            return;
        }

        token.ThrowIfCancellationRequested();
        var own = Parse(text, lexer, token);
        var deps = new List<string>();
        foreach (var specifier in ImportSpecifiers(text, lexer, token))
        {
            token.ThrowIfCancellationRequested();
            if (includes.TryPath(specifier, fromPath, out var depPath) &&
                (depPath == null || EntryComplete(depPath, includes, token)))
            {
                if (depPath != null)
                {
                    deps.Add(depPath);
                }

                continue;
            }

            if (!includes.TryImport())
            {
                break;
            }

            var file = read.Read(specifier, fromPath);
            if (file == null)
            {
                includes.SetPath(specifier, fromPath, null);
                continue;
            }

            EnsureCached(file.Value.Path, file.Value.Text, file.Value.Path, read, ensuring, lexer, token, includes,
                depth + 1);
            includes.SetPath(specifier, fromPath, file.Value.Path);
            deps.Add(file.Value.Path);
        }

        if (!includes.Limited)
        {
            includes.SetEntry(path, new(own, deps));
        }
    }

    private static bool EntryComplete(string path, IncludeSession includes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (includes.Complete.Contains(path))
        {
            return true;
        }

        var walking = new HashSet<string>(StringComparer.Ordinal);
        if (!WalkComplete(path, includes, walking, token, 0))
        {
            return false;
        }

        foreach (var item in walking)
        {
            includes.Complete.Add(item);
        }

        return true;
    }

    private static bool WalkComplete(string path, IncludeSession includes, HashSet<string> walking,
        CancellationToken token, int depth)
    {
        token.ThrowIfCancellationRequested();
        if (depth >= IncludeCache.ImportDepthLimit)
        {
            return false;
        }

        if (includes.Complete.Contains(path) || !walking.Add(path))
        {
            return true;
        }

        if (!includes.TryEntry(path, out var entry))
        {
            return false;
        }

        foreach (var dep in entry.Dependencies)
        {
            if (!WalkComplete(dep, includes, walking, token, depth + 1))
            {
                return false;
            }
        }

        return true;
    }

    private static void Expand(string path, List<Symbol> buffer, HashSet<string> visited, IncludeSession includes,
        int depth = 0)
    {
        if (depth >= IncludeCache.ImportDepthLimit || !visited.Add(path) ||
            !includes.TryEntry(path, out var entry))
        {
            return;
        }

        buffer.AddRange(entry.Members);
        foreach (var dep in entry.Dependencies)
        {
            Expand(dep, buffer, visited, includes, depth + 1);
        }
    }

    private static IEnumerable<string> ImportSpecifiers(string text, LexerOptions lexer, CancellationToken token)
    {
        var quoted = AviSynthPatterns.Clean(text, lexer, maskStrings: false, token: token);
        var clean = AviSynthPatterns.Clean(text, lexer, token: token);
        return ImportSpecifiers(clean, quoted, token);
    }

    private static IEnumerable<string> ImportSpecifiers(string clean, string quoted, CancellationToken token)
    {
        foreach (Match match in AviSynthPatterns.Import().Matches(quoted))
        {
            token.ThrowIfCancellationRequested();
            if (match.Index >= clean.Length || !char.IsLetter(clean[match.Index]))
            {
                continue;
            }

            yield return match.Groups[1].Success && match.Groups[1].Length > 0
                ? match.Groups[1].Value
                : match.Groups[2].Value.Replace("\"\"", "\"", StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Merges parsed user-function headers into a native catalog; compiled plugins keep native signatures.
    /// </summary>
    public static IReadOnlyList<Symbol> UnionByName(IReadOnlyList<Symbol> native, IReadOnlyList<Symbol> parsed)
    {
        var result = new List<Symbol>(native.Count + parsed.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parsedLookup = parsed.ToLookup(symbol => symbol.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var group in native.GroupBy(symbol => symbol.Name, StringComparer.OrdinalIgnoreCase))
        {
            seen.Add(group.Key);
            var parsedGroup = parsedLookup[group.Key].ToList();
            var nativeGroup = group.ToList();
            result.AddRange(EnrichGroup(nativeGroup, parsedGroup));
        }

        foreach (var group in parsed.GroupBy(symbol => symbol.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (seen.Add(group.Key))
            {
                result.AddRange(group);
            }
        }

        return result;
    }

    private static IReadOnlyList<Symbol> EnrichGroup(List<Symbol> nativeGroup, List<Symbol> parsedGroup)
    {
        if (parsedGroup.Count == 0)
        {
            return nativeGroup;
        }

        if (nativeGroup.Count == 1 && parsedGroup.Count == 1 && NamedCount(nativeGroup[0]) == 0 &&
            NamedCount(parsedGroup[0]) > 0 && IsIncompletePrefix(nativeGroup[0], parsedGroup[0]))
        {
            return parsedGroup;
        }

        var used = new HashSet<int>();
        var enriched = new List<Symbol>(nativeGroup.Count);
        foreach (var nativeSymbol in nativeGroup)
        {
            var match = -1;
            if (NamedCount(nativeSymbol) == 0)
            {
                for (var i = 0; i < parsedGroup.Count; i++)
                {
                    if (used.Contains(i) || NamedCount(parsedGroup[i]) == 0 ||
                        !SameShape(nativeSymbol, parsedGroup[i]))
                    {
                        continue;
                    }

                    match = i;
                    break;
                }
            }

            if (match >= 0)
            {
                used.Add(match);
                enriched.Add(parsedGroup[match]);
            }
            else
            {
                enriched.Add(nativeSymbol);
            }
        }

        return enriched;
    }

    private static bool IsIncompletePrefix(Symbol native, Symbol parsed)
    {
        var a = native.Parameters ?? [];
        var b = parsed.Parameters ?? [];
        if (a.Length == 0 || a.Length >= b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (!TypeKey(a[i]).Equals(TypeKey(b[i]), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameShape(Symbol left, Symbol right)
    {
        var a = left.Parameters ?? [];
        var b = right.Parameters ?? [];
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (!TypeKey(a[i]).Equals(TypeKey(b[i]), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string TypeKey(string parameter)
    {
        var text = parameter.Trim();
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "";
        }

        return parts[0];
    }

    private static int NamedCount(Symbol symbol)
    {
        if (symbol.Parameters == null)
        {
            return 0;
        }

        var count = 0;
        foreach (var parameter in symbol.Parameters)
        {
            var name = ParameterNames.OfAviSynth(parameter);
            if (name != null && !parameter.Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }
}

/// <summary>
/// A parsed <c>function</c> header and the offset of its closing <c>)</c>.
/// </summary>
internal readonly record struct AviSynthFunctionSpan(Symbol Symbol, int Start, int ParenClose);