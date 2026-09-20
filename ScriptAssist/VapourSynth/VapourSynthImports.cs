namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Places a top-level <c>import module</c> so an explorer insert is valid.
/// </summary>
public static class VapourSynthImports
{
    private static readonly LexerOptions Lexer = new()
    {
        HashLineComments = true,
        SingleQuotes = true,
        TripleQuotes = true,
        StringEscapes = true,
        PythonLineContinuations = true
    };

    /// <summary>
    /// Returns whether a column-0 <c>import</c> already binds <paramref name="module"/>.
    /// <c>from module import</c> does not count; that does not bind the module name.
    /// </summary>
    public static bool Contains(string text, string module)
    {
        if (!module.HasText())
        {
            return false;
        }

        var prepared = PreparedDocument.Create(text ?? "", Lexer);
        foreach (var span in prepared.Statements)
        {
            if (VapourSynthClassRanges.IndentAt(prepared.Masked.Code, span.Start) != 0)
            {
                continue;
            }

            if (!VapourSynthBinder.Keyword(prepared.Quoted.Code, span.Start, span.End, "import"))
            {
                continue;
            }

            var list = prepared.Quoted.Code[VapourSynthBinder.AfterKeyword(prepared.Quoted.Code, span.Start, span.End,
                "import")..span.End];
            foreach (var part in ParameterNames.Split(list))
            {
                var spec = part.Trim();
                if (spec.Length == 0)
                {
                    continue;
                }

                var aliased = ParameterNames.TryAlias(spec, out var imported, out var alias);
                if (!aliased && imported.Contains('.', StringComparison.Ordinal))
                {
                    alias = imported[..imported.IndexOf('.')];
                }

                if (alias.Equals(module, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Offset after the last complete column-0 header <c>import</c> / <c>from</c> statement, or 0.
    /// </summary>
    public static int InsertionOffset(string text)
    {
        var prepared = PreparedDocument.Create(text ?? "", Lexer);
        var last = 0;
        foreach (var span in prepared.Statements)
        {
            if (VapourSynthClassRanges.IndentAt(prepared.Masked.Code, span.Start) != 0)
            {
                if (Blank(prepared.Masked.Code, span))
                {
                    continue;
                }

                break;
            }

            var quoted = prepared.Quoted.Code;
            var header = VapourSynthBinder.Keyword(quoted, span.Start, span.End, "import") ||
                VapourSynthBinder.Keyword(quoted, span.Start, span.End, "from");
            if (!header)
            {
                if (Blank(prepared.Masked.Code, span))
                {
                    continue;
                }

                break;
            }

            if (Unclosed(prepared.Masked.Code, span))
            {
                break;
            }

            last = AfterStatement(text ?? "", span);
        }

        return last;
    }

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

    private static bool Blank(string masked, StatementScanner.Span span)
    {
        for (var i = span.Start; i < span.End && i < masked.Length; i++)
        {
            if (!char.IsWhiteSpace(masked[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Unclosed(string masked, StatementScanner.Span span)
    {
        var depth = 0;
        for (var i = span.Start; i < span.End && i < masked.Length; i++)
        {
            var c = masked[i];
            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}' && depth > 0)
            {
                depth--;
            }
        }

        return depth > 0;
    }

    private static int AfterStatement(string text, StatementScanner.Span span)
    {
        var i = span.End;
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
}
