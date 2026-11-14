using System.Runtime.InteropServices;
using Xunit;

namespace HanumanInstitute.AviSynthApi.Tests.Integration;

[Collection("AviSynthNative")]
public class AvsAudioIntegrationTests
{
    [Theory]
    [InlineData("ConvertAudioTo8bit", AvsSampleType.Int8, 1, 128)]
    [InlineData("ConvertAudioTo16bit", AvsSampleType.Int16, 2, 0)]
    [InlineData("ConvertAudioTo24bit", AvsSampleType.Int24, 3, 0)]
    [InlineData("ConvertAudioTo32bit", AvsSampleType.Int32, 4, 0)]
    [InlineData("ConvertAudioToFloat", AvsSampleType.Float, 4, 0)]
    public void ReadAudio_AudioOnlyClip_ReturnsInterleavedSamples(string convert, AvsSampleType sampleType, int bytes, byte silence)
    {
        var source = $"Tone(length=0.01, samplerate=48000, channels=2, type=\"Silence\").{convert}()";
        using var script = AvsScript.LoadScript(source);
        var info = script.SourceVideoInfo;

        var samples = script.ReadAudio(0, 8);

        Assert.False(info.HasVideo);
        Assert.True(info.HasAudio);
        Assert.Equal(48000, info.SampleRate);
        Assert.Equal(2, info.Channels);
        Assert.Equal(480, info.AudioSampleCount);
        Assert.Equal(sampleType, info.SampleType);
        Assert.Equal(bytes, info.BytesPerChannelSample);
        Assert.Equal(8 * 2 * bytes, samples.Length);
        Assert.All(samples, value => Assert.Equal(silence, value));
    }

    [Fact]
    public void ReadAudio_StereoTone_PreservesChannelOrder()
    {
        const string source = """
            a = Tone(length=0.01, samplerate=48000, channels=1, type="Square", level=0.25)
            b = Tone(length=0.01, samplerate=48000, channels=1, type="Square", level=0.5)
            MergeChannels(a, b)
            """;
        using var script = AvsScript.LoadScript(source);

        var samples = MemoryMarshal.Cast<byte, float>(script.ReadAudio(1, 1)).ToArray();

        Assert.Equal(2, samples.Length);
        Assert.Equal(0.25f, samples[0]);
        Assert.Equal(0.5f, samples[1]);
    }

    [Fact]
    public void ReadAudio_InsufficientBuffer_ThrowsArgumentOutOfRangeException()
    {
        using var script = AvsScript.LoadScript("Tone(length=0.01, channels=2)");
        var buffer = new byte[7];

        var act = () => { script.ReadAudio(0, 1, buffer); };

        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void ReadAudio_RangePastEnd_ThrowsArgumentOutOfRangeException()
    {
        using var script = AvsScript.LoadScript("Tone(length=0.01, samplerate=48000)");

        var act = () => script.ReadAudio(479, 2);

        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void ReadAudio_EndOfClip_ReturnsEmptyBuffer()
    {
        using var script = AvsScript.LoadScript("Tone(length=0.01, samplerate=48000)");

        var samples = script.ReadAudio(480, 0);

        Assert.Empty(samples);
    }

    [Fact]
    public void GetFrame_AudioOnlyClip_ThrowsAvsException()
    {
        using var script = AvsScript.LoadScript("Tone(length=0.01)");

        var act = () => script.GetFrame(0);

        Assert.Throws<AvsException>(act);
    }

    [Fact]
    public void GetPlane_DisposedScript_RemainsReadable()
    {
        using var script = AvsScript.LoadScript("BlankClip(length=1, width=16, height=16, pixel_type=\"RGB32\", color=$FF0000)");
        using var frame = script.GetFrame(0);
        script.Dispose();

        var red = Marshal.ReadByte(frame.GetPlane(0).Pointer, 2);

        Assert.Equal(255, red);
    }

    [Fact]
    public void GetFrame_DisposedScriptWithRetainedFrame_ThrowsObjectDisposedException()
    {
        using var script = AvsScript.LoadScript("BlankClip(length=1, width=16, height=16)");
        using var frame = script.GetFrame(0);
        script.Dispose();

        var act = () => script.GetFrame(0);

        Assert.Throws<ObjectDisposedException>(act);
    }
}
