using System.Reflection;
using System.Runtime.InteropServices;

namespace HanumanInstitute.ApiVapourSynth;

internal static class VsInvoke
{
    private const string VsScript = NativeScriptHosts.VapourSynth;

    static VsInvoke()
    {
        NativeLibrary.SetDllImportResolver(typeof(VsInvoke).Assembly, Resolve);
    }

    public const int ApiVersion = 4 << 16;
    public const int ScriptApiVersion = (4 << 16) | 1;

    [DllImport(VsScript, ExactSpelling = true)]
    public static extern IntPtr getVSScriptAPI(int version);

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        var profile = NativeScriptHosts.VapourSynthScript;
        return NativeLibraryLocator.Matches(profile, libraryName)
            ? NativeLibraryLocator.Resolve(profile)
            : IntPtr.Zero;
    }
}
