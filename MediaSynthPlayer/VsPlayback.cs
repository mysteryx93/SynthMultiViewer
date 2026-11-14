using Avalonia.Media.Imaging;
using Avalonia.Threading;
using HanumanInstitute.VapourSynthApi;

namespace HanumanInstitute.MediaSynthUI;

/// <summary>
/// Plays VapourSynth output through the host frame bitmap.
/// </summary>
internal sealed class VsPlayback : ISynthPlayback
{
    private readonly ISynthPlayerSink _sink;
    private readonly Lock _gate;
    private VsScript? _script;
    private VsOutput? _output;
    private VsVideoInfo? _video;
    private bool _unloading;
    private int _readySeq;

    private VsPlayback(ISynthPlayerSink sink, VsScript script, VsOutput output, VsVideoInfo video)
    {
        _sink = sink;
        _gate = sink.Gate;
        _script = script;
        _output = output;
        _video = video;
        Width = video.Width;
        Height = video.Height;
        Duration = TimeSpan.FromSeconds(Math.Max(video.NumFrames - 1, 0));
        ClipInfo = script.SourceVideoInfo != null ? ClipInfo.FromVapourSynth(script.SourceVideoInfo) : null;
        _output.FrameDone += OnFrameDone;
        _output.FrameReady += OnFrameReady;
    }

    public static VsPlayback Open(string? file, string? script, ISynthPlayerSink sink)
    {
        var loaded = script != null ? VsScript.LoadScript(script, file) : VsScript.LoadFile(file!);
        var output = loaded.GetOutput(0);
        return new(sink, loaded, output, output.VideoInfo);
    }

    public int Width { get; }
    public int Height { get; }
    public TimeSpan Duration { get; }
    public ClipInfo? ClipInfo { get; }

    public IReadOnlyList<FrameProperty> ReadFrameProperties(int index)
    {
        VsScript? script;
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
        VsOutput output;
        VsVideoInfo video;
        WriteableBitmap bitmap;
        lock (_gate)
        {
            if (_output == null || _video == null || sink.Bitmap == null || _video.NumFrames <= 0)
            {
                return;
            }

            output = _output;
            video = _video;
            bitmap = sink.Bitmap;
        }

        try
        {
            index = index.Clamp(0, video.NumFrames - 1);
            using var frame = output.GetFrame(index);
            CopyFrame(frame, bitmap);
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

    public void Play()
    {
        _sink.ShowBitmap();
        FillQueue();
    }

    public void Pause()
    {
        var sink = _sink;
        VsOutput? output;
        lock (_gate)
        {
            output = _output;
        }

        if (output != null)
        {
            sink.PositionRequested -= output.ClearQueue();
        }
    }

    public void Seek(int index, bool playing)
    {
        var sink = _sink;
        VsOutput? output;
        lock (_gate)
        {
            output = _output;
            if (output == null || _unloading) { return; }

            if (playing)
            {
                sink.PositionRequested = index - 1;
            }
        }

        output.ClearQueue();
        if (playing)
        {
            FillQueue();
        }
        else
        {
            Present(index);
        }
    }

    public void Unload()
    {
        var sink = _sink;
        VsOutput? output;
        lock (_gate)
        {
            if (_output == null || _unloading) { return; }

            _unloading = true;
            output = _output;
        }

        output.ClearQueue(() =>
        {
            DisposeSession();
            Dispatcher.UIThread.Post(() =>
            {
                sink.ClearVideo();
                sink.ContinueAfterStop();
            });
        });
    }

    public void SetThreadCount(int threads)
    {
        lock (_gate)
        {
            _output?.SetThreadCount(threads);
        }
    }

    public void ApplyLimitFps(bool limit)
    {
        lock (_gate)
        {
            if (_output != null && _video != null)
            {
                _output.MaxFps = limit && _video.FpsDen > 0 ? (double)_video.FpsNum / _video.FpsDen : 0;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _unloading = true;
        }

        DisposeSession();
    }

    private void FillQueue()
    {
        var sink = _sink;
        lock (_gate)
        {
            FillQueueLocked(sink);
        }
    }

    private void FillQueueLocked(ISynthPlayerSink sink)
    {
        if (_output == null || _unloading) { return; }

        var addCount = Math.Max(sink.ThreadCount - _output.GetQueueLength(VsFrameState.Requested), 0);
        for (var i = 0; i < addCount; i++)
        {
            RequestNextLocked(sink, false);
        }
    }

    private void RequestNext(bool force)
    {
        var sink = _sink;
        lock (_gate)
        {
            RequestNextLocked(sink, force);
        }
    }

    private void RequestNextLocked(ISynthPlayerSink sink, bool force)
    {
        if (_output != null && !_unloading && (force || sink.IsPlaying) && _video != null &&
            sink.PositionRequested < _video.NumFrames - 1)
        {
            _output.GetFrameAsync(++sink.PositionRequested);
        }
    }

    private void OnFrameDone(object? sender, VsFrameStatus e)
    {
        var sink = _sink;
        VsOutput output;
        lock (_gate)
        {
            if (_output == null || _unloading) { return; }

            output = _output;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (sender != output || sink.IsDisposed)
            {
                return;
            }

            if (e.Error == null)
            {
                RequestNext(false);
            }
            else
            {
                sink.DisplayError(e.Error);
            }
        });
    }

    private void OnFrameReady(object? sender, VsFrameStatus e)
    {
        var sink = _sink;
        VsOutput output;
        lock (_gate)
        {
            if (_output == null || _unloading || sender != _output || e.Error != null || e.Frame == null ||
                sink.IsDisposed)
            {
                return;
            }

            output = _output;
        }

        // Copy the borrowed event frame before the managed dispatcher releases it.
        var pixels = CopyRgb(e.Frame!);
        var index = e.Index;
        IReadOnlyList<FrameProperty> properties;
        try
        {
            properties = MapProperties(e.Frame.GetProperties());
        }
        catch
        {
            properties = [];
        }

        var seq = Interlocked.Increment(ref _readySeq);
        Dispatcher.UIThread.Post(() =>
        {
            using (pixels)
            {
                var bitmap = sink.Bitmap;
                if (seq != Volatile.Read(ref _readySeq) || sender != output || sink.IsDisposed ||
                    sink.IsErrorVisible || bitmap == null)
                {
                    return;
                }

                CopyToBitmap(pixels, bitmap);
                sink.ShowBitmap();
                sink.SetPosition(index, properties);
            }
        });
    }

    private void DisposeSession()
    {
        VsOutput? output;
        VsScript? script;
        lock (_gate)
        {
            output = _output;
            script = _script;
            _output = null;
            _script = null;
            _video = null;
            Interlocked.Increment(ref _readySeq);
        }

        output?.Dispose();
        script?.Dispose();
    }

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

    private static FrameBuffer CopyRgb(VsFrame frame)
    {
        var red = frame.GetPlane(0);
        var green = frame.GetPlane(1);
        var blue = frame.GetPlane(2);
        return FrameBuffer.CopyRgbToBgra(red.Ptr, red.Stride, green.Ptr, green.Stride, blue.Ptr, blue.Stride, red.Width, red.Height);
    }

    private static void CopyFrame(VsFrame frame, WriteableBitmap bitmap)
    {
        using var pixels = CopyRgb(frame);
        CopyToBitmap(pixels, bitmap);
    }

    private static void CopyToBitmap(FrameBuffer pixels, WriteableBitmap bitmap)
    {
        using var framebuffer = bitmap.Lock();
        pixels.CopyTo(framebuffer.Address, framebuffer.RowBytes);
    }
}
