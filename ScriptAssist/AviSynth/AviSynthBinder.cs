using System.Text.RegularExpressions;

namespace HanumanInstitute.ScriptAssist.AviSynth;

/// <summary>
/// Collects assignments, <c>last</c>, and buffer-declared functions.
/// </summary>
internal static class AviSynthBinder
{
    /// <summary>
    /// Script-level last-assignment-wins. Function parameters and inner assignments stay in their scope.
    /// </summary>
    public static DocumentBindings Bind(string text, IReadOnlyList<Symbol> catalog, LexerOptions lexer,
        CancellationToken token, string? documentPath = null, IIncludeSource? read = null,
        IncludeCache? includes = null)
    {
        return Bind(PreparedDocument.Create(text, lexer, token), catalog, lexer, token, documentPath, read, includes);
    }

    public static DocumentBindings Bind(PreparedDocument prepared, IReadOnlyList<Symbol> catalog, LexerOptions lexer,
        CancellationToken token, string? documentPath = null, IIncludeSource? read = null,
        IncludeCache? includes = null)
    {
        var joined = prepared.Masked.Code;
        var names = new Dictionary<string, TypeRef>(StringComparer.OrdinalIgnoreCase)
        {
            ["last"] = AviSynthTypes.Clip
        };
        var spans = AviSynthFunctions.Spans(joined, prepared.Quoted.Code, token);
        var buffer = new List<Symbol>(spans.Count);
        foreach (var span in spans)
        {
            buffer.Add(span.Symbol);
        }

        AviSynthFunctions.AddImports(joined, prepared.Quoted.Code, documentPath, read, buffer,
            new(StringComparer.Ordinal), lexer, token, includes);
        var scopes = FunctionScopes(joined, spans);
        Dictionary<string, TypeRef>? visible = null;
        BindingScope? visibleScope = null;
        var n = 0;
        foreach (Match match in AviSynthPatterns.NameAssign().Matches(joined))
        {
            if ((n++ & 63) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            var name = match.Groups[2].Value;
            var inner = Innermost(scopes, match.Index);
            var lookup = Visible(names, scopes, match.Index, inner, ref visible, ref visibleScope);
            var rhs = prepared.Quoted.Code.Substring(match.Groups[3].Index, match.Groups[3].Length).Trim();
            var type = Infer(rhs, lookup, catalog, token);
            if (match.Groups[1].Success || inner == null)
            {
                names[name] = type;
            }
            else
            {
                ((Dictionary<string, TypeRef>)inner.Names)[name] = type;
            }

            if (visible != null)
            {
                visible[name] = type;
            }
        }

        names.Remove("");
        return new()
        {
            Names = names,
            BufferSymbols = buffer,
            Scopes = scopes
        };
    }

    private static List<BindingScope> FunctionScopes(string joined, IReadOnlyList<AviSynthFunctionSpan> spans)
    {
        var scopes = new List<BindingScope>(spans.Count);
        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            var brace = span.ParenClose + 1;
            while (brace < joined.Length && char.IsWhiteSpace(joined[brace]))
            {
                brace++;
            }

            var hasBody = brace < joined.Length && joined[brace] == '{';
            var headerEnd = hasBody ? brace : span.ParenClose + 1;
            var closed = hasBody ? FunctionHeaders.MatchingBrace(joined, brace) : -1;
            var end = closed >= 0
                ? closed
                : i + 1 < spans.Count
                    ? spans[i + 1].Start - 1
                    : joined.Length;
            if (headerEnd > end)
            {
                headerEnd = end;
            }
            var names = new Dictionary<string, TypeRef>(StringComparer.OrdinalIgnoreCase);
            BindParameters(span.Symbol, names);
            scopes.Add(new()
            {
                Start = span.Start,
                End = end,
                Name = span.Symbol.Name,
                HeaderEnd = headerEnd,
                Names = names
            });
        }

        return scopes;
    }

    private static void BindParameters(Symbol symbol, Dictionary<string, TypeRef> names)
    {
        if (symbol.Parameters == null)
        {
            return;
        }

        foreach (var parameter in symbol.Parameters)
        {
            var name = ParameterNames.OfAviSynth(parameter);
            if (name == null)
            {
                continue;
            }

            var trimmed = parameter.Trim();
            if (name.Equals(trimmed, StringComparison.OrdinalIgnoreCase) && AviSynthTypes.IsTypeName(name))
            {
                continue;
            }

            var type = ParameterType(trimmed);
            if (type != null)
            {
                names[name] = type.Value;
            }
        }
    }

    private static TypeRef? ParameterType(string parameter)
    {
        var i = 0;
        while (i < parameter.Length && (char.IsLetter(parameter[i]) || parameter[i] == '_'))
        {
            i++;
        }

        if (i == 0)
        {
            return null;
        }

        var word = parameter[..i];
        if (word.Equals("clip", StringComparison.OrdinalIgnoreCase))
        {
            return AviSynthTypes.Clip;
        }

        if (word.Equals("int", StringComparison.OrdinalIgnoreCase))
        {
            return AviSynthTypes.Int;
        }

        if (word.Equals("float", StringComparison.OrdinalIgnoreCase))
        {
            return AviSynthTypes.Float;
        }

        if (word.Equals("bool", StringComparison.OrdinalIgnoreCase))
        {
            return AviSynthTypes.Bool;
        }

        if (word.Equals("string", StringComparison.OrdinalIgnoreCase))
        {
            return AviSynthTypes.String;
        }

        return null;
    }

