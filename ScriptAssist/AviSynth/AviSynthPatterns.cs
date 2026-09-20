using System.Text.RegularExpressions;

namespace HanumanInstitute.ScriptAssist.AviSynth;

/// <summary>
/// Regular expressions for AviSynth buffer bindings.
/// </summary>
internal static partial class AviSynthPatterns
{
    [GeneratedRegex(@"\bfunction\s+([\p{L}_][\p{L}\p{N}_]*)\s*\(", RegexOptions.IgnoreCase)]
    public static partial Regex Functions();

    [GeneratedRegex(@"\bImport\s*\(\s*(?:""""""([\s\S]*?)""""""|""([^""]*)"")\s*\)", RegexOptions.IgnoreCase)]
    public static partial Regex Import();

    [GeneratedRegex(@"(?<=^|{)[^\S\r\n]*(?:(global)[^\S\r\n]+)?([\p{L}_][\p{L}\p{N}_\p{M}]*)[^\S\r\n]*=(?!=)([^}\r\n]*)",
        RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    public static partial Regex NameAssign();

    [GeneratedRegex(@"\s+")]
    public static partial Regex Whitespace();

    /// <summary>
    /// Turns AviSynth <c>\</c> line continuations into spaces of the same length.
    /// </summary>
    public static string JoinContinuations(string clean) => BufferLexer.JoinBackslashLines(clean);

    /// <summary>
    /// Masks comments and strings. <see cref="BufferLexer.Mask"/> joins <c>\</c> continuations first,
    /// so a leading <c>\</c> still joins a statement when the next line sits inside a triple-quoted string.
    /// </summary>
    public static string Clean(string text, LexerOptions lexer, bool maskStrings = true,
        CancellationToken token = default) =>
        BufferLexer.Mask(text, lexer, maskStrings, token: token, trackLiterals: false).Code;
}
