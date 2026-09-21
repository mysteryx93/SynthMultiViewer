namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Static members of the vapoursynth module, Core, VideoNode, AudioNode, and VideoFormat.
/// </summary>
public static class VapourSynthHostTypes
{
    private static readonly string[] Families = ["UNDEFINED", "RGB", "YUV", "GRAY"];

    private static readonly string[] Formats =
    [
        "NONE", "GRAY8", "GRAY9", "GRAY10", "GRAY12", "GRAY14", "GRAY16", "GRAY32", "GRAYH", "GRAYS",
        "YUV420P8", "YUV422P8", "YUV444P8", "YUV410P8", "YUV411P8", "YUV440P8",
        "YUV420P9", "YUV422P9", "YUV444P9", "YUV420P10", "YUV422P10", "YUV444P10",
        "YUV420P12", "YUV422P12", "YUV444P12", "YUV420P14", "YUV422P14", "YUV444P14",
        "YUV420P16", "YUV422P16", "YUV444P16", "YUV420PH", "YUV420PS", "YUV422PH", "YUV422PS",
        "YUV444PH", "YUV444PS",
        "RGB24", "RGB27", "RGB30", "RGB36", "RGB42", "RGB48", "RGBH", "RGBS"
    ];

    /// <summary>
    /// Gets module-level names offered after <c>vs.</c>.
    /// </summary>
    public static IReadOnlyList<Symbol> ModuleMembers { get; } =
    [
        new("core", null, SymbolKind.Namespace, ReturnType: "Core"),
        .. Formats.Select(x => new Symbol(x, null, SymbolKind.Property, ReturnType: "int")),
        .. Families.Select(x => new Symbol(x, null, SymbolKind.Property, ReturnType: "int")),
        new("INTEGER", null, SymbolKind.Property, ReturnType: "int"),
        new("FLOAT", null, SymbolKind.Property, ReturnType: "int"),
        new("Error", []),
        new("VideoNode", null, SymbolKind.Namespace),
        new("AudioNode", null, SymbolKind.Namespace)
    ];

    /// <summary>
    /// Gets Core attributes offered after <c>core.</c> besides plugin namespaces.
    /// </summary>
    public static IReadOnlyList<Symbol> CoreMembers { get; } =
    [
        new("num_threads", null, SymbolKind.Property, ReturnType: "int"),
        new("max_cache_size", null, SymbolKind.Property, ReturnType: "int"),
        new("used_cache_size", null, SymbolKind.Property, ReturnType: "int"),
        new("clear_cache", [], ReturnType: ""),
        new("plugins", []),
        new("get_video_format", ["id:int"], ReturnType: "VideoFormat"),
        new("query_video_format",
            ["color_family:int", "sample_type:int", "bits_per_sample:int", "subsampling_w:int:opt",
                "subsampling_h:int:opt"], ReturnType: "VideoFormat")
    ];

    /// <summary>
    /// Gets VideoNode members offered after a typed clip, besides plugin namespaces.
    /// </summary>
    public static IReadOnlyList<Symbol> VideoNodeMembers { get; } =
    [
        new("format", null, SymbolKind.Property, ReturnType: "VideoFormat"),
        new("width", null, SymbolKind.Property, ReturnType: "int"),
        new("height", null, SymbolKind.Property, ReturnType: "int"),
        new("num_frames", null, SymbolKind.Property, ReturnType: "int"),
        new("fps", null, SymbolKind.Property, ReturnType: "Fraction"),
        new("fps_num", null, SymbolKind.Property, ReturnType: "int"),
        new("fps_den", null, SymbolKind.Property, ReturnType: "int"),
        new("get_frame", ["n:int"], ReturnType: "vframe"),
        new("get_frame_async", ["n:int"]),
        new("set_output", ["index:int:opt", "alpha:vnode:opt", "alt_output:int:opt"]),
        new("output", ["fileobj", "y4m:int:opt"]),
        new("frames", ["prefetch:int:opt", "backlog:int:opt"]),
        new("clear_cache", [])
    ];

    /// <summary>
    /// Gets AudioNode members offered after a typed audio clip, besides plugin namespaces.
    /// </summary>
    public static IReadOnlyList<Symbol> AudioNodeMembers { get; } =
    [
        new("sample_type", null, SymbolKind.Property, ReturnType: "int"),
        new("bits_per_sample", null, SymbolKind.Property, ReturnType: "int"),
        new("bytes_per_sample", null, SymbolKind.Property, ReturnType: "int"),
        new("num_channels", null, SymbolKind.Property, ReturnType: "int"),
        new("sample_rate", null, SymbolKind.Property, ReturnType: "int"),
        new("get_frame", ["n:int"]),
        new("set_output", ["index:int:opt"]),
        new("output", ["fileobj"]),
        new("frames", ["prefetch:int:opt"]),
        new("clear_cache", [])
    ];

    /// <summary>
    /// Gets VideoFormat members offered after <c>clip.format.</c>.
    /// </summary>
    public static IReadOnlyList<Symbol> FormatMembers { get; } =
    [
        new("id", null, SymbolKind.Property, ReturnType: "int"),
        new("name", null, SymbolKind.Property, ReturnType: "string"),
        new("color_family", null, SymbolKind.Property, ReturnType: "int"),
        new("sample_type", null, SymbolKind.Property, ReturnType: "int"),
        new("bits_per_sample", null, SymbolKind.Property, ReturnType: "int"),
        new("bytes_per_sample", null, SymbolKind.Property, ReturnType: "int"),
        new("subsampling_w", null, SymbolKind.Property, ReturnType: "int"),
        new("subsampling_h", null, SymbolKind.Property, ReturnType: "int"),
        new("num_planes", null, SymbolKind.Property, ReturnType: "int"),
        new("replace",
            ["color_family:int:opt", "sample_type:int:opt", "bits_per_sample:int:opt", "subsampling_w:int:opt",
                "subsampling_h:int:opt"], ReturnType: "VideoFormat")
    ];

    /// <summary>
    /// Gets VideoFrame members offered after <c>get_frame</c>.
    /// </summary>
    public static IReadOnlyList<Symbol> VideoFrameMembers { get; } =
    [
        new("format", null, SymbolKind.Property, ReturnType: "VideoFormat"),
        new("width", null, SymbolKind.Property, ReturnType: "int"),
        new("height", null, SymbolKind.Property, ReturnType: "int"),
        new("props", null, SymbolKind.Property),
        new("copy", [], ReturnType: "vframe"),
        new("close", [])
    ];
}
