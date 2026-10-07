namespace HanumanInstitute.ApiVapourSynth;

/// <summary>
/// Describes how to locate a native VapourSynth script host.
/// </summary>
internal sealed class NativeLibraryProfile
{
    public required string Id { get; init; }
    public required string ImportName { get; init; }
    public required IReadOnlyList<string> FileNames { get; init; }
    public required IReadOnlyList<string> RequiredExports { get; init; }
}
