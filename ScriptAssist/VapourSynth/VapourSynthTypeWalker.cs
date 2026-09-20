namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Types dotted, called, and indexed VapourSynth expressions.
/// </summary>
internal static class VapourSynthTypeWalker
{
    /// <summary>
    /// Types <paramref name="segments"/> left to right.
    /// </summary>
    public static TypeRef TypeOf(IReadOnlyList<PathSegment> segments, DocumentBindings bindings,
        VapourSynthCatalogIndex index)
    {
        if (segments.Count == 0)
        {
            return TypeRef.Root;
        }

        var current = ResolveName(segments[0].Name, bindings);
        current = ApplyUse(current, segments[0]);
        for (var i = 1; i < segments.Count; i++)
        {
            current = Step(current, segments[i], index, bindings);
        }

        return current;
    }

    /// <summary>
    /// Types a right-hand side, including clip copy through <c>+</c> and <c>*</c>.
    /// </summary>
    public static TypeRef Infer(string expression, DocumentBindings bindings, VapourSynthCatalogIndex index,
        CancellationToken token = default)
    {
        var unwrapped = ExpressionParts.UnwrapParentheses(expression);
        if (IsInteger(unwrapped))
        {
            return VapourSynthTypes.Int;
        }

        if (IsFloat(unwrapped))
        {
            return VapourSynthTypes.Float;
        }

        if (unwrapped is "True" or "False")
        {
            return VapourSynthTypes.Bool;
        }

        if (IsStringLiteral(unwrapped))
        {
            return VapourSynthTypes.String;
        }

        if (TryConditional(unwrapped, out var left, out var right))
        {
            var whenTrue = InferPart(left, bindings, index, token);
            var whenFalse = InferPart(right, bindings, index, token);
            return whenTrue == whenFalse && !whenTrue.IsUnknown ? whenTrue : TypeRef.Unknown;
        }

        var parts = ExpressionParts.SplitAddMul(unwrapped);
        if (parts.Count == 1)
        {
            return InferPart(expression, bindings, index, token);
        }

        var node = TypeRef.Unknown;
        var last = TypeRef.Unknown;
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            last = InferPart(ExpressionParts.GroupOperand(expression, part), bindings, index, token);
            if (VapourSynthTypes.IsNode(last))
            {
                node = last;
            }
        }

        if (!node.IsUnknown)
        {
            return node;
        }