    private static Dictionary<string, TypeRef> Visible(Dictionary<string, TypeRef> global,
        IReadOnlyList<BindingScope> scopes, int offset, BindingScope? inner,
        ref Dictionary<string, TypeRef>? visible, ref BindingScope? visibleScope)
    {
        if (inner == null)
        {
            visible = null;
            visibleScope = null;
            return global;
        }

        if (!ReferenceEquals(visibleScope, inner) || visible == null)
        {
            visible = new(global, global.Comparer);
            foreach (var scope in scopes)
            {
                if (offset < scope.Start || offset > scope.End)
                {
                    continue;
                }

                foreach (var pair in scope.Names)
                {
                    visible[pair.Key] = pair.Value;
                }
            }

            visibleScope = inner;
        }

        return visible;
    }

    private static BindingScope? Innermost(IReadOnlyList<BindingScope> scopes, int offset)
    {
        BindingScope? inner = null;
        foreach (var scope in scopes)
        {
            if (offset < scope.Start || offset > scope.End)
            {
                continue;
            }

            if (inner == null || scope.Start >= inner.Start)
            {
                inner = scope;
            }
        }

        return inner;
    }

    private const int MaxInferDepth = 48;
    private const int MaxInferWork = 250_000;

    /// <summary>
    /// Types an AviSynth expression, including grouping, literals, and clip copies.
    /// </summary>
    internal static TypeRef Infer(string expression, IReadOnlyDictionary<string, TypeRef> names,
        IReadOnlyList<Symbol> catalog, CancellationToken token)
    {
        var table = names as Dictionary<string, TypeRef> ??
            new Dictionary<string, TypeRef>(names, StringComparer.OrdinalIgnoreCase);
        return InferCore(expression, table, catalog, 0, 0, token);
    }

    private static TypeRef InferCore(string expression, Dictionary<string, TypeRef> names,
        IReadOnlyList<Symbol> catalog, int depth, int work, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        work += Math.Max(1, expression.Length);
        if (depth > MaxInferDepth || work > MaxInferWork)
        {
            return TypeRef.Unknown;
        }

        var trimmed = ExpressionParts.UnwrapParentheses(expression);
        if (trimmed.StartsWith("Default(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(')'))
        {
            var inner = trimmed[8..^1];
            var args = ParameterNames.Split(inner);
            if (args.Length > 0)
            {
                var first = InferCore(args[0], names, catalog, depth + 1, work, token);
                if (!first.IsUnknown)
                {
                    return first;
                }

                if (args.Length > 1)
                {
                    return InferCore(args[1], names, catalog, depth + 1, work, token);
                }
            }
        }

        var question = ExpressionParts.IndexOutsideBrackets(trimmed, '?');
        if (question >= 0)
        {
            var rest = trimmed[(question + 1)..];
            var colon = ExpressionParts.IndexOutsideBrackets(rest, ':');
            if (colon >= 0)
            {
                var whenTrue = InferCore(rest[..colon].Trim(), names, catalog, depth + 1, work, token);
                var whenFalse = InferCore(rest[(colon + 1)..].Trim(), names, catalog, depth + 1, work, token);
                if (whenTrue == AviSynthTypes.Clip || whenFalse == AviSynthTypes.Clip)
                {
                    return AviSynthTypes.Clip;
                }

                if (!whenTrue.IsUnknown && whenTrue == whenFalse)
                {
                    return whenTrue;
                }

                return whenFalse.IsUnknown ? whenTrue : whenFalse;
            }
        }

        var parts = ExpressionParts.SplitAddMul(trimmed);
        if (parts.Count == 1)
        {
            return InferPart(trimmed, names, catalog);
        }

        var clip = TypeRef.Unknown;
        TypeRef? numeric = null;
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            var next = InferPart(ExpressionParts.GroupOperand(expression, part), names, catalog);
            if (next == AviSynthTypes.Clip)
            {
                clip = next;
                continue;
            }

            numeric = CombineNumber(numeric, next);
        }

        if (!clip.IsUnknown)
        {
            return clip;
        }

        return numeric ?? TypeRef.Unknown;
    }

    private static TypeRef CombineNumber(TypeRef? left, TypeRef right)
    {
        if (left == null)
        {
            return right;
        }

        if (left.Value.IsUnknown || right.IsUnknown)
        {
            return TypeRef.Unknown;
        }

        if (IsNumber(left.Value) && IsNumber(right))
        {
            return left.Value == AviSynthTypes.Float || right == AviSynthTypes.Float
                ? AviSynthTypes.Float
                : AviSynthTypes.Int;
        }

        return left.Value == right ? left.Value : TypeRef.Unknown;
    }

    private static bool IsNumber(TypeRef type) =>
        type == AviSynthTypes.Int || type == AviSynthTypes.Float;

    private static TypeRef InferPart(string part, Dictionary<string, TypeRef> names, IReadOnlyList<Symbol> catalog)
    {
        var literal = LiteralType(part);
        if (!literal.IsUnknown)
        {
            return literal;
        }

        var segments = ExpressionReader.Parse(part);
        if (segments.Count == 0)
        {
            return TypeRef.Unknown;
        }

        return AviSynthTypeWalker.TypeOf(segments, new() { Names = names }, catalog);
    }

    private static TypeRef LiteralType(string text)
    {
        if (text.Length >= 2 && text[0] is '"' or '\'')
        {
            return AviSynthTypes.String;
        }

        if (text.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return AviSynthTypes.Bool;
        }

        var i = 0;
        if (text.Length > 0 && text[0] is '+' or '-')
        {
            i++;
        }

        var digits = false;
        var dot = false;
        for (; i < text.Length; i++)
        {
            if (char.IsDigit(text[i]))
            {
                digits = true;
                continue;
            }

            if (text[i] == '.' && !dot)
            {
                dot = true;
                continue;
            }

            return TypeRef.Unknown;
        }

        if (!digits)
        {
            return TypeRef.Unknown;
        }

        return dot ? AviSynthTypes.Float : AviSynthTypes.Int;
    }
}
