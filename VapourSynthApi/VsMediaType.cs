namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Identifies the native media carried by an output or frame.
/// </summary>
public enum VsMediaType
{
    /// <summary>
    /// Video frames with image planes.
    /// </summary>
    Video = 1,
    /// <summary>
    /// Audio frames with separate channel buffers.
    /// </summary>
    Audio = 2
}
