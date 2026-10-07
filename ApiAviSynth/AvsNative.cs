using System.Runtime.InteropServices;
using System.Text;

namespace HanumanInstitute.ApiAviSynth;

internal sealed class AvsNative : IDisposable
{
    private const int InterfaceVersion = 6;
    private readonly IntPtr _library;
    private bool _disposed;
    private readonly CreateEnvironmentDelegate _createEnvironment;
    private readonly DeleteEnvironmentDelegate _deleteEnvironment;
    private readonly InvokeDelegate _invoke;
    private readonly TakeClipDelegate _takeClip;
    private readonly ReleaseClipDelegate _releaseClip;
    private readonly ReleaseFrameDelegate _releaseFrame;
    private readonly ReleaseValueDelegate _releaseValue;
    private readonly GetVideoInfoDelegate _getVideoInfo;
    private readonly GetFrameDelegate _getFrame;
    private readonly GetPlaneValueDelegate _getPitch;
    private readonly GetPlaneValueDelegate _getRowSize;
    private readonly GetPlaneValueDelegate _getHeight;
    private readonly GetReadPtrDelegate _getReadPtr;
    private readonly SetVarDelegate _setVar;
    private readonly GetVarDelegate _getVar;
    private readonly GetVarTryDelegate? _getVarTry;
    private readonly FunctionExistsDelegate _functionExists;
    private readonly GetFramePropsDelegate? _getFramePropsRo;
    private readonly PropNumKeysDelegate? _propNumKeys;
    private readonly PropGetKeyDelegate? _propGetKey;
    private readonly PropNumElementsDelegate? _propNumElements;
    private readonly PropGetTypeDelegate? _propGetType;
    private readonly PropGetIntDelegate? _propGetInt;
    private readonly PropGetFloatDelegate? _propGetFloat;
    private readonly PropGetDataDelegate? _propGetData;
    private readonly PropGetDataSizeDelegate? _propGetDataSize;

    private AvsNative(IntPtr library)
    {
        _library = library;
        _createEnvironment = GetDelegate<CreateEnvironmentDelegate>("avs_create_script_environment");
        _deleteEnvironment = GetDelegate<DeleteEnvironmentDelegate>("avs_delete_script_environment");
        _invoke = GetDelegate<InvokeDelegate>("avs_invoke");
        _takeClip = GetDelegate<TakeClipDelegate>("avs_take_clip");
        _releaseClip = GetDelegate<ReleaseClipDelegate>("avs_release_clip");
        _releaseFrame = GetDelegate<ReleaseFrameDelegate>("avs_release_video_frame");
        _releaseValue = GetDelegate<ReleaseValueDelegate>("avs_release_value");
        _getVideoInfo = GetDelegate<GetVideoInfoDelegate>("avs_get_video_info");
        _getFrame = GetDelegate<GetFrameDelegate>("avs_get_frame");
        _getPitch = GetDelegate<GetPlaneValueDelegate>("avs_get_pitch_p");
        _getRowSize = GetDelegate<GetPlaneValueDelegate>("avs_get_row_size_p");
        _getHeight = GetDelegate<GetPlaneValueDelegate>("avs_get_height_p");
        _getReadPtr = GetDelegate<GetReadPtrDelegate>("avs_get_read_ptr_p");
        _setVar = GetDelegate<SetVarDelegate>("avs_set_var");
        _getVar = GetDelegate<GetVarDelegate>("avs_get_var");
        _getVarTry = TryGetDelegate<GetVarTryDelegate>("avs_get_var_try");
        _functionExists = GetDelegate<FunctionExistsDelegate>("avs_function_exists");
        _getFramePropsRo = TryGetDelegate<GetFramePropsDelegate>("avs_get_frame_props_ro");
        _propNumKeys = TryGetDelegate<PropNumKeysDelegate>("avs_prop_num_keys");
        _propGetKey = TryGetDelegate<PropGetKeyDelegate>("avs_prop_get_key");
        _propNumElements = TryGetDelegate<PropNumElementsDelegate>("avs_prop_num_elements");
        _propGetType = TryGetDelegate<PropGetTypeDelegate>("avs_prop_get_type");
        _propGetInt = TryGetDelegate<PropGetIntDelegate>("avs_prop_get_int");
        _propGetFloat = TryGetDelegate<PropGetFloatDelegate>("avs_prop_get_float");
        _propGetData = TryGetDelegate<PropGetDataDelegate>("avs_prop_get_data");
        _propGetDataSize = TryGetDelegate<PropGetDataSizeDelegate>("avs_prop_get_data_size");
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        NativeLibrary.Free(_library);
        _disposed = true;
    }

