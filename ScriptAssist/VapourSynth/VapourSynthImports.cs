namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Places a top-level <c>import module</c> so an explorer insert is valid.
/// </summary>
public static class VapourSynthImports
{
    /// <summary>
    /// Returns whether the buffer already has a column-0 <c>import module</c> (optional <c>as</c>).
    /// <c>from module import</c> does not count; that does not bind the module name.
    /// </summary>
    public static bool Contains(string text, string module)
    {
        if (!module.HasText())
        {
            return false;
        }

        foreach (var line in Lines(text ?? ""))
        {
            if (LineImports(line.Text, module))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Offset after the last column-0 <c>import</c> / <c>from</c> line, or 0 when none exist.
    /// </summary>
    public static int InsertionOffset(string text)
    {
        var last = 0;
        foreach (var line in Lines(text ?? ""))
        {
            if (line.Text.StartsWith("import ", StringComparison.Ordinal) ||
                line.Text.StartsWith("from ", StringComparison.Ordinal))
            {
                last = line.End;
            }
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

    private static bool LineImports(ReadOnlySpan<char> line, string module)
    {
        if (!line.StartsWith("import ", StringComparison.Ordinal))
        {
            return false;
        }

        var rest = line[7..];
        while (rest.Length > 0 && rest[0] == ' ')
        {
            rest = rest[1..];
        }

        if (rest.Length < module.Length ||
            !rest.StartsWith(module, StringComparison.Ordinal))
        {
            return false;
        }

        if (rest.Length == module.Length)
        {
            return true;
        }

        var next = rest[module.Length];
        return next is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_');
    }

    private static IEnumerable<(string Text, int End)> Lines(string text)
    {
        var i = 0;
        while (i < text.Length)
        {
            var newline = text.IndexOf('\n', i);
            var lineEnd = newline < 0 ? text.Length : newline;
            var span = text.AsSpan(i, lineEnd - i);
            if (span.Length > 0 && span[^1] == '\r')
            {
                span = span[..^1];
            }

            var end = newline < 0 ? text.Length : newline + 1;
            yield return (span.ToString(), end);
            i = end;
        }
    }
}
