namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Masked and quoted buffers plus the statement list from one scan of the masked text.
/// </summary>
internal sealed record PreparedDocument(
    LexedBuffer Masked,
    LexedBuffer Quoted,
    IReadOnlyList<StatementScanner.Span> Statements,
    bool[] Joins)
{
    public static PreparedDocument Create(string text, LexerOptions lexer, CancellationToken token = default,
        ILanguage? language = null)
    {
        var masked = BufferLexer.Mask(text, lexer, token: token);
        var quoted = BufferLexer.Mask(text, lexer, maskStrings: false, token: token, trackLiterals: false);
        var statements = StatementScanner.Scan(masked.Code, token, language);
        return new(masked, quoted, statements, StatementScanner.JoinsFrom(masked.Code, statements));
    }
}
