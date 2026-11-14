using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Owns a native audio or video frame and retains its output until disposal.
/// </summary>
public sealed class VsFrame : SafeHandle
{
    private readonly VsOutput _output;

    /// <summary>
    /// Serializes access to this frame's native storage and disposal.
    /// </summary>
    internal Lock Gate { get; } = new();

    /// <summary>
    /// Gets the core table retained by this frame's output.
    /// </summary>
    internal VsCoreApi Api => _output.Api;

    /// <summary>
    /// Gets the live frame address while the caller holds Gate.
    /// </summary>
    internal IntPtr Pointer
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsClosed, this);
            return handle;
        }
    }

    /// <summary>
    /// Gets the zero-based video frame or audio block index.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets whether this frame contains image planes or planar audio channels.
    /// </summary>
    public VsMediaType MediaType => _output.MediaType;

    /// <summary>
    /// Gets whether this frame was created as an independently writable copy.
    /// </summary>
    public bool IsWritable { get; }

    /// <summary>
    /// Gets whether the native frame reference is absent.
    /// </summary>
    public override bool IsInvalid => handle == IntPtr.Zero;

    /// <summary>
    /// Occurs when an asynchronous frame request is submitted; subscriber failures are traced.
    /// </summary>
    public static event EventHandler<int>? Requested;

    /// <summary>
    /// Occurs when a native frame is wrapped; subscriber failures are traced.
    /// </summary>
    public static event EventHandler<VsFrame>? Allocated;

    /// <summary>
    /// Occurs after a native frame is released; subscriber failures are traced.
    /// </summary>
    public static event EventHandler<VsFrame>? Deallocated;

    /// <summary>
    /// Reports a queued frame without allowing diagnostic subscribers to break retrieval.
    /// </summary>
    internal static void RaiseRequested(int index) => Notify(Requested, index);

    /// <summary>
    /// Retains the owning node until the frame's native reference is released.
    /// </summary>
    internal VsFrame(VsOutput output, IntPtr frame, int index, bool writable = false) : base(IntPtr.Zero, true)
    {
        var retained = false;
        output.DangerousAddRef(ref retained);
        _output = output;
        Index = index;
        IsWritable = writable;
        SetHandle(frame);
        Notify(Allocated, this);
    }

    /// <summary>
    /// Copies the actual video format of this frame, including variable-format outputs.
    /// </summary>
    public VsFormat Format
    {
        get
        {
            lock (Gate)
            {
                RequireMediaType(VsMediaType.Video);
                return Api.GetVideoFrameFormat(Pointer);
            }
        }
    }

    /// <summary>
    /// Copies the planar sample storage of this audio frame.
    /// </summary>
    public VsAudioFormat AudioFormat
    {
        get
        {
            lock (Gate)
            {
                RequireMediaType(VsMediaType.Audio);
                return Api.GetAudioFrameFormat(Pointer);
            }
        }
    }

    /// <summary>
    /// Gets the samples per channel in this audio block, including a shorter final block.
    /// </summary>
    public int AudioSampleCount
    {
        get
        {
            lock (Gate)
            {
                RequireMediaType(VsMediaType.Audio);
                return Api.GetFrameLength(Pointer);
            }
        }
    }

    /// <summary>
    /// Returns a video plane view, valid until this frame is disposed.
    /// </summary>
    public VsPlane GetPlane(int plane)
    {
        lock (Gate)
        {
            plane.CheckRange(min: 0, max: Format.NumPlanes - 1);
            return new(this, plane);
        }
    }

    /// <summary>
    /// Returns one audio channel's native sample bytes in ascending speaker-bit order.
    /// The span is valid only while this frame remains undisposed.
    /// </summary>
    public unsafe ReadOnlySpan<byte> GetChannel(int channel)
    {
        lock (Gate)
        {
            var format = AudioFormat;
            channel.CheckRange(min: 0, max: format.NumChannels - 1);
            var length = checked(AudioSampleCount * format.BytesPerSample);
            return new((void*)Api.GetReadPtr(Pointer, channel), length);
        }
    }

    /// <summary>
    /// Returns an independently owned writable copy that can outlive a frame event handler.
    /// </summary>
    public VsFrame Copy()
    {
        lock (Gate)
        {
            var copy = Api.CopyFrame(Pointer, _output.CorePointer);
            if (copy == IntPtr.Zero) { throw new VsException("VapourSynth could not copy the frame."); }
            try { return new(_output, copy, Index, true); }
            catch
            {
                Api.FreeFrame(copy);
                throw;
            }
        }
    }

    /// <summary>
    /// Copies the frame property map as display name/value pairs without retaining native maps.
    /// </summary>
    public IReadOnlyList<(string Name, string Value)> GetProperties()
    {
        lock (Gate)
        {
            var map = Api.GetFramePropertiesRo(Pointer);
            if (map == IntPtr.Zero) { return []; }
            var count = Api.MapNumKeys(map);
            if (count <= 0) { return []; }

            var properties = new List<(string Name, string Value)>(count);
            for (var i = 0; i < count; i++)
            {
                var keyPointer = Api.MapGetKey(map, i);
                var key = Utf8Ptr.FromUtf8Ptr(keyPointer);
                if (key.HasValue()) { properties.Add((key, FormatValue(Api, map, keyPointer))); }
            }

            return properties;
        }
    }

    /// <summary>
    /// Closes frame access atomically with respect to native reads through its plane views.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        lock (Gate) { base.Dispose(disposing); }
    }

    /// <summary>
    /// Releases the frame once, then releases its hold on the output and script environment.
    /// </summary>
    protected override bool ReleaseHandle()
    {
        Api.FreeFrame(handle);
        _output.DangerousRelease();
        Notify(Deallocated, this);
        return true;
    }

    // Reject media-specific reads before calling native functions with an incompatible frame.
    private void RequireMediaType(VsMediaType expected)
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        if (MediaType != expected) { throw new VsException($"The frame contains {MediaType}, not {expected}."); }
    }

    // Diagnostic events never take ownership of errors or interrupt native lifetime cleanup.
    private static void Notify<T>(EventHandler<T>? handlers, T value)
    {
        if (handlers == null) { return; }
        foreach (EventHandler<T> handler in handlers.GetInvocationList())
        {
            try { handler(value, value); }
            catch (Exception error) { try { Trace.TraceError("VapourSynth frame diagnostic failed: {0}", error); } catch { } }
        }
    }

    private static string FormatValue(VsCoreApi api, IntPtr map, IntPtr key)
    {
        var type = api.MapGetType(map, key);
        var count = api.MapNumElements(map, key);
        if (count <= 0)
        {
            return "";
        }

        return type switch
        {
            1 => Join(count, i => api.MapGetInt(map, key, i, out _).ToString()),
            2 => Join(count, i => api.MapGetFloat(map, key, i, out _).ToString("G")),
            3 => Join(count, i => FormatData(api, map, key, i)),
            4 => Named(count, "function"),
            5 => Named(count, "video node"),
            6 => Named(count, "audio node"),
            7 => Named(count, "video frame"),
            8 => Named(count, "audio frame"),
            _ => "<unset>"
        };
    }

    private static string FormatData(VsCoreApi api, IntPtr map, IntPtr key, int index)
    {
        var pointer = api.MapGetData(map, key, index, out _);
        if (pointer == IntPtr.Zero)
        {
            return "";
        }

        var size = api.MapGetDataSize(map, key, index, out _);
        if (size <= 0) { return ""; }
        var data = new byte[size];
        Marshal.Copy(pointer, data, 0, size);
        return System.Text.Encoding.UTF8.GetString(data);
    }

    private static string Named(int count, string type) => count == 1 ? "<" + type + ">" : count + " " + type + "s";

    private static string Join(int count, Func<int, string> value)
    {
        if (count == 1)
        {
            return value(0);
        }

        var parts = new string[count];
        for (var i = 0; i < count; i++)
        {
            parts[i] = value(i);
        }

        return string.Join(", ", parts);
    }
}
