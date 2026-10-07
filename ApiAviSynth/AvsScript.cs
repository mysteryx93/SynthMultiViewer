namespace HanumanInstitute.ApiAviSynth;

/// <summary>
/// Owns an AviSynth script environment and its default video clip.
/// </summary>
public sealed class AvsScript : IDisposable
{
    private readonly AvsNative _native;
    private IntPtr _environment;
    private IntPtr _clip;
    private IntPtr _sourceClip;

    private AvsScript(AvsNative native, IntPtr environment, IntPtr clip, IntPtr sourceClip, AvsVideoInfo sourceVideoInfo)
    {
        _native = native;
        _environment = environment;
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
        AvsPathResolver.GetPluginDirectories(libraryPath);

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
            var native = AvsNative.Load();
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
    public static AvsScript LoadFile(string path)
    {
        var resolvedPath = ResolveScriptPath(path);
        var text = File.ReadAllText(resolvedPath);
        AvsImportGraph.ThrowIfRecursive(text, resolvedPath);
        return Load((native, environment) =>
            EvalClip(native, environment, "Import(\"" + Escape(resolvedPath) + "\")",
                "AviSynth could not evaluate the script."));
    }

    /// <summary>
    /// Evaluates AviSynth script text and returns its video clip.
    /// </summary>
    public static AvsScript LoadScript(string script) => LoadScript(script, null);

    /// <summary>
    /// Evaluates AviSynth script text. <paramref name="scriptPath"/> is not read; its directory
    /// becomes the working directory for relative source paths.
    /// </summary>
    public static AvsScript LoadScript(string script, string? scriptPath)
    {
        script.CheckNotNullOrEmpty();
        var resolvedPath = scriptPath.HasText() ? ResolveScriptPath(scriptPath) : null;
        AvsImportGraph.ThrowIfRecursive(script, resolvedPath);
        var directory = resolvedPath.HasText() ? Path.GetDirectoryName(resolvedPath) : null;
        return Load((native, environment) =>
        {
            if (directory.HasValue() && Directory.Exists(directory))
            {
                EvalDiscard(native, environment, "SetWorkingDir(\"" + Escape(directory) + "\")",
                    "AviSynth could not set the working directory.");
            }

            return EvalClip(native, environment, script, "AviSynth could not evaluate the script.");
        });
    }

    private const string ProbeScript = """BlankClip(length=1, width=16, height=16, pixel_type="RGB24")""";

    // Run after the user script so `return` cannot skip display conversion.
    // _Matrix if tagged (1=709, 5/6=601, 9/10=2020); else Rec.709 at 720p+, Rec.601 below.
    // Convert can prefer _Matrix over the matrix argument, so retag 0/2/other to match.
    // ConvertBits(8) first so 10/12/14/16/float land on packed 8-bit RGB32, not a luma plane.
    private const string DisplayConversion = """
mx = FunctionExists("propNumElements") && propNumElements(last, "_Matrix") > 0 ? propGetInt(last, "_Matrix") : 0
mat = mx == 1 ? "Rec709" : mx == 9 || mx == 10 ? "Rec2020" : mx == 5 || mx == 6 ? "Rec601" : last.Height >= 720 ? "Rec709" : "Rec601"
mid = mat == "Rec709" ? 1 : mat == "Rec2020" ? 9 : 6
last = IsRGB() ? last : FunctionExists("propSet") ? propSet("_Matrix", mid) : last
last = FunctionExists("ConvertBits") && FunctionExists("BitsPerComponent") && BitsPerComponent() != 8 ? ConvertBits(8) : last
IsRGB() ? ConvertToRGB32() : ConvertToRGB32(matrix=mat)
""";

    private static string Escape(string value) => value.Replace("\"", "\"\"", StringComparison.Ordinal);

    private static AvsScript Load(Func<AvsNative, IntPtr, AvsValue> evaluateUser)
    {
        var native = AvsNative.Load();
        var environment = native.CreateEnvironment();
        if (environment == IntPtr.Zero)
        {
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
    /// Gets the script video information after display conversion.
    /// </summary>
    public AvsVideoInfo VideoInfo
    {
        get
        {
            ObjectDisposedException.ThrowIf(_clip == IntPtr.Zero, this);
            return _native.GetVideoInfo(_clip);
        }
    }

    /// <summary>
    /// Gets the script video information before display conversion.
    /// </summary>
    public AvsVideoInfo SourceVideoInfo { get; }

    /// <summary>
    /// Gets a video frame. The returned frame must be disposed.
    /// </summary>
    public AvsFrame GetFrame(int index)
    {
        ObjectDisposedException.ThrowIf(_clip == IntPtr.Zero, this);
        var frame = _native.GetFrame(_clip, index);
        if (frame == IntPtr.Zero)
        {
            throw new AvsException("AviSynth could not return the requested frame.");
        }

        return new(_native, frame);
    }

    /// <summary>
    /// Returns frame properties from the source clip, before display conversion.
    /// </summary>
    public IReadOnlyList<(string Name, string Value)> GetSourceFrameProperties(int index)
    {
        ObjectDisposedException.ThrowIf(_sourceClip == IntPtr.Zero, this);
        if (!_native.HasFrameProperties)
        {
            return [];
        }

        var frame = _native.GetFrame(_sourceClip, index);
        if (frame == IntPtr.Zero)
        {
            throw new AvsException("AviSynth could not return the requested frame.");
        }

        try
        {
            return _native.ReadFrameProperties(_environment, frame);
        }
        finally
        {
            _native.ReleaseFrame(frame);
        }
    }

    /// <summary>
    /// Gets the native AviSynth clip handle for advanced API use.
    /// </summary>
    public IntPtr Clip
    {
        get
        {
            ObjectDisposedException.ThrowIf(_clip == IntPtr.Zero, this);
            return _clip;
        }
    }

    /// <summary>
    /// Releases the clip and script environment.
    /// </summary>
    public void Dispose()
    {
        if (_environment == IntPtr.Zero) { return; }

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

        _native.DeleteEnvironment(_environment);
        _environment = IntPtr.Zero;
    }
}
