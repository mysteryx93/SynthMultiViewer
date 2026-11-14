namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Identifies a VapourSynth color family.
/// </summary>
public enum VsColorFamily
{
    /// <summary>
    /// No color family.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// A grayscale plane.
    /// </summary>
    Gray = 1,

    /// <summary>
    /// Red, green, and blue planes.
    /// </summary>
    RGB = 2,

    /// <summary>
    /// Luma and chroma planes.
    /// </summary>
    YUV = 3
}
