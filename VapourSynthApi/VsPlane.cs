namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Provides a video-plane view whose native access is guarded by its owning frame.
/// </summary>
public sealed class VsPlane
{
    private readonly VsFrame _frame;

    /// <summary>
    /// Gets the zero-based image plane index.
    /// </summary>
    public int Plane { get; }

    /// <summary>
    /// Binds the view to an already validated image plane.
    /// </summary>
    internal VsPlane(VsFrame frame, int plane)
    {
        _frame = frame;
        Plane = plane;
    }

    /// <summary>
    /// Gets the distance in bytes between adjacent image rows.
    /// </summary>
    public int Stride
    {
        get { lock (_frame.Gate) { return checked((int)_frame.Api.GetStride(_frame.Pointer, Plane)); } }
    }

    /// <summary>
    /// Gets the read-only pixel address, valid until the owning frame is disposed.
    /// Read it again after GetWritePtr, which may replace shared storage.
    /// </summary>
    public IntPtr Ptr
    {
        get { lock (_frame.Gate) { return _frame.Api.GetReadPtr(_frame.Pointer, Plane); } }
    }

    /// <summary>
    /// Gets the plane width in pixels, accounting for chroma subsampling.
    /// </summary>
    public int Width
    {
        get { lock (_frame.Gate) { return _frame.Api.GetFrameWidth(_frame.Pointer, Plane); } }
    }

    /// <summary>
    /// Gets the plane height in pixels, accounting for chroma subsampling.
    /// </summary>
    public int Height
    {
        get { lock (_frame.Gate) { return _frame.Api.GetFrameHeight(_frame.Pointer, Plane); } }
    }

    /// <summary>
    /// Gets writable pixels from a frame created by Copy; retrieved source frames are read-only.
    /// The address remains valid only while the owning frame is alive.
    /// </summary>
    public IntPtr GetWritePtr()
    {
        lock (_frame.Gate)
        {
            var pointer = _frame.Pointer;
            if (!_frame.IsWritable) { throw new InvalidOperationException("Copy the frame before requesting writable pixels."); }
            return _frame.Api.GetWritePtr(pointer, Plane);
        }
    }
}
