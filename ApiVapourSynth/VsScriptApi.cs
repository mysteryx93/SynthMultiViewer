using System.Runtime.InteropServices;

namespace HanumanInstitute.ApiVapourSynth;

internal sealed class VsScriptApi
{
    private readonly GetApiVersionDelegate _getApiVersion;
    private readonly GetVsApiDelegate _getVsApi;
    private readonly CreateScriptDelegate _createScript;
    private readonly GetCoreDelegate _getCore;
    private readonly EvaluateBufferDelegate _evaluateBuffer;
    private readonly EvaluateFileDelegate _evaluateFile;
    private readonly GetErrorDelegate _getError;
    private readonly GetOutputNodeDelegate _getOutputNode;
    private readonly FreeScriptDelegate _freeScript;
    private readonly EvalSetWorkingDirDelegate _evalSetWorkingDir;

    private VsScriptApi(VsScriptApiTable table)
    {
        _getApiVersion = Marshal.GetDelegateForFunctionPointer<GetApiVersionDelegate>(table.GetApiVersion);
        _getVsApi = Marshal.GetDelegateForFunctionPointer<GetVsApiDelegate>(table.GetVsApi);
        _createScript = Marshal.GetDelegateForFunctionPointer<CreateScriptDelegate>(table.CreateScript);
        _getCore = Marshal.GetDelegateForFunctionPointer<GetCoreDelegate>(table.GetCore);
        _evaluateBuffer = Marshal.GetDelegateForFunctionPointer<EvaluateBufferDelegate>(table.EvaluateBuffer);
        _evaluateFile = Marshal.GetDelegateForFunctionPointer<EvaluateFileDelegate>(table.EvaluateFile);
        _getError = Marshal.GetDelegateForFunctionPointer<GetErrorDelegate>(table.GetError);
        _getOutputNode = Marshal.GetDelegateForFunctionPointer<GetOutputNodeDelegate>(table.GetOutputNode);
        _freeScript = Marshal.GetDelegateForFunctionPointer<FreeScriptDelegate>(table.FreeScript);
        _evalSetWorkingDir = Marshal.GetDelegateForFunctionPointer<EvalSetWorkingDirDelegate>(table.EvalSetWorkingDir);
    }

    public int ApiVersion => _getApiVersion();
    public IntPtr CoreApi => _getVsApi(VsInvoke.ApiVersion);
    public IntPtr CreateScript() => _createScript(IntPtr.Zero);
    public IntPtr GetCore(IntPtr script) => _getCore(script);
    public int EvaluateBuffer(IntPtr script, IntPtr buffer, IntPtr fileName) => _evaluateBuffer(script, buffer, fileName);
    public int EvaluateFile(IntPtr script, IntPtr fileName) => _evaluateFile(script, fileName);
    public string? GetError(IntPtr script) => Utf8Ptr.FromUtf8Ptr(_getError(script));
    public IntPtr GetOutputNode(IntPtr script, int index) => _getOutputNode(script, index);
    public void FreeScript(IntPtr script) => _freeScript(script);
    public void SetWorkingDirectory(IntPtr script, bool enabled) => _evalSetWorkingDir(script, enabled ? 1 : 0);

    public static VsScriptApi Load()
    {
        var pointer = VsInvoke.getVSScriptAPI(VsInvoke.ScriptApiVersion);
        if (pointer == IntPtr.Zero)
        {
            var detail = NativeLibraryLocator.TryReadScriptLastError();
            throw new VsException(detail.HasText()
                ? detail : "The loaded VSScript library does not support API 4.1.");
        }

        return new(Marshal.PtrToStructure<VsScriptApiTable>(pointer));
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int GetApiVersionDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetVsApiDelegate(int version);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr CreateScriptDelegate(IntPtr core);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetCoreDelegate(IntPtr script);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int EvaluateBufferDelegate(IntPtr script, IntPtr buffer, IntPtr fileName);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int EvaluateFileDelegate(IntPtr script, IntPtr fileName);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetErrorDelegate(IntPtr script);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetOutputNodeDelegate(IntPtr script, int index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void FreeScriptDelegate(IntPtr script);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void EvalSetWorkingDirDelegate(IntPtr script, int enabled);
}
