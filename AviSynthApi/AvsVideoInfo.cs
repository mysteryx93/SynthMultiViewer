using System.Runtime.InteropServices;

namespace HanumanInstitute.AviSynthApi;

/// <summary>
/// Copies the native AviSynth clip metadata, including its optional audio stream.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct AvsVideoInfo
{
    /// <summary>
    /// Gets the frame width.
    /// </summary>
    public int Width;

    /// <summary>
    /// Gets the frame height.
    /// </summary>
    public int Height;

    /// <summary>
    /// Gets the frame-rate numerator.
    /// </summary>
    public uint FpsNumerator;

    /// <summary>
    /// Gets the frame-rate denominator.
    /// </summary>
    public uint FpsDenominator;

    /// <summary>
    /// Gets the number of frames.
    /// </summary>
    public int FrameCount;

    /// <summary>
    /// Gets the AviSynth pixel-format identifier.
    /// </summary>
    public int PixelType;

    /// <summary>
    /// Gets the audio sampling frequency, or zero when the clip has no audio.
    /// </summary>
    public int SampleRate;

    /// <summary>
    /// Gets the storage of one audio channel sample.
    /// </summary>
    public AvsSampleType SampleType;

    /// <summary>
    /// Gets the number of audio samples per channel.
    /// </summary>
    public long AudioSampleCount;

    /// <summary>
    /// Gets the number of interleaved audio channels.
    /// </summary>
    public int Channels;

    /// <summary>
    /// Gets the native field-order and channel-mask flags.
    /// </summary>
    public int ImageType;

    /// <summary>
    /// Gets whether the clip contains video frames.
    /// </summary>
    public readonly bool HasVideo => Width > 0 && Height > 0;

    /// <summary>
    /// Gets whether the clip contains an audio stream.
    /// </summary>
    public readonly bool HasAudio => SampleRate > 0 && Channels > 0;

    /// <summary>
    /// Gets the storage bytes occupied by one channel sample.
    /// </summary>
    public readonly int BytesPerChannelSample => SampleType switch
    {
        AvsSampleType.Int8 => 1,
        AvsSampleType.Int16 => 2,
        AvsSampleType.Int24 => 3,
        AvsSampleType.Int32 or AvsSampleType.Float => 4,
        _ => 0
    };

    /// <summary>
    /// Gets the storage bytes for one sample across all interleaved channels.
    /// </summary>
    public readonly int BytesPerAudioSample => checked(BytesPerChannelSample * Channels);

    /// <summary>
    /// Gets the script pixel-type name, such as YUV420P10.
    /// </summary>
    public readonly string FormatName => AvsPixelFormat.GetName(PixelType);
}
