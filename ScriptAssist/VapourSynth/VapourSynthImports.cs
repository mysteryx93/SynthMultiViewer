namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Places a top-level <c>import module</c> so an explorer insert is valid.
/// </summary>
public static class VapourSynthImports
{
    /// <summary>
    /// One prefix scan: whether <paramref name="module"/> still needs a column-0 import,
    /// and where to write it after shebang, encoding, docstring, and existing imports.
    /// A later non-module binding of the qualifier cannot be repaired and is not needed.
    /// </summary>
    public static ImportPlan Plan(string text, string module)
    {
        text ??= "";
        var scan = Scan(text, module);
        var needed = module.HasText() && !Present(scan, module) && !Occupied(scan, module);
        return new(needed, scan.Insert, needed ? Statement(text, scan.Insert, module) : "");
    }

    /// <summary>
    /// Returns whether a column-0 <c>import</c> currently binds <paramref name="module"/>.
    /// <c>from module import</c> does not count; that does not bind the module name.
    /// </summary>
    internal static bool Contains(string text, string module) =>
        module.HasText() && Present(Scan(text ?? "", module), module);

    /// <summary>
    /// Offset after the last complete column-0 header <c>import</c> / <c>from</c> statement,
    /// shebang, encoding cookie, or module docstring, or 0.
    /// </summary>
    internal static int InsertionOffset(string text) => Scan(text ?? "", "").Insert;

    /// <summary>
    /// An <c>import module</c> statement, with a leading newline when <paramref name="offset"/>
    /// does not follow a line break. Uses the document's newline sequence.
    /// </summary>
    internal static string Statement(string text, int offset, string module)
    {
        var nl = Newline(text);
        var line = "import " + module + nl;
        if (offset > 0 && text[offset - 1] is not ('\n' or '\r'))
        {
            return nl + line;
        }

        return line;
    }

    private static bool Present(ScanResult scan, string module)
    {
        var root = Root(module);
        return scan.Names.TryGetValue(root, out var path) && path != null && RefersTo(path, module);
    }

    private static bool Occupied(ScanResult scan, string module) =>
        scan.Names.TryGetValue(Root(module), out var path) && path == null;

    private static bool RefersTo(string path, string module) =>
        path.Equals(module, StringComparison.Ordinal) ||
        path.StartsWith(module + ".", StringComparison.Ordinal);

    private static string Root(string module)
    {
        var dot = module.IndexOf('.');
        return dot < 0 ? module : module[..dot];
    }

    private static ScanResult Scan(string text, string module)
    {
        var names = new Dictionary<string, string?>(StringComparer.Ordinal);
        var tracked = new HashSet<string>(StringComparer.Ordinal);
        if (module.HasText())
        {
            tracked.Add(Root(module));
        }

        var insert = 0;
        var header = true;
        var lineTop = false;
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
                lineTop = false;
                continue;
            }

            var col0 = IsColumn0(text, i);
            if (col0)
            {
                lineTop = true;
            }

            var start = i;
            var statement = ReadStatement(text, i);
            i = statement.Next;

            if (!lineTop)
            {
                continue;
            }

