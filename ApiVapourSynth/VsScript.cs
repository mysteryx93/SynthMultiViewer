namespace HanumanInstitute.ApiVapourSynth;

/// <summary>
/// Owns a VapourSynth 4 script environment and its outputs.
/// </summary>
public sealed class VsScript : IDisposable
{
    private readonly VsScriptApi _scriptApi;
    private readonly IntPtr _handle;
    private VsOutput? _sourceOutput;
    private bool _disposed;

    private VsScript(VsScriptApi scriptApi, IntPtr handle)
    {
        _scriptApi = scriptApi;
        _handle = handle;
    }

    /// <summary>
    /// Creates an empty script environment.
    /// </summary>
    public static VsScript CreateEmpty()
    {
        var scriptApi = VsScriptApi.Load();
        var handle = scriptApi.CreateScript();
        if (handle == IntPtr.Zero)
        {
            throw new VsException("VapourSynth could not create a script environment.");
        }

        return new(scriptApi, handle);
    }

    /// <summary>
    /// Gets the absolute path used when evaluating a script file.
    /// </summary>
    public static string ResolveScriptPath(string path) => VsPathResolver.ResolveScriptPath(path);

    /// <summary>
    /// Loads a script file and evaluates it with its directory as the working directory.
    /// </summary>
    /// <param name="path">The script file to load.</param>
    public static VsScript LoadFile(string path)
    {
        var resolvedPath = ResolveScriptPath(path);
        return LoadScript(File.ReadAllText(resolvedPath), resolvedPath);
    }

    /// <summary>
    /// Loads a script file.
    /// </summary>
    /// <param name="path">The script file to load.</param>
    /// <param name="convertToCompatBgr32">Unsupported by VapourSynth 4. The output must be converted in the script.</param>
    public static VsScript LoadFile(string path, bool convertToCompatBgr32)
    {
        if (convertToCompatBgr32)
        {
            throw new NotSupportedException("COMPATBGR32 was removed in VapourSynth 4. Convert the output to RGB in the script.");
        }

        return LoadFileDirect(path, true);
    }

    /// <summary>
    /// Loads a script file and optionally sets its directory as the temporary working directory.
    /// </summary>
    public static VsScript LoadFileDirect(string path, bool setWorkingDir)
    {
        var resolvedPath = ResolveScriptPath(path);
        var script = CreateEmpty();
        using var fileName = new Utf8Ptr(resolvedPath);

        script._scriptApi.SetWorkingDirectory(script._handle, setWorkingDir);
        if (script._scriptApi.EvaluateFile(script._handle, fileName.ptr) == 0)
        {
            return script;
        }

        var error = script.GetError();
        script.Dispose();
        throw new VsException(error ?? "VapourSynth could not evaluate the script.");
    }

