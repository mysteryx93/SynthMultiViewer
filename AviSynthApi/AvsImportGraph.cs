namespace HanumanInstitute.AviSynthApi;

/// <summary>
/// Walks <c>Import</c> files before native evaluation so a cycle cannot hang AviSynth.
/// </summary>
internal static class AvsImportGraph
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// Reads literal Import arguments outside strings and comments.
    /// Computed paths remain the native evaluator's responsibility.
    /// </summary>
    internal static IEnumerable<string> GetImports(string script)
    {
        var index = 0;
        while (index < script.Length)
        {
            SkipTrivia(script, ref index);
            if (index >= script.Length) { break; }
            if (script[index] == '"')
            {
                ReadString(script, ref index);
                continue;
            }

            if (!char.IsLetter(script[index]) && script[index] != '_')
            {
                index++;
                continue;
            }

            var start = index++;
            while (index < script.Length && (char.IsLetterOrDigit(script[index]) || script[index] == '_')) { index++; }
            if (!script.AsSpan(start, index - start).Equals("Import", StringComparison.OrdinalIgnoreCase)) { continue; }

            SkipTrivia(script, ref index);
            if (index >= script.Length || script[index] != '(') { continue; }

            index++;
            SkipTrivia(script, ref index);
            if (index >= script.Length || script[index] != '"') { continue; }

            while (index < script.Length && script[index] == '"')
            {
                var path = ReadString(script, ref index);
                SkipTrivia(script, ref index);
                if (path != null && index < script.Length && script[index] is ')' or ',') { yield return path; }
                if (index >= script.Length || script[index] != ',') { break; }
                index++;
                SkipTrivia(script, ref index);
            }
        }
    }

    // Skip AviSynth line comments, block comments, and nested [* *] comments.
    private static void SkipTrivia(string script, ref int index)
    {
        while (index < script.Length)
        {
            if (char.IsWhiteSpace(script[index])) { index++; continue; }
            if (script[index] == '#')
            {
                while (index < script.Length && script[index] != '\n') { index++; }
                continue;
            }

            if (script.AsSpan(index).StartsWith("/*", StringComparison.Ordinal))
            {
                var end = script.IndexOf("*/", index + 2, StringComparison.Ordinal);
                index = end < 0 ? script.Length : end + 2;
                continue;
            }

            if (script.AsSpan(index).StartsWith("[*", StringComparison.Ordinal))
            {
                index += 2;
                var depth = 1;
                while (index < script.Length && depth > 0)
                {
                    if (script.AsSpan(index).StartsWith("[*", StringComparison.Ordinal)) { depth++; index += 2; }
                    else if (script.AsSpan(index).StartsWith("*]", StringComparison.Ordinal)) { depth--; index += 2; }
                    else { index++; }
                }
                continue;
            }

            break;
        }
    }

    // Consume an ordinary or triple-quoted string without scanning its contents as code.
    private static string? ReadString(string script, ref int index)
    {
        var delimiter = script.AsSpan(index).StartsWith("\"\"\"", StringComparison.Ordinal) ? "\"\"\"" : "\"";
        index += delimiter.Length;
        var start = index;
        var end = script.IndexOf(delimiter, start, StringComparison.Ordinal);
        index = end < 0 ? script.Length : end + delimiter.Length;
        return end < 0 ? null : script[start..end];
    }

    /// <summary>
    /// Throws when an <c>Import</c> re-enters a file already being loaded.
    /// </summary>
    public static void ThrowIfRecursive(string script, string? scriptPath)
    {
        var stack = new List<string>();
        var done = new HashSet<string>(PathComparer);
        if (scriptPath.HasText())
        {
            try
            {
                var root = Path.GetFullPath(scriptPath);
                stack.Add(root);
                done.Add(root);
            }
            catch (ArgumentException)
            {
            }
        }

        Walk(script, scriptPath, stack, done);
    }

    // Follow literal imports while keeping an active stack separate from visited files.
    private static void Walk(string script, string? fromPath, List<string> stack, HashSet<string> done)
    {
        foreach (var specifier in GetImports(script))
        {
            var path = Resolve(specifier, fromPath);
            if (path == null)
            {
                continue;
            }

            if (stack.Contains(path, PathComparer))
            {
                throw new AvsException("AviSynth Import cycle: " + path);
            }

            if (!done.Add(path))
            {
                continue;
            }

            string nested;
            try
            {
                nested = File.ReadAllText(path);
            }
            catch (System.IO.IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            stack.Add(path);
            Walk(nested, path, stack, done);
            stack.RemoveAt(stack.Count - 1);
        }
    }

    // Resolve native import candidates before opening them for the cycle check.
    private static string? Resolve(string specifier, string? fromPath)
    {
        specifier = specifier.Trim();
        if (specifier.Length == 0)
        {
            return null;
        }

        try
        {
            var combined = Path.IsPathRooted(specifier) || !fromPath.HasText()
                ? specifier
                : Path.Combine(Path.GetDirectoryName(fromPath) ?? "", specifier);
            var full = Path.GetFullPath(combined);
            return File.Exists(full) ? full : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
