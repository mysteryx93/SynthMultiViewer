using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace HanumanInstitute.MediaSynthUI;

/// <summary>
/// Scrollable zoom/pan viewer for video frames.
/// </summary>
public class ZoomViewer : ScrollViewer
{
    private readonly ZoomSurface _surface = new();
    private Point _origin;
    private Point _start;
    private bool _panning;
    private bool _updatingOffset;
    private Vector? _pendingOffset;
    private Point? _pendingCenter;

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    /// <summary>
    /// Creates a scrollable surface with mouse zoom and pan support.
    /// </summary>
    public ZoomViewer()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        AllowAutoHide = false;
        Focusable = true;
        IsTabStop = false;
        ClipToBounds = true;
        UseLayoutRounding = true;
        Background = Brushes.Transparent;
        Content = _surface.Root;
        BringIntoViewOnFocusChange = false;
        _surface.ContentSizeChanged += (_, _) => UpdateZoomSize();

        _surface.Root.AddHandler(PointerPressedEvent, OnPanPointerPressed, RoutingStrategies.Tunnel);
        _surface.Root.AddHandler(PointerWheelChangedEvent, OnZoomPointerWheelChanged, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPanPointerReleased, RoutingStrategies.Tunnel, true);
        AddHandler(PointerMovedEvent, OnPanPointerMoved, RoutingStrategies.Tunnel, true);

