using System.Runtime.InteropServices;

namespace HanumanInstitute.AviSynthApi;

/// <summary>
/// Owns an AviSynth frame reference.
/// </summary>
public sealed class AvsFrame : SafeHandle
{
    private readonly Lock _gate = new();
    private readonly AvsScript _script;
    private readonly AvsNative _native;

    /// <summary>
    /// Retains the script environment while this native frame remains alive.
    /// </summary>
    internal AvsFrame(AvsScript script, AvsNative native, IntPtr frame) : base(IntPtr.Zero, true)
    {
        var retained = false;
        script.DangerousAddRef(ref retained);
        _script = script;
        _native = native;
        SetHandle(frame);
    }

    /// <summary>
    /// Gets a read-only image plane.
    /// </summary>
    public AvsPlane GetPlane(int plane)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(IsClosed, this);
            return new(_native.GetReadPtr(handle, plane), _native.GetPitch(handle, plane),
                _native.GetRowSize(handle, plane), _native.GetHeight(handle, plane));
        }
    }

    /// <summary>
    /// Gets whether the native frame reference is absent.
    /// </summary>
    public override bool IsInvalid => handle == IntPtr.Zero;

    /// <summary>
    /// Serializes native plane access with disposal of this frame.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        lock (_gate) { base.Dispose(disposing); }
    }

    /// <summary>
    /// Releases this frame once, then releases its hold on the script environment.
    /// </summary>
    protected override bool ReleaseHandle()
    {
        _native.ReleaseFrame(handle);
        _script.DangerousRelease();
        return true;
    }
}
