# VideoLab v0.2 — isolated programmable composition

VideoLab owns composition semantics; FFmpeg is a lowering/rendering backend. The experiment is a dependency-free .NET 10 executable under this directory, with no production references, solution entries, packaging changes or shared version imports. It is not a SoundScript language extension. The frozen `labs-videolab-poc-v0.1.0` tag remains unchanged.

`JSON → immutable Composition → runtime bindings → immutable Snapshot → SceneAt(frame) → FFmpeg → MP4/WebM`

## Run

The [browser explorer](web/README.md) presents real rendered MVP snapshots and exact
timeline inspection. It is a static proof interface; custom media composition remains
in this CLI. Regenerate its versioned distribution with `dotnet run -c Release -- webproof`.

Requires .NET 10 and FFmpeg/ffprobe on PATH, including libx264/AAC and libvpx-vp9/libopus. Validation was performed with FFmpeg 9.0.1. Rendering requires the `-/filter_complex` option-file syntax; older builds lacking it are unsupported. The expanded synthetic suite also uses libx265 for an SDR HEVC compatibility fixture.

```powershell
cd experiments/VideoLab
dotnet build
dotnet run -- selftest
dotnet run -- inspect examples/transforms.json 15
dotnet run -- plan examples/transforms.json artifacts/test.mp4
dotnet run -- render examples/transforms.json artifacts/test.mp4 startX=60
dotnet run -- render examples/transforms.json artifacts/test.webm
dotnet run -- batch examples/expressions.json examples/batch.json
dotnet run -- realtest real-media/manifest.json
```

`selftest` generates synthetic assets, runs all original 95 checks plus v0.2 checks, and writes ignored artifacts. It never reads the optional real-media manifest. `realtest` returns a successful skip if the manifest is missing. Exit codes: 0 success/optional skip, 1 validation/dependency/render failure, 2 CLI usage. Asset paths are relative to the script; ordinary output paths are relative to the working directory; batch destinations are relative to the batch file. Numeric values use invariant culture.

## Supported authoring

| Example | Purpose |
| --- | --- |
| [demo.json](examples/demo.json) | Unchanged original two clips, crossfade, animated shape, music and clip audio |
| [transforms.json](examples/transforms.json) | Position, size, scale, rotation, opacity, crop, pivot and animated gain |
| [expressions.json](examples/expressions.json) | Frame/progress-driven motion and runtime parameter |
| [effects.json](examples/effects.json) | Reusable zoom, fade, slide and pulse |
| [conditional.json](examples/conditional.json) | Parameter and progress conditions |
| [data-sequence.json](examples/data-sequence.json) | Structured data generates ordered video spans |
| [batch.json](examples/batch.json) | Multiple parameter bindings over the same compiled expression composition |

Properties accept numbers, expression strings or integer-frame keyframes. Expressions compile into a typed immutable AST with arithmetic `+ - * /`, unary minus, parentheses, numeric comparisons, and `min`, `max`, `clamp`, `abs`. Built-ins are local `frame`, local `progress`, `fps`, `canvasWidth`, `canvasHeight`. Animations support linear, quadratic ease-in/out/in-out and step. Conditions must return boolean. No host calls or raw FFmpeg expressions are accepted.

Clips/shapes support x/y, width/height, scale/scaleX/scaleY, degrees of rotation, opacity, normalized crop and anchor. Audio gain uses the same expression/animation model. Effects are immutable transform templates with validated arguments; nested effects are forbidden. Bounded data records generate sequences. See [SEMANTICS.md](SEMANTICS.md) for exact author-facing meaning and coordinate spaces, and [ARCHITECTURE.md](ARCHITECTURE.md) for implementation and centralized resource limits.

## API and inspection

```csharp
var composition = Composition.Compile(source, assetDirectory);
var runtime = composition.CreateRuntime();
var snapshotA = runtime.Bind();
runtime.SetMany(new Dictionary<string, decimal> { ["startX"] = 60 });
var snapshotB = runtime.Bind();
var scene = snapshotA.SceneAt(15);
var plan = Ffmpeg.Plan(snapshotB, "B.mp4"); // No process or media I/O.
await Ffmpeg.Render(snapshotA, "A.mp4");
await Ffmpeg.Render(snapshotB, "B.mp4");
```

`SetMany` validates the entire candidate state before publishing; failure changes nothing. `Bind()` freezes parameters while sharing composition structure. `SceneAt` is pure random access: ordered layers expose source, normalized source frame, inclusion, z-order, transform/crop and evaluated values; audio exposes source position, inclusion and gain. Legacy `Clips`/`Shapes` projections remain. `Plan` returns complete graph/arguments, inputs and expected output settings.

`Batches.Bind` shares structure and isolates each binding. Output destinations are unique and cannot replace inputs; the CLI also protects script/batch files. Replacement is atomic per output, not globally transactional: earlier successful batch outputs remain if a later render fails.

## Source policy and validation

Output FPS remains integer 24/25/30/50/60. Validated CFR source rates include fractional 24000/1001, 30000/1001 and 60000/1001 through explicitly rounded timestamp normalization; these are not fractional output-rate support. `SourceFrame` always means the index after normalization, never the raw encoded frame index. Display-rotation metadata is explicitly ignored; encoded pixels define the source plane. Non-square sample aspect ratio, detected VFR/discontinuities, interlaced and signaled HDR/wide-gamut/high-bit-depth video are rejected. Sources must have trustworthy duration/stream metadata. See the detailed limits of prefix timing validation in [SEMANTICS.md](SEMANTICS.md).

`AssetProbe` identifies missing files/streams, unknown duration, short spans, unsupported timing/geometry/color and malformed metadata with concise codes, paths and requested frame spans. An optional `AssetFingerprint.Read` returns path, byte size and SHA-256. Ordinary rendering does not hash assets. Snapshot immutability applies to the composition and bound parameters, **not external file bytes**.

For local footage, copy [real-media-manifest.example.json](examples/real-media-manifest.example.json) to `real-media/manifest.json` and adjust paths/expectations. The harness renders both formats twice, compares hashes, fully decodes, checks frame count/canvas/48 kHz stereo and confirms source bytes did not change. Rejection expectations are explicit. Local manifests, media, hashes and derived outputs stay under ignored `real-media/`; nothing proprietary is required by selftest. [REAL_MEDIA_VALIDATION.md](REAL_MEDIA_VALIDATION.md) separates actual recordings from generated equivalents.

## Boundaries

The frame-oriented programmable renderer remains a correctness-first **reference backend**, capped at 600 frames and 4096 element-frames, with pixel-work limits. It is a semantic oracle for a future optimized backend, not a long-form editor. The original efficient legacy route remains unchanged in meaning. No optimized backend is implemented.

Repeated output bytes are guaranteed only for identical script, bindings, asset bytes, VideoLab code, FFmpeg/encoders and machine environment. Software codecs, fixed timing/formats, single-threaded work, bitexact flags and stripped metadata are used. No cross-machine/version equality claim is made. See [VALIDATION.md](VALIDATION.md) for measured results.

Unsupported: text/subtitles/font discovery, masks/blur/grading, HDR/wide-gamut/Dolby Vision preservation, general scripting/plugins, arbitrary overlapping video tracks, negative scale, speed ramps, nested effects, audio-envelope analysis, GPU rendering, interactive preview, production packaging or automatic extraction. Parameterized batches provide procedural generation. Inputs are local trusted media: FFmpeg is not sandboxed for hostile uploads. Processes use argument lists, no shell and `-nostdin`; ordinary failure cleans temporary graph/output files before any destination replacement.
