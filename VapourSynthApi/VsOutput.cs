using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Owns a video or audio node and dispatches ordered frame events on managed workers.
/// Pending requests and frames retain the native environment after disposal.
/// </summary>
public sealed class VsOutput : SafeHandle, IAsyncDisposable
{
    private readonly Lock _gate = new();
    private bool _disposed;
    private readonly VsScript _script;
    private readonly IntPtr _core;
    private readonly List<Request> _queue = [];
    private readonly Queue<Request> _completed = [];
    private readonly int _frameCount;
    private CancellationTokenSource _cancellation = new();
    private TaskCompletionSource? _idle;
    private bool _dispatching;
    private Request? _delivering;
    private long _lastDisplay;
    private double _maxFps;
    private TimeSpan _displayInterval;
    private TaskCompletionSource _intervalChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Occurs when a request finishes, including failures with a null Frame and an Error.
    /// Subscribers run on managed workers and may queue more frames or dispose this output.
    /// </summary>
    public event EventHandler<VsFrameStatus>? FrameDone;

    /// <summary>
    /// Occurs in request order; successful frames remain valid until all handlers return.
    /// Failed requests carry a null Frame and an Error. Copy frames to retain them.
    /// </summary>
    public event EventHandler<VsFrameStatus>? FrameReady;

    /// <summary>
    /// Reports subscriber exceptions without interrupting frame cleanup or later subscribers.
    /// Exceptions thrown by this diagnostic event are traced and contained.
    /// </summary>
    public event EventHandler<Exception>? CallbackFailed;

    /// <summary>
    /// Gets the native function table shared by this output's environment.
    /// </summary>
    internal VsCoreApi Api { get; }

    /// <summary>
    /// Retains the script and copies the node's media type before exposing its metadata.
    /// </summary>
    internal VsOutput(VsScript script, IntPtr node) : base(IntPtr.Zero, true)
    {
        _script = script;
        Api = script.Api;
        MediaType = Api.GetNodeType(node);
        _frameCount = MediaType switch
        {
            VsMediaType.Video => Marshal.PtrToStructure<VsVideoInfo>(Api.GetVideoInfo(node))!.NumFrames,
            VsMediaType.Audio => Api.GetAudioInfo(node).NumFrames,
            _ => throw new VsException("The script output has an unsupported media type.")
        };
        _core = script.Core;
        var retained = false;
        script.DangerousAddRef(ref retained);
        SetHandle(node);
    }

    /// <summary>
    /// Gets whether this output contains video frames or planar audio frames.
    /// </summary>
    public VsMediaType MediaType { get; }

    /// <summary>
    /// Gets the native core, borrowed until this output is disposed.
    /// </summary>
    public IntPtr Core
    {
        get { lock (_gate) { ThrowIfDisposed(); return _core; } }
    }

    /// <summary>
    /// Gets the retained core while a dependent frame keeps this output alive.
    /// </summary>
    internal IntPtr CorePointer => _core;

    /// <summary>
    /// Gets whether the native node reference is absent.
    /// </summary>
    public override bool IsInvalid => handle == IntPtr.Zero;

