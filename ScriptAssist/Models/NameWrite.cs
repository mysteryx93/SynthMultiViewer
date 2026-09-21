namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// One module-level name change, in bind order, used to reconstruct names at a caret.
/// </summary>
internal readonly record struct NameWrite(int Offset, string Name, TypeRef Type, bool Remove = false);
