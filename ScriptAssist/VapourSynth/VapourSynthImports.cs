namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Places a top-level <c>import module</c> so an explorer insert is valid.
/// </summary>
public static class VapourSynthImports
{
    /// <summary>
    /// One prefix scan: whether <paramref name="module"/> still needs a column-0 import,
    /// and where to write it after shebang, encoding, docstring, and existing imports.
    /// </summary>
    public static ImportPlan Plan(string text, string module)
    {
        text ??= "";
        var scan = Scan(text);
        var needed = module.HasText() && !Present(scan, module);
        return new(needed, scan.Insert, needed ? Statement(text, scan.Insert, module) : "");
    }

    /// <summary>
    /// Returns whether a column-0 <c>import</c> currently binds <paramref name="module"/>.
    /// <c>from module import</c> does not count; that does not bind the module name.
    /// </summary>
    public static bool Contains(string text, string module) =>
        module.HasText() && Present(Scan(text ?? ""), module);

    /// <summary>
    /// Offset after the last complete column-0 header <c>import</c> / <c>from</c> statement,
    /// shebang, encoding cookie, or module docstring, or 0.
    /// </summary>
    public static int InsertionOffset(string text) => Scan(text ?? "").Insert;

    /// <summary>
    /// An <c>import module</c> statement, with a leading newline when <paramref name="offset"/>
    /// does not follow a line break.
    /// </summary>
    public static string Statement(string text, int offset, string module)
    {
        var line = "import " + module + "\n";
        if (offset > 0 && text[offset - 1] != '\n')
        {
            return "\n" + line;
        }

        return line;
    }

    private static bool Present(ScanResult scan, string module)
    {
        if (scan.Names.TryGetValue(module, out var path) && path != null && RefersTo(path, module))
        {
            return true;
        }

        return module.Contains('.', StringComparison.Ordinal) && scan.Paths.Contains(module) &&
            (!scan.Names.TryGetValue(module, out var bound) || bound != null);
    }

    private static bool RefersTo(string path, string module) =>
        path.Equals(module, StringComparison.Ordinal) ||
        path.StartsWith(module + ".", StringComparison.Ordinal);

    private static ScanResult Scan(string text)
    {
        var names = new Dictionary<string, string?>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var insert = 0;
        var header = true;
        var i = 0;
        var line = 1;
        while (i < text.Length)
        {
            SkipHorizontal(text, ref i);
            if (i >= text.Length)
            {
                break;
            }

            if (text[i] is '\n' or '\r')
            {
                i = AfterNewline(text, i);
                line++;
                continue;
            }

            var col0 = IsColumn0(text, i);
            var start = i;
            var statement = ReadStatement(text, i);
            i = statement.Next;

            if (header && col0)
            {
                if (IsComment(text, start))
                {
                    if (line == 1 && StartsWith(text, start, "#!") ||
                        line <= 2 && IsEncoding(text, start, statement.End))
                    {
                        insert = statement.LineEnd;
                    }

                    continue;
                }

                if (IsString(text, start))
                {
                    insert = statement.LineEnd;
                    continue;
                }

                if (IsKeyword(text, start, statement.End, "import") ||
                    IsKeyword(text, start, statement.End, "from"))
                {
                    if (statement.Unclosed)
                    {
                        break;
                    }

                    if (IsKeyword(text, start, statement.End, "import"))
                    {
                        ApplyImport(text, AfterKeyword(text, start, statement.End, "import"), statement.End,
                            names, paths);
                    }

                    insert = statement.LineEnd;
                    continue;
                }

                header = false;
            }

            if (!header && col0)
            {
                ApplyAssignment(text, start, statement.End, names);
            }
        }

        return new(insert, names, paths);
    }

    private readonly record struct ScanResult(
        int Insert,
        Dictionary<string, string?> Names,
        HashSet<string> Paths);

    private readonly record struct StatementRead(int End, int Next, int LineEnd, bool Unclosed);

