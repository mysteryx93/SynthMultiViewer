namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Splits an expression on top-level <c>+</c> and <c>*</c> outside brackets.
/// </summary>
internal static class ExpressionParts
{
    /// <summary>
    /// Returns trimmed operands of a clip-copy expression.
    /// </summary>
    public static IReadOnlyList<string> SplitAddMul(string expression, bool stringEscapes = true)
    {
        var parts = new List<string>();
        var start = 0;
        var depth = 0;
        var quote = '\0';
        for (var i = 0; i < expression.Length; i++)
        {
            var c = expression[i];
            if (quote != '\0')
            {
                SkipStringChar(expression, ref i, ref quote, stringEscapes);
                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}')
            {
                if (depth > 0)
                {
                    depth--;
                }
            }
            else if (depth == 0 && c is '+' or '*')
            {
                parts.Add(expression[start..i].Trim());
                start = i + 1;
            }
        }

        parts.Add(expression[start..].Trim());
        return parts;
    }

    /// <summary>
    /// Returns the first <paramref name="symbol"/> not inside <c>()</c>, <c>[]</c>, or <c>{}</c>, or -1.
    /// </summary>
    public static int IndexOutsideBrackets(string expression, char symbol, bool stringEscapes = true)
    {
        var depth = 0;
        var quote = '\0';
        for (var i = 0; i < expression.Length; i++)
        {
            var c = expression[i];
            if (quote != '\0')
            {
                SkipStringChar(expression, ref i, ref quote, stringEscapes);
                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}' && depth > 0)
            {
                depth--;
            }
            else if (depth == 0 && c == symbol)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Gets whether matching outer parentheses wrap <paramref name="expression"/>.
    /// </summary>
    public static bool IsGrouped(string expression, bool stringEscapes = true)
    {
        var trimmed = expression.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '(' &&
            UnwrapParentheses(trimmed, stringEscapes) != trimmed;
    }

    /// <summary>
    /// Re-wraps an operand so continuation context from the original grouping is preserved.
    /// </summary>
    public static string GroupOperand(string expression, string part, bool stringEscapes = true) =>
        IsGrouped(expression, stringEscapes) ? "(" + part + ")" : part;

    /// <summary>
    /// Strips matching outer parentheses so <c>(cond ? a : b)</c> is a ternary.
    /// </summary>
    public static string UnwrapParentheses(string expression, bool stringEscapes = true) =>
        UnwrapSpan(expression, stringEscapes).Text;

    /// <summary>
    /// Strips matching outer parentheses and returns the inner text plus its offset in
    /// <paramref name="expression"/> so continuation context can keep the grouping.
    /// </summary>
    public static (string Text, int Offset) UnwrapSpan(string expression, bool stringEscapes = true)
    {
        var start = 0;
        var end = expression.Length;
        Trim(expression, ref start, ref end);
        const int maxUnwrap = 48;
        var unwraps = 0;
        while (end - start >= 2 && unwraps < maxUnwrap &&
            IsParenthesized(expression, start, end, stringEscapes))
        {
            unwraps++;
            start++;
            end--;
            Trim(expression, ref start, ref end);
        }

        return (expression[start..end], start);
    }

    private static void Trim(string expression, ref int start, ref int end)
    {
        while (start < end && char.IsWhiteSpace(expression[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(expression[end - 1]))
        {
            end--;
        }
    }

    internal static bool IsParenthesized(string expression, int start, int end, bool stringEscapes = true)
    {
        if (end - start < 2 || expression[start] != '(' || expression[end - 1] != ')')
        {
            return false;
        }

        var depth = 0;
        var quote = '\0';
        for (var i = start; i < end; i++)
        {
            var c = expression[i];
            if (quote != '\0')
            {
                SkipStringChar(expression, ref i, ref quote, stringEscapes);
                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}' && depth > 0)
            {
                depth--;
                if (depth == 0)
                {
                    return i == end - 1;
                }
            }
        }

        return false;
    }

    private static void SkipStringChar(string expression, ref int i, ref char quote, bool stringEscapes)
    {
        var c = expression[i];
        if (stringEscapes && c == '\\' && i + 1 < expression.Length)
        {
            i++;
            return;
        }

        if (c == quote)
        {
            quote = '\0';
        }
    }
}
