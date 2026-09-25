# SoundScript NuGet end to end

<!-- GENERATED:LIBRARY_INSTALL_START -->
Install the published library:

```bash
dotnet add package SoundScript --version 16.0.0
```

[SoundScript 16.0.0 on NuGet](https://www.nuget.org/packages/SoundScript/16.0.0).
<!-- GENERATED:LIBRARY_INSTALL_END -->

The applications below pin SoundScript 13.0.2 as a compatibility baseline; use the
current installation above for new applications. They reference the package directly and do not
reference component projects or invoke the SoundScript CLI. Their source is the
authority for every C# excerpt in this guide, the tutorials, and the
[developer article](articles/programmable-media-dotnet.md).

## Application data becomes media

[IndustrialMonitoring](../samples/IndustrialMonitoring) accepts a typed scenario
from JSON and uses one [builder](../samples/IndustrialMonitoring/MonitoringScenario.cs)
to produce three programs:

| Source | Consumer path | Output |
|---|---|---|
| `.ss` | `SoundScriptEngine.Compile`, `RenderMidi`, `RenderWave`, `RenderStereoWave` | MIDI, mono and stereo WAV |
| `.ssw` | same compiler, `RenderWave` with a delay effect | alert WAV |
| `.ssv` | parser, `VisualInterpreter`, `StateAt(t)`, scene builder, `TemporalAudioRenderer` | timeline/scene JSON and synchronized WAV; optional WebM |

These extensions share a grammar. `.ssw` denotes Wave-oriented authoring; it
does not select a second parser. The visual rail extends the same program model.
The samples keep the three generated source files so you can inspect exactly
what changed.

```sh
dotnet run --project samples/IndustrialMonitoring -c Release
dotnet run --project samples/MediaRoundTrip -c Release
```

Run from the repository root. Outputs go under ignored `artifacts/samples/`.
No hardware or industrial infrastructure is contacted. This is monitoring
sonification and media generation, not a certified alarm or protection system.

| Scenario | Temperature / vibration / load | Tempo | Pitch | Notes in four beats | Dynamic / instrument | Delay mix | Status cue |
|---|---|---:|---|---:|---|---:|---|
| Healthy | 68 / 0.14 / 52 | 90 | C5 | 2 | mp / flute | 0.085 | green, 100px, fades in over 3s |
| Warning | 86 / 0.48 / 78 | 116 | G5 | 4 | mf / piano | 0.170 | amber, 180px, fades in over 2s |
| Critical | 96 / 0.82 / 94 | 139 | C6 | 8 | ff / violin | 0.255 | red, 260px, fades in over 1s |

Load and status determine tempo, temperature selects a pitch band, vibration
sets delay mix, and status selects density, dynamics, instrument and presentation.
These are demonstration mappings, not calibrated industrial thresholds.
The generated programs include `perform expressive`: Wave honors the selected
instrument timbres in this mode. Without it, changing a MIDI instrument alone
does not demonstrate a different Wave timbre.
The [monitoring tutorial](tutorials/industrial-monitoring.md) asks you to edit just
four values and regenerate every output.

## The temporal model is available to applications

This excerpt from [Program.cs](../samples/IndustrialMonitoring/Program.cs) is the
core of the visual integration:

```csharp
var program = new Parser(new Tokenizer(sources.Visual).Tokenize()).Parse();
var timeline = VisualInterpreter.Interpret(program);
var states = new[] { 0.0, 2.0, 3.99 }.Select(t => timeline.StateAt(TimeSpan.FromSeconds(t))).ToArray();
var scenes = states.Select(TemporalVisualSceneBuilder.Build).ToArray();
```

`VisualTimeline` is independent of frame rate. A `TemporalVisualScene` is a
projection of evaluated state. The sample keeps states at zero, midpoint and
just before the end; intervals exclude their end instant. FPS is introduced
only when `TemporalVideoExportPlanBuilder` samples the timeline for export.

`TemporalAudioRenderer.RenderToWavBytes(program, timeline.Duration)` renders
the Wave audio rail and fits it to the four-second visual duration by padding
silence or truncating. Under the existing media profile, unavailable external
samples are skipped and paths use the working directory. This sample uses no
external audio assets, avoiding that policy difference from `CompileFile`.

`TemporalVideoFrameRenderer.WritePpmFrames` produces frames, then
`FfmpegWebmExporter.EncodeAndVerify` encodes VP9/Opus and decodes both streams
before replacing the destination. These are public APIs in the package.
FFmpeg must provide `libvpx-vp9`, `libopus` and the WebM muxer. The sample runs
`EnsureCapabilities` before export, accepts an executable as its third argument
or through `SOUNDSCRIPT_FFMPEG`, and reports a dependency-specific skip when
unavailable. MIDI, WAV, timeline and scene outputs still work. Other export
errors fail the application rather than being mislabeled as a missing dependency.

## Media becomes editable code

[MediaRoundTrip](../samples/MediaRoundTrip) generates a three-second original
sine-wave fixture, decodes it with `PcmWaveInput`, then calls the cancellable
`TranscriptionEngine.TranscribeAsync`. Native PCM WAV requires no FFmpeg.
The returned `TranscriptionResult` retains score, observations, confidence,
diagnostics and suitability in `Output/report.json`.

The sample checks `Suitability.CanGenerate` before calling
`TranscriptionSuitability.ScoreForGeneration`. It saves the transcribed source,
then edits the typed musical model (excerpt from its verified
[Program.cs](../samples/MediaRoundTrip/Program.cs)):

```csharp
var modified = score with { Tracks = score.Tracks.Select(track => track with { Instrument = 0 }).ToArray() };
string source = "perform expressive\n" + writer.Source(modified);
```

The original reconstruction uses flute (73); the modified score uses piano (0).
Both sources explicitly enable expressive performance for Wave timbre selection.
Both MIDI program changes and synthesized WAV change. The writer reparses its
source; compilation then generates `reconstructed.mid` and `reconstructed.wav`.
The [round-trip tutorial](tutorials/media-round-trip.md) covers your own PCM WAV.

This is bounded offline analysis: finite mono 16 kHz analysis audio, at most
120 seconds. Short, clean solo melodies are the best starting point. The
synthetic fixture demonstrates integration, not accuracy on recordings in the
wild. Polyphonic, melody-extraction, mixed-role and percussion modes are
experimental. Reanalyzing a reconstruction is not independent ground truth.
`DesktopMediaInput` supports desktop media decoding/excerpts through FFmpeg;
the sample intentionally uses native WAV to remain dependency-free.

## Verified CLI / NuGet parity

“Facade” means `SoundScriptEngine` / `SoundScriptCompilation`, not any public
class with an engine name. **A** = facade, **B** = bundled public components,
**C** = external dependency, **D** = CLI-only orchestration/report. None of the
audited media engines is missing from the package; some combined CLI reports
and policies have no packaged equivalent. The public API inventory and
[validation report](nuget-end-to-end-validation.md) distinguish package
verification from execution coverage.

| CLI capability | CLI | Facade | Bundled public path | External dependency / remaining CLI work | Class |
|---|---|---|---|---|---|
| `run`, `.ss → MIDI` | Yes | `RenderMidi` for pitched music | `ProgramLoader`, `Interpreter`, `VocalInterpreter.Apply`, `MidiGenerator` | CLI also applies vocal interpretation and consolidated diagnostics; facade is not complete vocal parity | A/B/D |
| `wave`, `.ss/.ssw → WAV` | Yes | `RenderWave` | `WaveRenderer`, `WaveRenderOptions` | Offline TTS selection/stem orchestration is application work; engine-dependent external tools | A/B/C |
| `--stereo` | Yes | `RenderStereoWave` | `WaveRenderer.RenderStereoToBytes` | None for built-in synthesis | A/B |
| `compose` | Yes | No | `PhonemeComposer.BuildAst`, `ComposeProgram` | None for built-in composition | B |
| `prosody` | Yes | No | `ProsodyComposer.BuildAst`, `ComposeProgram` | None for built-in composition | B |
| `--emit-ss` | Yes | No | `SsPrinter.Print` returns source | Application writes source; printer supports its defined AST subset | B |
| `--append` | Yes | No | both composers' `AppendTo(InterpretedProgram, text)` | App owns base score and output; append changes interpreted MIDI, not the original AST | B/D |
| compose/prosody `--wave` | Yes | source can be compiled | `WaveRenderer` renders composed AST | CLI currently renders/emits original AST even with append; do not claim combined append/Wave/source parity | B/D |
| `render`, MIDI + SoundCSS → WAV/OGG | Yes | No | `SoundCSSParser`, `OfflineRenderer.Render`, `RenderFile`, `RenderToWavBytes` | OGG encoder is a package dependency, no FFmpeg required | B |
| `validate` | Yes | parsing and selected render errors only | `ProgramLoader`, renderer/adaptation/interpreter validation, exceptions and loader warnings | `SourceAnalysis` recursion checks, aggregated diagnostics and target policy are CLI-only | B/D |
| `inspect` | Yes | loader warnings only | interpreted tracks/notes/tempo, Wave adaptation, visual timeline | combined `ProgramMetadata`, supported-output classification and audio-tail estimates are CLI-only | B/D |
| `visual`, `StateAt(t)` | Yes | No | `VisualInterpreter`, `VisualTimeline.StateAt` | No encoder needed | B |
| `video`, WebM | Yes | No | temporal plan/scene/frame/audio builders, `FfmpegWebmExporter.EncodeAndVerify` | FFmpeg; app manages frames and temp-directory lifetime | B/C |
| FFmpeg preflight / `video --check` | Yes | No | `EnsureAvailable`, `EnsureCapabilities` | FFmpeg; full `VideoPreflightResult` is CLI-only | B/C/D |
| `transcribe` | Yes | No | `PcmWaveInput` / `DesktopMediaInput`, `TranscriptionEngine`, typed options/result | FFmpeg for desktop compressed-media path; native PCM does not need it | B/C |
| transcription output/report/preview | Yes | No | `SoundScriptOutput`, `AnalysisJsonOutput`, suitability and comparison APIs, renderers | CLI completion manifest and combined report envelope are not packaged | B/D |
| `vocal generate` | Yes | No | `VocalEngineFactory`, `IVocalEngine.Synthesize`, `VocalEngineOptions` | Built-in prosody needs none; wordbank needs corpus; eSpeak engine needs eSpeak NG | B/C |
| `vocal batch` | Yes | No | `VocalBatchExporter.ExportFromProgram` / `ExportFromScript` | Same engine dependencies; structured stem paths/times returned | B/C |
| `wordbank ensure` | Yes | No | `WordbankAutoGenerate.EnsureLemma` | Loaded writable corpus; eSpeak NG for missing-audio generation | B/C |
| `wordbank normalize` | Yes | No | `WordbankNormalizer.Normalize`, `CorpusCatalog.GetLemmaKeys` | Existing corpus recordings; CLI `--all` is a loop with counters | B/D |

No sample shells out to SoundScript, parses console output, or exposes
`CliArguments`. FFmpeg is the intentional external codec boundary.

## Advanced API examples and ergonomics

The package-only [validation program](../tools/NuGetExamplesValidation/Program.cs)
also runs compose/prosody → source/MIDI/stereo WAV, append, SoundCSS → WAV/OGG,
built-in vocal generation and vocal batch. For example, these are actual lines
from that program (a shortened excerpt):

```csharp
var composed = PhonemeComposer.BuildAst("machine ready");
var prosody = ProsodyComposer.BuildAst("machine ready");
var engine = VocalEngineFactory.Create("prosody");
engine.Synthesize("machine ready", Path.Combine(probe, "vocal.wav"), new() { Seed = 7 });
```

Approximate core API call counts, excluding input/output handling:

| Workflow | Calls / friction |
|---|---|
| source → MIDI/mono/stereo WAV | 2: compile, render |
| `.ssw` → WAV | 2: same facade |
| source → timeline | tokenize, parse, interpret; public AST needed for advanced path |
| `.ssv` → WebM | ~8 stages plus temp files; most boilerplate, but each stage serves preview/export/cancellation |
| compose/prosody → source | 2: build AST, print |
| MIDI + SoundCSS → WAV | 1: `RenderToWavBytes`; this convenience uses temporary files internally |
| WAV → editable source | decode, transcribe, check suitability, select score, write source |
| vocal generate | factory + synthesis; file output, engine options |

Existing records/options already cover transcription, visual plans, Wave and
vocals. The examples add only sample-local scenario/source models. No new
package API, general artifact framework, output enum or stream overload is
needed. Byte arrays can go straight to HTTP responses or storage; advanced
`MidiGenerator.Write` and `WavWriter.WriteTo` already support streams. WebM
appropriately uses filesystem output. `SoundScriptCompilation` keeps its AST
private; advanced consumers explicitly use the existing public parser.

Cancellation is passed to transcription, frame generation and FFmpeg encoding.
Compilation, Wave synthesis, temporal plan construction and FFmpeg capability
preflight are synchronous without a cancellation argument. Vocal options carry
a token but support is engine-dependent; built-in prosody synthesis does not
check it. Corpus selection includes process-wide state, so service hosts should
configure it deliberately, not switch locales concurrently per request.
There is no consolidated packaged `validate`/`inspect` result. That is an
ergonomics gap, not missing media generation, and extracting CLI policy into
libraries would be a separate API design change.

## Reproduce validation

```powershell
pwsh -NoProfile -File tools/Validate-NuGetExamples.ps1
dotnet test src/SoundScript.Tests/SoundScript.Tests.csproj -c Release
```

The script copies the consumers outside the repository, restores only from
nuget.org into a fresh package cache, builds Release, runs each scenario twice,
and compares hashes and semantic properties. It also tests missing FFmpeg.
`hashes.json` and `public-api.json` remain under ignored artifacts; the isolated
consumer workspace under the user's local application data is retained for inspection. WebM bytes are excluded from
hash guarantees because container/codec builds can vary; exported clips must
pass the existing audio-and-video decode verification. No package publishing
is part of this workflow. The sample projects retain their pinned compatibility baseline; see their project files.
