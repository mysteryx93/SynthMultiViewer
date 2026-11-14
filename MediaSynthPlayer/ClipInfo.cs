using HanumanInstitute.AviSynthApi;
using HanumanInstitute.VapourSynthApi;

namespace HanumanInstitute.MediaSynthUI;

/// <summary>
/// Describes the script output before display conversion.
/// </summary>
public sealed class ClipInfo
{
    /// <summary>
    /// Gets which engine produced the clip.
    /// </summary>
    public required ScriptKind Host { get; init; }

    /// <summary>
    /// Gets the frame width in pixels.
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// Gets the frame height in pixels.
    /// </summary>
    public required int Height { get; init; }

    /// <summary>
    /// Gets the number of frames.
    /// </summary>
    public required int FrameCount { get; init; }

    /// <summary>
    /// Gets the frame-rate numerator.
    /// </summary>
    public required long FpsNumerator { get; init; }

    /// <summary>
    /// Gets the frame-rate denominator.
    /// </summary>
    public required long FpsDenominator { get; init; }

    /// <summary>
    /// Gets the pixel-format name, such as YUV420P10.
    /// </summary>
    public required string FormatName { get; init; }

    /// <summary>
    /// Gets the color family name, such as YUV or RGB.
    /// </summary>
    public required string ColorFamily { get; init; }

    /// <summary>
    /// Gets the significant bits per sample.
    /// </summary>
    public required int BitDepth { get; init; }

    /// <summary>
    /// Gets Integer or Float.
    /// </summary>
    public required string SampleType { get; init; }

    /// <summary>
    /// Gets chroma subsampling as 4:x:y, or null when the format has no chroma.
    /// </summary>
    public string? Subsampling { get; init; }

    /// <summary>
    /// Gets the number of image planes.
    /// </summary>
    public required int Planes { get; init; }

    /// <summary>
    /// Builds clip information from the AviSynth source video info.
    /// </summary>
    public static ClipInfo FromAviSynth(AvsVideoInfo info) => new()
    {
        Host = ScriptKind.AviSynth,
        Width = info.Width,
        Height = info.Height,
        FrameCount = info.FrameCount,
        FpsNumerator = info.FpsNumerator,
        FpsDenominator = info.FpsDenominator,
        FormatName = AvsPixelFormat.GetName(info.PixelType),
        ColorFamily = AvsPixelFormat.GetColorFamily(info.PixelType),
        BitDepth = AvsPixelFormat.GetBitDepth(info.PixelType),
        SampleType = AvsPixelFormat.GetSampleType(info.PixelType),
        Subsampling = AvsPixelFormat.GetSubsampling(info.PixelType),
        Planes = AvsPixelFormat.GetPlaneCount(info.PixelType)
    };

    /// <summary>
    /// Builds clip information from the VapourSynth source video info.
    /// </summary>
    public static ClipInfo FromVapourSynth(VsVideoInfo info) => new()
    {
        Host = ScriptKind.VapourSynth,
        Width = info.Width,
        Height = info.Height,
        FrameCount = info.NumFrames,
        FpsNumerator = info.FpsNum,
        FpsDenominator = info.FpsDen,
        FormatName = VsFormatName.From(info.Format),
        ColorFamily = info.Format.ColorFamily switch
        {
            VsColorFamily.Gray => "Gray",
            VsColorFamily.RGB => "RGB",
            VsColorFamily.YUV => "YUV",
            _ => "Unknown"
        },
        BitDepth = info.Format.BitsPerSample,
        SampleType = info.Format.SampleType == VsSampleType.Float ? "Float" : "Integer",
        Subsampling = VsFormatName.Subsampling(info.Format),
        Planes = info.Format.NumPlanes
    };
}

/// <summary>
/// A single frame-property name and display value.
/// </summary>
public sealed record FrameProperty(string Name, string Value);
