using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HanumanInstitute.ApiAviSynth;
using HanumanInstitute.ApiVapourSynth;
using HanumanInstitute.SynthMultiViewer.Views;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class SynthPlayerHostTests : ViewerTestsBase
{
    private readonly UiTestDriver _ui = new();

    [AvaloniaFact]
    public void ApplyZoomMode_DefaultZoom_ShowsZoomViewer()
    {
        var host = new SynthPlayerHost();

        using var window = _ui.Show(new() { Width = 320, Height = 240, Content = host });
        var grid = Assert.IsType<Grid>(host.HostContainer);

        Assert.False(grid.Children.OfType<Image>().Single().IsVisible);
        Assert.True(grid.Children.OfType<ZoomViewer>().Single().IsVisible);
    }

    [AvaloniaFact]
    public void PresentTestFrame_ColoredBitmap_SizesVisibleImage()
    {
        var host = new SynthPlayerHost();
        using var window = _ui.Show(new() { Width = 320, Height = 240, Content = host });
        var bitmap = new WriteableBitmap(
            new(64, 48), new(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var framebuffer = bitmap.Lock())
        {
            var color = unchecked((int)0xFFC06020);
            for (var y = 0; y < 48; y++)
            {
                for (var x = 0; x < 64; x++)
                {
                    Marshal.WriteInt32(framebuffer.Address, y * framebuffer.RowBytes + x * 4, color);
                }
            }
        }

        host.PresentTestFrame(bitmap);
        Dispatcher.UIThread.RunJobs();

        var grid = Assert.IsType<Grid>(host.HostContainer);
        var zoom = grid.Children.OfType<ZoomViewer>().Single();
        var sizer = Assert.IsType<Border>(Assert.IsType<Grid>(zoom.Content).Children.Single());
        Assert.True(zoom.IsVisible);
        Assert.True(host.HostContainer.IsVisible);
        Assert.Equal(bitmap, zoom.Child is Image image ? image.Source : null);
        Assert.Equal(bitmap.PixelSize.Width, sizer.Width);
        Assert.Equal(bitmap.PixelSize.Height, sizer.Height);
    }

    [AvaloniaFact]
    public void ApplyZoomMode_ScaleToFit_ShowsUniformImage()
    {
        var host = new SynthPlayerHost { ZoomScaleToFit = true };
        using var window = _ui.Show(new() { Width = 320, Height = 240, Content = host });
        var bitmap = new WriteableBitmap(new(64, 48), new(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

        host.PresentTestFrame(bitmap);
        Dispatcher.UIThread.RunJobs();

        var grid = Assert.IsType<Grid>(host.HostContainer);
        var image = grid.Children.OfType<Image>().Single();
        Assert.True(image.IsVisible);
        Assert.False(grid.Children.OfType<ZoomViewer>().Single().IsVisible);
    }

    [AvaloniaTheory]
    [InlineData(160, 40, 0.5, 0.05, 1)]
    [InlineData(160, 40, 0.5, 0.95, -1)]
    [InlineData(40, 160, 0.05, 0.5, 1)]
    [InlineData(40, 160, 0.95, 0.5, -1)]
    [InlineData(160, 40, 0.5, 0.5, 1)]
    public void ScaleToFit_WheelAnywhereInViewport_LeavesFitAndContinuesZooming(
        int width, int height, double x, double y, int direction)
    {
        var model = Workspace;
        var view = new ViewerView { DataContext = new ViewerViewModel() };
        var window = new Window { Width = 640, Height = 480, DataContext = model, Content = view };
        using var shown = _ui.Show(window);
        var host = view.FindControl<SynthPlayerHost>("PlayerHost")!;
        using var bitmap = new WriteableBitmap(
            new(width, height), new(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        host.PresentTestFrame(bitmap);
        model.Zoom = 0;
        Dispatcher.UIThread.RunJobs();
        var point = host.TranslatePoint(new(host.Bounds.Width * x, host.Bounds.Height * y), window)!.Value;

        window.MouseWheel(point, new(0, direction));
        Dispatcher.UIThread.RunJobs();
        var hostFit = host.ZoomScaleToFit;
        var modelFit = model.ZoomScaleToFit;
        var zoomAfterFirst = model.Zoom;
        window.MouseWheel(point, new(0, direction));
        Dispatcher.UIThread.RunJobs();

        Assert.False(hostFit);
        Assert.False(modelFit);
        Assert.Equal(1, zoomAfterFirst);
        Assert.Equal(direction > 0 ? model.ZoomIncrement : 1 / model.ZoomIncrement, model.Zoom, precision: 8);
    }

    [AvaloniaFact]
    public void ChildImage_SourceAssigned_SizesZoomSurfaceToBitmap()
    {
        var image = new Image { Stretch = Avalonia.Media.Stretch.None };
        var zoom = new ZoomViewer { Child = image };
        using var window = _ui.Show(new() { Width = 400, Height = 300, Content = zoom });
        using var bitmap = new WriteableBitmap(
            new(80, 40), new(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

        image.Source = bitmap;
        Dispatcher.UIThread.RunJobs();

        var sizer = Assert.IsType<Border>(Assert.IsType<Grid>(zoom.Content).Children.Single());
        Assert.Equal(80, sizer.Width);
        Assert.Equal(40, sizer.Height);
    }

    [AvaloniaFact]
    public void Zoom_ZeroWithMinZoom_KeepsScaleToFit()
    {
        var model = Workspace;
        var view = new ViewerView { DataContext = new ViewerViewModel() };
        using var window = _ui.Show(new()
        {
            Width = 640,
            Height = 480,
            DataContext = model,
            Content = view
        });
        var host = view.FindControl<SynthPlayerHost>("PlayerHost")!;

        model.Zoom = 0;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, model.Zoom);
        Assert.True(model.ZoomScaleToFit);
        Assert.True(host.ZoomScaleToFit);
        Assert.Equal(0, host.Zoom);
        Assert.NotEqual(0.1, host.Zoom);
    }

    [AvaloniaFact]
    public void Zoom_LargerThanViewport_CentersContent()
    {
        var image = new Image { Stretch = Avalonia.Media.Stretch.None };
        var zoom = new ZoomViewer { Child = image, MinZoom = 0.1, MaxZoom = 10 };
        using var window = _ui.Show(new()
        {
            Width = 240,
            Height = 200,
            Content = new Border { Width = 200, Height = 160, Child = zoom }
        });
        using var bitmap = new WriteableBitmap(
            new(80, 40), new(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        image.Source = bitmap;
        Dispatcher.UIThread.RunJobs();

        zoom.Zoom = 4;
        Dispatcher.UIThread.RunJobs();

        Assert.True(zoom.Extent.Width > zoom.Viewport.Width);
        Assert.True(zoom.Offset.X > 0);
    }

    [AvaloniaFact]
    public void PointerDrag_WhenZoomed_PansContent()
    {
        var image = new Image { Stretch = Avalonia.Media.Stretch.None };
        var zoom = new ZoomViewer { Child = image, MinZoom = 0.1, MaxZoom = 10 };
        var window = new Window
        {
            Width = 240,
            Height = 200,
            Content = new Border { Width = 200, Height = 160, Child = zoom }
        };
        using var _ = _ui.Show(window);
        using var bitmap = new WriteableBitmap(
            new(80, 40), new(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        image.Source = bitmap;
        zoom.Zoom = 4;
        Dispatcher.UIThread.RunJobs();
        var start = zoom.Offset;

        var origin = zoom.TranslatePoint(new(40, 40), window)!.Value;
        window.MouseMove(origin);
        window.MouseDown(origin, Avalonia.Input.MouseButton.Left);
        window.MouseMove(origin + new Vector(-30, -20));
        window.MouseUp(origin + new Vector(-30, -20), Avalonia.Input.MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(zoom.Offset.X > start.X);
        Assert.True(zoom.Offset.Y >= start.Y);
    }

    [AvaloniaFact]
    public void Stop_WhileSlowVapourSynthFrameInFlight_DoesNotBlockUi()
    {
        SkipIfVapourSynthUnavailable();
        var host = new SynthPlayerHost
        {
            Kind = ScriptKind.VapourSynth,
            AutoPlay = false,
            Threads = 1,
            LimitFps = false
        };
        using var window = _ui.Show(new() { Width = 320, Height = 240, Content = host });
        host.Script = """
            import vapoursynth as vs
            import time
            core = vs.core
            src = core.std.BlankClip(width=64, height=48, length=30, format=vs.RGB24)
            def slow(n):
                time.sleep(2)
                return src
            clip = core.std.FrameEval(src, slow)
            clip.set_output()
            """;
        Dispatcher.UIThread.RunJobs();
        var loaded = host.IsMediaLoaded;
        host.IsPlaying = true;
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(200);
        Dispatcher.UIThread.RunJobs();

        var started = DateTime.UtcNow;
        host.Stop();
        var elapsed = DateTime.UtcNow - started;
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (host.IsMediaLoaded && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }

        Assert.True(loaded);
        Assert.True(elapsed < TimeSpan.FromSeconds(1),
            "Stop blocked the UI thread while a VapourSynth frame was still in flight.");
        Assert.False(host.IsMediaLoaded);
    }

    [AvaloniaFact]
    public void Script_AviSynthPaused_PresentsFirstFrame()
    {
        SkipIfAviSynthUnavailable();
        var host = new SynthPlayerHost { Kind = ScriptKind.AviSynth };
        using var window = _ui.Show(new() { Width = 320, Height = 240, Content = host });

        host.Script = "BlankClip(length=12, width=160, height=120, pixel_type=\"RGB24\")\n";
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(host.VideoSource);
        Assert.Equal(TimeSpan.Zero, host.Position);
        Assert.Equal(new(160, 120), host.VideoSource.PixelSize);
    }

    [AvaloniaTheory]
    [InlineData("YV12")]
    [InlineData("YUV420P10")]
    [InlineData("YUV420PS")]
    public void Script_AviSynthReturnedYuv_PresentsColorFrameRightSideUp(string pixelType)
    {
        SkipIfAviSynthUnavailable();
        var host = new SynthPlayerHost { Kind = ScriptKind.AviSynth, AutoPlay = false };
        using var window = _ui.Show(new() { Width = 320, Height = 240, Content = host });

        host.Script = $"""
            top = BlankClip(length=1, width=32, height=8, pixel_type="{pixelType}", color=$FF0000)
            bot = BlankClip(length=1, width=32, height=8, pixel_type="{pixelType}", color=$0000FF)
            stacked = StackVertical(top, bot)
            return stacked
            """;
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(host.VideoSource);
        Assert.Equal(new(32, 16), host.VideoSource.PixelSize);
        var (top, bottom) = ReadBitmapCorners(host.VideoSource);
        Assert.True(top[2] > 180 && top[2] > top[0] + 60, $"top B={top[0]} G={top[1]} R={top[2]} A={top[3]}");
        Assert.True(bottom[0] > 180 && bottom[0] > bottom[2] + 60,
            $"bottom B={bottom[0]} G={bottom[1]} R={bottom[2]} A={bottom[3]}");
        Assert.Equal(byte.MaxValue, top[3]);
        Assert.Equal(byte.MaxValue, bottom[3]);
    }

    [AvaloniaTheory]
    [InlineData("YUV420P8")]
    [InlineData("YUV420P10")]
    [InlineData("YUV420PS")]
    [InlineData("GRAY32")]
    public void Script_VapourSynthFormat_PresentsFrameRightSideUp(string format)
    {
        SkipIfVapourSynthUnavailable();
        var host = new SynthPlayerHost { Kind = ScriptKind.VapourSynth, AutoPlay = false };
        using var window = _ui.Show(new() { Width = 320, Height = 240, Content = host });
        var gray = format.StartsWith("GRAY", StringComparison.Ordinal);

        host.Script = format == "GRAY32"
            ? """
                import vapoursynth as vs
                core = vs.core
                top = core.std.BlankClip(width=32, height=8, length=1, format=vs.GRAY32, color=[4294967295])
                bot = core.std.BlankClip(width=32, height=8, length=1, format=vs.GRAY32, color=[0])
                clip = core.std.StackVertical([top, bot])
                clip.set_output()
                """
            : "import vapoursynth as vs\n" +
              "core = vs.core\n" +
              "top = core.std.BlankClip(width=32, height=8, length=1, format=vs.RGB24, color=[255, 0, 0])\n" +
              "bot = core.std.BlankClip(width=32, height=8, length=1, format=vs.RGB24, color=[0, 0, 255])\n" +
              "clip = core.std.StackVertical([top, bot])\n" +
              "fmt = vs." + format + "\n" +
              "if clip.format.id != fmt:\n" +
              "    args = {\"format\": fmt}\n" +
              "    if \"" + format + "\".startswith((\"YUV\", \"GRAY\")):\n" +
              "        args[\"matrix_s\"] = \"170m\"\n" +
              "    clip = clip.resize.Bicubic(**args)\n" +
              "clip.set_output()\n";
        Dispatcher.UIThread.RunJobs();

        Assert.False(host.IsErrorVisible, host.ErrorMessage);
        Assert.NotNull(host.VideoSource);
        Assert.Equal(new(32, 16), host.VideoSource.PixelSize);
        var (top, bottom) = ReadBitmapCorners(host.VideoSource);
        Assert.Equal(byte.MaxValue, top[3]);
        Assert.Equal(byte.MaxValue, bottom[3]);
        if (gray)
        {
            Assert.True(top[2] > bottom[2] + 40, $"top R={top[2]} bottom R={bottom[2]}");
            return;
        }
        Assert.True(top[2] > 180 && top[2] > top[0] + 60, $"top B={top[0]} G={top[1]} R={top[2]} A={top[3]}");
        Assert.True(bottom[0] > 180 && bottom[0] > bottom[2] + 60,
            $"bottom B={bottom[0]} G={bottom[1]} R={bottom[2]} A={bottom[3]}");
    }

    [AvaloniaFact]
    public void Position_AviSynthSeekWhilePaused_PresentsRequestedFrame()
    {
        SkipIfAviSynthUnavailable();
        var host = new SynthPlayerHost { Kind = ScriptKind.AviSynth };
        using var window = _ui.Show(new() { Width = 320, Height = 240, Content = host });
        host.Script = "BlankClip(length=12, width=160, height=120, pixel_type=\"RGB24\")\n";
        Dispatcher.UIThread.RunJobs();

        host.Position = TimeSpan.FromSeconds(5);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(host.VideoSource);
        Assert.Equal(TimeSpan.FromSeconds(5), host.Position);
    }

    private static (byte[] top, byte[] bottom) ReadBitmapCorners(WriteableBitmap bitmap)
    {
        var top = new byte[4];
        var bottom = new byte[4];
        using (var framebuffer = bitmap.Lock())
        {
            Marshal.Copy(framebuffer.Address, top, 0, top.Length);
            Marshal.Copy(IntPtr.Add(framebuffer.Address, framebuffer.RowBytes * (bitmap.PixelSize.Height - 1)),
                bottom, 0, bottom.Length);
        }

        return (top, bottom);
    }

    private static void SkipIfAviSynthUnavailable() =>
        Assert.SkipUnless(AvsScript.TryFindLibrary(out _), "AviSynth+ native library was not found.");

    private static void SkipIfVapourSynthUnavailable() =>
        Assert.SkipUnless(VsHelper.TryFindLibrary(out _), "VapourSynth native library was not found.");
}