    private static StatementRead ReadStatement(string text, int start)
    {
        var depth = 0;
        var quote = '\0';
        var triple = false;
        var comment = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (comment)
            {
                if (c is '\n' or '\r')
                {
                    return new(i, AfterNewline(text, i), AfterNewline(text, i), false);
                }

                continue;
            }

            if (quote != '\0')
            {
                if (!triple && c is '\n' or '\r')
                {
                    quote = '\0';
                    if (depth == 0)
                    {
                        return new(i, AfterNewline(text, i), AfterNewline(text, i), false);
                    }

                    continue;
                }

                if (c == '\\' && i + 1 < text.Length)
                {
                    i++;
                    continue;
                }

                if (c == quote && TripleClose(text, ref i, quote, ref triple))
                {
                    quote = '\0';
                    triple = false;
                }

                continue;
            }

            if (c == '#')
            {
                comment = true;
                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                triple = IsTriple(text, i, c);
                if (triple)
                {
                    i += 2;
                }

                continue;
            }

            if (c is '(' or '[' or '{')
            {
                depth++;
                continue;
            }

            if (c is ')' or ']' or '}' && depth > 0)
            {
                depth--;
                continue;
            }

            if (c == ';' && depth == 0)
            {
                return new(i, i + 1, AfterPhysicalLine(text, i), false);
            }

            if (c is '\n' or '\r')
            {
                if (Continues(text, i))
                {
                    continue;
                }

                if (depth == 0)
                {
                    var next = AfterNewline(text, i);
                    return new(i, next, next, false);
                }
            }
        }

