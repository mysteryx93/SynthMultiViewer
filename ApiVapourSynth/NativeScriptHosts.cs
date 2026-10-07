using System.Runtime.InteropServices;

namespace HanumanInstitute.ApiVapourSynth;

internal static class NativeScriptHosts
{
    public const string VapourSynth = "vsscript";

    public static NativeLibraryProfile VapourSynthScript { get; } = new()
    {
        Id = VapourSynth,
        ImportName = VapourSynth,
        FileNames = FileNamesForCurrentOs(),
        RequiredExports = ["getVSScriptAPI"]
    };

    // Same bare name FFmpeg opens and vspipe links.
    private static IReadOnlyList<string> FileNamesForCurrentOs()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ["VSScript.dll"];
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ["libvapoursynth-script.dylib"];
        }

        return ["libvapoursynth-script.so"];
    }
}
