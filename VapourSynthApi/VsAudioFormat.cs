using System.Runtime.InteropServices;

namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Describes the planar sample storage and speaker layout of a VapourSynth audio stream.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct VsAudioFormat
{
    /// <summary>
    /// Gets whether samples are signed integers or floating-point values.
    /// </summary>
    public VsSampleType SampleType;
    /// <summary>
    /// Gets the number of significant bits in one channel sample.
    /// </summary>
    public int BitsPerSample;
    /// <summary>
    /// Gets the storage bytes per sample; 24-bit integer samples occupy four bytes.
    /// </summary>
    public int BytesPerSample;
    /// <summary>
    /// Gets the number of separate channel buffers.
    /// </summary>
    public int NumChannels;
    /// <summary>
    /// Gets the native speaker-position bit mask; channels follow ascending set bits.
    /// </summary>
    public ulong ChannelLayout;
}
