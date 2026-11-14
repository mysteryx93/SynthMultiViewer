namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Describes the result and queue state of an asynchronous frame request.
/// </summary>
public class VsFrameStatus : EventArgs
{
    /// <summary>
    /// Creates an empty frame request status.
    /// </summary>
    public VsFrameStatus() { }

    /// <summary>
    /// Creates a request status for the specified frame index.
    /// </summary>
    public VsFrameStatus(int index)
    {
        Index = index;
    }

    /// <summary>
    /// Gets the requested zero-based frame index.
    /// </summary>
    public int Index { get; internal set; }

    /// <summary>
    /// Gets the frame reference, which is null on failure and released after FrameReady handlers return.
    /// </summary>
    public VsFrame? Frame { get; internal set; }

    /// <summary>
    /// Gets the native error message, or null for a successful request.
    /// </summary>
    public string? Error { get; internal set; }

    /// <summary>
    /// Gets the request state in the processing queue.
    /// </summary>
    public VsFrameState State { get; internal set; } = VsFrameState.Requested;
}
