namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Reads dotted, called, and indexed expressions while skipping balanced <c>()</c> and <c>[]</c>.
/// </summary>
internal static class ExpressionReader
{
    private const int MaxDepth = 48;
    private const int MaxWork = 250_000;

    private static readonly LexerOptions StructureLexer = new()
    {
        HashLineComments = true,
        SingleQuotes = true,
        TripleQuotes = true,
        StringEscapes = true
    };

    /// <summary>
    /// Reads the path at <paramref name="caret"/> for completion replacement.
    /// </summary>
    public static CaretPath Read(string code, int caret, ILanguage? language = null,
        CancellationToken token = default, bool[]? joins = null)
    {
        caret = caret.Clamp(0, code.Length);
        var start = caret;
        while (start > 0 && BufferLexer.IsIdentifier(code[start - 1]))
        {
            start--;
        }

        var end = caret;
        while (end < code.Length && BufferLexer.IsIdentifier(code[end]))
        {
            end++;
        }

        var typed = start <= caret && caret <= code.Length ? code[start..caret] : "";
        joins ??= StatementScanner.Joins(code, language, token);
        var work = 0;
        return new()
        {
            Start = start,
            End = end,
            Typed = typed,
            Segments = WalkLeft(code, start, 0, joins, language, token, 0, ref work, out _)
        };
    }

    /// <summary>
    /// Parses a whole expression, including a trailing identifier, call, or index.
    /// Returns an empty path when the expression is not fully consumed.
    /// </summary>
    public static IReadOnlyList<PathSegment> Parse(string code, ILanguage? language = null,
        CancellationToken token = default)
    {
        if (!code.HasText())
        {
            return [];
        }

        token.ThrowIfCancellationRequested();
        var structure = BufferLexer.Mask(code, StructureLexer, token: token).Code;
        var joins = StatementScanner.Joins(structure, language, token);
        var work = 0;
        return ParseRange(structure, 0, structure.Length, joins, language, token, 0, ref work);
    }

    private static IReadOnlyList<PathSegment> ParseRange(string code, int start, int end, bool[] joins,
        ILanguage? language, CancellationToken token, int depth, ref int work)
    {
        if (depth > MaxDepth || (work += Math.Max(1, end - start)) > MaxWork)
        {
            return [];
        }

        token.ThrowIfCancellationRequested();
        Trim(code, ref start, ref end);
        var unwraps = 0;
        while (end - start >= 2 &&
            ExpressionParts.IsParenthesized(code, start, end, language?.Lexer.StringEscapes ?? true))
        {
            if (++unwraps > MaxDepth || (work += Math.Max(1, end - start)) > MaxWork)
            {
                return [];
            }

            start++;
            end--;
            Trim(code, ref start, ref end);
        }

        if (start >= end)
        {
            return [];
        }

        IReadOnlyList<PathSegment> segments;
        int remaining;
        if (code[end - 1] is ')' or ']')
        {
            if (!TryParseClosed(code, start, end, joins, language, token, depth, ref work, out segments,
                    out remaining))
            {
                return [];
            }
        }
        else
        {
            var typedStart = end;
            while (typedStart > start && BufferLexer.IsIdentifier(code[typedStart - 1]))
            {
                typedStart--;
            }

            var prefix = WalkLeft(code, typedStart, start, joins, language, token, depth, ref work, out remaining);
            if (typedStart == end)
            {
                segments = prefix;
            }
            else
            {
                var list = new List<PathSegment>(prefix.Count + 1);
                list.AddRange(prefix);
                list.Add(new() { Name = code[typedStart..end], Kind = PathSegmentKind.Name });
                segments = list;
            }
        }

        return Leftover(code, start, remaining) ? [] : segments;
    }

    private static bool TryParseClosed(string code, int start, int end, bool[] joins, ILanguage? language,
        CancellationToken token, int depth, ref int work, out IReadOnlyList<PathSegment> segments, out int remaining)
    {
        segments = [];
        remaining = 0;
        var pos = end;
        while (pos > start && char.IsWhiteSpace(code[pos - 1]))
        {
            pos--;
        }

        if (pos <= start || code[pos - 1] is not (')' or ']'))
        {
            return false;
        }

        if (!TryReadPostfix(code, start, ref pos, out var name, out var uses))
        {
            return false;
        }

        var prefix = WalkLeft(code, pos, start, joins, language, token, depth, ref work, out remaining);
        var list = new List<PathSegment>(prefix.Count + uses.Count);
        list.AddRange(prefix);
        AppendUses(list, name, uses, reverse: false);
        segments = list;
        return true;
    }

    /// <summary>
    /// Reads the callee to the left of an opening parenthesis.
    /// </summary>
    public static IReadOnlyList<PathSegment> Callee(string code, int openParen, ILanguage? language = null,
        CancellationToken token = default, bool[]? joins = null)
    {
        var end = openParen.Clamp(0, code.Length);
        joins ??= StatementScanner.Joins(code, language, token);
        if (!SkipJoin(code, 0, ref end, joins))
        {
            return [];
        }

        var start = end;
        while (start > 0 && BufferLexer.IsIdentifier(code[start - 1]))
        {
            start--;
        }

        if (start == end)
        {
            return [];
        }

        var name = code[start..end];
        var work = 0;
        var prefix = WalkLeft(code, start, 0, joins, language, token, 0, ref work, out _);
        var segments = new List<PathSegment>(prefix.Count + 1);
        segments.AddRange(prefix);
        segments.Add(new() { Name = name, Kind = PathSegmentKind.Name });
        return segments;
    }

