using System.Runtime.InteropServices;

namespace HanumanInstitute.AviSynthApi;

/// <summary>
/// Owns an AviSynth clip and environment until its last frame is released.
/// </summary>
public sealed class AvsScript : SafeHandle
{
    private readonly Lock _gate = new();
    private bool _disposed;
    private readonly AvsNative _native;
    private IntPtr _clip;
    private IntPtr _sourceClip;

    // Take ownership only after script evaluation has produced a retained clip.
    private AvsScript(AvsNative native, IntPtr environment, IntPtr clip, IntPtr sourceClip, AvsVideoInfo sourceVideoInfo)
        : base(IntPtr.Zero, true)
    {
        _native = native;
        SetHandle(environment);
        _clip = clip;
        _sourceClip = sourceClip;
        SourceVideoInfo = sourceVideoInfo;
    }

    /// <summary>
    /// Gets the absolute path used when evaluating a script file.
    /// </summary>
    public static string ResolveScriptPath(string path) => AvsPathResolver.ResolveScriptPath(path);

    /// <summary>
    /// Returns plugin directories the loaded core already uses.
    /// </summary>
    public static IReadOnlyList<string> GetPluginDirectories(string? libraryPath = null) =>
        AvsPathResolver.GetPluginDirectories(libraryPath ?? (TryFindLibrary(out var path) ? path : null));

    /// <summary>
    /// Returns whether an AviSynth library that exports the C API can be loaded.
    /// </summary>
    public static bool TryFindLibrary(out string? path) => AvsNative.TryFindLibrary(out path);

