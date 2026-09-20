namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Class body ranges and indent-based block ends for def/class headers.
/// </summary>
internal static class VapourSynthClassRanges
{
    public static List<(int Start, int End)> ClassRanges(string clean, string quoted,
        IReadOnlyList<StatementScanner.Span> statements)
    {
        var ranges = new List<(int Start, int End)>();
        for (var i = 0; i < statements.Count; i++)
        {
            var span = statements[i];
            if (!TryClass(quoted, clean, span, out var headerEnd))
            {
                continue;
            }

            ranges.Add((span.Start, BlockEnd(clean, statements, i, span, headerEnd)));
        }

        return ranges;
    }

    public static bool DirectlyInClass(int offset, IReadOnlyList<(int Start, int End)> classes,
        BindingScope? inner)
    {
        foreach (var range in classes)
        {
            if (offset <= range.Start || offset > range.End)
            {
                continue;
            }

            return inner == null || inner.Start <= range.Start;
        }

        return false;
    }

    public static bool EnclosedByClass(BindingScope self, IReadOnlyList<(int Start, int End)> classes)
    {
        var parent = self.Enclosing;
        foreach (var range in classes)
        {
            if (self.Start <= range.Start || self.Start > range.End)
            {
                continue;
            }

            return parent == null || range.Start >= parent.Start;
        }

        return false;
    }

    public static int HeaderColon(string text, int parenClose)
    {
        var i = parenClose + 1;
        while (i < text.Length && text[i] is ' ' or '\t')
        {
            i++;
        }

        if (i + 1 < text.Length && text[i] == '-' && text[i + 1] == '>')
        {
            i += 2;
            while (i < text.Length && text[i] is not ':' and not '\n' and not '\r')
            {
                i++;
            }
        }

        while (i < text.Length && text[i] is not ':' and not '\n' and not '\r')
        {
            i++;
        }

        return i < text.Length && text[i] == ':' ? i + 1 : parenClose + 1;
    }

    public static int BlockEnd(string text, IReadOnlyList<StatementScanner.Span> statements, int defIndex,
        StatementScanner.Span def, int headerEnd)
    {
        var i = headerEnd;
        while (i < def.End && i < text.Length && text[i] is ' ' or '\t')
        {
            i++;
        }

        if (i < def.End && i < text.Length && text[i] is not '\n' and not '\r' and not '#')
        {
            var line = LineStart(text, def.Start);
            var end = def.End;
            for (var n = defIndex + 1; n < statements.Count; n++)
            {
                if (LineStart(text, statements[n].Start) != line)
                {
                    break;
                }

                end = statements[n].End;
            }

            return end;
        }

        var defIndent = IndentAt(text, def.Start);
        var bodyIndent = -1;
        for (var n = defIndex + 1; n < statements.Count; n++)
        {
            var statement = statements[n];
            var indent = IndentAt(text, statement.Start);
            if (bodyIndent < 0)
            {
                if (indent <= defIndent)
                {
                    var start = LineStart(text, statement.Start);
                    return start == 0 ? headerEnd : start - 1;
                }

                bodyIndent = indent;
                continue;
            }

            if (indent < bodyIndent)
            {
                var start = LineStart(text, statement.Start);
                return start == 0 ? headerEnd : start - 1;
            }
        }

        return text.Length;
    }

    public static int IndentAt(string text, int offset) => LineIndent(text, LineStart(text, offset));

    public static Symbol ClassExport(string quoted, string clean, StatementScanner.Span span,
        IReadOnlyList<(int Start, int End)> classes, IReadOnlyList<StatementScanner.Span> statements,
        IReadOnlyList<BindingScope> scopes, string name)
    {
        var parameters = ConstructorParameters(clean, span.Start, classes, statements, scopes);
        if (parameters != null)
        {
            return new(name, parameters);
        }

        if (IsDataclass(quoted, clean, span.Start, statements))
        {
            return new(name, DataclassParameters(quoted, clean, span.Start, classes, statements) ?? []);
        }

        if (!IsTypeNameClass(quoted, span.Start, span.End))
        {
            return new(name, null);
        }

        return new(name, TypeMembers(quoted, clean, span.Start, classes, statements), SymbolKind.Namespace);
    }

