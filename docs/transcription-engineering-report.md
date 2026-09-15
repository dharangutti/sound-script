# SoundScript Transcription — engineering report

Branch: `codex/transcription`. Implementation date: 2026-09-16.
Specification: [master prompt](SoundScript_Transcription_Master_Prompt.md).

Validation environment: Windows, .NET SDK 10.0.303, FFmpeg 9.0.1, and the desktop
app's Chromium-based browser. Other OS/browser codec combinations were not run locally.

## 1. Outcome and verified scope

Implemented an additive, end-to-end monophonic import subsystem: real media →
normalized PCM → observations → canonical score → typed AST → actual SoundScript
source/parser → existing Wave renderer → musical reanalysis/comparison. CLI and a
separate Playground **Transcription** tab expose the workflow. No new NuGet package,
cloud service, model download or codec binary was introduced.

The five original generated fixtures achieve 100% exact-pitch recall, no missed or
extra notes, 7.5–10 ms mean onset error and 5–10 ms mean duration error. This supports
the synthetic monophonic acceptance path; it is not evidence of excellent accuracy
on arbitrary real singers, acoustic recordings or commercial songs.

**Known measured failures:** the rubato fixture's automatic tempo differs from its
nominal reference by 25 BPM. A simple two-source mixture has 0% melody pitch recall
and four extra notes. Renderer release tails reduce short-rest agreement in the
round trip. These are reported, not hidden behind overall confidence percentages.

## 2. Architecture and files

```text
PcmWaveInput / DesktopMediaInput / Web Audio browser input
               ↓ AnalysisAudio (mono 16 kHz float PCM)
       IMusicalAnalyzer / MonophonicAnalyzer
               ↓ MusicalObservations (pitch frames + diagnostics)
          MonophonicTranscriber.Interpret
               ↓ MusicalScore (observed seconds + editable beats)
       SoundScriptOutput / TimelineOutput / AnalysisJsonOutput
               ↓ AST → SsPrinter → actual Parser
        existing Wave renderer / existing MIDI workflow
               ↓ rendered audio → reanalysis → MusicalComparison
```

New project `src/SoundScript.Transcription`, registered in the existing solution,
contains Models, PcmWaveInput, DesktopMediaInput, MonophonicAnalyzer,
MonophonicTranscriber, Outputs and MusicalComparison. Tests follow the repository's
existing `src/SoundScript.Tests` layout: TranscriptionModelTests,
TranscriptionAnalysisTests, TranscriptionIntegrationTests, TranscriptionFixtures,
and `Golden/transcription`. A dependency-free Node test covers the browser bridge.

Integration files: CLI command definition/dispatch and
`CommandHandlers.Transcription.cs`; Playground tabs and
`Pages/TranscriptionPanel.razor`, `wwwroot/js/transcription-input.js`, scoped CSS and
script inclusion. Existing SsPrinter gains rest/instrument emission. Media exposes
its existing FFmpeg runner to the decoder, preserving timeout/cancellation/pipe
handling and executable configuration.

## 3. Canonical model and adapter decisions

Core's existing AST is executable notation and cannot express uncertain acoustic
evidence. The new score therefore preserves both observation seconds and notation
beats. It includes tempo points, nullable meter/key, sections, role-bearing tracks,
notes, explicit rests, optional chords, legitimate text and expression observations.
Evidence includes a numerical measure and its interpretation. Unknown key, meter,
instrument identity, lyrics and chords stay unknown. Playback instrument is a user
choice, not a recognition result. Performance interpretation stays in the existing
engine; transcription does not insert `perform expressive`.

`ITranscriptionInputAdapter<T>` normalizes media; `IMusicalAnalyzer` permits later
replacement of pitch/source analysis; `ITranscriptionOutput<T>` consumes canonical
music. Source generation constructs typed AST and uses SsPrinter, then verifies
the real parser. The writer rejects unsupported score features such as tempo maps,
chords or expression rather than silently dropping them. Future symbolic importers
can directly construct scores. No input-format-specific output converters exist.

## 4. Algorithms, dependencies and formats

YIN CMND with interpolated period, 40 ms windows and 10 ms hops; adaptive RMS silence
threshold; pitch/energy onset segmentation; median pitch smoothing; 60 ms minimum
stable note; integer onset-grid tempo search; conservative sixteenth-grid snapping
and preserved off-grid timing. Frequency uses A4=440 equal temperament. Details,
thresholds and formulas are in [usage and metric definitions](transcription.md).

