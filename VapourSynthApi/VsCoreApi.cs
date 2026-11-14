using System.Runtime.InteropServices;

namespace HanumanInstitute.VapourSynthApi;

internal sealed class VsCoreApi
{
    private readonly FreeNodeDelegate _freeNode;
    private readonly FreeFrameDelegate _freeFrame;
    private readonly GetVideoInfoDelegate _getVideoInfo;
    private readonly GetVideoInfoDelegate _getAudioInfo;
    private readonly GetNodeTypeDelegate _getNodeType;
    private readonly GetVideoInfoDelegate _getVideoFrameFormat;
    private readonly GetVideoInfoDelegate _getAudioFrameFormat;
    private readonly GetNodeTypeDelegate _getFrameLength;
    private readonly CopyFrameDelegate _copyFrame;
    private readonly GetFrameDelegate _getFrame;
    private readonly GetStrideDelegate _getStride;
    private readonly GetReadPtrDelegate _getReadPtr;
    private readonly GetWritePtrDelegate _getWritePtr;
    private readonly GetFrameDimensionDelegate _getFrameWidth;
    private readonly GetFrameDimensionDelegate _getFrameHeight;
    private readonly SetThreadCountDelegate _setThreadCount;
    private readonly GetFramePropertiesDelegate _getFramePropertiesRo;
    private readonly MapCountDelegate _mapNumKeys;
    private readonly MapGetKeyDelegate _mapGetKey;
    private readonly MapNumElementsDelegate _mapNumElements;
    private readonly MapGetTypeDelegate _mapGetType;
    private readonly MapGetIntDelegate _mapGetInt;
    private readonly MapGetFloatDelegate _mapGetFloat;
    private readonly MapGetDataDelegate _mapGetData;
    private readonly MapGetDataSizeDelegate _mapGetDataSize;
    private readonly GetCoreInfoDelegate _getCoreInfo;
    private readonly CreateCoreDelegate _createCore;
    private readonly FreeCoreDelegate _freeCore;
    private readonly NextPluginDelegate _getNextPlugin;
    private readonly NextFunctionDelegate _getNextPluginFunction;
    private readonly GetStringDelegate _getPluginNamespace;
    private readonly GetStringDelegate? _getPluginName;
    private readonly GetStringDelegate _getPluginFunctionName;
    private readonly GetStringDelegate _getPluginFunctionArguments;
    private readonly GetStringDelegate? _getPluginFunctionReturnType;

