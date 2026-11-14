using System.Runtime.InteropServices;

namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Copies the core version, worker count, and native frame-buffer usage.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VsCoreInfo
{
    /// <summary>
    /// Gets the borrowed native version text.
    /// </summary>
    public IntPtr VersionString;
    /// <summary>
    /// Gets the core release number.
    /// </summary>
    public int Core;
    /// <summary>
    /// Gets the core API version.
    /// </summary>
    public int Api;
    /// <summary>
    /// Gets the native worker count.
    /// </summary>
    public int NumThreads;
    /// <summary>
    /// Gets the configured frame-buffer limit in bytes.
    /// </summary>
    public long MaxFramebufferSize;
    /// <summary>
    /// Gets the current frame-buffer allocation in bytes.
    /// </summary>
    public long UsedFramebufferSize;
}