No added packages or licenses. The local implementation follows the algorithm in
[de Cheveigné and Kawahara, 2002](https://doi.org/10.1121/1.1458024).
Native WAV/resampling is managed C# on desktop/WASM. The existing optional FFmpeg
executable handles desktop compressed media, with platform/codec availability
determined by the user's build. Existing Media/Wave/Parser dependencies are reused;
Playground already referenced them. Browser Web Audio is codec-dependent and may
decode differently from FFmpeg. No cross-decoder bit-identity claim is made.

Implemented input paths: PCM/float WAV; FFmpeg MP3/MP4/M4A/WebM/Ogg/FLAC/AAC/MOV;
browser PCM WAV or browser-supported encoded media. Real WAV, MP3 and an MP4 with
both video and AAC audio were exercised. Additional FFmpeg formats share the
adapter but were not separately measured. Audio-less MP4 failure is tested.

Implemented outputs: AST, editable `.ss`, canonical note timeline, JSON analysis,
confidence/diagnostic report and WAV reconstruction. MIDI works through the existing
CLI `run` command on emitted source and was exercised. A new direct MIDI writer and
MusicXML are not implemented. Limits: 64 MiB input, 120 s, roughly 65–1,000 Hz pitch.

## 5. Product workflow

```powershell
dotnet run --project src/SoundScript.Cli -- transcribe input.wav --out melody.ss
dotnet run --project src/SoundScript.Cli -- transcribe input.mp3 --out melody.ss --report analysis.json --preview preview.wav
dotnet run --project src/SoundScript.Cli -- transcribe input.mp4 --out melody.ss --tempo 100 --instrument flute
dotnet run --project src/SoundScript.Cli -- run melody.ss --out melody.mid
```

CLI validates distinct output paths, reports note count/tempo/periodicity/grid fit,
warns about ambiguity and performs round-trip validation for every successful import.
Source, preview and report use existing individually atomic writes. Cancellation
propagates through decoding/analysis; rendering uses the existing synchronous renderer.

The new tab has independent upload, tempo/instrument controls, source editor,
analysis/cancel, play/stop and SoundScript/WAV/report exports. Browser UI automation
verified WAV, MP3 and MP4 imports, six-note results, 100% reconstruction pitch recall,
editing and replay, downloaded files, and cancellation preserving source. Analysis
yields every 20 frames. Decode/resample/render can still block briefly. Browser
codec failures display a WAV/CLI fallback, preserving editor text. Existing tabs and
startup are unchanged. A rebuild during development required refreshing the server's
asset state; a fresh development origin loaded correctly.

## 6. Validation corpus and source accuracy

All five fixtures are original generated material dedicated to CC0. Source WAV,
generated source, rendered preview, generator and complete ground truth are committed.
The JSON includes every note's MIDI pitch, start/duration seconds and nominal beats.
Synthetic vocal/bowed/piano timbres test harmonics, vibrato, attack and decay; they
are not recordings of actual humans or acoustic instruments.

| Fixture | Notes / MIDI pitches | Ground truth timing | Estimated/reference BPM | Onset MAE | Duration MAE | Rest IoU |
|---|---|---|---:|---:|---:|---:|
| clean-sine | 6: 60,64,67,62,65,69 | 0.5 s spacing; 0.4 s notes | 120/120 | 10 ms | 10 ms | 89.29% |
| vocal-style | 5: 57,60,62,64,62 | 0.6 s spacing; 0.5 s notes | 100/100 | 10 ms | 10 ms | 88.89% |
| sustained-bowed | 4: 67,69,72,71 | starts 0,.625,1.875,2.5; durations .58,1.2,.58,1.2 s | 96/96 | 7.5 ms | 5 ms | 76.47% |
| keyboard-repeated | 6: 60,60,64,64,67,60 | 0.5 s spacing; 0.32 s notes | 120/120 | 10 ms | 10 ms | 93.75% |
| rests-rubato | 5: 62,65,69,67,64 | starts .25,.75,1.7,2.45,3.1; durations .3,.65,.45,.35,.6 s | 95/120 nominal | 10 ms | 10 ms | 96.43% |

All five: exact-pitch recall 100%; missed=0; extra=0; melodic contour 100%.
The repeated fixture matches both adjacent repeated-note pairs (2/2).
Full numbers: [measurements.json](../src/SoundScript.Tests/Golden/transcription/measurements.json).
The rubato fixture intentionally cannot be accurately described by one tempo.

## 7. Round-trip measurements

Generated AST → SsPrinter → parser → existing Wave → PCM decoder → YIN reanalysis.
Reference here is the canonical score's beat schedule, not its original measured seconds.

| Fixture | Pitch recall | Missed / extra | Onset MAE | Duration MAE | Rest IoU |
|---|---:|---:|---:|---:|---:|
| clean-sine | 100% | 0 / 0 | 10 ms | 85 ms | 19.23% |
| vocal-style | 100% | 0 / 0 | 10 ms | 90.0002 ms | 0% |
| sustained-bowed | 100% | 0 / 0 | 10 ms | 42.5 ms | 0% |
| keyboard-repeated | 100% | 0 / 0 | 10 ms | 90 ms | 37.74% |
| rests-rubato | 100% | 0 / 0 | 8.74 ms | 91.05 ms | 65.63% |

All contours remain correct. Existing renderer release envelopes extend audible
notes and fill some short gaps. This is an objectively measured limitation of the
reconstruction, not exact timing preservation. Changing established renderer
semantics to improve these metrics was intentionally avoided. No waveform-matching
optimization or iterative refinement is implemented.

## 8. Confidence and determinism

Per-note pitch evidence is mean YIN periodicity; aggregate pitch confidence averages
those note means. Timing evidence measures boundary proximity to a sixteenth grid.
Neither is a calibrated correctness probability. Silence, short input, clipping,
weak periodicity, fixed-tempo ambiguity and the monophonic assumption have distinct
diagnostics. No instrument/key/overall confidence percentage is fabricated.

Identical inputs/options produce identical serialized analysis and source in tests
for every fixture. Synchronous and cooperative browser analysis produce identical
observations. Analysis uses no random state or external model. Fixture noise uses
explicit seeds. Existing deterministic Wave/MIDI regression tests pass unchanged.
Hardware/runtime floating-point differences and external codec differences remain
outside a cross-platform transcription byte-identity guarantee.

## 9. Tests and compatibility

Full suite: **1,069 passed, 0 failed, 0 skipped**, including 38 added .NET cases.
Browser bridge: **4 passed** with Node's built-in test runner; no npm dependency.
The final trimmed Release Playground publish succeeded; its browser startup,
WAV analysis and JSON report download were also exercised successfully.
Coverage includes pitch/frequency mapping, onsets/offsets, rests, repeated and legato
notes, quantization, tempo, deterministic analysis, malformed/unsupported/silent/
short/noisy/clipped/oversized input, MP3/MP4 extraction, no-audio video failure, AST/
source parsing/rendering, comparison/confidence, cancellation, CLI and committed media.
Real FFmpeg tests explicitly skip on machines without FFmpeg; this run executed them.

Milestone checks: first five model tests passed; after analysis, all 1,049 tests passed;
after product integration, expanded 1,068 tests passed; final adapter-boundary checks
bring the final total to 1,069. The initial 11 baseline
failures came from the uninitialized existing Wordbank submodule. Initializing its
pinned `10a5b2a8d49fbc38ba99a4f322016e7772b797d5` revision fixed them without changing
tests, embedded data or submodule revision. Three existing xUnit style warnings remain.

No established parser, performance, renderer or deterministic output baseline was
changed. SsPrinter's added AST cases are additive. Existing FFmpeg operations use
the same runner; the missing-executable message is now applicable to decoding too.
No deployment or remote push was performed.

## 10. Capability status and next steps

**Implemented and measured:** synthetic monophonic WAV transcription, MP3/MP4
decoding, canonical score, AST/source/timeline/JSON, CLI, dedicated browser workflow,
render/reanalysis metrics, generated corpus and deterministic regression.

**Experimental / limited:** tempo inference on rubato or ambiguous rhythm; real
humming, vocals, violin/flute/piano recordings; noisy attacks and changing timbre;
browser codec coverage; exact audible offsets/rest reconstruction. These require a
licensed real-recording corpus and wider measurements before stronger quality claims.

**Architecturally supported for later:** source-separation/ML analyzers, multi-voice
scores, symbolic MIDI import, live PCM/microphone input, MusicXML and other writers,
tempo maps, key/meter/chord/expression/legitimate-text evidence.

**Not implemented:** reliable polyphony, source separation, instrument recognition,
key/meter inference, lyrics recognition, microphone capture, automatic tempo-change
recovery, direct MIDI writer, MusicXML and iterative refinement.

The two-source experiment mixes the original six-note melody with a 196 Hz drone:
10 detected notes, 0% melody pitch recall, four extras, 28.3 ms matched onset MAE.
This is evidence against shipping a polyphonic mode now. Next steps: licensed real
solo recordings; octave/onset error evaluation across wider pitch and SNR ranges;
beat-tracking/tempo-map refinement; renderer-aware comparison separating gate time
from release tails; worker-based browser analysis; then measured multi-pitch/source
separation behind the analyzer boundary.
