namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Identifies the state of a queued frame request.
/// </summary>
public enum VsFrameState
{
    /// <summary>
    /// The native frame request is pending.
    /// </summary>
    Requested,

    /// <summary>
    /// The request was canceled and its result will be discarded.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The frame request has finished.
    /// </summary>
    Completed,

    /// <summary>
    /// The request failed and carries an error instead of a frame.
    /// </summary>
    Failed
}
