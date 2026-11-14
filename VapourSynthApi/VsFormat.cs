using System.Runtime.InteropServices;

namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Describes a VapourSynth 4 video pixel layout.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct VsFormat
{
    /// <summary>
    /// Gets the color family.
    /// </summary>
    public VsColorFamily ColorFamily;

    /// <summary>
    /// Gets the sample representation.
    /// </summary>
    public VsSampleType SampleType;

    /// <summary>
    /// Gets the significant bits in each sample.
    /// </summary>
    public int BitsPerSample;

    /// <summary>
    /// Gets the storage bytes in each sample.
    /// </summary>
    public int BytesPerSample;

    /// <summary>
    /// Gets the horizontal subsampling exponent.
    /// </summary>
    public int SubSamplingW;

    /// <summary>
    /// Gets the vertical subsampling exponent.
    /// </summary>
    public int SubSamplingH;

    /// <summary>
    /// Gets the number of image planes.
    /// </summary>
    public int NumPlanes;

    /// <summary>
    /// Gets the preset format name, such as YUV420P10.
    /// </summary>
    public readonly string Name => VsFormatName.From(this);
}
