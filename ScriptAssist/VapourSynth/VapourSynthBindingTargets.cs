namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Names bound by assignment, <c>del</c>, <c>for</c>, <c>def</c>, <c>class</c>, and <c>with</c> targets.
/// </summary>
internal static class VapourSynthBindingTargets
{
    /// <summary>
    /// Identifier spans bound on the left-hand side of a statement, stopping at <paramref name="stop"/>.
    /// </summary>
    public static IEnumerable<(int Start, int Length)> Spans(string text, int start, int end, string? stop)
    {
        var i = start;
        while (i < end)
        {
            SkipWs(text, ref i, end);
            if (i >= end || stop != null && Keyword(text, i, end, stop))
            {
                yield break;
            }

            if (text[i] is ',' or '*')
            {
                i++;
                continue;
            }

            if (text[i] is '"' or '\'')
            {
                SkipString(text, ref i, end);
                continue;
            }

            if (text[i] is '(' or '[')
            {
                var close = text[i] == '(' ? ')' : ']';
                var innerEnd = SkipBalanced(text, i, end, text[i], close);
                foreach (var inner in Spans(text, i + 1, innerEnd - (innerEnd > i ? 1 : 0), null))
                {
                    yield return inner;
                }

                i = innerEnd;
                continue;
            }

            var ident = i;
            if (!TryIdentSpan(text, ref i, end, out var length))
            {
                i++;
                continue;
            }

            SkipWs(text, ref i, end);
            if (i < end && text[i] is '.' or '[' or '(')
            {
                SkipTrailers(text, ref i, end);
                continue;
            }

            yield return (ident, length);
        }
    }

    /// <summary>
    /// Bound identifier names; used by the binder.
    /// </summary>
    public static IEnumerable<string> Names(string text, int start, int end, string? stop)
    {
        foreach (var (at, length) in Spans(text, start, end, stop))
        {
            yield return text.Substring(at, length);
        }
    }

    public static bool Keyword(string text, int start, int end, string word)
    {
        if (end - start < word.Length ||
            !text.AsSpan(start, word.Length).Equals(word, StringComparison.Ordinal))
        {
            return false;
        }

        var after = start + word.Length;
        return after == end || !BufferLexer.IsIdentifier(text[after]);
    }

    public static int AfterKeyword(string text, int start, int end, string word)
    {
        var i = start + word.Length;
        SkipWs(text, ref i, end);
        return i;
    }

    public static void SkipWs(string text, ref int i, int end)
    {
        while (i < end && char.IsWhiteSpace(text[i]))
        {
            i++;
        }
    }

    public static bool TryIdentSpan(string text, ref int i, int end, out int length)
    {
        var start = i;
        length = 0;
        if (i >= end || !BufferLexer.IsIdentifier(text[i]) || char.IsDigit(text[i]))
        {
            return false;
        }

        i++;
        while (i < end && BufferLexer.IsIdentifier(text[i]))
        {
            i++;
        }

        length = i - start;
        return true;
    }

    public static bool TryIdent(string text, ref int i, int end, out string name)
    {
        var start = i;
        if (!TryIdentSpan(text, ref i, end, out var length))
        {
            name = "";
            return false;
        }

        name = text.Substring(start, length);
        return true;
    }

    public static bool SpanEquals(string text, int start, int length, string name) =>
        name.Length == length && text.AsSpan(start, length).Equals(name, StringComparison.Ordinal);

    public static int SkipBalanced(string text, int start, int end, char open, char close)
    {
        var depth = 1;
        var i = start + 1;
        while (i < end && depth > 0)
        {
            if (text[i] is '"' or '\'')
            {
                SkipString(text, ref i, end);
                continue;
            }

            if (text[i] == open)
            {
                depth++;
            }
            else if (text[i] == close)
            {
                depth--;
            }

            i++;
        }

        return i;
    }

    public static void SkipString(string text, ref int i, int end)
    {
        var quote = text[i++];
        while (i < end)
        {
            if (text[i] == '\\' && i + 1 < end)
            {
                i += 2;
                continue;
            }

            if (text[i++] == quote)
            {
                return;
            }
        }
    }

    private static void SkipTrailers(string text, ref int i, int end)
    {
        while (i < end)
        {
            SkipWs(text, ref i, end);
            if (i >= end)
            {
                return;
            }

            if (text[i] == '.')
            {
                i++;
                SkipWs(text, ref i, end);
                TryIdentSpan(text, ref i, end, out _);
                continue;
            }

            if (text[i] is '[' or '(')
            {
                var close = text[i] == '[' ? ']' : ')';
                i = SkipBalanced(text, i, end, text[i], close);
                continue;
            }

            return;
        }
    }
}