    public bool FunctionExists(IntPtr environment, string name) => _functionExists(environment, name) != 0;

    public string? ReadStringVariable(IntPtr environment, string name)
    {
        AvsValue value;
        if (_getVarTry != null)
        {
            if (_getVarTry(environment, name, out value) == 0)
            {
                return null;
            }
        }
        else
        {
            value = _getVar(environment, name);
        }
        try
        {
            return value.Type == (short)'s' ? value.GetString() : null;
        }
        finally
        {
            ReleaseValue(value);
        }
    }

    public IntPtr CreateEnvironment() => _createEnvironment(InterfaceVersion);
    public void DeleteEnvironment(IntPtr environment) => _deleteEnvironment(environment);
    public IntPtr TakeClip(AvsValue value, IntPtr environment) => _takeClip(value, environment);
    public void ReleaseClip(IntPtr clip) => _releaseClip(clip);
    public void ReleaseFrame(IntPtr frame) => _releaseFrame(frame);
    public void ReleaseValue(AvsValue value) => _releaseValue(value);
    public AvsVideoInfo GetVideoInfo(IntPtr clip) => Marshal.PtrToStructure<AvsVideoInfo>(_getVideoInfo(clip));
    public IntPtr GetFrame(IntPtr clip, int index) => _getFrame(clip, index);
    public int GetPitch(IntPtr frame, int plane) => _getPitch(frame, plane);
    public int GetRowSize(IntPtr frame, int plane) => _getRowSize(frame, plane);
    public int GetHeight(IntPtr frame, int plane) => _getHeight(frame, plane);
    public IntPtr GetReadPtr(IntPtr frame, int plane) => _getReadPtr(frame, plane);

    public AvsValue Import(IntPtr environment, string path) => InvokeString(environment, "Import", path);

    public AvsValue Eval(IntPtr environment, string script) => InvokeString(environment, "Eval", script);

    public AvsValue Invoke(IntPtr environment, string name) =>
        _invoke(environment, name, new() { Type = (short)'a', ArraySize = 0 }, IntPtr.Zero);

    public void SetVar(IntPtr environment, string name, AvsValue value) => _setVar(environment, name, value);

    public bool HasFrameProperties =>
        _getFramePropsRo != null && _propNumKeys != null && _propGetKey != null && _propNumElements != null &&
        _propGetType != null && _propGetInt != null && _propGetFloat != null && _propGetData != null &&
        _propGetDataSize != null;

    public IReadOnlyList<(string Name, string Value)> ReadFrameProperties(IntPtr environment, IntPtr frame)
    {
        if (!HasFrameProperties)
        {
            return [];
        }

        var map = _getFramePropsRo!(environment, frame);
        if (map == IntPtr.Zero)
        {
            return [];
        }

        var count = _propNumKeys!(environment, map);
        if (count <= 0)
        {
            return [];
        }

        var properties = new List<(string Name, string Value)>(count);
        for (var i = 0; i < count; i++)
        {
            var key = Marshal.PtrToStringUTF8(_propGetKey!(environment, map, i));
            if (!key.HasValue())
            {
                continue;
            }

            properties.Add((key, FormatProperty(environment, map, key)));
        }

        return properties;
    }

    private string FormatProperty(IntPtr environment, IntPtr map, string key)
    {
        var type = _propGetType!(environment, map, key);
        var count = _propNumElements!(environment, map, key);
        if (count <= 0)
        {
            return "";
        }

        return type switch
        {
            (byte)'i' => Join(count, i => _propGetInt!(environment, map, key, i, out _).ToString()),
            (byte)'f' => Join(count, i => _propGetFloat!(environment, map, key, i, out _).ToString("G")),
            (byte)'s' => Join(count, i => FormatData(environment, map, key, i)),
            (byte)'c' => count == 1 ? "<clip>" : count + " clips",
            (byte)'v' => count == 1 ? "<frame>" : count + " frames",
            _ => "<" + (char)type + ">"
        };
    }

    private string FormatData(IntPtr environment, IntPtr map, string key, int index)
    {
        var pointer = _propGetData!(environment, map, key, index, out _);
        if (pointer == IntPtr.Zero)
        {
            return "";
        }

        var size = _propGetDataSize!(environment, map, key, index, out _);
        return size <= 0 ? "" : Marshal.PtrToStringUTF8(pointer, size) ?? "";
    }