    /// <summary>
    /// Evaluates a tiny RGB clip to verify the loaded AviSynth can run scripts.
    /// </summary>
    public static bool TryEvaluate(out string? error)
    {
        try
        {
            using var script = LoadScript(ProbeScript);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or AvsException)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Returns the loaded library's VersionString, such as AviSynth+ 3.7.5.
    /// </summary>
    public static bool TryReadVersion(out string? version, out string? detail)
    {
        version = null;
        detail = null;
        try
        {
            using var native = AvsNative.Load();
            var environment = native.CreateEnvironment();
            if (environment == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                var value = native.Eval(environment, "VersionString()");
                try
                {
                    if (value.Type != (short)'s')
                    {
                        return false;
                    }

                    detail = value.GetString();
                    if (!detail.HasText())
                    {
                        return false;
                    }

                    var paren = detail.IndexOf(" (", StringComparison.Ordinal);
                    version = paren > 0 ? detail[..paren] : detail.Trim();
                    return true;
                }
                finally
                {
                    native.ReleaseValue(value);
                }
            }
            finally
            {
                native.DeleteEnvironment(environment);
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Loads an AviSynth script file with its directory as the working directory.
    /// </summary>
    public static AvsScript LoadFile(string path, bool convertForDisplay = true, bool checkImportCycles = true)
    {
        var resolvedPath = ResolveScriptPath(path);
        var text = File.ReadAllText(resolvedPath);
        if (checkImportCycles) { AvsImportGraph.ThrowIfRecursive(text, resolvedPath); }
        return Load((native, environment) =>
            EvalClip(native, environment, "Import(\"" + Escape(resolvedPath) + "\", utf8=true)",
                "AviSynth could not evaluate the script."), convertForDisplay);
    }

    /// <summary>
    /// Evaluates AviSynth script text. <paramref name="scriptPath"/> is not read; its directory
    /// becomes the working directory for relative source paths. Display conversion can be
    /// disabled when processing the original clip without previewing it.
    /// </summary>
    /// <param name="script">The AviSynth script text to evaluate.</param>
    /// <param name="scriptPath">The source path used to resolve relative paths, or null for an unsaved script.</param>
    /// <param name="convertForDisplay">Whether to prepare a converted clip for preview; defaults to true.</param>
    /// <param name="checkImportCycles">Whether to reject statically detected import cycles before evaluation.</param>
    public static AvsScript LoadScript(string script, string? scriptPath = null, bool convertForDisplay = true, bool checkImportCycles = true)
    {
        script.CheckNotNullOrEmpty();
        if (script.Contains('\0')) { throw new ArgumentException("Script contains an embedded NUL.", nameof(script)); }
        var resolvedPath = scriptPath.HasText() ? ResolveScriptPath(scriptPath) : null;
        if (checkImportCycles) { AvsImportGraph.ThrowIfRecursive(script, resolvedPath); }
        var directory = resolvedPath.HasText() ? Path.GetDirectoryName(resolvedPath) : null;
        return Load((native, environment) =>
        {
            if (directory.HasValue() && Directory.Exists(directory))
            {
                if (OperatingSystem.IsWindows()) { Environment.CurrentDirectory = directory; }
                else { EvalDiscard(native, environment, "SetWorkingDir(\"" + Escape(directory) + "\")", "AviSynth could not set the working directory."); }
            }

            return EvalClip(native, environment, script, "AviSynth could not evaluate the script.");
        }, convertForDisplay);
    }

    private const string ProbeScript = """BlankClip(length=1, width=16, height=16, pixel_type="RGB24")""";

    // Run after the user script so `return` cannot skip display conversion.
    // _Matrix if tagged (1=709, 5/6=601, 9/10=2020); else Rec.709 at 720p+, Rec.601 below.
    // Convert can prefer _Matrix over the matrix argument, so retag 0/2/other to match.
    // ConvertBits(8) first so 10/12/14/16/float land on packed 8-bit RGB32, not a luma plane.
    private const string DisplayConversion = """
mx = FunctionExists("propNumElements") && propNumElements(last, "_Matrix") > 0 ? propGetInt(last, "_Matrix") : 0
Assert(mx != 10, "BT.2020 constant-luminance preview conversion is not supported by AviSynth")
mat = mx == 1 ? "Rec709" : mx == 9 || mx == 10 ? "Rec2020" : mx == 5 || mx == 6 ? "Rec601" : last.Height >= 720 ? "Rec709" : "Rec601"
mid = mat == "Rec709" ? 1 : mat == "Rec2020" ? 9 : 6
last = IsRGB() ? last : FunctionExists("propSet") ? propSet("_Matrix", mid) : last
last = FunctionExists("ConvertBits") && FunctionExists("BitsPerComponent") && BitsPerComponent() != 8 ? ConvertBits(8) : last
IsRGB() ? ConvertToRGB32() : ConvertToRGB32(matrix=mat)
""";

    private static string Escape(string value) => value.Replace("\"", "\" + Chr(34) + \"", StringComparison.Ordinal);

    // Transfer the environment and clips together, balancing every failed load's library reference.
    private static AvsScript Load(Func<AvsNative, IntPtr, AvsValue> evaluateUser, bool convertForDisplay = true)
    {
        var native = AvsNative.Load();
        IntPtr environment;
        try { environment = native.CreateEnvironment(); }
        catch { native.Dispose(); throw; }
        if (environment == IntPtr.Zero)
        {
            native.Dispose();
            throw new AvsException("AviSynth could not create a script environment.");
        }

        try
        {
            var user = evaluateUser(native, environment);
            var sourceClip = IntPtr.Zero;
            try
            {
                sourceClip = native.TakeClip(user, environment);
                if (sourceClip == IntPtr.Zero)
                {
                    throw new AvsException("The AviSynth script did not return a video clip.");
                }

                var sourceInfo = native.GetVideoInfo(sourceClip);
                if (!convertForDisplay || !sourceInfo.HasVideo)
                {
                    var sourceScript = new AvsScript(native, environment, sourceClip, sourceClip, sourceInfo);
                    sourceClip = IntPtr.Zero;
                    return sourceScript;
                }

                native.SetVar(environment, "last", user);
                var result = Eval(native, environment, DisplayConversion,
                    "AviSynth could not convert the output for display.");
                var clip = native.TakeClip(result, environment);
                native.ReleaseValue(result);
                if (clip == IntPtr.Zero)
                {
                    throw new AvsException("The AviSynth script did not return a video clip.");
                }

                var script = new AvsScript(native, environment, clip, sourceClip, sourceInfo);
                sourceClip = IntPtr.Zero;
                return script;
            }
            finally
            {
                if (sourceClip != IntPtr.Zero)
                {
                    native.ReleaseClip(sourceClip);
                }

                native.ReleaseValue(user);
            }
        }
        catch
        {
            native.DeleteEnvironment(environment);
            native.Dispose();
            throw;
        }
    }

    private static AvsValue EvalClip(AvsNative native, IntPtr environment, string script, string fallbackError)
    {
        var result = Eval(native, environment, script, fallbackError);
        if (result.Type != (short)'c')
        {
            native.ReleaseValue(result);
            throw new AvsException("The AviSynth script did not return a video clip.");
        }

        return result;
    }

    private static void EvalDiscard(AvsNative native, IntPtr environment, string script, string fallbackError)
    {
        var result = Eval(native, environment, script, fallbackError);
        native.ReleaseValue(result);
    }

    private static AvsValue Eval(AvsNative native, IntPtr environment, string script, string fallbackError)
    {
        var result = native.Eval(environment, script);
        if (result.Type == (short)'e')
        {
            var message = result.GetString() ?? fallbackError;
            native.ReleaseValue(result);
            throw new AvsException(message);
        }

        return result;
    }

    /// <summary>
    /// Gets the script video information after display conversion, when enabled.
    /// </summary>
    public AvsVideoInfo VideoInfo
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _native.GetVideoInfo(_clip);
            }
        }
    }

    /// <summary>
    /// Gets the script video information before display conversion.
    /// </summary>
    public AvsVideoInfo SourceVideoInfo { get; }

    /// <summary>
    /// Gets a video frame from the prepared display clip or the original source.
    /// The returned frame must be disposed.
    /// </summary>
    /// <param name="index">The zero-based frame index.</param>
    /// <param name="convertForDisplay">Whether to use the display clip, if conversion was enabled when loading.</param>
    public AvsFrame GetFrame(int index, bool convertForDisplay = true)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!SourceVideoInfo.HasVideo)
            {
                throw new AvsException("The AviSynth clip does not contain video.");
            }

