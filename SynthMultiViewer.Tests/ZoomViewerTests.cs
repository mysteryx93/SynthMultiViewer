using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class ZoomViewerTests
{

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void MouseWheel_FromMaximumZoom_BackToMinimum_ChangesZoomOnEveryStep(bool autoHide)
    {
        using var fixture = new ViewerFixture(autoHide);
        fixture.Viewer.MinZoom = 0.1;
        fixture.Viewer.MaxZoom = 10;
        fixture.Viewer.Zoom = 10;
        Dispatcher.UIThread.RunJobs();

        foreach (var direction in new[] { -1, 1, -1 })
        {
            for (var step = 0; step < 30; step++)
            {
                var expected = Math.Clamp(direction > 0
                    ? fixture.Viewer.Zoom * fixture.Viewer.ZoomIncrement
                    : fixture.Viewer.Zoom / fixture.Viewer.ZoomIncrement, 0.1, 10);
                var position = fixture.Viewer.TranslatePoint(new(100, 80), fixture.Window)!.Value;
                fixture.Window.MouseWheel(position, new(0, direction));
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(expected, fixture.Viewer.Zoom, 10);
                var center = fixture.Image.TranslatePoint(new(40, 20), fixture.Viewer)!.Value;
                AssertNear(fixture.Viewer.Viewport.Width / 2, center.X);
                AssertNear(fixture.Viewer.Viewport.Height / 2, center.Y);
            }
        }
    }

    [AvaloniaFact]
    public void MouseWheel_ZoomDisabled_ScrollsNormally()
    {
        using var fixture = new ViewerFixture();
        fixture.Viewer.Zoom = 10;
        fixture.Viewer.AllowZoom = false;
        Dispatcher.UIThread.RunJobs();
        var before = fixture.Viewer.Offset;
        var position = fixture.Viewer.TranslatePoint(new(100, 80), fixture.Window)!.Value;

        fixture.Window.MouseWheel(position, new(0, -1));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(10, fixture.Viewer.Zoom);
        Assert.True(fixture.Viewer.Offset.Y > before.Y);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Zoom_AboveAndBelowOriginalSize_KeepsRenderedImageCentered(bool autoHide)
    {
        using var fixture = new ViewerFixture(autoHide);
        foreach (var scale in new[] { 0.5, 1, 1.5, 4, 6, 10, 3, 1, 0.5 })
        {
            fixture.Viewer.Zoom = scale;
            Dispatcher.UIThread.RunJobs();

            var center = fixture.Image.TranslatePoint(new(40, 20), fixture.Viewer)!.Value;
            AssertNear(fixture.Viewer.Viewport.Width / 2, center.X);
            AssertNear(fixture.Viewer.Viewport.Height / 2, center.Y);
        }
    }

    [AvaloniaFact]
    public void Zoom_WhilePanned_PreservesContentPointAtViewportCenter()
    {
        using var fixture = new ViewerFixture();
        fixture.Viewer.Zoom = 6;
        Dispatcher.UIThread.RunJobs();
        fixture.Viewer.Offset = new(280, 30);
        Dispatcher.UIThread.RunJobs();
        var before = fixture.Viewer.TranslatePoint(
            new(fixture.Viewer.Viewport.Width / 2, fixture.Viewer.Viewport.Height / 2), fixture.Image)!.Value;

        fixture.Viewer.Zoom = 10;
        Dispatcher.UIThread.RunJobs();

        var after = fixture.Viewer.TranslatePoint(
            new(fixture.Viewer.Viewport.Width / 2, fixture.Viewer.Viewport.Height / 2), fixture.Image)!.Value;
        AssertNear(before.X, after.X);
        AssertNear(before.Y, after.Y);
        Assert.True(fixture.Viewer.Offset.X > 280);
    }

    [AvaloniaFact]
    public void Zoom_MultipleChangesBeforeLayout_KeepsCenter()
    {
        using var fixture = new ViewerFixture();
        fixture.Viewer.Zoom = 4;
        fixture.Viewer.Zoom = 6;
        fixture.Viewer.Zoom = 10;

        Dispatcher.UIThread.RunJobs();

        var center = fixture.Image.TranslatePoint(new(40, 20), fixture.Viewer)!.Value;
        AssertNear(fixture.Viewer.Viewport.Width / 2, center.X);
        AssertNear(fixture.Viewer.Viewport.Height / 2, center.Y);
    }

    [AvaloniaFact]
    public void Offset_ChangesBothAxes_PublishesBothWithoutReentry()
    {
        using var fixture = new ViewerFixture();
        fixture.Viewer.Zoom = 6;
        Dispatcher.UIThread.RunJobs();

        fixture.Viewer.Offset = new(40, 30);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new(40, 30), fixture.Viewer.Offset);
        Assert.Equal(40, fixture.Viewer.ScrollHorizontalOffset);
        Assert.Equal(30, fixture.Viewer.ScrollVerticalOffset);
    }

    [AvaloniaTheory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void Scrollbar_AfterContentPan_DraggingThumbScrollsForward(Orientation orientation)
    {
        using var fixture = new ViewerFixture(false);
        fixture.Viewer.Zoom = 10;
        Dispatcher.UIThread.RunJobs();
        var contentStart = fixture.Viewer.TranslatePoint(new(40, 40), fixture.Window)!.Value;
        Drag(fixture.Window, contentStart, new(-20, -20));

        var bar = fixture.Viewer.GetVisualDescendants().OfType<ScrollBar>().Single(x => x.Orientation == orientation);
        var thumb = bar.GetVisualDescendants().OfType<Thumb>().Single();
        var start = thumb.TranslatePoint(new(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), fixture.Window)!.Value;
        var before = fixture.Viewer.Offset;
        Drag(fixture.Window, start, orientation == Orientation.Horizontal ? new(15, 0) : new Vector(0, 15));

        if (orientation == Orientation.Horizontal)
        {
            Assert.True(fixture.Viewer.Offset.X > before.X);
            Assert.Equal(before.Y, fixture.Viewer.Offset.Y);
        }
        else
        {
            Assert.True(fixture.Viewer.Offset.Y > before.Y);
            Assert.Equal(before.X, fixture.Viewer.Offset.X);
        }
    }

    [AvaloniaFact]
    public void ContentPan_MovesBothAxes_AndStopsOnRelease()
    {
        using var fixture = new ViewerFixture();
        fixture.Viewer.Zoom = 10;
        Dispatcher.UIThread.RunJobs();
        var before = fixture.Viewer.Offset;
        var start = fixture.Viewer.TranslatePoint(new(40, 40), fixture.Window)!.Value;

        Drag(fixture.Window, start, new(-20, -15));
        var afterDrag = fixture.Viewer.Offset;
        fixture.Window.MouseMove(start + new Vector(-30, -25));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(before + new Vector(20, 15), afterDrag);
        Assert.Equal(before + new Vector(20, 15), fixture.Viewer.Offset);
    }

    [AvaloniaFact]
    public void Reset_BeforeZoomLayout_CancelsPendingCenter()
    {
        using var fixture = new ViewerFixture();
        fixture.Viewer.Zoom = 10;

        fixture.Viewer.Reset();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, fixture.Viewer.Zoom);
        Assert.Equal(default(Vector), fixture.Viewer.Offset);
    }

    private static void Drag(Window window, Point start, Vector delta)
    {
        window.MouseMove(start);
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + delta);
        window.MouseUp(start + delta, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertNear(double expected, double actual) => Assert.InRange(actual, expected - 1, expected + 1);

    private sealed class ViewerFixture : IDisposable
    {
        private readonly WriteableBitmap _bitmap = new(
            new(80, 40), new(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        private readonly IDisposable _shown;

        public ViewerFixture(bool autoHide = true)
        {
            Image = new() { Source = _bitmap, Stretch = Stretch.None };
            Viewer = new() { Child = Image, AllowAutoHide = autoHide };
            Window = new()
            {
                Width = 240, Height = 200,
                Content = new Border { Width = 200, Height = 160, Child = Viewer }
            };
            _shown = new UiTestDriver().Show(Window);
        }

        public Image Image { get; }
        public ZoomViewer Viewer { get; }
        public Window Window { get; }

        public void Dispose()
        {
            _shown.Dispose();
            _bitmap.Dispose();
        }
    }
}
