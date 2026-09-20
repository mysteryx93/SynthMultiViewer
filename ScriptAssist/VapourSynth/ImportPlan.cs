namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Where to write a column-0 <c>import</c> for an explorer insert, if it is needed.
/// </summary>
public readonly record struct ImportPlan(bool Needed, int Offset, string Text);
