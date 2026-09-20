namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Finds the innermost unclosed call and its comma index.
/// </summary>
internal static class CallScanner
{
    /// <summary>
    /// Walks delimiters once and returns both the resolved call and the innermost unclosed delimiter.
    /// </summary>
    public static CallWalk Walk(string code, ILanguage language, DocumentBindings bindings,
        IReadOnlyList<Symbol> catalog, CancellationToken token, string? source = null, int caret = -1,
        bool[]? joins = null)
    {
        if (caret < 0 || caret > code.Length)
        {
            caret = code.Length;
        }

        var comparer = language.Comparison == StringComparison.OrdinalIgnoreCase
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        joins ??= StatementScanner.Joins(code, language, token);
        var stack = new Stack<CallFrame>();
        var from = StatementScanner.StatementStart(code, joins, caret);
        for (var i = from; i < caret; i++)
        {
            if ((i & 4095) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            var c = code[i];
            if (ParameterNames.IsLambdaKeyword(code, i))
            {
                i = ParameterNames.SkipLambdaHeader(code, i, caret);
                continue;
            }

            if (c is '(' or '[' or '{')
            {
                stack.Push(new(c, i, i + 1, comparer));
            }
            else if (c is ')' or ']' or '}')
            {
                CloseFrame(stack, c);
            }
            else if (c == ',' && stack.Count > 0)
            {
                stack.Peek().Comma(i + 1);
            }
            else if (c == '=' && stack.Count > 0 && ParameterNames.IsKeywordAssign(code, i))
            {
                stack.Peek().Keyword(code, i);
            }
            else if (c is '\n' or '\r')
            {
                var last = c == '\r' && i + 1 < code.Length && code[i + 1] == '\n' ? i + 1 : i;
                if (!StatementScanner.Continues(code, i) && StatementScanner.Recovers(code, last + 1, language))
                {
                    stack.Clear();
                }

                i = last;
            }
        }

        var unclosed = stack.Count == 0 ? (char?)null : stack.Peek().Delimiter;
        var nested = false;
        foreach (var frame in stack)
        {
            if (frame.Delimiter != '(')
            {
                nested = true;
                continue;
            }

            var callee = ExpressionReader.Callee(code, frame.Offset, language, token, joins);
            if (callee.Count == 0)
            {
                nested = true;
                continue;
            }

            var resolved = language.ResolveCall(callee, bindings, catalog);
            if (resolved is { Overloads.Count: > 0 })
            {
                var current = (source ?? code)[frame.ArgumentStart..caret];
                var first = FirstArgument(code, frame.Offset, caret, source ?? code);
                var used = CanonicalNames(frame.UsedNames, resolved, language);
                var keyword = KeywordName(code[frame.ArgumentStart..caret]);
                var slots = new int[resolved.Overloads.Count];
                var skips = new bool[resolved.Overloads.Count];
                for (var i = 0; i < slots.Length; i++)
                {
                    var overload = resolved.Overloads[i];
                    skips[i] = language is ICallReceiver mapping
                        ? mapping.OmitsFirstClip(resolved, overload, first, bindings, catalog, token)
                        : resolved.ImplicitReceiver;
                    slots[i] = ActivePhysical(resolved.Overloads, keyword, frame.Positional,
                        skips[i], used, language, overload);
                }

                var parameter = slots.Length == 0 ? frame.Positional : slots[0];
                return new(new(resolved.Overloads, parameter, resolved.ImplicitReceiver, nested,
                    current, used, frame.Positional, keyword)
                {
                    OverloadSlots = slots,
                    OverloadSkips = skips
                }, unclosed);
            }

            if (callee[^1].Name.Length > 0)
            {
                return new(null, unclosed);
            }

            nested = true;
        }

        return new(null, unclosed);
    }

    private static void CloseFrame(Stack<CallFrame> stack, char close)
    {
        var open = StatementScanner.Opening(close);
        while (stack.Count > 0 && stack.Peek().Delimiter != open)
        {
            stack.Pop();
        }

        if (stack.Count > 0)
        {
            stack.Pop();
        }
    }

    internal static int ActivePhysical(IReadOnlyList<Symbol> overloads, string? keyword, int positional,
        bool implicitClip, IReadOnlySet<string>? used, ILanguage language, Symbol? overload = null)
    {
        var skip = implicitClip ? 1 : 0;
        if (overload != null)
        {
            return overload.Parameters == null
                ? positional + skip
                : MapOverload(overload, keyword, positional, skip, used, language);
        }

        foreach (var candidate in overloads)
        {
            if (candidate.Parameters == null)
            {
                continue;
            }

            return MapOverload(candidate, keyword, positional, skip, used, language);
        }

        return positional + skip;
    }

    private static int MapOverload(Symbol overload, string? keyword, int positional, int skip,
        IReadOnlySet<string>? used, ILanguage language) =>
        ParameterNames.MapActive(overload.Parameters!, keyword, positional, skip, used, language.ParameterName,
            language.Comparison, NativeAlias(overload));

    internal static bool NativeAlias(Symbol overload) =>
        overload.Name.StartsWith("core.", StringComparison.Ordinal);

    private static string FirstArgument(string code, int openParen, int caret, string source)
    {
        var start = openParen + 1;
        if ((uint)start >= (uint)caret)
        {
            return "";
        }

        var depth = 0;
        for (var i = start; i < caret; i++)
        {
            var c = code[i];
            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}')
            {
                if (depth == 0)
                {
                    return Slice(source, start, i);
                }

                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                return Slice(source, start, i);
            }
        }

        return Slice(source, start, caret);
    }