        return TypeRef.Unknown;
    }

    private static TypeRef InferPart(string part, DocumentBindings bindings, VapourSynthCatalogIndex index,
        CancellationToken token)
    {
        if (IsInteger(part))
        {
            return VapourSynthTypes.Int;
        }

        if (IsFloat(part))
        {
            return VapourSynthTypes.Float;
        }

        if (part is "True" or "False")
        {
            return VapourSynthTypes.Bool;
        }

        if (IsStringLiteral(part))
        {
            return VapourSynthTypes.String;
        }

        var segments = ExpressionReader.Parse(part, token: token);
        if (segments.Count == 0)
        {
            return TypeRef.Unknown;
        }

        var type = TypeOf(segments, bindings, index);
        return type.IsRoot ? TypeRef.Unknown : type;
    }

    private static bool IsInteger(string part)
    {
        if (part.Length == 0)
        {
            return false;
        }

        var i = part[0] == '-' ? 1 : 0;
        if (i >= part.Length)
        {
            return false;
        }

        while (i < part.Length)
        {
            if (!char.IsDigit(part[i]))
            {
                return false;
            }

            i++;
        }

        return true;
    }

    private static bool IsFloat(string part) =>
        part.IndexOf('.') >= 0 &&
        double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static bool IsStringLiteral(string part) =>
        part.Length >= 2 && part[0] is '"' or '\'' && part[^1] == part[0];

    private static TypeRef ResolveName(string name, DocumentBindings bindings)
    {
        if (bindings.Names.TryGetValue(name, out var typed))
        {
            return typed;
        }

        foreach (var symbol in bindings.BufferSymbols)
        {
            if (symbol.Name.Equals(name, StringComparison.Ordinal) &&
                (symbol.Kind == SymbolKind.Function || symbol.Parameters != null))
            {
                return VapourSynthTypes.Function(symbol);
            }
        }

        return TypeRef.Unknown;
    }

    private static TypeRef Step(TypeRef current, PathSegment segment, VapourSynthCatalogIndex index,
        DocumentBindings bindings)
    {
        if (current.IsUnknown)
        {
            return TypeRef.Unknown;
        }
        if (segment.Kind == PathSegmentKind.Index)
        {
            if (segment.Name.Length > 0)
            {
                current = Step(current, new() { Name = segment.Name, Kind = PathSegmentKind.Name },
                    index, bindings);
            }

            return Index(current);
        }

        if (current == VapourSynthTypes.Module && segment.Name == "get_core")
        {
            return segment.Kind == PathSegmentKind.Call ? VapourSynthTypes.Core : TypeRef.Unknown;
        }

        var symbol = VapourSynthMembers.Find(current, segment.Name, bindings, index);
        if (symbol == null)
        {
            return TypeRef.Unknown;
        }

        if (symbol.Kind == SymbolKind.Namespace)
        {
            if (segment.Kind == PathSegmentKind.Call)
            {
                return TypeRef.Unknown;
            }

            if (current == VapourSynthTypes.Module && segment.Name == "core")
            {
                return VapourSynthTypes.Core;
            }

            if (current == VapourSynthTypes.Core)
            {
                return VapourSynthTypes.Plugin(segment.Name);
            }

            if (VapourSynthTypes.IsNode(current))
            {
                return VapourSynthTypes.Bound(segment.Name, current);
            }
        }

        return ApplyMember(symbol, segment, VapourSynthTypes.IsBound(current));
    }

    private static TypeRef ApplyUse(TypeRef current, PathSegment segment)
    {
        if (segment.Kind == PathSegmentKind.Index)
        {
            return Index(current);
        }
        if (segment.Kind != PathSegmentKind.Call)
        {
            return current;
        }

        var snapshot = VapourSynthTypes.FunctionSymbol(current);
        if (snapshot != null)
        {
            return ReturnOf(snapshot);
        }

        return TypeRef.Unknown;
    }

    private static bool TryConditional(string text, out string left, out string right)
    {
        left = "";
        right = "";
        var ifAt = KeywordAt(text, "if", 0);
        if (ifAt < 0)
        {
            return false;
        }

        var elseAt = KeywordAt(text, "else", ifAt + 2);
        if (elseAt < 0)
        {
            return false;
        }

        left = text[..ifAt].Trim();
        right = text[(elseAt + 4)..].Trim();
        return left.Length > 0 && right.Length > 0;
    }

    private static int KeywordAt(string text, string word, int from)
    {
        var depth = 0;
        var quote = '\0';
        for (var i = from; i + word.Length <= text.Length; i++)
        {
            var c = text[i];
            if (quote != '\0')
            {
                if (c == '\\' && i + 1 < text.Length)
                {
                    i++;
                    continue;
                }

                if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
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

            if (depth != 0)
            {
                continue;
            }

            if (!text.AsSpan(i, word.Length).Equals(word, StringComparison.Ordinal))
            {
                continue;
            }

            if (i > 0 && BufferLexer.IsIdentifier(text[i - 1]))
            {
                continue;
            }

            var after = i + word.Length;
            if (after < text.Length && BufferLexer.IsIdentifier(text[after]))
            {
                continue;
            }

            return i;
        }

        return -1;
    }

    private static TypeRef ReturnOf(Symbol symbol) => VapourSynthTypes.FromReturn(symbol.ReturnType);

    private static TypeRef ApplyMember(Symbol symbol, PathSegment segment, bool bound)
    {
        var nested = symbol.ReturnType != null
            ? VapourSynthTypes.ScriptOf(new(symbol.ReturnType))
            : null;
        if (nested != null)
        {
            return segment.Kind == PathSegmentKind.Call ? TypeRef.Unknown : VapourSynthTypes.Script(nested);
        }

        if (segment.Kind == PathSegmentKind.Call)
        {
            return symbol.Kind == SymbolKind.Function || symbol.Parameters != null
                ? ReturnOf(symbol)
                : TypeRef.Unknown;
        }

        if (symbol.Kind == SymbolKind.Function || symbol.Parameters != null)
        {
            return VapourSynthTypes.Function(symbol, bound);
        }

        return ReturnOf(symbol);
    }

    private static TypeRef Index(TypeRef current) =>
        VapourSynthTypes.IsNode(current) ? current : TypeRef.Unknown;
}
