namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Builds VapourSynth format names from a <see cref="VsFormat"/> layout.
/// </summary>
public static class VsFormatName
{
    /// <summary>
    /// Returns the preset name, such as YUV420P10 or RGB24.
    /// </summary>
    public static string From(VsFormat format)
    {
        if (format.ColorFamily == VsColorFamily.RGB)
        {
            if (format.SampleType == VsSampleType.Float)
            {
                return format.BitsPerSample == 16 ? "RGBH" : "RGBS";
            }

            return format.BitsPerSample switch
            {
                8 => "RGB24",
                9 => "RGB27",
                10 => "RGB30",
                12 => "RGB36",
                14 => "RGB42",
                16 => "RGB48",
                _ => "RGB" + format.BitsPerSample
            };
        }

        if (format.ColorFamily == VsColorFamily.Gray)
        {
            if (format.SampleType == VsSampleType.Float)
            {
                return format.BitsPerSample == 16 ? "GRAYH" : "GRAYS";
            }

            return "GRAY" + format.BitsPerSample;
        }

        if (format.ColorFamily != VsColorFamily.YUV)
        {
            return "Unknown";
        }

        var sub = (format.SubSamplingW, format.SubSamplingH) switch
        {
            (2, 2) => "410",
            (2, 0) => "411",
            (0, 1) => "440",
            (1, 1) => "420",
            (1, 0) => "422",
            (0, 0) => "444",
            _ => format.SubSamplingW + "x" + format.SubSamplingH
        };
        var suffix = format.SampleType == VsSampleType.Float
            ? format.BitsPerSample == 16 ? "H" : "S"
            : format.BitsPerSample.ToString();
        return "YUV" + sub + "P" + suffix;
    }

    /// <summary>
    /// Returns chroma subsampling as 4:x:y, or null for RGB and gray.
    /// </summary>
    public static string? Subsampling(VsFormat format)
    {
        if (format.ColorFamily != VsColorFamily.YUV)
        {
            return null;
        }

        return (format.SubSamplingW, format.SubSamplingH) switch
        {
            (0, 0) => "4:4:4",
            (1, 0) => "4:2:2",
            (1, 1) => "4:2:0",
            (2, 0) => "4:1:1",
            (2, 2) => "4:1:0",
            (0, 1) => "4:4:0",
            _ => null
        };
    }
}