    private static IReadOnlyList<PathSegment> WalkLeft(string code, int position, int min, bool[] joins,
        ILanguage? language, CancellationToken token, int depth, ref int work, out int remaining)
    {
        var collected = new List<PathSegment>();
        var pos = position;
        var steps = 0;
        while (pos > min)
        {
            if ((steps++ & 4095) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            if (depth > MaxDepth || ++work > MaxWork)
            {
                collected.Clear();
                break;
            }

            if (!SkipJoin(code, min, ref pos, joins))
            {
                break;
            }

            if (pos <= min || code[pos - 1] != '.')
            {
                break;
            }

            pos--;
            if (!SkipJoin(code, min, ref pos, joins))
            {
                break;
            }

            if (pos > min && code[pos - 1] is ')' or ']')
            {
                var saved = pos;
                if (TryReadPostfix(code, min, ref pos, out var name, out var uses))
                {
                    AppendUses(collected, name, uses, reverse: true);
                    continue;
                }

                pos = saved;
                if (code[pos - 1] != ')')
                {
                    break;
                }

                var close = pos - 1;
                var open = SkipBalanced(code, close, min, ')', '(');
                if (open >= close)
                {
                    break;
                }

                var inner = ParseRange(code, open, close + 1, joins, language, token, depth + 1, ref work);
                for (var i = inner.Count - 1; i >= 0; i--)
                {
                    collected.Add(inner[i]);
                }

                pos = open;
                continue;
            }

            if (pos > min && BufferLexer.IsIdentifier(code[pos - 1]))
            {
                var nameEnd = pos;
                while (pos > min && BufferLexer.IsIdentifier(code[pos - 1]))
                {
                    pos--;
                }

                collected.Add(new() { Name = code[pos..nameEnd], Kind = PathSegmentKind.Name });
                continue;
            }

            break;
        }

        remaining = pos;
        collected.Reverse();
        return collected;
    }

    private static bool SkipJoin(string code, int min, ref int pos, bool[] joins)
    {
        var origin = pos;
        while (pos > min)
        {
            var saved = pos;
            while (pos > min && code[pos - 1] is ' ' or '\t')
            {
                pos--;
            }

            if (pos <= min || code[pos - 1] is not ('\n' or '\r'))
            {
                return true;
            }

            var last = pos - 1;
            var newline = code[last] == '\n' && last > min && code[last - 1] == '\r' ? last - 1 : last;
            if (newline < 0 || newline >= joins.Length || !joins[newline])
            {
                pos = saved;
                return saved != origin;
            }

            pos = newline;
            if (pos > min && code[pos - 1] == '\\')
            {
                pos--;
            }
        }

        return true;
    }

    private static bool Leftover(string code, int start, int remaining)
    {
        for (var i = start; i < remaining; i++)
        {
            if (!char.IsWhiteSpace(code[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static void Trim(string code, ref int start, ref int end)
    {
        while (start < end && char.IsWhiteSpace(code[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(code[end - 1]))
        {
            end--;
        }
    }

    private static bool TryReadPostfix(string code, int min, ref int pos, out string name, out List<PathSegmentKind> uses)
    {
        name = "";
        uses = [];
        while (pos > min && code[pos - 1] is ')' or ']')
        {
            var closer = code[pos - 1];
            var opener = closer == ')' ? '(' : '[';
            var kind = closer == ')' ? PathSegmentKind.Call : PathSegmentKind.Index;
            pos = SkipBalanced(code, pos - 1, min, closer, opener);
            while (pos > min && code[pos - 1] is ' ' or '\t')
            {
                pos--;
            }

            uses.Add(kind);
        }

        uses.Reverse();
        var nameEnd = pos;
        while (pos > 0 && BufferLexer.IsIdentifier(code[pos - 1]))
        {
            pos--;
        }

        if (pos == nameEnd || pos < min || uses.Count == 0)
        {
            return false;
        }

        name = code[pos..nameEnd];
        return true;
    }

    private static void AppendUses(List<PathSegment> segments, string name, List<PathSegmentKind> uses, bool reverse)
    {
        if (reverse)
        {
            for (var i = uses.Count - 1; i >= 0; i--)
            {
                segments.Add(new() { Name = i == 0 ? name : "", Kind = uses[i] });
            }

            return;
        }

        for (var i = 0; i < uses.Count; i++)
        {
            segments.Add(new() { Name = i == 0 ? name : "", Kind = uses[i] });
        }
    }

    private static int SkipBalanced(string code, int closerIndex, int min, char closer, char opener)
    {
        var depth = 1;
        var i = closerIndex;
        while (i > min && depth > 0)
        {
            i--;
            var c = code[i];
            if (c == closer)
            {
                depth++;
            }
            else if (c == opener)
            {
                depth--;
            }
        }

        return depth == 0 ? i : 0;
    }
}
