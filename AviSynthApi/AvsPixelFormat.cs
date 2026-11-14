namespace HanumanInstitute.AviSynthApi;

/// <summary>
/// Decodes AviSynth pixel-type identifiers into display names and layout facts.
/// </summary>
public static class AvsPixelFormat
{
    private const int Yuva = 1 << 27;
    private const int Bgr = 1 << 28;
    private const int Yuv = 1 << 29;
    private const int Interleaved = 1 << 30;
    private const int Planar = unchecked((int)0x80000000);
    private const int SubWidthMask = 7;
    private const int SubWidth1 = 3;
    private const int SubWidth4 = 1;
    private const int VPlaneFirst = 1 << 3;
    private const int SubHeightMask = 7 << 8;
    private const int SubHeight1 = 3 << 8;
    private const int SubHeight4 = 1 << 8;
    private const int SampleBitsMask = 7 << 16;
    private const int SampleBits10 = 5 << 16;
    private const int SampleBits12 = 6 << 16;
    private const int SampleBits14 = 7 << 16;
    private const int SampleBits16 = 1 << 16;
    private const int SampleBits32 = 2 << 16;
    private const int RgbType = 1;
    private const int RgbaType = 2;
    private const int GenericYuv444 = Planar | Yuv | VPlaneFirst | SubWidth1 | SubHeight1;
    private const int GenericYuv422 = Planar | Yuv | VPlaneFirst | SubHeight1;
    private const int GenericYuv420 = Planar | Yuv | VPlaneFirst;
    private const int GenericY = Planar | Interleaved | Yuv;
    private const int GenericRgbP = Planar | Bgr | RgbType;
    private const int GenericRgbaP = Planar | Bgr | RgbaType;
    private const int GenericYuva444 = Planar | Yuva | VPlaneFirst | SubWidth1 | SubHeight1;
    private const int GenericYuva422 = Planar | Yuva | VPlaneFirst | SubHeight1;
    private const int GenericYuva420 = Planar | Yuva | VPlaneFirst;

    private static readonly Dictionary<int, string> Names = new()
    {
        [RgbType | Bgr | Interleaved] = "RGB24",
        [RgbaType | Bgr | Interleaved] = "RGB32",
        [1 << 2 | Yuv | Interleaved] = "YUY2",
        [1 << 5 | Interleaved] = "RAW32",
        [GenericYuv444] = "YV24",
        [GenericYuv422] = "YV16",
        [GenericYuv420] = "YV12",
        [Planar | Yuv | (1 << 4)] = "I420",
        [Planar | Yuv | VPlaneFirst | SubWidth4 | SubHeight1] = "YV411",
        [Planar | Yuv | VPlaneFirst | SubWidth4 | SubHeight4] = "YUV9",
        [GenericY] = "Y8",
        [GenericYuv444 | SampleBits10] = "YUV444P10",
        [GenericYuv422 | SampleBits10] = "YUV422P10",
        [GenericYuv420 | SampleBits10] = "YUV420P10",
        [GenericY | SampleBits10] = "Y10",
        [GenericYuv444 | SampleBits12] = "YUV444P12",
        [GenericYuv422 | SampleBits12] = "YUV422P12",
        [GenericYuv420 | SampleBits12] = "YUV420P12",
        [GenericY | SampleBits12] = "Y12",
        [GenericYuv444 | SampleBits14] = "YUV444P14",
        [GenericYuv422 | SampleBits14] = "YUV422P14",
        [GenericYuv420 | SampleBits14] = "YUV420P14",
        [GenericY | SampleBits14] = "Y14",
        [GenericYuv444 | SampleBits16] = "YUV444P16",
        [GenericYuv422 | SampleBits16] = "YUV422P16",
        [GenericYuv420 | SampleBits16] = "YUV420P16",
        [GenericY | SampleBits16] = "Y16",
        [GenericYuv444 | SampleBits32] = "YUV444PS",
        [GenericYuv422 | SampleBits32] = "YUV422PS",
        [GenericYuv420 | SampleBits32] = "YUV420PS",
        [GenericY | SampleBits32] = "Y32",
        [RgbType | Bgr | Interleaved | SampleBits16] = "RGB48",
        [RgbaType | Bgr | Interleaved | SampleBits16] = "RGB64",
        [GenericRgbP] = "RGBP",
        [GenericRgbP | SampleBits10] = "RGBP10",
        [GenericRgbP | SampleBits12] = "RGBP12",
        [GenericRgbP | SampleBits14] = "RGBP14",
        [GenericRgbP | SampleBits16] = "RGBP16",
        [GenericRgbP | SampleBits32] = "RGBPS",
        [GenericRgbaP] = "RGBAP",
        [GenericRgbaP | SampleBits10] = "RGBAP10",
        [GenericRgbaP | SampleBits12] = "RGBAP12",
        [GenericRgbaP | SampleBits14] = "RGBAP14",
        [GenericRgbaP | SampleBits16] = "RGBAP16",
        [GenericRgbaP | SampleBits32] = "RGBAPS",
        [GenericYuva444] = "YUVA444",
        [GenericYuva422] = "YUVA422",
        [GenericYuva420] = "YUVA420",
        [GenericYuva444 | SampleBits10] = "YUVA444P10",
        [GenericYuva422 | SampleBits10] = "YUVA422P10",
        [GenericYuva420 | SampleBits10] = "YUVA420P10",
        [GenericYuva444 | SampleBits12] = "YUVA444P12",
        [GenericYuva422 | SampleBits12] = "YUVA422P12",
        [GenericYuva420 | SampleBits12] = "YUVA420P12",
        [GenericYuva444 | SampleBits14] = "YUVA444P14",
        [GenericYuva422 | SampleBits14] = "YUVA422P14",
        [GenericYuva420 | SampleBits14] = "YUVA420P14",
        [GenericYuva444 | SampleBits16] = "YUVA444P16",
        [GenericYuva422 | SampleBits16] = "YUVA422P16",
        [GenericYuva420 | SampleBits16] = "YUVA420P16",
        [GenericYuva444 | SampleBits32] = "YUVA444PS",
        [GenericYuva422 | SampleBits32] = "YUVA422PS",
        [GenericYuva420 | SampleBits32] = "YUVA420PS"
    };

