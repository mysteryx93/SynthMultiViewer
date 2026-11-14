using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Xunit;

namespace HanumanInstitute.VapourSynthApi.Tests.Integration;

[Collection("VapourSynthNative")]
public class VsOutputIntegrationTests
{
    [Fact]
    public void GetProperties_EmbeddedNul_PreservesData()
    {
        using var script = VsScript.LoadScript("import vapoursynth as vs\nvs.core.std.BlankClip(width=16, height=16, length=1, format=vs.RGB24).std.SetFrameProps(sample=b'a\\x00b').set_output()", convertForDisplay: false);
        using var output = script.GetOutput();
        using var frame = output.GetFrame(0);

        var value = frame.GetProperties().Single(p => p.Name == "sample").Value;

        Assert.Equal("a\0b", value);
    }

    private const string Video = """
        import vapoursynth as vs
        vs.core.std.BlankClip(width=16, height=16, length=2, format=vs.RGB24, color=[10, 20, 30]).set_output()
        """;

    [Fact]
    public void GetPlane_DisposedParents_RemainsReadable()
    {
        using var script = VsScript.LoadScript(Video, convertForDisplay: false);
        using var output = script.GetOutput();
        using var frame = output.GetFrame(0);
        output.Dispose();
        script.Dispose();

        var value = Marshal.ReadByte(frame.GetPlane(0).Ptr);

        Assert.Equal(10, value);
    }

    [Fact]
    public void GetFrame_DisposedOutput_ThrowsObjectDisposedException()
    {
        using var script = VsScript.LoadScript(Video, convertForDisplay: false);
        using var output = script.GetOutput();
        using var frame = output.GetFrame(0);
        output.Dispose();

        var act = () => output.GetFrame(0);

        Assert.Throws<ObjectDisposedException>(act);
    }

    [Fact]
    public void GetPlane_DisposedFrame_ThrowsObjectDisposedException()
    {
        using var script = VsScript.LoadScript(Video, convertForDisplay: false);
        using var output = script.GetOutput();
        using var frame = output.GetFrame(0);
        var plane = frame.GetPlane(0);
        frame.Dispose();

        var act = () => { _ = plane.Ptr; };

        Assert.Throws<ObjectDisposedException>(act);
    }

    [Fact]
    public void Copy_ModifiedPixels_PreservesSourceFrame()
    {
        using var script = VsScript.LoadScript(Video, convertForDisplay: false);
        using var output = script.GetOutput();
        using var source = output.GetFrame(0);
        using var copy = source.Copy();

        Marshal.WriteByte(copy.GetPlane(0).GetWritePtr(), 77);

        Assert.Equal(77, Marshal.ReadByte(copy.GetPlane(0).Ptr));
        Assert.Equal(10, Marshal.ReadByte(source.GetPlane(0).Ptr));
    }