    private VsCoreApi(VsCoreApiTable table)
    {
        _freeNode = Marshal.GetDelegateForFunctionPointer<FreeNodeDelegate>(table.FreeNode);
        _freeFrame = Marshal.GetDelegateForFunctionPointer<FreeFrameDelegate>(table.FreeFrame);
        _getVideoInfo = Marshal.GetDelegateForFunctionPointer<GetVideoInfoDelegate>(table.GetVideoInfo);
        _getAudioInfo = Marshal.GetDelegateForFunctionPointer<GetVideoInfoDelegate>(table.GetAudioInfo);
        _getNodeType = Marshal.GetDelegateForFunctionPointer<GetNodeTypeDelegate>(table.GetNodeType);
        _getVideoFrameFormat = Marshal.GetDelegateForFunctionPointer<GetVideoInfoDelegate>(table.GetVideoFrameFormat);
        _getAudioFrameFormat = Marshal.GetDelegateForFunctionPointer<GetVideoInfoDelegate>(table.GetAudioFrameFormat);
        _getFrameLength = Marshal.GetDelegateForFunctionPointer<GetNodeTypeDelegate>(table.GetFrameLength);
        _copyFrame = Marshal.GetDelegateForFunctionPointer<CopyFrameDelegate>(table.CopyFrame);
        _getFrame = Marshal.GetDelegateForFunctionPointer<GetFrameDelegate>(table.GetFrame);
        _getStride = Marshal.GetDelegateForFunctionPointer<GetStrideDelegate>(table.GetStride);
        _getReadPtr = Marshal.GetDelegateForFunctionPointer<GetReadPtrDelegate>(table.GetReadPtr);
        _getWritePtr = Marshal.GetDelegateForFunctionPointer<GetWritePtrDelegate>(table.GetWritePtr);
        _getFrameWidth = Marshal.GetDelegateForFunctionPointer<GetFrameDimensionDelegate>(table.GetFrameWidth);
        _getFrameHeight = Marshal.GetDelegateForFunctionPointer<GetFrameDimensionDelegate>(table.GetFrameHeight);
        _setThreadCount = Marshal.GetDelegateForFunctionPointer<SetThreadCountDelegate>(table.SetThreadCount);
        _getFramePropertiesRo = Marshal.GetDelegateForFunctionPointer<GetFramePropertiesDelegate>(table.GetFramePropertiesRo);
        _mapNumKeys = Marshal.GetDelegateForFunctionPointer<MapCountDelegate>(table.MapNumKeys);
        _mapGetKey = Marshal.GetDelegateForFunctionPointer<MapGetKeyDelegate>(table.MapGetKey);
        _mapNumElements = Marshal.GetDelegateForFunctionPointer<MapNumElementsDelegate>(table.MapNumElements);
        _mapGetType = Marshal.GetDelegateForFunctionPointer<MapGetTypeDelegate>(table.MapGetType);
        _mapGetInt = Marshal.GetDelegateForFunctionPointer<MapGetIntDelegate>(table.MapGetInt);
        _mapGetFloat = Marshal.GetDelegateForFunctionPointer<MapGetFloatDelegate>(table.MapGetFloat);
        _mapGetData = Marshal.GetDelegateForFunctionPointer<MapGetDataDelegate>(table.MapGetData);
        _mapGetDataSize = Marshal.GetDelegateForFunctionPointer<MapGetDataSizeDelegate>(table.MapGetDataSize);
        _getCoreInfo = Marshal.GetDelegateForFunctionPointer<GetCoreInfoDelegate>(table.GetCoreInfo);
        _createCore = Marshal.GetDelegateForFunctionPointer<CreateCoreDelegate>(table.CreateCore);
        _freeCore = Marshal.GetDelegateForFunctionPointer<FreeCoreDelegate>(table.FreeCore);
        _getNextPlugin = Marshal.GetDelegateForFunctionPointer<NextPluginDelegate>(table.GetNextPlugin);
        _getNextPluginFunction = Marshal.GetDelegateForFunctionPointer<NextFunctionDelegate>(table.GetNextPluginFunction);
        _getPluginNamespace = Marshal.GetDelegateForFunctionPointer<GetStringDelegate>(table.GetPluginNamespace);
        if (table.GetPluginName != IntPtr.Zero)
        {
            _getPluginName = Marshal.GetDelegateForFunctionPointer<GetStringDelegate>(table.GetPluginName);
        }

        _getPluginFunctionName = Marshal.GetDelegateForFunctionPointer<GetStringDelegate>(table.GetPluginFunctionName);
        _getPluginFunctionArguments = Marshal.GetDelegateForFunctionPointer<GetStringDelegate>(table.GetPluginFunctionArguments);
        if (table.GetPluginFunctionReturnType != IntPtr.Zero)
        {
            _getPluginFunctionReturnType =
                Marshal.GetDelegateForFunctionPointer<GetStringDelegate>(table.GetPluginFunctionReturnType);
        }
    }

    public void FreeNode(IntPtr node) => _freeNode(node);
    public void FreeFrame(IntPtr frame) => _freeFrame(frame);
    public IntPtr GetVideoInfo(IntPtr node) => _getVideoInfo(node);

    /// <summary>
    /// Identifies a node before accessing its media-specific metadata.
    /// </summary>
    public VsMediaType GetNodeType(IntPtr node) => (VsMediaType)_getNodeType(node);

    /// <summary>
    /// Reads the metadata of a node already identified as audio.
    /// </summary>
    public VsAudioInfo GetAudioInfo(IntPtr node) => Marshal.PtrToStructure<VsAudioInfo>(_getAudioInfo(node));

    /// <summary>
    /// Copies the actual format of one video frame, including variable-format outputs.
    /// </summary>
    public VsFormat GetVideoFrameFormat(IntPtr frame) => Marshal.PtrToStructure<VsFormat>(_getVideoFrameFormat(frame));

    /// <summary>
    /// Copies the sample storage of one audio frame.
    /// </summary>
    public VsAudioFormat GetAudioFrameFormat(IntPtr frame) => Marshal.PtrToStructure<VsAudioFormat>(_getAudioFrameFormat(frame));

    /// <summary>
    /// Reads the sample count per channel in an audio frame.
    /// </summary>
    public int GetFrameLength(IntPtr frame) => _getFrameLength(frame);

