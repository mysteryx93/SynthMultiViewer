using Xunit;

namespace HanumanInstitute.VapourSynthApi.Tests.Integration;

[Collection("VapourSynthNative")]
public class VsAudioIntegrationTests
{
    [Theory]
    [InlineData(16, VsSampleType.Integer, 2)]
    [InlineData(24, VsSampleType.Integer, 4)]
    [InlineData(32, VsSampleType.Integer, 4)]
    [InlineData(32, VsSampleType.Float, 4)]
    public void GetChannel_AudioOutput_ReturnsPlanarSamplesAndShortFinalFrame(int bits, VsSampleType sampleType, int bytes)
    {
        var source = $"import vapoursynth as vs\nvs.core.std.BlankAudio(bits={bits}, sampletype={(int)sampleType}, samplerate=48000, length=3075).set_output()";
        using var script = VsScript.LoadScript(source);
        using var output = script.GetOutput();
        var info = output.AudioInfo;

        using var frame = output.GetFrame(1);
        var left = frame.GetChannel(0).ToArray();
        var right = frame.GetChannel(1).ToArray();

        Assert.Equal(VsMediaType.Audio, output.MediaType);
        Assert.Null(script.SourceVideoInfo);
        Assert.Equal(48000, script.SourceAudioInfo!.Value.SampleRate);
        Assert.Equal(3075, info.NumSamples);
        Assert.Equal(2, info.NumFrames);
        Assert.Equal(3UL, info.Format.ChannelLayout);
        Assert.Equal(2, info.Format.NumChannels);
        Assert.Equal(bits, frame.AudioFormat.BitsPerSample);
        Assert.Equal(sampleType, frame.AudioFormat.SampleType);
        Assert.Equal(3, frame.AudioSampleCount);
        Assert.Equal(3 * bytes, left.Length);
        Assert.Equal(left, right);
        Assert.All(left, value => Assert.Equal(0, value));
    }

    [Fact]
    public void VideoInfo_AudioOutput_ThrowsVsException()
    {
        using var script = VsScript.LoadScript("import vapoursynth as vs\nvs.core.std.BlankAudio(length=8).set_output()");
        using var output = script.GetOutput();

        var act = () => output.VideoInfo;

        Assert.Throws<VsException>(act);
    }

    [Fact]
    public void GetChannel_VideoFrame_ThrowsVsException()
    {
        using var script = VsScript.LoadScript("import vapoursynth as vs\nvs.core.std.BlankClip(width=16, height=16, length=1, format=vs.RGB24).set_output()");
        using var output = script.GetOutput();
        using var frame = output.GetFrame(0);

        var act = () => { frame.GetChannel(0); };

        Assert.Throws<VsException>(act);
    }

    [Fact]
    public void GetOutput_NonzeroAudioIndex_LoadsWithoutOutputZero()
    {
        const string source = "import vapoursynth as vs\nvs.core.std.BlankAudio(length=8).set_output(3)";
        using var script = VsScript.LoadScript(source, convertForDisplay: false);

        using var output = script.GetOutput(3);

        Assert.Equal(VsMediaType.Audio, output.MediaType);
        Assert.Equal(8, output.AudioInfo.NumSamples);
    }

    [Fact]
    public void Evaluate_EmptyEnvironment_ExposesEvaluatedOutput()
    {
        using var script = VsScript.CreateEmpty();
        const string source = "import vapoursynth as vs\nvs.core.std.BlankAudio(length=8).set_output(2)";

        script.Evaluate(source);
        using var output = script.GetOutput(2);

        Assert.Equal(8, output.AudioInfo.NumSamples);
    }

    [Fact]
    public void Evaluate_ReplacedVideoOutput_RefreshesAudioSourceMetadata()
    {
        using var script = VsScript.LoadScript("import vapoursynth as vs\nvs.core.std.BlankClip(width=16, height=16, length=1, format=vs.RGB24).set_output()");
        const string replacement = "vs.core.std.BlankAudio(length=8).set_output()";

        script.Evaluate(replacement);

        Assert.Null(script.SourceVideoInfo);
        Assert.Equal(8, script.SourceAudioInfo!.Value.NumSamples);
    }
}