        return new(text.Length, text.Length, text.Length, depth > 0 || quote != '\0');
    }

    private static void ApplyImport(string text, int start, int end, Dictionary<string, string?> names,
        HashSet<string> paths)
    {
        foreach (var part in SplitList(text, start, end))
        {
            var spec = part.Trim();
            if (spec.Length == 0)
            {
                continue;
            }

            var aliased = ParameterNames.TryAlias(spec, out var imported, out var alias);
            imported = imported.Trim();
            alias = alias.Trim();
            if (imported.Length == 0)
            {
                continue;
            }

            paths.Add(imported);
            if (!aliased && imported.Contains('.', StringComparison.Ordinal))
            {
                alias = imported[..imported.IndexOf('.')];
            }

            if (alias.Length > 0)
            {
                names[alias] = imported;
            }
        }
    }

    private static void ApplyAssignment(string text, int start, int end, Dictionary<string, string?> names)
    {
        var i = start;
        SkipHorizontal(text, ref i);
        if (!TryIdent(text, ref i, end, out var name))
        {
            return;
        }

        SkipHorizontal(text, ref i);
        if (i < end && text[i] == ':')
        {
            i++;
            while (i < end && text[i] != '=')
            {
                if (text[i] is '"' or '\'')
                {
                    SkipString(text, ref i, end);
                    continue;
                }

                i++;
            }
        }

        if (i < end && text[i] == '=' && (i + 1 >= end || text[i + 1] != '='))
        {
            names[name] = null;
        }
    }

    private static IEnumerable<string> SplitList(string text, int start, int end)
    {
        var depth = 0;
        var from = start;
        for (var i = start; i < end; i++)
        {
            var c = text[i];
            if (c is '"' or '\'')
            {
                SkipString(text, ref i, end);
                continue;
            }

            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}' && depth > 0)
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                yield return text[from..i];
                from = i + 1;
            }
        }

        if (from < end)
        {
            yield return text[from..end];
        }
    }

    private static bool IsKeyword(string text, int start, int end, string word)
    {
        SkipHorizontal(text, ref start);
        if (end - start < word.Length ||
            !text.AsSpan(start, word.Length).Equals(word, StringComparison.Ordinal))
        {
            return false;
        }

        var after = start + word.Length;
        return after >= end || !BufferLexer.IsIdentifier(text[after]);
    }

    private static int AfterKeyword(string text, int start, int end, string word)
    {
        SkipHorizontal(text, ref start);
        start += word.Length;
        SkipHorizontal(text, ref start);
        return Math.Min(start, end);
    }

    private static bool IsComment(string text, int start)
    {
        SkipHorizontal(text, ref start);
        return start < text.Length && text[start] == '#';
    }

    private static bool IsString(string text, int start)
    {
        SkipHorizontal(text, ref start);
        if (start < text.Length && text[start] is 'f' or 'r' or 'b' or 'u' or 'F' or 'R' or 'B' or 'U')
        {
            start++;
            if (start < text.Length && text[start] is 'r' or 'f' or 'b' or 'R' or 'F' or 'B')
            {
                start++;
            }
        }

        return start < text.Length && text[start] is '"' or '\'';
    }

    private static bool IsEncoding(string text, int start, int end)
    {
        SkipHorizontal(text, ref start);
        if (start >= end || text[start] != '#')
        {
            return false;
        }

        var comment = text[start..end];
        return comment.Contains("coding:", StringComparison.OrdinalIgnoreCase) ||
            comment.Contains("coding=", StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWith(string text, int start, string value)
    {
        SkipHorizontal(text, ref start);
        return start + value.Length <= text.Length &&
            text.AsSpan(start, value.Length).Equals(value, StringComparison.Ordinal);
    }

    private static bool IsColumn0(string text, int i)
    {
        while (i > 0 && text[i - 1] is ' ' or '\t')
        {
            i--;
        }

        return i == 0 || text[i - 1] is '\n' or '\r';
    }

    private static void SkipHorizontal(string text, ref int i)
    {
        while (i < text.Length && text[i] is ' ' or '\t' or '\f')
        {
            i++;
        }
    }

    private static int AfterNewline(string text, int i)
    {
        if (i < text.Length && text[i] == '\r')
        {
            i++;
        }

        if (i < text.Length && text[i] == '\n')
        {
            i++;
        }

        return i;
    }

    private static int AfterPhysicalLine(string text, int i)
    {
        while (i < text.Length && text[i] is not ('\n' or '\r'))
        {
            i++;
        }

        return AfterNewline(text, i);
    }

    private static bool Continues(string text, int newline)
    {
        var i = newline - 1;
        while (i >= 0 && text[i] is ' ' or '\t')
        {
            i--;
        }

        return i >= 0 && text[i] == '\\';
    }

    private static bool IsTriple(string text, int i, char quote) =>
        i + 2 < text.Length && text[i + 1] == quote && text[i + 2] == quote;

    private static bool TripleClose(string text, ref int i, char quote, ref bool triple)
    {
        if (!triple)
        {
            return true;
        }

        if (i + 2 < text.Length && text[i + 1] == quote && text[i + 2] == quote)
        {
            i += 2;
            return true;
        }

        return false;
    }

    private static void SkipString(string text, ref int i, int end)
    {
        if (i >= end)
        {
            return;
        }

        var quote = text[i];
        var triple = IsTriple(text, i, quote);
        i += triple ? 3 : 1;
        while (i < end)
        {
            var c = text[i];
            if (c == '\\' && i + 1 < end)
            {
                i += 2;
                continue;
            }

            if (c == quote)
            {
                if (!triple)
                {
                    i++;
                    return;
                }

                if (i + 2 < end && text[i + 1] == quote && text[i + 2] == quote)
                {
                    i += 3;
                    return;
                }
            }

            i++;
        }
    }

    private static bool TryIdent(string text, ref int i, int end, out string name)
    {
        var start = i;
        if (i >= end || !BufferLexer.IsIdentifier(text[i]) || char.IsDigit(text[i]))
        {
            name = "";
            return false;
        }

        i++;
        while (i < end && BufferLexer.IsIdentifier(text[i]))
        {
            i++;
        }

        name = text[start..i];
        return true;
    }
}