    /// <summary>
    /// Creates an independently writable frame with native copy-on-write storage.
    /// </summary>
    public IntPtr CopyFrame(IntPtr frame, IntPtr core) => _copyFrame(frame, core);
    public IntPtr GetFrame(int index, IntPtr node, IntPtr error, int errorSize) => _getFrame(index, node, error, errorSize);
    public nint GetStride(IntPtr frame, int plane) => _getStride(frame, plane);
    public IntPtr GetReadPtr(IntPtr frame, int plane) => _getReadPtr(frame, plane);
    public IntPtr GetWritePtr(IntPtr frame, int plane) => _getWritePtr(frame, plane);
    public int GetFrameWidth(IntPtr frame, int plane) => _getFrameWidth(frame, plane);
    public int GetFrameHeight(IntPtr frame, int plane) => _getFrameHeight(frame, plane);
    public int SetThreadCount(int threads, IntPtr core) => _setThreadCount(threads, core);
    public IntPtr GetFramePropertiesRo(IntPtr frame) => _getFramePropertiesRo(frame);
    public int MapNumKeys(IntPtr map) => _mapNumKeys(map);
    public IntPtr MapGetKey(IntPtr map, int index) => _mapGetKey(map, index);
    public int MapNumElements(IntPtr map, IntPtr key) => _mapNumElements(map, key);
    public int MapGetType(IntPtr map, IntPtr key) => _mapGetType(map, key);
    public long MapGetInt(IntPtr map, IntPtr key, int index, out int error) => _mapGetInt(map, key, index, out error);
    public double MapGetFloat(IntPtr map, IntPtr key, int index, out int error) => _mapGetFloat(map, key, index, out error);
    public IntPtr MapGetData(IntPtr map, IntPtr key, int index, out int error) => _mapGetData(map, key, index, out error);
    public int MapGetDataSize(IntPtr map, IntPtr key, int index, out int error) => _mapGetDataSize(map, key, index, out error);
    public VsCoreInfo GetCoreInfo(IntPtr core)
    {
        _getCoreInfo(core, out var info);
        return info;
    }

    public IntPtr CreateCore(int flags) => _createCore(flags);
    public void FreeCore(IntPtr core) => _freeCore(core);
    public IntPtr GetNextPlugin(IntPtr plugin, IntPtr core) => _getNextPlugin(plugin, core);
    public IntPtr GetNextPluginFunction(IntPtr function, IntPtr plugin) => _getNextPluginFunction(function, plugin);
    public string PluginNamespace(IntPtr plugin) => Marshal.PtrToStringUTF8(_getPluginNamespace(plugin)) ?? "";
    public string? PluginName(IntPtr plugin) =>
        _getPluginName == null ? null : Marshal.PtrToStringUTF8(_getPluginName(plugin));
    public string PluginFunctionName(IntPtr function) => Marshal.PtrToStringUTF8(_getPluginFunctionName(function)) ?? "";
    public string? PluginFunctionArguments(IntPtr function) => Marshal.PtrToStringUTF8(_getPluginFunctionArguments(function));
    public string? PluginFunctionReturnType(IntPtr function) =>
        _getPluginFunctionReturnType == null ? null : Marshal.PtrToStringUTF8(_getPluginFunctionReturnType(function));

    public static VsCoreApi Load(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero)
        {
            throw new VsException("The loaded VSScript library does not expose the VapourSynth 4 core API.");
        }

        return new(Marshal.PtrToStructure<VsCoreApiTable>(pointer));
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void FreeNodeDelegate(IntPtr node);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void FreeFrameDelegate(IntPtr frame);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetVideoInfoDelegate(IntPtr node);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int GetNodeTypeDelegate(IntPtr node);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr CopyFrameDelegate(IntPtr frame, IntPtr core);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetFrameDelegate(int index, IntPtr node, IntPtr error, int errorSize);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint GetStrideDelegate(IntPtr frame, int plane);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetReadPtrDelegate(IntPtr frame, int plane);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetWritePtrDelegate(IntPtr frame, int plane);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int GetFrameDimensionDelegate(IntPtr frame, int plane);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int SetThreadCountDelegate(int threads, IntPtr core);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetFramePropertiesDelegate(IntPtr frame);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int MapCountDelegate(IntPtr map);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr MapGetKeyDelegate(IntPtr map, int index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int MapNumElementsDelegate(IntPtr map, IntPtr key);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int MapGetTypeDelegate(IntPtr map, IntPtr key);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate long MapGetIntDelegate(IntPtr map, IntPtr key, int index, out int error);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate double MapGetFloatDelegate(IntPtr map, IntPtr key, int index, out int error);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr MapGetDataDelegate(IntPtr map, IntPtr key, int index, out int error);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GetCoreInfoDelegate(IntPtr core, out VsCoreInfo info);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int MapGetDataSizeDelegate(IntPtr map, IntPtr key, int index, out int error);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr CreateCoreDelegate(int flags);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void FreeCoreDelegate(IntPtr core);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr NextPluginDelegate(IntPtr plugin, IntPtr core);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr NextFunctionDelegate(IntPtr function, IntPtr plugin);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetStringDelegate(IntPtr value);
}