    private static string Slice(string source, int start, int end)
    {
        if ((uint)start >= (uint)source.Length)
        {
            return "";
        }

        if (end > source.Length)
        {
            end = source.Length;
        }

        return start >= end ? "" : source[start..end].Trim();
    }

    private static string? KeywordName(string argument)
    {
        var eq = ParameterNames.TopLevelKeywordEquals(argument);
        if (eq <= 0)
        {
            return null;
        }

        var name = argument[..eq].Trim();
        if (name.Length == 0)
        {
            return null;
        }

        for (var i = 0; i < name.Length; i++)
        {
            if (!BufferLexer.IsIdentifier(name[i]) || i == 0 && char.IsDigit(name[i]))
            {
                return null;
            }
        }

        return name;
    }

    private static IReadOnlySet<string> CanonicalNames(IReadOnlySet<string> used, CallResolution resolved,
        ILanguage language)
    {
        var names = new HashSet<string>(language.Comparison == StringComparison.OrdinalIgnoreCase
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
        foreach (var written in used)
        {
            var matched = false;
            foreach (var overload in resolved.Overloads)
            {
                if (overload.Parameters == null)
                {
                    continue;
                }

                foreach (var parameter in overload.Parameters)
                {
                    var name = language.ParameterName(parameter);
                    if (name == null ||
                        !ParameterNames.ArgumentEquals(name, written, language.Comparison, NativeAlias(overload)))
                    {
                        continue;
                    }

                    names.Add(name);
                    matched = true;
                }
            }

            if (!matched)
            {
                names.Add(written);
            }
        }

        return names;
    }

    private sealed class CallFrame(char delimiter, int offset, int argumentStart, StringComparer comparer)
    {
        public char Delimiter { get; } = delimiter;
        public int Offset { get; } = offset;
        public int Parameter { get; private set; }
        public int ArgumentStart { get; private set; } = argumentStart;
        public int Positional { get; private set; }
        public HashSet<string> UsedNames { get; } = new(comparer);
        private bool _keyword;

        public void Comma(int nextStart)
        {
            if (!_keyword)
            {
                Positional++;
            }

            Parameter++;
            ArgumentStart = nextStart;
            _keyword = false;
        }

        public void Keyword(string code, int equals)
        {
            if (Delimiter != '(' || _keyword)
            {
                return;
            }

            if (equals < ArgumentStart || equals > code.Length)
            {
                return;
            }

            var name = code[ArgumentStart..equals].Trim();
            if (name.Length == 0)
            {
                return;
            }

            for (var i = 0; i < name.Length; i++)
            {
                if (!BufferLexer.IsIdentifier(name[i]) || i == 0 && char.IsDigit(name[i]))
                {
                    return;
                }
            }

            _keyword = true;
            UsedNames.Add(name);
        }
    }
}

/// <summary>
/// Delimiter walk plus an optional resolved call.
/// </summary>
internal readonly record struct CallWalk(CallScan? Scan, char? Unclosed);

/// <summary>
/// Resolved call plus internal argument-scan context.
/// </summary>
internal sealed record CallScan(
    IReadOnlyList<Symbol> Overloads,
    int ActiveParameter,
    bool ImplicitClip,
    bool InNestedDelimiter,
    string CurrentArgument,
    IReadOnlySet<string> UsedNames,
    int PositionalConsumed,
    string? Keyword)
{
    internal int[]? OverloadSlots { get; init; }

    /// <summary>
    /// Per-overload first-clip skip; insight <see cref="ImplicitClip"/> stays the receiver flag.
    /// </summary>
    internal bool[]? OverloadSkips { get; init; }

    /// <summary>
    /// Gets the consumer-facing insight.
    /// </summary>
    public CallInsight Insight => new(Overloads, ActiveParameter, ImplicitClip)
    {
        Keyword = Keyword,
        Positional = PositionalConsumed,
        UsedNames = UsedNames,
        OverloadSlots = OverloadSlots
    };
}
