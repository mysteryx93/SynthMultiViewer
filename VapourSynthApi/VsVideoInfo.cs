using System.Runtime.InteropServices;

namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Describes a VapourSynth 4 video output.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public class VsVideoInfo
{
    /// <summary>
    /// Gets the pixel format.
    /// </summary>
    public VsFormat Format;

    /// <summary>
    /// Gets the frame-rate numerator.
    /// </summary>
    public long FpsNum;

    /// <summary>
    /// Gets the frame-rate denominator.
    /// </summary>
    public long FpsDen;

    /// <summary>
    /// Gets the frame width.
    /// </summary>
    public int Width;

    /// <summary>
    /// Gets the frame height.
    /// </summary>
    public int Height;

    /// <summary>
    /// Gets the number of frames.
    /// </summary>
    public int NumFrames;

    /// <summary>
    /// Checks whether the output has a fixed size and format.
    /// </summary>
    public bool IsConstantFormat => Width > 0 && Height > 0 && Format.NumPlanes > 0;

    /// <summary>
    /// Checks whether two outputs have the same dimensions and format.
    /// </summary>
    public bool IsSameFormat(VsVideoInfo clip) => Width == clip.Width && Height == clip.Height && Format.Equals(clip.Format);
}
