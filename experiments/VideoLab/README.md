# VideoLab — isolated composition experiment

An independent .NET 10 proof of concept: declarative JSON + local media → immutable frame timeline → runtime bindings → FFmpeg → MP4 or WebM. This is **not a SoundScript language extension or production feature**. There are no package or production project references, solution entries, shared version properties, or production changes. The local `Directory.Build.props` deliberately stops inheritance from SoundScript. Copy this directory to extract the experiment later; extraction is not part of this milestone.

## Run the proof

Requires .NET 10 SDK and `ffmpeg` / `ffprobe` on PATH, with libx264/AAC and libvpx-vp9/libopus encoders. No NuGet dependencies. From the repository root:

```powershell
cd experiments/VideoLab
dotnet run -- selftest
dotnet run -- inspect examples/demo.json 98
dotnet run -- render examples/demo.json artifacts/custom.mp4 musicGain=0.45 accentX=100
dotnet run -- render examples/demo.json artifacts/custom.webm
```

`selftest` generates two synthetic video assets and a music WAV. It renders six-second snapshots A and B twice in **each** container, compares complete file SHA-256 hashes, decodes all frames, inspects transition and overlay pixels, measures audio gain and verifies both input frequencies are mixed. Additional checks exercise rejection, snapshot isolation, animation, explicit placement and black gaps. Outputs, exact FFmpeg version, source hashes, filter graph and `proof.json` remain under ignored `artifacts/`. The demo needs those generated assets; replace their paths to use your own media. Source paths resolve relative to the script, while output paths resolve relative to the working directory. Quote paths containing spaces.

The CLI returns 0 on success, 1 on script/dependency/render failure, and 2 for usage. Exports atomically replace the named output after FFmpeg succeeds. Missing streams, unknown durations and sources too short for the requested trim are rejected before export. Inputs cannot be the output path. FFmpeg runs through an argument list, never a shell command.

## Script semantics

See [`examples/demo.json`](examples/demo.json) for the complete small declarative script. Unknown JSON members are rejected. All time fields are **integer frames**, with spans `[at, at + frames)`. Supported constant frame rates: 24, 25, 30, 50, 60. Canvas and shape dimensions must be positive even integers; canvas is limited to 4096 in each dimension. Timeline is capped at 216,000 frames.

| Field | Meaning |
| --- | --- |
| `width`, `height`, `fps`, `frames` | Output canvas, rate and total duration |
| `parameters` | Named decimal values with `default`, `min`, `max` |
| `videos[].asset` | Local video file; first video stream |
| `videos[].trim`, `frames` | Source in-point and duration after frame-rate normalization |
| `videos[].at` | Explicit placement; omission sequences after previous clip, minus fade |
| `videos[].fade` | Incoming alpha crossfade in frames, default 0 |
| `audio[]` | Explicit source audio spans; first audio stream; `gain` names a parameter |
| `shapes[]` | Solid rectangle with six-digit hex `color`, lifetime and linear X animation |
| `shapes[].x`, `toX`, `y` | Parameter name for initial X, fixed final X, fixed Y |

Video clips must appear in timeline order. Cuts and gaps are supported; gaps are black. Only the incoming crossfade may overlap the previous clip; it must end exactly when that previous clip ends, and transitions cannot overlap each other. Clips are fit and letterboxed to the canvas, with square pixels and yuv420p output. Source video audio is **explicit**: reference that same asset in `audio[]` to include it, as the demo does. Music and clip audio are resampled to 48 kHz stereo, trimmed at corresponding sample boundaries, multiplied by gain, delayed and mixed without automatic normalization. A fixed limiter prevents overload; silence fills uncovered timeline portions. Multiple audio spans can share an asset or parameter. Gain declarations must stay in [0,4].

Shapes are rendered in declaration order above videos. X interpolates from its bound value at the first frame to `toX` at the final included frame. A one-frame shape stays at its starting X. Pixel coordinates are quantized by FFmpeg; `SceneAt` returns the decimal position before rasterization. The fade opacity is `(frame - at) / fade`, clamped at 1. `SceneAt` returns ordered layers and their source frame/opacity, not precomposited pixels.

## Architecture and API

`Model.cs` owns parsing, validation, immutable structural compilation, parameter state and evaluation. `Ffmpeg.cs` only consumes a bound snapshot and handles lowering, probing and export. `Program.cs` is the CLI host; `Proof.cs` is the executable acceptance suite. JSON is the intentionally small DSL for this first milestone; a custom parser can be added independently if authoring demonstrates a need.

```csharp
var composition = Composition.Compile(source, assetDirectory);
var runtime = composition.CreateRuntime();
var snapshotA = runtime.Bind();
runtime.SetMany(new Dictionary<string, decimal> {
    ["musicGain"] = 0.6m, ["accentX"] = 180m
});
var snapshotB = runtime.Bind();

// ReferenceEquals(snapshotA.Composition, snapshotB.Composition) == true
var scene = snapshotA.SceneAt(98); // Pure random-access evaluation.
await Ffmpeg.Render(snapshotA, "A.mp4");
await Ffmpeg.Render(snapshotB, "B.mp4");
```

`SetMany` validates the entire batch before publishing it under a lock. `Bind` captures an immutable value map: later runtime writes cannot change existing snapshots. Runtime parameters affect gain and initial shape X; structural timing is compiled once. No wall clock, random state, interactive playback engine or expression evaluator is involved. `Ffmpeg.Plan(snapshot, output)` returns the exact argument vector, inputs and graph without launching processes.

## Determinism and boundaries

The guarantee is repeated renders with identical script, bindings and asset bytes on the **same FFmpeg build, encoders and machine environment**. Software codecs, single-threaded decode/filter/encode, explicit formats, fixed timing, stripped metadata and bitexact flags are used. Both MP4 and WebM are tested by complete-file hash, not just perceptual comparison. Different codec builds, CPUs and operating systems are not promised byte-equivalent. Asset paths are captured by the composition, but file contents are read at render time: keep files immutable between renders. The proof records their hashes.

Duration preflight depends on container/stream metadata and is intentionally conservative. This is not an untrusted-media service or a general editor. No HDR/color-management guarantees, rotation/crop controls, speed ramps, subtitles/text/fonts, live rebinding during an export, arbitrary layer stacks or plugin filters. A shape overlay satisfies the first milestone without font dependencies. Streams with unreliable timing, malformed media, or unusual edit lists need further investigation before wider use. There is no production packaging, deployment or automatic extraction.

## Architecture decision

The experiment adds an inspectable timeline, pure scene evaluation and isolated parameter snapshots above FFmpeg, rather than exposing FFmpeg expressions to scripts. The first proof establishes the technical path. Keep it here until review with real input media confirms the timeline semantics, authoring ergonomics, media compatibility and maintenance cost. A later independent repository is a separate decision; do not merge this into the production product.