    private static string Join(int count, Func<int, string> value)
    {
        if (count == 1)
        {
            return value(0);
        }

        var parts = new string[count];
        for (var i = 0; i < count; i++)
        {
            parts[i] = value(i);
        }

        return string.Join(", ", parts);
    }

    public AvsValue InvokeClip(IntPtr environment, string name, IntPtr clip)
    {
        var arg = new AvsValue { Type = (short)'c', Value = clip };
        var argHandle = Marshal.AllocCoTaskMem(Marshal.SizeOf<AvsValue>());
        try
        {
            Marshal.StructureToPtr(arg, argHandle, false);
            return _invoke(environment, name, new() { Type = (short)'a', ArraySize = 1, Value = argHandle },
                IntPtr.Zero);
        }
        finally
        {
            Marshal.FreeCoTaskMem(argHandle);
        }
    }

    private AvsValue InvokeString(IntPtr environment, string name, string value)
    {
        var bytes = Encoding.Default.GetBytes(value + '\0');
        var memory = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, memory, bytes.Length);
            return _invoke(environment, name, new() { Type = (short)'s', Value = memory }, IntPtr.Zero);
        }
        finally
        {
            Marshal.FreeCoTaskMem(memory);
        }
    }

    // The loader opens the same bare name FFmpeg and avs2yuv use.
    public static bool TryFindLibrary(out string? path)
    {
        foreach (var candidate in AvsPathResolver.LibraryFileNames)
        {
            if (NativeLibrary.TryLoad(candidate, out var library))
            {
                path = AvsPathResolver.ResolveExistingPath(candidate, library);
                NativeLibrary.Free(library);
                return true;
            }
        }

        path = null;
        return false;
    }

    public static AvsNative Load()
    {
        foreach (var candidate in AvsPathResolver.LibraryFileNames)
        {
            if (NativeLibrary.TryLoad(candidate, out var library))
            {
                return new(library);
            }
        }

        throw new DllNotFoundException("Could not load AviSynth.");
    }

    private T GetDelegate<T>(string export) where T : Delegate
    {
        if (!NativeLibrary.TryGetExport(_library, export, out var pointer))
        {
            throw new EntryPointNotFoundException($"The loaded AviSynth library does not export {export}.");
        }

        return Marshal.GetDelegateForFunctionPointer<T>(pointer);
    }

    private T? TryGetDelegate<T>(string export) where T : Delegate
    {
        if (!NativeLibrary.TryGetExport(_library, export, out var pointer))
        {
            return null;
        }

        return Marshal.GetDelegateForFunctionPointer<T>(pointer);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate AvsValue GetVarDelegate(IntPtr environment, string name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetVarTryDelegate(IntPtr environment, string name, out AvsValue value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int FunctionExistsDelegate(IntPtr environment, string name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr CreateEnvironmentDelegate(int version);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void DeleteEnvironmentDelegate(IntPtr environment);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate AvsValue InvokeDelegate(
        IntPtr environment, string name, AvsValue args, IntPtr argNames);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr TakeClipDelegate(
        AvsValue value, IntPtr environment);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void ReleaseClipDelegate(IntPtr clip);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void ReleaseFrameDelegate(IntPtr frame);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void ReleaseValueDelegate(AvsValue value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetVideoInfoDelegate(IntPtr clip);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetFrameDelegate(IntPtr clip, int index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int GetPlaneValueDelegate(IntPtr frame, int plane);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetReadPtrDelegate(IntPtr frame, int plane);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int SetVarDelegate(IntPtr environment, string name, AvsValue value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetFramePropsDelegate(IntPtr environment, IntPtr frame);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int PropNumKeysDelegate(IntPtr environment, IntPtr map);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr PropGetKeyDelegate(IntPtr environment, IntPtr map, int index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int PropNumElementsDelegate(IntPtr environment, IntPtr map, string key);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate byte PropGetTypeDelegate(IntPtr environment, IntPtr map, string key);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate long PropGetIntDelegate(
        IntPtr environment, IntPtr map, string key, int index, out int error);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate double PropGetFloatDelegate(
        IntPtr environment, IntPtr map, string key, int index, out int error);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr PropGetDataDelegate(
        IntPtr environment, IntPtr map, string key, int index, out int error);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int PropGetDataSizeDelegate(
        IntPtr environment, IntPtr map, string key, int index, out int error);
}