        this.GetObservable(OffsetProperty).Subscribe(_ =>
        {
            if (_updatingOffset || _pendingCenter != null || _pendingOffset != null)
            {
                return;
            }

            PublishOffset();
        });
        this.GetObservable(BoundsProperty).Subscribe(_ => UpdateSurfaceMinSize());
        this.GetObservable(ViewportProperty).Subscribe(_ => UpdateSurfaceMinSize());
        LayoutUpdated += (_, _) => FlushPendingOffset();
    }

    /// <summary>
    /// Gets or sets the control to zoom and pan.
    /// </summary>
    public Control? Child
    {
        get => _surface.Child;
        set
        {
            _surface.Child = value;
            UpdateZoomSize();
        }
    }

    /// <summary>
    /// Defines the <see cref="AllowPan"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> AllowPanProperty = AvaloniaProperty.Register<ZoomViewer, bool>(nameof(AllowPan), true);
    /// <summary>
    /// Gets or sets whether dragging pans the content.
    /// </summary>
    public bool AllowPan
    {
        get => GetValue(AllowPanProperty);
        set => SetValue(AllowPanProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="AllowZoom"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> AllowZoomProperty = AvaloniaProperty.Register<ZoomViewer, bool>(nameof(AllowZoom), true);
    /// <summary>
    /// Gets or sets whether the mouse wheel changes zoom.
    /// </summary>
    public bool AllowZoom
    {
        get => GetValue(AllowZoomProperty);
        set => SetValue(AllowZoomProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="AllowReset"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> AllowResetProperty = AvaloniaProperty.Register<ZoomViewer, bool>(nameof(AllowReset), true);
    /// <summary>
    /// Gets or sets whether right-click resets zoom and scroll position.
    /// </summary>
    public bool AllowReset
    {
        get => GetValue(AllowResetProperty);
        set => SetValue(AllowResetProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="ScrollVerticalOffset"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ScrollVerticalOffsetProperty = AvaloniaProperty.Register<ZoomViewer, double>(nameof(ScrollVerticalOffset), -1);
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
    public static readonly StyledProperty<double> ScrollHorizontalOffsetProperty = AvaloniaProperty.Register<ZoomViewer, double>(nameof(ScrollHorizontalOffset), -1);
    /// <summary>
    /// Gets or sets the horizontal scroll offset; a negative value leaves it unspecified.
    /// </summary>
    public double ScrollHorizontalOffset
    {
        get => GetValue(ScrollHorizontalOffsetProperty);
        set => SetValue(ScrollHorizontalOffsetProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="ZoomIncrement"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ZoomIncrementProperty = AvaloniaProperty.Register<ZoomViewer, double>(nameof(ZoomIncrement), 1.2);
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
    public static readonly StyledProperty<double> MinZoomProperty = AvaloniaProperty.Register<ZoomViewer, double>(nameof(MinZoom));
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
    public static readonly StyledProperty<double> MaxZoomProperty = AvaloniaProperty.Register<ZoomViewer, double>(nameof(MaxZoom));
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
    public static readonly StyledProperty<double> ZoomProperty = AvaloniaProperty.Register<ZoomViewer, double>(nameof(Zoom), 1.0);
    /// <summary>
    /// Gets or sets the zoom factor, where one displays the original size.
    /// </summary>
    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="BitmapInterpolationMode"/> property.
    /// </summary>
    public static readonly StyledProperty<BitmapInterpolationMode> BitmapInterpolationModeProperty = AvaloniaProperty.Register<ZoomViewer, BitmapInterpolationMode>(nameof(BitmapInterpolationMode), BitmapInterpolationMode.HighQuality);
    /// <summary>
    /// Gets or sets the interpolation used when resizing the content.
    /// </summary>
    public BitmapInterpolationMode BitmapInterpolationMode
    {
        get => GetValue(BitmapInterpolationModeProperty);
        set => SetValue(BitmapInterpolationModeProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ZoomProperty)
        {
            var zoom = CoerceZoom(change.GetNewValue<double>());
            if (Math.Abs(zoom - Zoom) > double.Epsilon)
            {
                SetCurrentValue(ZoomProperty, zoom);
                return;
            }

            var oldZoom = change.GetOldValue<double>();
            if (oldZoom <= 0)
            {
                oldZoom = 1;
            }

            ApplyZoom(oldZoom);
        }
        else if ((change.Property == ScrollHorizontalOffsetProperty || change.Property == ScrollVerticalOffsetProperty) && !_updatingOffset)
        {
            ApplyOffset();
        }
        else if (change.Property == BitmapInterpolationModeProperty && _surface.Child != null)
        {
            RenderOptions.SetBitmapInterpolationMode(_surface.Child, BitmapInterpolationMode);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        StopPan();
    }

    private void OnPanPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsRightButtonPressed && AllowReset)
        {
            Reset();
            e.Handled = true;
            return;
        }

        if (point.Properties.IsLeftButtonPressed && AllowPan)
        {
            _start = e.GetPosition(this);
            _origin = new(Math.Max(0, Offset.X), Math.Max(0, Offset.Y));
            _panning = true;
            Cursor = new(StandardCursorType.Hand);
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    private void OnPanPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_panning || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        StopPan();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnPanPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_panning || !AllowPan)
        {
            return;
        }

        var position = e.GetPosition(this);
        var delta = _start - position;
        QueueOffset(_origin.X + delta.X, _origin.Y + delta.Y);
        e.Handled = true;
    }

    private void OnZoomPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!AllowZoom || e.Delta.Y == 0)
        {
            return;
        }

        var newZoom = e.Delta.Y > 0 ? Zoom * ZoomIncrement : Zoom / ZoomIncrement;
        SetCurrentValue(ZoomProperty, CoerceZoom(newZoom));
        e.Handled = true;
    }

    /// <summary>
    /// Restores the original scale and scrolls to the top-left corner.
    /// </summary>
    public void Reset()
    {
        SetCurrentValue(ZoomProperty, 1.0);
        SetCurrentValue(ScrollHorizontalOffsetProperty, 0.0);
        SetCurrentValue(ScrollVerticalOffsetProperty, 0.0);
        QueueOffset(0, 0);
    }

    private void StopPan()
    {
        if (!_panning)
        {
            return;
        }

        _panning = false;
        Cursor = new(StandardCursorType.Arrow);
    }

    private double CoerceZoom(double value)
    {
        if (value <= 0)
        {
            return Zoom > 0 ? Zoom : 1;
        }

        if (MinZoom > 0)
        {
            value = Math.Max(value, MinZoom);
        }

        if (MaxZoom > 0)
        {
            value = Math.Min(value, MaxZoom);
        }

        return value;
    }

    private void UpdateZoomSize()
    {
        _surface.ApplyScale(Zoom);
        UpdateSurfaceMinSize();
        InvalidateMeasure();
    }

    private void ApplyZoom(double oldZoom)
    {
        var viewWidth = Viewport.Width > 0 ? Viewport.Width : Bounds.Width;
        var viewHeight = Viewport.Height > 0 ? Viewport.Height : Bounds.Height;
        if (viewWidth > 0 && viewHeight > 0 && oldZoom > 0)
        {
            _pendingCenter ??= new(
                _surface.Sizer.Width <= viewWidth ? _surface.Sizer.Width / oldZoom / 2
                    : (Math.Max(0, Offset.X) + viewWidth / 2) / oldZoom,
                _surface.Sizer.Height <= viewHeight ? _surface.Sizer.Height / oldZoom / 2
                    : (Math.Max(0, Offset.Y) + viewHeight / 2) / oldZoom);
            _pendingOffset = null;
        }

        UpdateZoomSize();
    }

    private void UpdateSurfaceMinSize()
    {
        var width = Viewport.Width > 0 ? Viewport.Width : Bounds.Width;
        var height = Viewport.Height > 0 ? Viewport.Height : Bounds.Height;
        _surface.FitViewport(new(width, height));
    }

    private void ApplyOffset()
    {
        if (ScrollHorizontalOffset < 0 && ScrollVerticalOffset < 0)
        {
            return;
        }

        QueueOffset(Math.Max(0, ScrollHorizontalOffset), Math.Max(0, ScrollVerticalOffset));
    }

    private void QueueOffset(double x, double y)
    {
        _pendingCenter = null;
        _pendingOffset = new Vector(Math.Max(0, x), Math.Max(0, y));
        FlushPendingOffset();
    }

    private void FlushPendingOffset()
    {
        if ((_pendingOffset == null && _pendingCenter == null) ||
            !IsMeasureValid || !IsArrangeValid || !_surface.Root.IsMeasureValid || !_surface.Root.IsArrangeValid ||
            !_surface.Sizer.IsMeasureValid || !_surface.Sizer.IsArrangeValid ||
            Viewport.Width <= 0 || Viewport.Height <= 0)
        {
            return;
        }

        var pending = _pendingCenter is { } center
            ? new(Math.Max(0, center.X * Zoom - Viewport.Width / 2),
                Math.Max(0, center.Y * Zoom - Viewport.Height / 2))
            : _pendingOffset!.Value;
        var maxX = Math.Max(0, Extent.Width - Viewport.Width);
        var maxY = Math.Max(0, Extent.Height - Viewport.Height);

        _pendingOffset = null;
        _pendingCenter = null;
        _updatingOffset = true;
        try
        {
            SetCurrentValue(OffsetProperty, new(Math.Min(pending.X, maxX), Math.Min(pending.Y, maxY)));
        }
        finally
        {
            _updatingOffset = false;
        }
        PublishOffset();
    }

    private void PublishOffset()
    {
        _updatingOffset = true;
        try
        {
            SetCurrentValue(ScrollHorizontalOffsetProperty, Offset.X);
            SetCurrentValue(ScrollVerticalOffsetProperty, Offset.Y);
        }
        finally
        {
            _updatingOffset = false;
        }
    }
}