    /// <summary>
    /// Gets or sets the event delivery limit in frames per second; zero disables throttling.
    /// </summary>
    public double MaxFps
    {
        get { lock (_gate) { return _maxFps; } }
        set
        {
            value.CheckRange(min: 0d, max: double.MaxValue);
            var interval = value > 0 ? TimeSpan.FromSeconds(1 / value) : TimeSpan.Zero;
            lock (_gate)
            {
                ThrowIfDisposed();
                _maxFps = value;
                _displayInterval = interval;
                _intervalChanged.TrySetResult();
                _intervalChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    /// <summary>
    /// Copies video metadata; requesting it from an audio output throws VsException.
    /// </summary>
    public VsVideoInfo VideoInfo
    {
        get
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                RequireMediaType(VsMediaType.Video);
                return Marshal.PtrToStructure<VsVideoInfo>(Api.GetVideoInfo(handle))!;
            }
        }
    }

    /// <summary>
    /// Copies audio metadata; requesting it from a video output throws VsException.
    /// </summary>
    public VsAudioInfo AudioInfo
    {
        get
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                RequireMediaType(VsMediaType.Audio);
                return Api.GetAudioInfo(handle);
            }
        }
    }

    /// <summary>
    /// Retrieves a frame synchronously. Audio indices address blocks of up to 3072 samples.
    /// The caller owns the returned frame and must dispose it.
    /// </summary>
    public VsFrame GetFrame(int index)
    {
        var retained = false;
        lock (_gate)
        {
            ThrowIfDisposed();
            index.CheckRange(min: 0, max: _frameCount - 1);
            DangerousAddRef(ref retained);
        }

        var frame = IntPtr.Zero;
        try
        {
            frame = ReadNativeFrame(index);
            lock (_gate)
            {
                ThrowIfDisposed();
                var result = new VsFrame(this, frame, index);
                frame = IntPtr.Zero;
                return result;
            }
        }
        finally
        {
            if (frame != IntPtr.Zero) { Api.FreeFrame(frame); }
            if (retained) { DangerousRelease(); }
        }
    }

    /// <summary>
    /// Sets the native worker count; zero selects automatic detection.
    /// Returns the count accepted by the core.
    /// </summary>
    public int SetThreadCount(int threads)
    {
        threads.CheckRange(min: 0);
        lock (_gate)
        {
            ThrowIfDisposed();
            return Api.SetThreadCount(threads, Core);
        }
    }

    /// <summary>
    /// Queues a frame for FrameDone and ordered FrameReady delivery on managed workers.
    /// Native synchronous retrieval runs outside event dispatch, allowing safe callback disposal.
    /// </summary>
    public void GetFrameAsync(int index)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            index.CheckRange(min: 0, max: _frameCount - 1);
            if (_queue.Count == 0 && !_dispatching)
            {
                _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            var retained = false;
            DangerousAddRef(ref retained);
            var request = new Request(new(index), _cancellation.Token);
            _queue.Add(request);
            _ = Task.Run(() => CompleteRequest(request));
        }

        VsFrame.RaiseRequested(index);
    }

    /// <summary>
    /// Cancels queued delivery; already running handlers may finish before the queue becomes idle.
    /// </summary>
    public int ClearQueue() => ClearQueue(null);

    /// <summary>
    /// Cancels queued delivery and invokes this callback on a managed worker after it drains.
    /// Each registered callback runs once; the caller is never blocked by frame retrieval.
    /// </summary>
    public int ClearQueue(Action? callback)
    {
        Task idle;
        int cleared;
        lock (_gate)
        {
            cleared = _disposed ? 0 : CancelQueue();
            idle = _idle?.Task ?? Task.CompletedTask;
        }

        if (callback != null)
        {
            _ = Task.Run(async () =>
            {
                await idle.ConfigureAwait(false);
                try { callback(); }
                catch (Exception error) { ReportCallbackError(error); }
            });
        }

        return cleared;
    }

    /// <summary>
    /// Counts requests still in the queue with the specified state.
    /// </summary>
    public int GetQueueLength(VsFrameState state)
    {
        state.CheckEnumValid();
        lock (_gate) { return _queue.Count(request => request.Status.State == state); }
    }

    /// <summary>
    /// Closes this output immediately and awaits cleanup of all queued requests and handlers.
    /// Use Dispose inside a synchronous handler instead of blocking on this operation.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Dispose();
        Task idle;
        lock (_gate) { idle = _idle?.Task ?? Task.CompletedTask; }
        await idle.ConfigureAwait(false);
    }

    /// <summary>
    /// Cancels delivery and closes the owner; frames and pending requests defer native release.
    /// This operation is idempotent and may be called inside a frame event handler.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _disposed = true;
                CancelQueue();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Releases the native node once, then releases its hold on the script environment.
    /// </summary>
    protected override bool ReleaseHandle()
    {
        _cancellation.Dispose();
        Api.FreeNode(handle);
        _script.DangerousRelease();
        return true;
    }

    // Retrieve outside the queue lock so independent requests can use the native worker pool.
    private void CompleteRequest(Request request)
    {
        var frame = IntPtr.Zero;
        string? error = null;
        var dispatch = false;
        try
        {
            if (!request.Cancellation.IsCancellationRequested)
            {
                try { frame = ReadNativeFrame(request.Status.Index); }
                catch (Exception failure) { error = failure.Message; }
            }

            lock (_gate)
            {
                if (!request.Cancellation.IsCancellationRequested && !_disposed)
                {
                    request.Status.Frame = frame == IntPtr.Zero ? null : new(this, frame, request.Status.Index);
                    frame = IntPtr.Zero;
                    request.Status.Error = error;
                    request.Status.State = error == null ? VsFrameState.Completed : VsFrameState.Failed;
                }

                request.Completed = true;
                _completed.Enqueue(request);
                if (!_dispatching)
                {
                    _dispatching = true;
                    dispatch = true;
                }
            }
        }
        finally
        {
            if (frame != IntPtr.Zero) { Api.FreeFrame(frame); }
            DangerousRelease();
        }

        if (dispatch) { _ = DispatchAsync(); }
    }

    // Deliver completion events as they arrive and ready events in original request order.
    private async Task DispatchAsync()
    {
        while (true)
        {
            Request request;
            bool done;
            lock (_gate)
            {
                if (_queue.Count > 0 && _queue[0].Completed && _queue[0].Notified)
                {
                    request = _queue[0];
                    _queue.RemoveAt(0);
                    _delivering = request;
                    done = false;
                }
                else if (_completed.TryDequeue(out request!))
                {
                    request.Notified = true;
                    done = true;
                }
                else
                {
                    _dispatching = false;
                    if (_queue.Count == 0) { _idle?.TrySetResult(); }
                    return;
                }
            }

            if (done)
            {
                if (!request.Cancellation.IsCancellationRequested) { Notify(FrameDone, request.Status); }
                continue;
            }

            try
            {
                while (true)
                {
                    TimeSpan interval;
                    Task changed;
                    lock (_gate) { interval = _displayInterval; changed = _intervalChanged.Task; }
                    var elapsed = Stopwatch.GetElapsedTime(_lastDisplay);
                    if (_lastDisplay == 0 || interval <= elapsed) { break; }
                    var milliseconds = Math.Min((interval - elapsed).TotalMilliseconds, int.MaxValue);
                    var delay = Task.Delay(TimeSpan.FromMilliseconds(milliseconds), request.Cancellation);
                    await Task.WhenAny(delay, changed).ConfigureAwait(false);
                    request.Cancellation.ThrowIfCancellationRequested();
                }

                bool deliver;
                lock (_gate)
                {
                    deliver = !request.Cancellation.IsCancellationRequested;
                    _delivering = null;
                }
                if (deliver)
                {
                    Notify(FrameReady, request.Status);
                    _lastDisplay = Stopwatch.GetTimestamp();
                }
            }
            catch (OperationCanceledException) when (request.Cancellation.IsCancellationRequested) { }
            finally
            {
                lock (_gate) { if (_delivering == request) { _delivering = null; } }
                request.Status.Frame?.Dispose();
            }
        }
    }

    // Mark cancellation without freeing frames that a running event handler may still be reading.
    private int CancelQueue()
    {
        var count = 0;
        foreach (var request in _queue)
        {
            if (request.Status.State == VsFrameState.Cancelled) { continue; }
            request.Status.State = VsFrameState.Cancelled;
            count++;
        }

        if (_delivering != null && _delivering.Status.State != VsFrameState.Cancelled)
        {
            _delivering.Status.State = VsFrameState.Cancelled;
            count++;
        }
        _cancellation.Cancel();
        _cancellation.Dispose();
        _cancellation = new();
        return count;
    }

    // Translate native retrieval failures before exposing a managed frame owner.
    private IntPtr ReadNativeFrame(int index)
    {
        const int bufferSize = 4096;
        var buffer = Marshal.AllocCoTaskMem(bufferSize);
        try
        {
            Marshal.WriteByte(buffer, 0);
            var frame = Api.GetFrame(index, handle, buffer, bufferSize);
            if (frame == IntPtr.Zero)
            {
                var error = Utf8Ptr.FromUtf8Ptr(buffer, bufferSize);
                throw new VsException(error.HasText() ? error : "VapourSynth could not return the requested frame.");
            }

            return frame;
        }
        finally { Marshal.FreeCoTaskMem(buffer); }
    }

    // Isolate each subscriber so another handler and native cleanup still run after a failure.
    private void Notify(EventHandler<VsFrameStatus>? handlers, VsFrameStatus status)
    {
        if (handlers == null) { return; }
        foreach (EventHandler<VsFrameStatus> handler in handlers.GetInvocationList())
        {
            try { handler(this, status); }
            catch (Exception error) { ReportCallbackError(error); }
        }
    }

    // Keep the diagnostic event from becoming another unhandled callback failure.
    private void ReportCallbackError(Exception error)
    {
        try { Trace.TraceError("VapourSynth frame callback failed: {0}", error); } catch { }
        var handlers = CallbackFailed;
        if (handlers == null) { return; }
        foreach (EventHandler<Exception> handler in handlers.GetInvocationList())
        {
            try { handler(this, error); }
            catch (Exception failure) { try { Trace.TraceError("VapourSynth diagnostic callback failed: {0}", failure); } catch { } }
        }
    }

    // Prevent a media-specific native call from receiving the wrong node type.
    private void RequireMediaType(VsMediaType expected)
    {
        if (MediaType != expected) { throw new VsException($"The output contains {MediaType}, not {expected}."); }
    }

    // Refuse fresh operations after disposal even when frames retain the underlying native node.
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    // Keep native completion separate from cancellation and the public result snapshot.
    private sealed class Request(VsFrameStatus status, CancellationToken cancellation)
    {
        /// <summary>
        /// Holds the managed result borrowed by completion and ready handlers.
        /// </summary>
        public VsFrameStatus Status { get; } = status;

        /// <summary>
        /// Cancels delivery without invalidating a native request already in progress.
        /// </summary>
        public CancellationToken Cancellation { get; } = cancellation;

        /// <summary>
        /// Records that native retrieval has returned and its frame can be released.
        /// </summary>
        public bool Completed { get; set; }

        /// <summary>
        /// Records completion-event delivery before the ordered ready event is dispatched.
        /// </summary>
        public bool Notified { get; set; }
    }
}
