using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Input.Platform;
using Avalonia.Platform;
using Avalonia.Threading;
using HanumanInstitute.MediaPlayer.Avalonia;
// ReSharper disable MemberCanBePrivate.Global

namespace HanumanInstitute.MediaSynthUI;

/// <summary>
/// Hosts VapourSynth or AviSynth script output as a video surface.
/// </summary>
public class SynthPlayerHost : PlayerHostBase, IDisposable, ISynthPlayerSink
{
    private readonly Grid _surface = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        UseLayoutRounding = true
    };
    private readonly Grid _host = new() { Background = Brushes.Transparent, UseLayoutRounding = true };
    private readonly Image _imageFit = new() { Stretch = Stretch.Uniform, UseLayoutRounding = true };
    private readonly Image _imageZoom = new() { Stretch = Stretch.None, UseLayoutRounding = true };
    private readonly ZoomViewer _zoom = new();
    private readonly TextBlock _error = new()
    {
        FontSize = 16,
        FontWeight = FontWeight.Bold,
        Foreground = Brush.Parse("#FFF35555"),
        TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new(12),
        IsVisible = false
    };

    private int _posRequested;
    private ISynthPlayback? _playback;
    private readonly Lock _outputLock = new();
    private WriteableBitmap? _bmp;
    private string? _autoLoadFile;
    private string? _autoLoadScript;
    private ScriptKind _autoLoadKind;
    private bool _loadQueued;
    private bool _disposed;

    /// <summary>
    /// Creates the video display controls.
    /// </summary>
    public SynthPlayerHost()
    {
        AutoPlay = false;
        IsVideoVisible = true;
        UseLayoutRounding = true;
        _host.HorizontalAlignment = HorizontalAlignment.Stretch;
        _host.VerticalAlignment = VerticalAlignment.Stretch;
        _imageFit.HorizontalAlignment = HorizontalAlignment.Center;
        _imageFit.VerticalAlignment = VerticalAlignment.Center;
        _zoom.Child = _imageZoom;
        _host.Children.Add(_imageFit);
        _host.Children.Add(_zoom);
        _host.Children.Add(_error);
        _surface.Children.Add(_host);
        LogicalChildren.Add(_surface);
        VisualChildren.Add(_surface);

        _host.PointerWheelChanged += HostOnPointerWheelChanged;
        _imageFit.PointerPressed += ImageFitOnPointerPressed;
        _zoom.GetObservable(ZoomViewer.ScrollHorizontalOffsetProperty).Subscribe(v => ScrollHorizontalOffset = v);
        _zoom.GetObservable(ZoomViewer.ScrollVerticalOffsetProperty).Subscribe(v => ScrollVerticalOffset = v);
        _zoom.GetObservable(ZoomViewer.ZoomProperty).Subscribe(v =>
        {
            if (ZoomScaleToFit || v <= 0)
            {
                return;
            }

            if (Math.Abs(Zoom - v) > double.Epsilon)
            {
                Zoom = v;
            }
        });

        ApplyZoomMode();
        ApplyInterpolation();
    }

    /// <inheritdoc />
    public override Visual HostContainer => _host;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        _surface.Measure(availableSize);
        var width = double.IsInfinity(availableSize.Width) ? _surface.DesiredSize.Width : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? _surface.DesiredSize.Height : availableSize.Height;
        return new(width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        _surface.Arrange(new(finalSize));
        return finalSize;
    }

    /// <summary>
    /// Defines the <see cref="LimitFps"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> LimitFpsProperty = AvaloniaProperty.Register<SynthPlayerHost, bool>(nameof(LimitFps), true);
    /// <summary>
    /// Gets or sets whether playback is limited to the script frame rate.
    /// </summary>
    public bool LimitFps
    {
        get => GetValue(LimitFpsProperty);
        init => SetValue(LimitFpsProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="Threads"/> property.
    /// </summary>
    public static readonly StyledProperty<int> ThreadsProperty = AvaloniaProperty.Register<SynthPlayerHost, int>(nameof(Threads));
    /// <summary>
    /// Gets or sets the worker count; zero uses the processor count.
    /// </summary>
    public int Threads
    {
        get => GetValue(ThreadsProperty);
        set => SetValue(ThreadsProperty, Math.Max(value, 0));
    }

    /// <summary>
    /// Defines the <see cref="ScrollVerticalOffset"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ScrollVerticalOffsetProperty = AvaloniaProperty.Register<SynthPlayerHost, double>(nameof(ScrollVerticalOffset), -1);
    /// <summary>
    /// Gets or sets the vertical scroll offset; a negative value leaves it unspecified.
    /// </summary>
    public double ScrollVerticalOffset
    {
        get => GetValue(ScrollVerticalOffsetProperty);
        set => SetValue(ScrollVerticalOffsetProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="ScrollHorizontalOffset"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ScrollHorizontalOffsetProperty = AvaloniaProperty.Register<SynthPlayerHost, double>(nameof(ScrollHorizontalOffset), -1);
    /// <summary>
    /// Gets or sets the horizontal scroll offset; a negative value leaves it unspecified.
    /// </summary>
    public double ScrollHorizontalOffset
    {
        get => GetValue(ScrollHorizontalOffsetProperty);
        set => SetValue(ScrollHorizontalOffsetProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="Path"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> PathProperty = AvaloniaProperty.Register<SynthPlayerHost, string?>(nameof(Path));
    /// <summary>
    /// Gets or sets the script file path. Used as <c>__file__</c> and the working directory
    /// when Script is set; omit it for unsaved text. Loaded from disk when Script is empty.
    /// </summary>
    public string? Path
    {
        get => GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="Script"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> ScriptProperty = AvaloniaProperty.Register<SynthPlayerHost, string?>(nameof(Script));
    /// <summary>
    /// Gets or sets the script text to render, taking precedence over Path.
    /// </summary>
    public string? Script
    {
        get => GetValue(ScriptProperty);
        set => SetValue(ScriptProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="Kind"/> property.
    /// </summary>
    public static readonly StyledProperty<ScriptKind> KindProperty = AvaloniaProperty.Register<SynthPlayerHost, ScriptKind>(nameof(Kind));
    /// <summary>
    /// Gets or sets which native engine evaluates the script.
    /// </summary>
    public ScriptKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="ZoomIncrement"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ZoomIncrementProperty = AvaloniaProperty.Register<SynthPlayerHost, double>(nameof(ZoomIncrement), 1.2);
    /// <summary>
    /// Gets or sets the multiplier applied by each mouse-wheel zoom step.
    /// </summary>
    public double ZoomIncrement
    {
        get => GetValue(ZoomIncrementProperty);
        set => SetValue(ZoomIncrementProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="MinZoom"/> property.
    /// </summary>
    public static readonly StyledProperty<double> MinZoomProperty = AvaloniaProperty.Register<SynthPlayerHost, double>(nameof(MinZoom));
    /// <summary>
    /// Gets or sets the minimum zoom factor; zero disables the lower limit.
    /// </summary>
    public double MinZoom
    {
        get => GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="MaxZoom"/> property.
    /// </summary>
    public static readonly StyledProperty<double> MaxZoomProperty = AvaloniaProperty.Register<SynthPlayerHost, double>(nameof(MaxZoom));
    /// <summary>
    /// Gets or sets the maximum zoom factor; zero disables the upper limit.
    /// </summary>
    public double MaxZoom
    {
        get => GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="Zoom"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ZoomProperty = AvaloniaProperty.Register<SynthPlayerHost, double>(nameof(Zoom), 1.0);
    /// <summary>
    /// Gets or sets the zoom factor, where one displays the original size.
    /// </summary>
    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="ZoomScaleToFit"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> ZoomScaleToFitProperty = AvaloniaProperty.Register<SynthPlayerHost, bool>(nameof(ZoomScaleToFit));
    /// <summary>
    /// Gets or sets whether the whole frame fits the available area.
    /// </summary>
    public bool ZoomScaleToFit
    {
        get => GetValue(ZoomScaleToFitProperty);
        set => SetValue(ZoomScaleToFitProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="SquarePixels"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> SquarePixelsProperty = AvaloniaProperty.Register<SynthPlayerHost, bool>(nameof(SquarePixels));
    /// <summary>
    /// Gets or sets whether frames use nearest-neighbor interpolation.
    /// </summary>
    public bool SquarePixels
    {
        get => GetValue(SquarePixelsProperty);
        set => SetValue(SquarePixelsProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="ErrorMessage"/> property.
    /// </summary>
    public static readonly DirectProperty<SynthPlayerHost, string?> ErrorMessageProperty = AvaloniaProperty.RegisterDirect<SynthPlayerHost, string?>(nameof(ErrorMessage), o => o.ErrorMessage);
    private string? _errorMessage;
    /// <summary>
    /// Gets the current player error, or null when no error is present.
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetAndRaise(ErrorMessageProperty, ref _errorMessage, value);
    }

    /// <summary>
    /// Defines the <see cref="IsErrorVisible"/> property.
    /// </summary>
    public static readonly DirectProperty<SynthPlayerHost, bool> IsErrorVisibleProperty = AvaloniaProperty.RegisterDirect<SynthPlayerHost, bool>(nameof(IsErrorVisible), o => o.IsErrorVisible);
    private bool _isErrorVisible;
    /// <summary>
    /// Gets whether the player displays an error.
    /// </summary>
    public bool IsErrorVisible
    {
        get => _isErrorVisible;
        private set
        {
            SetAndRaise(IsErrorVisibleProperty, ref _isErrorVisible, value);
            _error.IsVisible = value;
        }
    }

    /// <summary>
    /// Defines the <see cref="VideoSource"/> property.
    /// </summary>
    public static readonly DirectProperty<SynthPlayerHost, WriteableBitmap?> VideoSourceProperty = AvaloniaProperty.RegisterDirect<SynthPlayerHost, WriteableBitmap?>(nameof(VideoSource), o => o.VideoSource);
    private WriteableBitmap? _videoSource;
    /// <summary>
    /// Gets the current frame bitmap, owned by the player.
    /// </summary>
    public WriteableBitmap? VideoSource
    {
        get => _videoSource;
        private set
        {
            SetAndRaise(VideoSourceProperty, ref _videoSource, value);
            RefreshVideoSurface();
        }
    }

    /// <summary>
    /// Defines the <see cref="ClipInfo"/> property.
    /// </summary>
    public static readonly DirectProperty<SynthPlayerHost, ClipInfo?> ClipInfoProperty =
        AvaloniaProperty.RegisterDirect<SynthPlayerHost, ClipInfo?>(nameof(ClipInfo), o => o.ClipInfo);
    private ClipInfo? _clipInfo;
    /// <summary>
    /// Gets the source clip information from before display conversion.
    /// </summary>
    public ClipInfo? ClipInfo
    {
        get => _clipInfo;
        private set => SetAndRaise(ClipInfoProperty, ref _clipInfo, value);
    }

    /// <summary>
    /// Defines the <see cref="FrameProperties"/> property.
    /// </summary>
    public static readonly DirectProperty<SynthPlayerHost, IReadOnlyList<FrameProperty>> FramePropertiesProperty =
        AvaloniaProperty.RegisterDirect<SynthPlayerHost, IReadOnlyList<FrameProperty>>(
            nameof(FrameProperties), o => o.FrameProperties);
    private IReadOnlyList<FrameProperty> _frameProperties = [];
    /// <summary>
    /// Gets the current frame properties from the source clip.
    /// </summary>
    public IReadOnlyList<FrameProperty> FrameProperties
    {
        get => _frameProperties;
        private set => SetAndRaise(FramePropertiesProperty, ref _frameProperties, value);
    }

    /// <summary>
    /// Copies the current video frame to the clipboard.
    /// </summary>
    public Task CopyFrameToClipboardAsync()
    {
        if (VideoSource is null) { return Task.CompletedTask; }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) { return Task.CompletedTask; }

        return clipboard.SetBitmapAsync(VideoSource);
    }

    /// <inheritdoc />
    protected override void IsVideoVisibleChanged(bool value) => _host.IsVisible = true;

    /// <inheritdoc />
    protected override void SetDisplayText() => Text = Title ?? string.Empty;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (Design.IsDesignMode)
        {
            return;
        }

        if (change.Property == LimitFpsProperty)
        {
            _playback?.ApplyLimitFps(LimitFps);
        }
        else if (change.Property == ThreadsProperty)
        {
            _playback?.SetThreadCount(GetThreadCount());
        }
        else if (change.Property == ScriptProperty || change.Property == PathProperty || change.Property == KindProperty)
        {
            QueueScriptLoad();
        }
        else if (change.Property == ZoomProperty)
        {
            if (!ZoomScaleToFit && Zoom > 0)
            {
                _zoom.Zoom = Zoom;
            }

            ApplyZoomMode();
        }
        else if (change.Property == MinZoomProperty)
        {
            _zoom.MinZoom = MinZoom;
        }
        else if (change.Property == MaxZoomProperty)
        {
            _zoom.MaxZoom = MaxZoom;
        }
        else if (change.Property == ZoomIncrementProperty)
        {
            _zoom.ZoomIncrement = ZoomIncrement;
        }
        else if (change.Property == ZoomScaleToFitProperty)
        {
            ApplyZoomMode();
        }
        else if (change.Property == SquarePixelsProperty)
        {
            ApplyInterpolation();
        }
        else if (change.Property == ScrollHorizontalOffsetProperty)
        {
            _zoom.ScrollHorizontalOffset = ScrollHorizontalOffset;
        }
        else if (change.Property == ScrollVerticalOffsetProperty)
        {
            _zoom.ScrollVerticalOffset = ScrollVerticalOffset;
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Stop();
    }

    /// <inheritdoc />
    protected override void PositionChanged(TimeSpan value, bool isSeeking)
    {
        var rounded = TimeSpan.FromSeconds((int)value.TotalSeconds);
        if (rounded != value)
        {
            SetPositionNoSeek(rounded);
            value = rounded;
        }

        if (isSeeking)
        {
            lock (_outputLock)
            {
                if (_playback != null)
                {
                    var newPos = (int)value.TotalSeconds;
                    if (_posRequested != newPos)
                    {
                        _playback.Seek(newPos, IsPlaying);
                    }
                }
                else
                {
                    _posRequested = (int)value.TotalSeconds;
                }
            }
        }

        base.PositionChanged(value, isSeeking);
    }

    /// <inheritdoc />
    protected override void IsPlayingChanged(bool value)
    {
        lock (_outputLock)
        {
            if (_playback != null)
            {
                if (value)
                {
                    _playback.Play();
                }
                else
                {
                    _playback.Pause();
                }
            }
        }

        base.IsPlayingChanged(value);
    }

    /// <inheritdoc />
    public override void Stop()
    {
        base.Stop();
        if (IsPlaying)
        {
            IsPlaying = false;
        }

        var bitmap = _bmp;
        _bmp = null;
        VideoSource = null;
        ClipInfo = null;
        FrameProperties = [];
        bitmap?.Dispose();
        _playback?.Unload();
    }

    internal void PresentTestFrame(WriteableBitmap bitmap)
    {
        _bmp = bitmap;
        VideoSource = bitmap;
        IsVideoVisible = true;
        ApplyZoomMode();
    }

    /// <summary>
    /// Stops playback and displays an error message.
    /// </summary>
    public void DisplayError(string err)
    {
        Stop();
        ErrorMessage = err;
        VideoSource = null;
        IsErrorVisible = true;
        _error.Text = err;
    }

    Lock ISynthPlayerSink.Gate => _outputLock;
    bool ISynthPlayerSink.IsPlaying => IsPlaying;
    bool ISynthPlayerSink.LimitFps => LimitFps;
    bool ISynthPlayerSink.IsDisposed => _disposed;
    bool ISynthPlayerSink.IsErrorVisible => IsErrorVisible;
    WriteableBitmap? ISynthPlayerSink.Bitmap => _bmp;
    int ISynthPlayerSink.PositionRequested
    {
        get => _posRequested;
        set => _posRequested = value;
    }
    int ISynthPlayerSink.ThreadCount => GetThreadCount();
    void ISynthPlayerSink.ShowBitmap() => VideoSource = _bmp;
    void ISynthPlayerSink.SetPosition(int index, IReadOnlyList<FrameProperty> properties)
    {
        FrameProperties = properties;
        SetPositionNoSeek(TimeSpan.FromSeconds(index));
    }
    void ISynthPlayerSink.ClearVideo()
    {
        VideoSource = null;
        Title = null;
    }
    void ISynthPlayerSink.ContinueAfterStop() => ContinueAfterStop();

    private static string GetLoadError(ScriptKind kind, Exception ex)
    {
        var message = ex is DllNotFoundException or EntryPointNotFoundException
            ? ex.Message
            : ex.InnerException?.Message ?? ex.Message;
        if (ex is not DllNotFoundException)
        {
            return message;
        }

        if (!message.Contains("Unable to load", StringComparison.OrdinalIgnoreCase))
        {
            return message;
        }

        var name = kind == ScriptKind.AviSynth ? "AviSynth" : "VapourSynth";
        return $"Could not load {name}.";
    }

    private int GetThreadCount() => Threads > 0 ? Threads : Environment.ProcessorCount;

    private void ApplyZoomMode()
    {
        if (ZoomScaleToFit)
        {
            _imageFit.Width = double.NaN;
            _imageFit.Height = double.NaN;
            _imageFit.Stretch = Stretch.Uniform;
            _imageFit.HorizontalAlignment = HorizontalAlignment.Stretch;
            _imageFit.VerticalAlignment = VerticalAlignment.Stretch;
            _imageFit.IsVisible = true;
            _zoom.IsVisible = false;
            return;
        }

        _imageFit.IsVisible = false;
        _zoom.IsVisible = true;
        if (Zoom > 0)
        {
            _zoom.Zoom = Zoom;
        }
    }

    private void ApplyImagePixelSize()
    {
        if (ZoomScaleToFit || _bmp == null)
        {
            _imageFit.Width = double.NaN;
            _imageFit.Height = double.NaN;
            return;
        }

        _imageFit.Width = _bmp.PixelSize.Width;
        _imageFit.Height = _bmp.PixelSize.Height;
    }

    private void RefreshVideoSurface()
    {
        var source = _videoSource;
        _imageFit.Source = null;
        _imageZoom.Source = null;
        _imageFit.Source = source;
        _imageZoom.Source = source;
        ApplyImagePixelSize();
        _imageFit.InvalidateMeasure();
        _imageFit.InvalidateVisual();
        _imageZoom.InvalidateMeasure();
        _imageZoom.InvalidateVisual();
        _host.InvalidateMeasure();
        _host.InvalidateVisual();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void ApplyInterpolation()
    {
        var mode = SquarePixels ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality;
        RenderOptions.SetBitmapInterpolationMode(_imageFit, mode);
        RenderOptions.SetBitmapInterpolationMode(_imageZoom, mode);
        _zoom.BitmapInterpolationMode = mode;
    }

    private void QueueScriptLoad()
    {
        if (_loadQueued) { return; }

        _loadQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _loadQueued = false;
            var script = Script;
            var path = Path;
            if (!script.HasValue() && !path.HasValue())
            {
                Stop();
            }
            else if (script.HasValue())
            {
                LoadScript(path, script, Kind);
            }
            else if (path.HasValue())
            {
                LoadScript(path, null, Kind);
            }
        });
    }

    private void LoadScript(string? file, string? script, ScriptKind kind)
    {
        if (_disposed) { return; }

        try
        {
            ISynthPlayback playback;
            lock (_outputLock)
            {
                if (_playback != null)
                {
                    _autoLoadFile = file;
                    _autoLoadScript = script;
                    _autoLoadKind = kind;
                    Stop();
                    return;
                }

                SetPositionNoSeek(TimeSpan.Zero);
                Duration = TimeSpan.FromSeconds(1);
                _posRequested = -1;
                ErrorMessage = null;
                IsErrorVisible = false;
                _error.Text = null;
                playback = kind == ScriptKind.AviSynth
                    ? AvsPlayback.Open(file, script, this)
                    : VsPlayback.Open(file, script, this);
                _playback = playback;
                Duration = playback.Duration;
                ClipInfo = playback.ClipInfo;
                FrameProperties = [];
            }

            _bmp = new(
                new(playback.Width, playback.Height),
                new(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);
            IsVideoVisible = true;
            playback.ApplyLimitFps(LimitFps);
            playback.SetThreadCount(GetThreadCount());
            ApplyZoomMode();
            if (IsPlaying)
            {
                playback.Play();
            }
            else
            {
                playback.Present(0);
            }

            if (IsErrorVisible)
            {
                return;
            }

            OnMediaLoaded();
            _zoom.ScrollVerticalOffset = ScrollVerticalOffset;
            _zoom.ScrollHorizontalOffset = ScrollHorizontalOffset;
        }
        catch (Exception ex)
        {
            _playback?.Dispose();
            _playback = null;
            DisplayError(GetLoadError(kind, ex));
        }
    }

    private void ContinueAfterStop()
    {
        _playback = null;
        OnMediaUnloaded();
        SetPositionNoSeek(TimeSpan.Zero);
        if (_autoLoadFile != null || _autoLoadScript != null)
        {
            var file = _autoLoadFile;
            var script = _autoLoadScript;
            var kind = _autoLoadKind;
            _autoLoadFile = null;
            _autoLoadScript = null;
            LoadScript(file, script, kind);
        }
    }

    private void HostOnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_zoom.AllowZoom && ZoomScaleToFit && e.Delta.Y != 0)
        {
            Zoom = 1;
            ZoomScaleToFit = false;
            e.Handled = true;
        }
    }

    private void ImageFitOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(_imageFit).Properties.IsRightButtonPressed && _zoom.AllowReset)
        {
            _zoom.Reset();
        }
    }

    /// <summary>
    /// Stops playback and releases the loaded script and frame bitmap.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        _autoLoadFile = null;
        _autoLoadScript = null;
        Stop();
        GC.SuppressFinalize(this);
    }
}
