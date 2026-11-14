using Avalonia.Media.Imaging;
using Avalonia.Threading;
using HanumanInstitute.AviSynthApi;

namespace HanumanInstitute.MediaSynthUI;

/// <summary>
/// Plays AviSynth output through the host frame bitmap.
/// </summary>
internal sealed class AvsPlayback : ISynthPlayback
{
    private readonly ISynthPlayerSink _sink;
    private readonly Lock _gate;
    private AvsScript? _script;
    private readonly AvsVideoInfo _video;
    private int _requestId;
    private int _inFlight;
    private bool _stopping;
    private bool _resume;
    private bool _resumeForce;

    private AvsPlayback(ISynthPlayerSink sink, AvsScript script)
    {
        _sink = sink;
        _gate = sink.Gate;
        _script = script;
        _video = script.VideoInfo;
        ClipInfo = ClipInfo.FromAviSynth(script.SourceVideoInfo);
    }

    public static AvsPlayback Open(string? file, string? script, ISynthPlayerSink sink)
    {
        var loaded = script != null ? AvsScript.LoadScript(script, file) : AvsScript.LoadFile(file!);
        return new(sink, loaded);
    }

    public int Width => _video.Width;
    public int Height => _video.Height;
    public TimeSpan Duration => TimeSpan.FromSeconds(Math.Max(_video.FrameCount - 1, 0));
    public ClipInfo ClipInfo { get; }

    public IReadOnlyList<FrameProperty> ReadFrameProperties(int index)
    {
        AvsScript? script;
        lock (_gate)
        {
            script = _script;
        }

        if (script == null)
        {
            return [];
        }

        try
        {
            return MapProperties(script.GetSourceFrameProperties(index));
        }
        catch
        {
            return [];
        }
    }

    public void Present(int index)
    {
        var sink = _sink;
        AvsScript script;
        WriteableBitmap bitmap;
        lock (_gate)
        {
            if (_script == null || sink.Bitmap == null || _video.FrameCount <= 0)
            {
                return;
            }

            script = _script;
            bitmap = sink.Bitmap;
        }

        try
        {
            index = index.Clamp(0, _video.FrameCount - 1);
            using var frame = script.GetFrame(index);
            var plane = frame.GetPlane(0);
            using var pixels = CopyPlane(plane);
            using (var framebuffer = bitmap.Lock())
            {
                pixels.CopyTo(framebuffer.Address, framebuffer.RowBytes);
            }

            sink.ShowBitmap();
            lock (_gate)
            {
                sink.PositionRequested = index;
            }

            sink.SetPosition(index, ReadFrameProperties(index));
        }
        catch (Exception ex)
        {
            sink.DisplayError(ex.InnerException?.Message ?? ex.Message);
        }
    }

    public void Play() => RequestNext(false);

    public void Pause() => CancelQueue();

    public void Seek(int index, bool playing)
    {
        var sink = _sink;
        CancelQueue();
        if (playing)
        {
            lock (_gate)
            {
                sink.PositionRequested = index - 1;
            }

            RequestNext(false);
        }
        else
        {
            Present(index);
        }
    }

    public void Unload()
    {
        var sink = _sink;
        bool pending;
        lock (_gate)
        {
            CancelQueueLocked();
            _stopping = true;
            pending = _inFlight > 0;
            if (!pending)
            {
                DisposeSessionLocked();
            }
        }

        if (!pending)
        {
            sink.ContinueAfterStop();
        }
    }

    public void SetThreadCount(int threads)
    {
    }

    public void ApplyLimitFps(bool limit)
    {
    }

    public void Dispose()
    {
        lock (_gate)
        {
            DisposeSessionLocked();
        }
    }

    private void CancelQueue()
    {
        lock (_gate)
        {
            CancelQueueLocked();
        }
    }

    private void CancelQueueLocked()
    {
        _requestId++;
        _resume = false;
    }

    private void Resume(ISynthPlayerSink sink)
    {
        if (_stopping && _inFlight == 0)
        {
            DisposeSessionLocked();
            sink.ContinueAfterStop();
            return;
        }

        if (_resume && _inFlight == 0)
        {
            _resume = false;
            RequestNext(_resumeForce);
        }
    }

    private void RequestNext(bool force)
    {
        var sink = _sink;
        lock (_gate)
        {
            if (_script == null || _stopping || (!force && !sink.IsPlaying))
            {
                return;
            }

            if (sink.PositionRequested >= _video.FrameCount - 1)
            {
                return;
            }

            if (_inFlight > 0)
            {
                _resume = true;
                _resumeForce = force;
                return;
            }

            var index = ++sink.PositionRequested;
            var requestId = _requestId;
            _inFlight++;
            ThreadPool.QueueUserWorkItem(_ => GetFrame(requestId, index));
        }
    }

    private void GetFrame(int requestId, int index)
    {
        var sink = _sink;
        FrameBuffer? pixels = null;
        IReadOnlyList<FrameProperty> properties = [];
        AvsScript? script;
        lock (_gate)
        {
            script = requestId == _requestId && !sink.IsDisposed ? _script : null;
        }

        try
        {
            if (script != null)
            {
                using var frame = script.GetFrame(index);
                var plane = frame.GetPlane(0);
                pixels = CopyPlane(plane);
                properties = MapProperties(script.GetSourceFrameProperties(index));
            }
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => sink.DisplayError(ex.InnerException?.Message ?? ex.Message));
        }
        finally
        {
            lock (_gate)
            {
                if (_inFlight > 0)
                {
                    _inFlight--;
                }
            }
        }

        Dispatcher.UIThread.Post(() =>
        {
            using (pixels)
            {
                WriteableBitmap? bitmap;
                lock (_gate)
                {
                    bitmap = sink.Bitmap;
                    if (pixels == null || requestId != _requestId || sink.IsDisposed || _script == null ||
                        sink.IsErrorVisible || bitmap == null)
                    {
                        Resume(sink);
                        return;
                    }
                }

                using (var framebuffer = bitmap.Lock())
                {
                    pixels.CopyTo(framebuffer.Address, framebuffer.RowBytes);
                }

                sink.ShowBitmap();
                sink.SetPosition(index, properties);
            }

            lock (_gate)
            {
                if (_stopping)
                {
                    Resume(sink);
                    return;
                }
            }

            if (sink.IsPlaying && sink.LimitFps && _video.FpsNumerator > 0)
            {
                var delay = TimeSpan.FromSeconds((double)_video.FpsDenominator / _video.FpsNumerator);
                Task.Delay(delay).ContinueWith(_ => RequestNext(false));
            }
            else
            {
                RequestNext(false);
            }
        });
    }

    private static FrameBuffer CopyPlane(AvsPlane plane) =>
        FrameBuffer.CopyFrom(plane.Pointer, plane.Stride, plane.RowSize, plane.Height, flipVertical: true, opaqueBgra: true);

    private static IReadOnlyList<FrameProperty> MapProperties(IReadOnlyList<(string Name, string Value)> properties)
    {
        if (properties.Count == 0)
        {
            return [];
        }

        var mapped = new FrameProperty[properties.Count];
        for (var i = 0; i < properties.Count; i++)
        {
            mapped[i] = new(properties[i].Name, properties[i].Value);
        }

        return mapped;
    }

    private void DisposeSessionLocked()
    {
        _script?.Dispose();
        _script = null;
        _stopping = false;
        _inFlight = 0;
        _resume = false;
    }
}
