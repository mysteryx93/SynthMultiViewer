# AviSynthApi for .NET

Evaluate AviSynth+ scripts from .NET 10 and read video frames, interleaved audio, frame properties, and the installed function list.

Install AviSynth+ for the same architecture as your process. The library must be on the system search path: `avisynth.dll` on Windows, `libavisynth.so` on Linux, or `libavisynth.dylib` on macOS. Frame properties require a current AviSynth+. AviSynth+ and its plugins are not included in this package.

```sh
dotnet add package AviSynthApi
```

## Video

```csharp
using HanumanInstitute.AviSynthApi;

using var script = AvsScript.LoadScript(
    """BlankClip(length=10, width=320, height=240, pixel_type="YV12")""",
    convertForDisplay: false);
using var frame = script.GetFrame(0);
var luma = frame.GetPlane(0);
Console.WriteLine($"{script.VideoInfo.FormatName}: {luma.RowSize} bytes per row");
```

`LoadFile` and `LoadScript` prepare a packed 8-bit RGB32 clip for preview. Pass `convertForDisplay: false` when you want the original pixels. `SourceVideoInfo` describes that original clip. `GetFrame(index, convertForDisplay: false)` reads it even after a preview clip was prepared. `GetSourceFrameProperties` reads properties from the original clip.

A clip whose first frame is tagged BT.2020 constant luminance (matrix 10) cannot be preview-converted. Load it with `convertForDisplay: false`.

`GetPlane` takes AviSynth plane ids. `0` is the default plane, Y/U/V are `1`/`2`/`4`, alpha is `16`, and planar R/G/B are `32`/`64`/`128`. Packed RGB is stored bottom-up. Use the pointer only while the frame is alive.

## Audio

```csharp
using System.Runtime.InteropServices;
using HanumanInstitute.AviSynthApi;

using var script = AvsScript.LoadScript(
    "Tone(length=1.0, samplerate=48000, channels=2)");
var info = script.SourceVideoInfo;
byte[] pcm = script.ReadAudio(start: 0, count: 1024);
ReadOnlySpan<float> samples = MemoryMarshal.Cast<byte, float>(pcm);
Console.WriteLine($"{info.SampleRate} Hz, {info.Channels} channels, {samples.Length} values");
```

`count` is samples per channel. The bytes are native-endian interleaved PCM in the clip's channel order. `ReadAudio(start, count, destination)` fills a buffer you supply. The range must fall inside the clip. A clip with no audio throws `AvsException`.

Check `SampleType` before you interpret the bytes. `AvsSampleType` is unsigned 8-bit (silence is 128), signed 16-bit, packed signed 24-bit, signed 32-bit, or 32-bit float. `Tone` produces float. `HasVideo` and `HasAudio` tell the streams apart. An audio-only clip is returned as source audio, with no preview conversion.

## Installed functions

`AvsCatalog.Read()` returns the functions available from a fresh AviSynth environment.

`TryFindLibrary`, `TryEvaluate`, and `TryReadVersion` tell you whether the library loads, whether a script runs, and which version is installed.

`LoadFile` and `LoadScript` reject an `Import` cycle written as a literal path, ignoring comments and strings. Computed or conditional imports are left to AviSynth. Pass `checkImportCycles: false` to skip the check.

## Lifetime

Dispose each script and frame when you are done with it. A frame you already hold stays valid after the script is disposed. Further calls on that script throw `ObjectDisposedException`.

Script and frame errors throw `AvsException`. A frame or sample index outside the clip throws `ArgumentOutOfRangeException`. Loading a script can change the process working directory. The script runs inside your process.

On Windows, non-ASCII paths work for `Import` and for the script's working directory.

## License

MIT

## Author
[Etienne Charland](https://www.hanumaninstitute.com) — Coherence Programming | Reality Engineering | Soul Architect