            index.CheckRange(min: 0, max: SourceVideoInfo.FrameCount - 1);
            var clip = convertForDisplay ? _clip : _sourceClip;
            var frame = _native.GetFrame(clip, index);
            if (frame == IntPtr.Zero)
            {
                throw new AvsException(_native.GetClipError(clip) ?? "AviSynth could not return the requested frame.");
            }

            return new(this, _native, frame);
        }
    }

    /// <summary>
    /// Returns frame properties from the source clip, before display conversion.
    /// </summary>
    public IReadOnlyList<(string Name, string Value)> GetSourceFrameProperties(int index)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_native.HasFrameProperties || !SourceVideoInfo.HasVideo)
            {
                return [];
            }

            using var frame = GetFrame(index, convertForDisplay: false);
            return _native.ReadFrameProperties(handle, frame.DangerousGetHandle());
        }
    }

    /// <summary>
    /// Copies a sample range from the source audio as interleaved PCM bytes.
    /// The sample count is per channel; SourceVideoInfo describes the storage format.
    /// </summary>
    public byte[] ReadAudio(long start, int count)
    {
        lock (_gate)
        {
            var result = new byte[GetAudioBufferSize(start, count)];
            ReadAudio(start, count, result);
            return result;
        }
    }

    /// <summary>
    /// Copies interleaved source audio into a caller-owned buffer.
    /// Returns the number of bytes written; the sample range must be within the clip.
    /// </summary>
    public unsafe int ReadAudio(long start, int count, Span<byte> destination)
    {
        lock (_gate)
        {
            var bytes = GetAudioBufferSize(start, count);
            destination.Length.CheckRange(min: bytes);
            if (bytes == 0) { return 0; }

            fixed (byte* buffer = destination)
            {
                if (_native.GetAudio(_sourceClip, (IntPtr)buffer, start, count) != 0)
                {
                    throw new AvsException(_native.GetClipError(_sourceClip) ?? "AviSynth could not return the requested audio.");
                }
            }

            return bytes;
        }
    }

    // Validate ranges before allocating or exposing a native destination buffer.
    private int GetAudioBufferSize(long start, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!SourceVideoInfo.HasAudio)
        {
            throw new AvsException("The AviSynth clip does not contain audio.");
        }

        start.CheckRange(min: 0L, max: SourceVideoInfo.AudioSampleCount);
        count.CheckRange(min: 0, max: (int)Math.Min(int.MaxValue, SourceVideoInfo.AudioSampleCount - start));
        return checked(count * SourceVideoInfo.BytesPerAudioSample);
    }

    /// <summary>
    /// Gets the native AviSynth clip handle for advanced API use.
    /// </summary>
    public IntPtr Clip
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _clip;
            }
        }
    }

    /// <summary>
    /// Gets whether the native script environment is absent.
    /// </summary>
    public override bool IsInvalid => handle == IntPtr.Zero;

    /// <summary>
    /// Closes the script to new operations while existing frames retain its environment.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        lock (_gate)
        {
            _disposed = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Releases native clips, the environment, and its balanced library load reference.
    /// </summary>
    protected override bool ReleaseHandle()
    {
        if (_clip == _sourceClip) { _clip = IntPtr.Zero; }

        if (_clip != IntPtr.Zero)
        {
            _native.ReleaseClip(_clip);
            _clip = IntPtr.Zero;
        }

        if (_sourceClip != IntPtr.Zero)
        {
            _native.ReleaseClip(_sourceClip);
            _sourceClip = IntPtr.Zero;
        }

        _native.DeleteEnvironment(handle);
        _native.Dispose();
        return true;
    }
}
