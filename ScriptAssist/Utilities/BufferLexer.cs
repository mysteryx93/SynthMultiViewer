using System.Collections;
using System.Text.RegularExpressions;

namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Position-preserving lexer; offsets remain AvaloniaEdit UTF-16 offsets.
/// </summary>
internal static partial class BufferLexer
{
    /// <summary>
    /// Recognizes UTF-16 identifier characters, including combining marks and surrogate pairs.
    /// </summary>
    public static bool IsIdentifier(char c) => char.IsLetterOrDigit(c) || c == '_' || char.IsSurrogate(c) ||
        char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;

    /// <summary>
    /// Blanks comments and optionally strings while preserving original document offsets.
    /// </summary>
    public static LexedBuffer Mask(string text, LexerOptions options, bool maskStrings = true,
        CancellationToken token = default, bool trackLiterals = true)
    {
        if (options.BackslashLineContinuations)
        {
            text = JoinBackslashLines(text);
        }

        var code = text.ToCharArray();
        var quote = '\0';
        var triple = false;
        var line = false;
        var block = new Stack<char>();
        var literal = trackLiterals ? new BitArray(text.Length + 1) : null;

        var i = 0;
        for (; i < text.Length; i++)
        {
            if ((i & 4095) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            Mark(i);
            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (line)
            {
                if (c is '\n' or '\r')
                {
                    line = false;
                }
                else
                {
                    Hide(i);
                }
                continue;
            }

            if (block.Count > 0)
            {
                Hide(i);
                if (IsBlockClose(block.Peek(), c, next))
                {
                    Consume();
                    Hide(i);
                    block.Pop();
                }
                else if (TryOpenBlock(c, next, options, out var nested))
                {
                    Consume();
                    Hide(i);
                    block.Push(nested);
                }
                continue;
            }

            if (quote != '\0')
            {
                if (!triple && c is '\n' or '\r')
                {
                    quote = '\0';
                    continue;
                }

                HideString(i);
                if (options.StringEscapes && c == '\\' && next != '\0')
                {
                    Consume();
                    HideString(i);
                    if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        Consume();
                        HideString(i);
                    }

                    continue;
                }

                if (c == quote)
                {
                    if (triple)
                    {
                        if (next == quote && i + 2 < text.Length && text[i + 2] == quote)
                        {
                            Consume();
                            HideString(i);
                            Consume();
                            HideString(i);
                            quote = '\0';
                        }
                    }
                    else if (options.DoubledQuotes && next == quote)
                    {
                        Consume();
                        HideString(i);
                    }
                    else
                    {
                        quote = '\0';
                    }
                }
                continue;
            }

            if (options.HashLineComments && c == '#')
            {
                line = true;
                Mark(i);
                Hide(i);
            }
            else if (TryOpenBlock(c, next, options, out var marker))
            {
                block.Push(marker);
                Mark(i);
                Hide(i);
                Consume();
                Hide(i);
            }
            else if (c == '"' || (options.SingleQuotes && c == '\''))
            {
                quote = c;
                triple = options.TripleQuotes && next == c && i + 2 < text.Length && text[i + 2] == c;
                Mark(i);
                HideString(i);
                if (triple)
                {
                    Consume();
                    HideString(i);
                    Consume();
                    HideString(i);
                }
            }
            else if (options.PythonLineContinuations && c == '\\' && TryPythonContinuation(text, i, out var last))
            {
                for (var k = i; k <= last; k++)
                {
                    code[k] = ' ';
                }

                i = last;
            }
        }

        var inLiteral = quote != '\0' || line || block.Count > 0;
        if (literal != null)
        {
            literal[text.Length] = inLiteral;
        }

        return new(new(code), inLiteral) { LiteralAt = literal };

        void Consume()
        {
            i++;
            Mark(i);
        }

        void Mark(int index)
        {
            if (literal != null && (uint)index < (uint)literal.Length)
            {
                literal[index] = quote != '\0' || line || block.Count > 0;
            }
        }

        void HideString(int index)
        {
            if (maskStrings)
            {
                Hide(index);
            }
        }

        void Hide(int index)
        {
            if (code[index] != '\n' && code[index] != '\r')
            {
                code[index] = ' ';
            }
        }
    }

    /// <summary>
    /// Turns AviSynth <c>\</c> line continuations into spaces of the same length.
    /// </summary>
    public static string JoinBackslashLines(string text) =>
        BackslashLine().Replace(text, static match => new(' ', match.Length));

    private static bool TryPythonContinuation(string text, int backslash, out int last)
    {
        var j = backslash + 1;
        while (j < text.Length && text[j] is ' ' or '\t')
        {
            j++;
        }

        last = backslash;
        if (j >= text.Length || text[j] is not ('\r' or '\n'))
        {
            return false;
        }

        last = text[j] == '\r' && j + 1 < text.Length && text[j + 1] == '\n' ? j + 1 : j;
        return true;
    }

    [GeneratedRegex(@"\\[ \t]*\r?\n[ \t]*\\?|\r?\n[ \t]*\\")]
    private static partial Regex BackslashLine();

    /// <summary>
    /// Closer markers: <c>*</c> is <c>*/</c>, <c>]</c> is <c>]/</c>, <c>[</c> is <c>*]</c>.
    /// </summary>
    private static bool TryOpenBlock(char c, char next, LexerOptions options, out char marker)
    {
        if (c == '/' && next == '*' && options.SlashStarBlocks)
        {
            marker = '*';
            return true;
        }

        if (c == '/' && next == '[' && options.SlashBracketBlocks)
        {
            marker = ']';
            return true;
        }

        if (c == '[' && next == '*' && options.StarBracketBlocks)
        {
            marker = '[';
            return true;
        }

        marker = '\0';
        return false;
    }

    private static bool IsBlockClose(char marker, char c, char next) =>
        marker == '*' && c == '*' && next == '/' ||
        marker == ']' && c == ']' && next == '/' ||
        marker == '[' && c == '*' && next == ']';
}