    /// <summary>
    /// Loads script text using an optional source path for error messages, <c>__file__</c>,
    /// and relative imports. VapourSynth evaluates the buffer; the path is not read from disk.
    /// Display conversion can be disabled when processing without previewing.
    /// </summary>
    /// <param name="script">The VapourSynth script text to evaluate.</param>
    /// <param name="scriptPath">The source path used for relative imports and diagnostics, or null for an unsaved script.</param>
    /// <param name="convertForDisplay">Whether to convert output 0 for preview; defaults to true.</param>
    public static VsScript LoadScript(string script, string? scriptPath = null, bool convertForDisplay = true)
    {
        script.CheckNotNullOrEmpty();
        var environment = CreateEmpty();
        try
        {
            environment.EvaluateBuffer(script, scriptPath);
            environment._sourceOutput = environment.GetOutput(0);
            environment.SourceVideoInfo = environment._sourceOutput.VideoInfo;
            if (convertForDisplay)
            {
                environment.ConvertOutputToRgb24();
            }

            return environment;
        }
        catch
        {
            environment.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Evaluates a tiny RGB clip to verify the loaded VapourSynth can run scripts.
    /// </summary>
    public static bool TryEvaluate(out string? error)
    {
        try
        {
            using var script = LoadScript(ProbeScript);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or VsException)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Returns the loaded core's release label, such as R79.
    /// </summary>
    public static bool TryReadVersion(out string? version, out string? detail)
    {
        version = null;
        detail = null;
        try
        {
            using var script = CreateEmpty();
            var core = script._scriptApi.GetCore(script._handle);
            if (core == IntPtr.Zero)
            {
                return false;
            }

            var info = VsCoreApi.Load(script._scriptApi.CoreApi).GetCoreInfo(core);
            detail = Utf8Ptr.FromUtf8Ptr(info.VersionString);
            if (info.Core > 0)
            {
                version = "R" + info.Core;
                return true;
            }

            if (!detail.HasText())
            {
                return false;
            }

            version = CompactVersion(detail);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string CompactVersion(string detail)
    {
        foreach (var line in detail.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Core R", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[5..].Trim();
            }

            if (trimmed.StartsWith("R", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 1 &&
                char.IsDigit(trimmed[1]))
            {
                return trimmed.Split(' ', 2)[0];
            }
        }

        return detail.Split('\n')[0].Trim();
    }

    private void EvaluateBuffer(string script, string? scriptPath)
    {
        using var buffer = new Utf8Ptr(script);
        EvaluateNamedBuffer(buffer.ptr, scriptPath, "VapourSynth could not evaluate the script.");
    }

    /// <summary>
    /// Converts output 0 to RGB24 after the user script has finished, so display packing
    /// does not change the graph the script built.
    /// </summary>
    private void ConvertOutputToRgb24()
    {
        using var buffer = new Utf8Ptr(DisplayConversion);
        EvaluateNamedBuffer(buffer.ptr, null, "VapourSynth could not convert the output for display.");
    }

    // VSScript uses "<string>" when the filename is NULL. Some releases still pass that
    // NULL into compile()/os.path and raise "expected str, bytes or os.PathLike object,
    // not NoneType" on unsaved buffers, so always send the placeholder name.
    private const string UnsavedScriptName = "<string>";

    private void EvaluateNamedBuffer(IntPtr buffer, string? scriptPath, string fallbackError)
    {
        if (!scriptPath.HasText())
        {
            using var unsaved = new Utf8Ptr(UnsavedScriptName);
            if (_scriptApi.EvaluateBuffer(_handle, buffer, unsaved.ptr) != 0)
            {
                throw new VsException(GetError() ?? fallbackError);
            }

            return;
        }

        var resolvedPath = ResolveScriptPath(scriptPath);
        using var fileName = new Utf8Ptr(resolvedPath);
        var directory = Path.GetDirectoryName(resolvedPath);
        if (directory.HasValue() && Directory.Exists(directory))
        {
            _scriptApi.SetWorkingDirectory(_handle, true);
        }

        if (_scriptApi.EvaluateBuffer(_handle, buffer, fileName.ptr) != 0)
        {
            throw new VsException(GetError() ?? fallbackError);
        }
    }

    private const string ProbeScript = """
        import vapoursynth as vs
        core = vs.core
        clip = core.std.BlankClip(width=16, height=16, length=1, format=vs.RGB24)
        clip.set_output()
        """;

    private const string DisplayConversion = """
import vapoursynth as synthmultiviewer_vs
synthmultiviewer_outputs = synthmultiviewer_vs.get_outputs()
if 0 not in synthmultiviewer_outputs:
    raise synthmultiviewer_vs.Error("The script did not set video output 0.")
synthmultiviewer_out = synthmultiviewer_outputs[0]
synthmultiviewer_node = synthmultiviewer_out.clip if hasattr(synthmultiviewer_out, "clip") else synthmultiviewer_out
if not isinstance(synthmultiviewer_node, synthmultiviewer_vs.VideoNode):
    raise synthmultiviewer_vs.Error("Output 0 is not a video node.")
synthmultiviewer_format = synthmultiviewer_node.format
# zimg/Expr reject 32-bit integer; take the high 8 bits so display conversion can run.
if (
    synthmultiviewer_format is not None
    and synthmultiviewer_format.color_family == synthmultiviewer_vs.GRAY
    and synthmultiviewer_format.sample_type == synthmultiviewer_vs.INTEGER
    and synthmultiviewer_format.bits_per_sample == 32
):
    synthmultiviewer_gray8 = synthmultiviewer_vs.core.std.BlankClip(
        synthmultiviewer_node, format=synthmultiviewer_vs.GRAY8)
    def synthmultiviewer_int32_to_8(n, f):
        src, proto = f[0], f[1]
        dst = proto.copy()
        sp, dp = src[0], dst[0]
        for y in range(src.height):
            for x in range(src.width):
                dp[y, x] = sp[y, x] >> 24
        return dst
    synthmultiviewer_node = synthmultiviewer_vs.core.std.ModifyFrame(
        synthmultiviewer_gray8, [synthmultiviewer_node, synthmultiviewer_gray8],
        synthmultiviewer_int32_to_8)
    synthmultiviewer_format = synthmultiviewer_node.format
synthmultiviewer_rgb24 = (
    synthmultiviewer_format is not None
    and synthmultiviewer_format.color_family == synthmultiviewer_vs.RGB
    and synthmultiviewer_format.bits_per_sample == 8
    and synthmultiviewer_format.sample_type == synthmultiviewer_vs.INTEGER
    and synthmultiviewer_format.num_planes == 3
)
if not synthmultiviewer_rgb24:
    synthmultiviewer_args = {"format": synthmultiviewer_vs.RGB24}
    if synthmultiviewer_format is None or synthmultiviewer_format.color_family in (
        synthmultiviewer_vs.YUV, synthmultiviewer_vs.GRAY):
        synthmultiviewer_mid = None
        try:
            synthmultiviewer_mid = synthmultiviewer_node.get_frame(0).props.get("_Matrix")
        except Exception:
            synthmultiviewer_mid = None
        if synthmultiviewer_mid == 1:
            synthmultiviewer_mat = "709"
        elif synthmultiviewer_mid in (5, 6):
            synthmultiviewer_mat = "170m"
        elif synthmultiviewer_mid in (9, 10):
            synthmultiviewer_mat = "2020ncl"
        else:
            synthmultiviewer_mat = "709" if synthmultiviewer_node.height >= 720 else "170m"
        # zimg keeps _Matrix over matrix_in_s when they disagree, so tag must match.
        synthmultiviewer_node = synthmultiviewer_node.std.SetFrameProps(
            _Matrix={"709": 1, "170m": 6, "2020ncl": 9}[synthmultiviewer_mat])
        synthmultiviewer_args["matrix_in_s"] = synthmultiviewer_mat
    synthmultiviewer_node = synthmultiviewer_node.resize.Bicubic(**synthmultiviewer_args)
synthmultiviewer_node.set_output()
""";

    /// <summary>
    /// Gets the video information before display conversion.
    /// </summary>
    public VsVideoInfo? SourceVideoInfo { get; private set; }

    /// <summary>
    /// Returns frame properties from the source output, before display conversion.
    /// Must not be called from a getFrameAsync callback; that deadlocks VapourSynth.
    /// </summary>
    public IReadOnlyList<(string Name, string Value)> GetSourceFrameProperties(int index)
    {
        ThrowIfDisposed();
        if (_sourceOutput == null)
        {
            return [];
        }

        using var frame = _sourceOutput.GetFrame(index);
        return frame.GetProperties();
    }

    /// <summary>
    /// Returns the first video output.
    /// </summary>
    public VsOutput GetOutput() => GetOutput(0);

    /// <summary>
    /// Returns the video output at the specified index.
    /// </summary>
    public VsOutput GetOutput(int index)
    {
        ThrowIfDisposed();
        return new(_scriptApi, _handle, index);
    }

    /// <summary>
    /// Releases the script environment after all outputs and frames have been released.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) { return; }

        _sourceOutput?.Dispose();
        _sourceOutput = null;
        _scriptApi.FreeScript(_handle);
        _disposed = true;
    }

    private string? GetError() => _scriptApi.GetError(_handle);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