            if (header)
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
                    if (statement.Unclosed)
                    {
                        break;
                    }

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
                            names, tracked);
                    }

                    insert = statement.LineEnd;
                    continue;
                }

                header = false;
            }

            if (tracked.Count > 0)
            {
                ApplyShadow(text, start, statement.End, names, tracked);
            }
        }

        return new(insert, names);
    }

    private readonly record struct ScanResult(int Insert, Dictionary<string, string?> Names);

    private readonly record struct StatementRead(int End, int Next, int LineEnd, bool Unclosed);

    private static StatementRead ReadStatement(string text, int start)
    {
        var depth = 0;
        var quote = '\0';
        var triple = false;
        var comment = false;
        var commentAt = -1;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (comment)
            {
                if (c is '\n' or '\r')
                {
                    var next = AfterNewline(text, i);
                    return new(commentAt, next, next, false);
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
                commentAt = i;
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

        var end = commentAt >= 0 ? commentAt : text.Length;
        return new(end, text.Length, text.Length, depth > 0 || quote != '\0');
    }

    private static void ApplyImport(string text, int start, int end, Dictionary<string, string?> names,
        HashSet<string> tracked)
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

            if (!aliased && imported.Contains('.', StringComparison.Ordinal))
            {
                alias = imported[..imported.IndexOf('.')];
            }

            if (alias.Length > 0)
            {
                names[alias] = imported;
                tracked.Add(alias);
            }
        }
    }

    private static void ApplyShadow(string text, int start, int end, Dictionary<string, string?> names,
        HashSet<string> tracked)
    {
        SkipHorizontal(text, ref start);
        if (start >= end)
        {
            return;
        }

        if (IsKeyword(text, start, end, "async"))
        {
            start = AfterKeyword(text, start, end, "async");
            SkipHorizontal(text, ref start);
        }

        if (IsKeyword(text, start, end, "def") || IsKeyword(text, start, end, "class"))
        {
            var word = IsKeyword(text, start, end, "def") ? "def" : "class";
            var i = AfterKeyword(text, start, end, word);
            var at = i;
            if (VapourSynthBindingTargets.TryIdentSpan(text, ref i, end, out var length))
            {
                Mark(names, tracked, text, at, length);
            }

            return;
        }

        if (IsKeyword(text, start, end, "del"))
        {
            MarkSpans(names, tracked, text, AfterKeyword(text, start, end, "del"), end, null);
            return;
        }

        if (IsKeyword(text, start, end, "for"))
        {
            MarkSpans(names, tracked, text, AfterKeyword(text, start, end, "for"), end, "in");
            return;
        }

        if (IsKeyword(text, start, end, "with"))
        {
            MarkWith(names, tracked, text, start, end);
            return;
        }

        var eq = ParameterNames.TopLevelKeywordEquals(text, start, end);
        if (eq >= 0)
        {
            MarkSpans(names, tracked, text, start, eq, null);
        }
    }

    private static void MarkWith(Dictionary<string, string?> names, HashSet<string> tracked, string text,
        int start, int end)
    {
        var i = AfterKeyword(text, start, end, "with");
        while (i < end)
        {
            SkipHorizontal(text, ref i);
            if (i >= end || text[i] == ':')
            {
                return;
            }

            if (IsKeyword(text, i, end, "as"))
            {
                i = AfterKeyword(text, i, end, "as");
                var at = i;
                if (VapourSynthBindingTargets.TryIdentSpan(text, ref i, end, out var length))
                {
                    Mark(names, tracked, text, at, length);
                }

                continue;
            }

            if (text[i] is '"' or '\'')
            {
                VapourSynthBindingTargets.SkipString(text, ref i, end);
                continue;
            }

            if (text[i] is '(' or '[' or '{')
            {
                var close = text[i] == '(' ? ')' : text[i] == '[' ? ']' : '}';
                i = VapourSynthBindingTargets.SkipBalanced(text, i, end, text[i], close);
                continue;
            }

            i++;
        }
    }

    private static void MarkSpans(Dictionary<string, string?> names, HashSet<string> tracked, string text,
        int start, int end, string? stop)
    {
        foreach (var (at, length) in VapourSynthBindingTargets.Spans(text, start, end, stop))
        {
            Mark(names, tracked, text, at, length);
        }
    }

    private static void Mark(Dictionary<string, string?> names, HashSet<string> tracked, string text, int start,
        int length)
    {
        foreach (var name in tracked)
        {
            if (VapourSynthBindingTargets.SpanEquals(text, start, length, name))
            {
                names[name] = null;
                return;
            }
        }
    }

    private static IEnumerable<string> SplitList(string text, int start, int end)
    {
        var depth = 0;
        var from = start;
        for (var i = start; i < end; i++)
        {
            var c = text[i];
            if (c == '#')
            {
                if (from < i)
                {
                    yield return text[from..i];
                }

                yield break;
            }

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

    private static string Newline(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                return i + 1 < text.Length && text[i + 1] == '\n' ? "\r\n" : "\r";
            }

            if (text[i] == '\n')
            {
                return "\n";
            }
        }

        return Environment.NewLine;
    }
}
