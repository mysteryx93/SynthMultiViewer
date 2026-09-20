using System.Collections;

namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Position-preserving code and whether the buffer ends inside a comment or string.
/// </summary>
internal sealed record LexedBuffer(string Code, bool InLiteral)
{
    /// <summary>
    /// Gets whether caret offset <paramref name="caret"/> sits inside a comment or string.
    /// </summary>
    internal bool IsLiteral(int caret)
    {
        if (LiteralAt != null && caret >= 0 && caret < LiteralAt.Length)
        {
            return LiteralAt[caret];
        }

        return InLiteral && caret >= Code.Length;
    }

    internal BitArray? LiteralAt { get; init; }

    internal int LiteralBytes => LiteralAt == null ? 0 : (LiteralAt.Length + 7) / 8;
}