    [Fact]
    public void GetWritePtr_SourceFrame_ThrowsInvalidOperationException()
    {
        using var script = VsScript.LoadScript(Video, convertForDisplay: false);
        using var output = script.GetOutput();
        using var frame = output.GetFrame(0);

        var act = () => { frame.GetPlane(0).GetWritePtr(); };

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public async Task GetFrameAsync_FailedRequest_DeliversErrorAndContinues()
    {
        const string source = """
            import vapoursynth as vs
            c = vs.core.std.BlankClip(width=16, height=16, length=2, format=vs.RGB24, color=[10, 20, 30])
            def select(n, f):
                if n == 0:
                    raise vs.Error("frame failure")
                return f
            c.std.ModifyFrame(c, select).set_output()
            """;
        using var script = VsScript.LoadScript(source, convertForDisplay: false);
        await using var output = script.GetOutput();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var statuses = new ConcurrentQueue<VsFrameStatus>();
        byte pixel = 0;
        output.FrameReady += OnReady;
        void OnReady(object? sender, VsFrameStatus status)
        {
            statuses.Enqueue(status);
            if (status.Frame != null) { pixel = Marshal.ReadByte(status.Frame.GetPlane(0).Ptr); }
            if (statuses.Count == 2) { completion.SetResult(); }
        }

        for (var index = 0; index < 2; index++) { output.GetFrameAsync(index); }
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Collection(statuses,
            failed => { Assert.Equal(VsFrameState.Failed, failed.State); Assert.Null(failed.Frame); Assert.Contains("frame failure", failed.Error); },
            succeeded => { Assert.Equal(1, succeeded.Index); Assert.Equal(VsFrameState.Completed, succeeded.State); Assert.Null(succeeded.Error); });
        Assert.Equal(10, pixel);
    }

    [Fact]
    public async Task GetFrameAsync_DisposeInsideHandler_CompletesAndKeepsCurrentFrameAlive()
    {
        using var script = VsScript.LoadScript(Video, convertForDisplay: false);
        await using var output = script.GetOutput();
        var completion = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
        output.FrameReady += OnReady;
        void OnReady(object? sender, VsFrameStatus status)
        {
            output.Dispose();
            script.Dispose();
            completion.SetResult(Marshal.ReadByte(status.Frame!.GetPlane(0).Ptr));
        }

        output.GetFrameAsync(0);
        var value = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(10, value);
    }

    [Fact]
    public async Task GetFrameAsync_ThrowingSubscriber_ReportsFailureAndInvokesNextSubscriber()
    {
        using var script = VsScript.LoadScript(Video, convertForDisplay: false);
        await using var output = script.GetOutput();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? failure = null;
        output.CallbackFailed += (_, error) => failure = error;
        output.FrameReady += (_, _) => throw new InvalidOperationException("subscriber failure");
        output.FrameReady += (_, _) => completion.SetResult();

        output.GetFrameAsync(0);
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal("subscriber failure", failure.Message);
    }

    [Fact]
    public async Task ClearQueue_InsideFrameDone_CancelsReadyDelivery()
    {
        using var script = VsScript.LoadScript(Video, convertForDisplay: false);
        await using var output = script.GetOutput();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyCount = 0;
        output.FrameDone += (_, _) => output.ClearQueue(() => completion.SetResult());
        output.FrameReady += (_, _) => readyCount++;

        output.GetFrameAsync(0);
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(0, readyCount);
    }

    [Fact]
    public void Format_VariableFormatClip_ReturnsActualFrameFormat()
    {
        const string source = """
            import vapoursynth as vs
            a = vs.core.std.BlankClip(width=16, height=16, length=1, format=vs.GRAY8)
            b = vs.core.std.BlankClip(width=16, height=16, length=1, format=vs.GRAY16)
            vs.core.std.Splice([a, b], mismatch=True).set_output()
            """;
        using var script = VsScript.LoadScript(source, convertForDisplay: false);
        using var output = script.GetOutput();

        using var frame = output.GetFrame(1);

        Assert.Equal(16, frame.Format.BitsPerSample);
        Assert.Equal(VsColorFamily.Gray, frame.Format.ColorFamily);
        Assert.False(output.VideoInfo.IsConstantFormat);
    }
    [Fact]
    public async Task ClearQueue_ThrottledFrame_CountsCancelledDelivery()
    {
        using var script = VsScript.LoadScript(Video, convertForDisplay: false);
        await using var output = script.GetOutput();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.MaxFps = 0.01;
        output.FrameReady += (_, e) => { if (e.Index == 0) { ready.SetResult(); } };
        output.FrameDone += (_, e) => { if (e.Index == 1) { done.SetResult(); } };
        output.GetFrameAsync(0);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        output.GetFrameAsync(1);
        await done.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var cleared = output.ClearQueue();

        Assert.Equal(1, cleared);
    }

    [Fact]
    public void LoadScript_ConstantLuminance_PreservesColorMatrix()
    {
        const string source = """
            import vapoursynth as vs
            c = vs.core.std.BlankClip(width=16, height=16, length=1, format=vs.RGB24, color=[220, 20, 60])
            c = c.std.SetFrameProps(_Transfer=14, _Primaries=9)
            c = c.resize.Bicubic(format=vs.YUV444P10, matrix_s="2020cl")
            c.set_output(0)
            c.resize.Bicubic(format=vs.RGB24, matrix_in_s="2020cl").set_output(1)
            """;
        using var script = VsScript.LoadScript(source);
        using var preview = script.GetOutput(0);
        using var reference = script.GetOutput(1);
        using var actual = preview.GetFrame(0);
        using var expected = reference.GetFrame(0);

        var pixel = Enumerable.Range(0, 3).Select(i => Marshal.ReadByte(actual.GetPlane(i).Ptr)).ToArray();

        Assert.Equal(Enumerable.Range(0, 3).Select(i => Marshal.ReadByte(expected.GetPlane(i).Ptr)), pixel);
    }
}
