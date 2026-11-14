using System.Runtime.InteropServices;

namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Copies the native format, duration, and frame count of an audio output.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct VsAudioInfo
{
    /// <summary>
    /// Gets the planar sample storage and speaker layout.
    /// </summary>
    public VsAudioFormat Format;
    /// <summary>
    /// Gets the sampling frequency in hertz.
    /// </summary>
    public int SampleRate;
    /// <summary>
    /// Gets the number of samples per channel in the complete output.
    /// </summary>
    public long NumSamples;
    /// <summary>
    /// Gets the number of audio frames, including the possibly shorter final frame.
    /// </summary>
    public int NumFrames;
}
