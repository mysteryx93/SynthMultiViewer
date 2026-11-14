# VapourSynthApi for .NET

Evaluate VapourSynth 4 scripts from .NET 10 and read video frames, planar audio, ordered frame events, frame properties, and the installed plugin list.

Install VapourSynth and a matching Python runtime for your process architecture. This package uses core API 4.0 and VSScript API 4.1. It loads the file in `VSSCRIPT_PATH` when that file exists. Otherwise it loads `VSScript.dll` on Windows, `libvapoursynth-script.so` on Linux, or `libvapoursynth-script.dylib` on macOS. VapourSynth, Python, and plugins are not included in this package.

```sh
dotnet add package VapourSynthApi
```

## Video

```csharp
using HanumanInstitute.VapourSynthApi;

using var script = VsScript.LoadScript("""
    import vapoursynth as vs
    vs.core.std.BlankClip(width=320, height=240, length=10, format=vs.YUV420P10).set_output(1)
    """, convertForDisplay: false);
using var output = script.GetOutput(1);
using var frame = output.GetFrame(0);
Console.WriteLine($"{frame.Format.Name}: {frame.GetPlane(0).Width} pixels");
```

`LoadFile` and `LoadScript` turn video output 0 into planar 8-bit RGB24. Audio output 0 stays as authored. Pass `convertForDisplay: false` to keep the original pixels. That also accepts a script that sets no output 0. `TryGetOutput` tells you whether an output index exists.

`SourceVideoInfo` and `SourceAudioInfo` describe the original output 0 when it exists. On any output, read `VideoInfo` or `AudioInfo` according to `MediaType`. Asking for the other one throws `VsException`. `frame.Format` is the format of that frame, which can change through a variable-format clip. Plane 0 is the first plane. Use plane pointers only while the frame is alive.

`CreateEmpty().Evaluate(text, path)` runs script text with no preview conversion. If that call fails, the script is closed. `LoadFileDirect` runs a file the same way and does not fill `SourceVideoInfo` or `SourceAudioInfo`. After another `Evaluate`, call `GetOutput` again. Outputs you already hold still refer to the previous graph.

## Audio

```csharp
using System.Runtime.InteropServices;
using HanumanInstitute.VapourSynthApi;

using var script = VsScript.LoadScript("""
    import vapoursynth as vs
    vs.core.std.BlankAudio(bits=32, sampletype=vs.FLOAT, samplerate=48000, length=48000).set_output()
    """);
using var output = script.GetOutput();
var info = output.AudioInfo;
using var frame = output.GetFrame(0);
ReadOnlySpan<float> left = MemoryMarshal.Cast<byte, float>(frame.GetChannel(0));
Console.WriteLine($"{info.SampleRate} Hz: {left.Length} samples in this block");
```

Each audio frame index is one block of up to 3072 samples per channel. `AudioInfo.NumSamples` is the whole stream. `frame.AudioSampleCount` is the block you hold, and the last block can be shorter. Channels are separate buffers, in the order of the set bits in `AudioFormat.ChannelLayout`, lowest bit first.

`GetChannel` borrows the channel's native-endian sample bytes from the frame. Call `ToArray()` when you need them after the frame is disposed. Check `SampleType`, `BitsPerSample`, and `BytesPerSample` before you interpret the bytes. A signed 24-bit sample occupies four bytes.

## Frame events

```csharp
using var script = VsScript.LoadFile("example.vpy");
await using var output = script.GetOutput();
output.CallbackFailed += (_, error) => Console.Error.WriteLine(error);
output.FrameReady += (_, status) =>
{
    if (status.Error != null)
    {
        Console.Error.WriteLine(status.Error);
        return;
    }

    using var kept = status.Frame!.Copy();
    Console.WriteLine(kept.GetProperties().Count);
};
output.GetFrameAsync(0);
```

`GetFrameAsync` fetches the frame on a background thread. `FrameDone` signals that the request finished. `FrameReady` raises the results in the order you requested them. A failed result has `State == Failed`, no frame, and an `Error`. The frame on the event is released when your handlers return, so call `Copy()` to keep it. `GetPlane(n).GetWritePtr()` writes into a copy. A frame you received from `GetFrame` or from the event is read-only.

Handlers run on background threads. Marshal any UI work yourself. An exception in a handler is raised through `CallbackFailed`, and the remaining handlers still run. `MaxFps` limits how fast `FrameReady` is raised. `ClearQueue()` cancels frames that have not been delivered yet and returns immediately. Pass a callback to run it once the current queue has drained.

`Dispose()` cancels delivery and returns at once, including from inside a handler. `DisposeAsync()` also waits until requests and handlers have finished. Do not wait on it from inside a handler. You can dispose the script while outputs and frames you already hold are still in use. Using a disposed script, output, or frame throws `ObjectDisposedException`.

## Installed plugins

`VsCatalog.Read()` returns the plugins and functions from your VapourSynth installation.

`VsHelper.TryFindLibrary`, `VsScript.TryEvaluate`, and `VsScript.TryReadVersion` tell you whether the library loads, whether a script runs, and which core release is installed.

Script and frame errors throw `VsException`. An index outside the output throws `ArgumentOutOfRangeException`. Loading a script can change the process working directory. The script runs inside your process.

## License

MIT

## Author
[Etienne Charland](https://www.hanumaninstitute.com) — Coherence Programming | Reality Engineering | Soul Architect