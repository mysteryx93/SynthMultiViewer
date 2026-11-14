using System.Runtime.InteropServices;

namespace HanumanInstitute.VapourSynthApi;

[StructLayout(LayoutKind.Sequential)]
internal struct VsScriptApiTable
{
    public IntPtr GetApiVersion;
    public IntPtr GetVsApi;
    public IntPtr CreateScript;
    public IntPtr GetCore;
    public IntPtr EvaluateBuffer;
    public IntPtr EvaluateFile;
    public IntPtr GetError;
    public IntPtr GetExitCode;
    public IntPtr GetVariable;
    public IntPtr SetVariables;
    public IntPtr GetOutputNode;
    public IntPtr GetOutputAlphaNode;
    public IntPtr GetAltOutputMode;
    public IntPtr FreeScript;
    public IntPtr EvalSetWorkingDir;
}