    public static string[]? ConstructorParameters(string clean, int classStart,
        IReadOnlyList<(int Start, int End)> classes, IReadOnlyList<StatementScanner.Span> statements,
        IReadOnlyList<BindingScope> scopes)
    {
        if (!TryClassRange(classStart, classes, out var classEnd, out var bodyIndent, statements, clean))
        {
            return null;
        }

        foreach (var scope in scopes)
        {
            if (scope.Name != "__init__" || scope.Start <= classStart || scope.Start > classEnd)
            {
                continue;
            }

            if (IndentAt(clean, scope.Start) != bodyIndent)
            {
                continue;
            }

            return WithoutReceiver(scope.Parameters);
        }

        return null;
    }

    private static bool TryClassRange(int classStart, IReadOnlyList<(int Start, int End)> classes,
        out int classEnd, out int bodyIndent, IReadOnlyList<StatementScanner.Span> statements, string clean)
    {
        classEnd = -1;
        bodyIndent = -1;
        foreach (var range in classes)
        {
            if (range.Start != classStart)
            {
                continue;
            }

            classEnd = range.End;
            break;
        }

        if (classEnd < 0)
        {
            return false;
        }

        var classIndent = IndentAt(clean, classStart);
        foreach (var span in statements)
        {
            if (span.Start <= classStart || span.Start > classEnd)
            {
                continue;
            }

            var indent = IndentAt(clean, span.Start);
            if (indent <= classIndent)
            {
                continue;
            }

            if (bodyIndent < 0 || indent < bodyIndent)
            {
                bodyIndent = indent;
            }
        }

        return bodyIndent >= 0;
    }