    /// <summary>
    /// Returns the AviSynth pixel-type name used in scripts, or "Unknown".
    /// </summary>
    public static string GetName(int pixelType) =>
        Names.TryGetValue(pixelType, out var name) ? name : "Unknown";

    /// <summary>
    /// Returns YUV, YUVA, RGB, Gray, or Unknown.
    /// </summary>
    public static string GetColorFamily(int pixelType)
    {
        if ((pixelType & Yuva) != 0)
        {
            return "YUVA";
        }

        if ((pixelType & Yuv) != 0)
        {
            return IsGray(pixelType) ? "Gray" : "YUV";
        }

        return (pixelType & Bgr) != 0 ? "RGB" : "Unknown";
    }

    /// <summary>
    /// Returns the significant bits per component.
    /// </summary>
    public static int GetBitDepth(int pixelType) => (pixelType & SampleBitsMask) switch
    {
        SampleBits10 => 10,
        SampleBits12 => 12,
        SampleBits14 => 14,
        SampleBits16 => 16,
        SampleBits32 => 32,
        _ => 8
    };

    /// <summary>
    /// Returns Integer or Float. AviSynth 32-bit planar samples are float.
    /// </summary>
    public static string GetSampleType(int pixelType) =>
        (pixelType & SampleBitsMask) == SampleBits32 && (pixelType & Planar) != 0 ? "Float" : "Integer";

    /// <summary>
    /// Returns chroma subsampling as 4:x:y, or null when the format has no chroma planes.
    /// </summary>
    public static string? GetSubsampling(int pixelType)
    {
        if ((pixelType & (Yuv | Yuva)) == 0 || IsGray(pixelType) || (pixelType & Planar) == 0)
        {
            return pixelType == (1 << 2 | Yuv | Interleaved) ? "4:2:2" : null;
        }

        var horizontal = (pixelType & SubWidthMask) switch
        {
            SubWidth1 => 1,
            SubWidth4 => 4,
            _ => 2
        };
        var vertical = (pixelType & SubHeightMask) switch
        {
            SubHeight1 => 1,
            SubHeight4 => 4,
            _ => 2
        };
        return (horizontal, vertical) switch
        {
            (1, 1) => "4:4:4",
            (2, 1) => "4:2:2",
            (2, 2) => "4:2:0",
            (4, 1) => "4:1:1",
            (4, 4) => "4:1:0",
            _ => null
        };
    }

    /// <summary>
    /// Returns the number of image planes, counting packed RGB/YUY2 as one.
    /// </summary>
    public static int GetPlaneCount(int pixelType)
    {
        if ((pixelType & Yuva) != 0 || (pixelType & GenericRgbaP) == GenericRgbaP)
        {
            return 4;
        }

        if (IsGray(pixelType) || (pixelType & Interleaved) != 0)
        {
            return 1;
        }

        return 3;
    }

    private static bool IsGray(int pixelType) =>
        (pixelType & (Planar | Interleaved | Yuv | Yuva | Bgr)) == GenericY;
}