    private static bool IsDataclass(string quoted, string clean, int classStart,
        IReadOnlyList<StatementScanner.Span> statements)
    {
        var classIndent = IndentAt(clean, classStart);
        var index = 0;
        while (index < statements.Count && statements[index].Start != classStart)
        {
            index++;
        }

        for (var n = index - 1; n >= 0; n--)
        {
            var span = statements[n];
            if (IndentAt(clean, span.Start) != classIndent)
            {
                return false;
            }

            var i = span.Start;
            VapourSynthBinder.SkipWs(quoted, ref i, span.End);
            if (i >= span.End || quoted[i] != '@')
            {
                return false;
            }

            if (DecoratorIsDataclass(quoted, i + 1, span.End))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DecoratorIsDataclass(string quoted, int start, int end)
    {
        var i = start;
        VapourSynthBinder.SkipWs(quoted, ref i, end);
        var last = "";
        while (VapourSynthBinder.TryIdent(quoted, ref i, end, out var part))
        {
            last = part;
            VapourSynthBinder.SkipWs(quoted, ref i, end);
            if (i >= end || quoted[i] != '.')
            {
                break;
            }

            i++;
            VapourSynthBinder.SkipWs(quoted, ref i, end);
        }

        return last == "dataclass";
    }

    private static string[]? DataclassParameters(string quoted, string clean, int classStart,
        IReadOnlyList<(int Start, int End)> classes, IReadOnlyList<StatementScanner.Span> statements)
    {
        if (!TryClassRange(classStart, classes, out var classEnd, out var bodyIndent, statements, clean))
        {
            return [];
        }

        var fields = new List<string>();
        foreach (var span in statements)
        {
            if (span.Start <= classStart || span.Start > classEnd)
            {
                continue;
            }

            if (IndentAt(clean, span.Start) != bodyIndent)
            {
                continue;
            }

            if (VapourSynthBinder.Keyword(quoted, span.Start, span.End, "def") ||
                VapourSynthBinder.Keyword(quoted, span.Start, span.End, "class") ||
                VapourSynthBinder.Keyword(quoted, span.Start, span.End, "async"))
            {
                continue;
            }

            var i = span.Start;
            if (!VapourSynthBinder.TryIdent(quoted, ref i, span.End, out var name) || name.StartsWith('_'))
            {
                continue;
            }

            VapourSynthBinder.SkipWs(quoted, ref i, span.End);
            if (i >= span.End || quoted[i] is not ('=' or ':'))
            {
                continue;
            }

            var end = span.End;
            while (end > span.Start && char.IsWhiteSpace(quoted[end - 1]))
            {
                end--;
            }

            fields.Add(quoted[span.Start..end].Trim());
        }

        return fields.Count == 0 ? [] : [..fields];
    }

    private static string[]? TypeMembers(string quoted, string clean, int classStart,
        IReadOnlyList<(int Start, int End)> classes, IReadOnlyList<StatementScanner.Span> statements)
    {
        if (!TryClassRange(classStart, classes, out var classEnd, out var bodyIndent, statements, clean))
        {
            return null;
        }

        var names = new List<string>();
        foreach (var span in statements)
        {
            if (span.Start <= classStart || span.Start > classEnd)
            {
                continue;
            }

            if (IndentAt(clean, span.Start) != bodyIndent)
            {
                continue;
            }

            if (VapourSynthBinder.Keyword(quoted, span.Start, span.End, "def") ||
                VapourSynthBinder.Keyword(quoted, span.Start, span.End, "class") ||
                VapourSynthBinder.Keyword(quoted, span.Start, span.End, "async"))
            {
                continue;
            }

            var i = span.Start;
            if (!VapourSynthBinder.TryIdent(quoted, ref i, span.End, out var name) || name.StartsWith('_'))
            {
                continue;
            }

            VapourSynthBinder.SkipWs(quoted, ref i, span.End);
            if (i >= span.End || quoted[i] is not ('=' or ':'))
            {
                continue;
            }

            names.Add(name);
        }

        return names.Count == 0 ? null : [..names];
    }

    private static bool IsTypeNameClass(string quoted, int start, int end)
    {
        var i = VapourSynthBinder.AfterKeyword(quoted, start, end, "class");
        if (!VapourSynthBinder.TryIdent(quoted, ref i, end, out _))
        {
            return false;
        }

        VapourSynthBinder.SkipWs(quoted, ref i, end);
        if (i >= end || quoted[i] != '(')
        {
            return false;
        }

        var close = FunctionHeaders.MatchingClose(quoted, i, end);
        return close > i && HasTypeNameBase(quoted, i + 1, close);
    }

    private static bool HasTypeNameBase(string text, int start, int end)
    {
        var depth = 0;
        var ident = -1;
        var last = "";
        for (var i = start; i <= end; i++)
        {
            var c = i < end ? text[i] : ',';
            if (depth == 0 && ident >= 0 && (i == end || !BufferLexer.IsIdentifier(c)))
            {
                last = text[ident..i];
                ident = -1;
            }

            if (c is '[' or '(')
            {
                depth++;
                continue;
            }

            if (c is ']' or ')')
            {
                if (depth > 0)
                {
                    depth--;
                }

                continue;
            }

            if (depth != 0)
            {
                continue;
            }

            if (i < end && BufferLexer.IsIdentifier(c) && (ident >= 0 || c is < '0' or > '9'))
            {
                if (ident < 0)
                {
                    ident = i;
                }

                continue;
            }

            if (c == ',' && IsTypeNameBase(last))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTypeNameBase(string name) =>
        name.Length > 0 && (name.EndsWith("Enum", StringComparison.Ordinal) ||
            name.EndsWith("Error", StringComparison.Ordinal) ||
            name is "IntFlag" or "Flag" or "TypedDict" or "NamedTuple" or "ABC" or "Protocol");

    private static string[] WithoutReceiver(IReadOnlyList<string> parameters)
    {
        var skip = 0;
        if (parameters.Count > 0)
        {
            var first = ParameterNames.LocalName(parameters[0]);
            if (first is "self" or "cls")
            {
                skip = 1;
            }
        }

        if (skip == 0 && parameters is string[] exact)
        {
            return exact;
        }

        var rest = new string[parameters.Count - skip];
        for (var i = 0; i < rest.Length; i++)
        {
            rest[i] = parameters[i + skip];
        }

        return rest;
    }

    private static bool TryClass(string quoted, string clean, StatementScanner.Span span, out int headerEnd)
    {
        headerEnd = span.Start;
        if (!VapourSynthBinder.Keyword(quoted, span.Start, span.End, "class"))
        {
            return false;
        }

        var i = VapourSynthBinder.AfterKeyword(quoted, span.Start, span.End, "class");
        if (!VapourSynthBinder.TryIdent(quoted, ref i, span.End, out _))
        {
            return false;
        }

        VapourSynthBinder.SkipWs(quoted, ref i, span.End);
        var close = i > 0 ? i - 1 : 0;
        if (i < span.End && quoted[i] == '(')
        {
            var match = FunctionHeaders.MatchingClose(clean, i, span.End);
            if (match < 0)
            {
                return false;
            }

            close = match;
        }

        headerEnd = HeaderColon(clean, close);
        return headerEnd > close;
    }

    private static int LineStart(string text, int offset)
    {
        var i = offset;
        while (i > 0 && text[i - 1] is not '\n' and not '\r')
        {
            i--;
        }

        return i;
    }

    private static int LineIndent(string text, int lineStart)
    {
        var n = 0;
        var i = lineStart;
        while (i < text.Length && text[i] is ' ' or '\t')
        {
            n += text[i] == '\t' ? 4 : 1;
            i++;
        }

        return n;
    }
}
