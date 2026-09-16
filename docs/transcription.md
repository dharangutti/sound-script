# SoundScript Transcription

Import a solo melody from media, obtain editable SoundScript, and measure its
reconstruction through the existing renderer. This is an additive, local monophonic
subsystem; it does not reconstruct arbitrary mixed songs.

An opt-in [experimental melody extraction mode](melody-extraction.md) attempts a
single dominant line from more complex audio. Use `--mode extract-melody` or select
the mode in Playground. Monophonic remains the default; all extraction results are
experimental, and most real piano excerpts tested still reject.

[Polyphonic / Piano (Experimental)](polyphonic-transcription.md) adds simultaneous
notes through `--mode polyphonic` and a third Playground mode. It uses parallel
SoundScript tracks, with separate polyphony/ambiguity diagnostics and comparisons.

## CLI

```powershell
dotnet run --project src/SoundScript.Cli -- transcribe melody.wav --out melody.ss
dotnet run --project src/SoundScript.Cli -- transcribe melody.mp3 --output melody.ss --report analysis.json --preview reconstructed.wav
dotnet run --project src/SoundScript.Cli -- transcribe clip.mp4 --out score.ss --tempo 100 --instrument flute
dotnet run --project src/SoundScript.Cli -- run melody.ss --out melody.mid
dotnet run --project src/SoundScript.Cli -- wave melody.ss --out edited.wav
```

`--out`, `--output`, and `-o` follow the existing CLI convention. `--tempo auto`
is the default; an integer 20–300 supplies a known tempo. `--instrument` chooses
the reconstruction instrument (GM name or 0–127); it does not identify the source.
FFmpeg selection is `--ffmpeg`, then `SOUNDSCRIPT_FFMPEG`, then PATH, as for video
export. Each successful import parses, renders and reanalyzes the generated source,
even when no preview/report file is requested. Silent/unpitched inputs fail without
writing an empty score. Output paths must differ from the input and each other.
Files are individually staged and atomically replaced; the set is not a transaction.

## Playground

Open the **Transcription** tab, select a file, optionally enter tempo and choose a
playback instrument, then **Analyze / Transcribe**. The dedicated editor contains
real SoundScript. **Play edited source** reparses and renders current editor text.
Use Stop, Export SoundScript, Export WAV or Export analysis. Import diagnostics
remain associated with the original import after editing. Analysis yields periodically
and supports cancellation. Decode/resample/render phases can still occupy the UI
thread; start with short clips. Music & Wave and Audio/Visual retain their editors.

No upload to a service occurs. Native PCM WAV uses the same C# decoder as the CLI;
other browser formats use Web Audio `decodeAudioData` and its installed codecs.
MP3/video availability differs by browser; unsupported containers display a clear
message directing users to PCM WAV or the FFmpeg CLI path.

## Inputs and limits

* Native WAV: RIFF integer PCM 8/16/24/32 bit or IEEE float32, 1–8 channels,
  8–192 kHz; normalized to mono float32 at 16 kHz with windowed-sinc resampling.
* Desktop compressed media: MP3, MP4, M4A, WebM, Ogg, FLAC, AAC, MOV and additional
  WAV encodings through an existing FFmpeg installation. The first audio stream
  is selected. Video without audio fails. No media binary or codec is bundled.
* Limits: 64 MiB file, 120 seconds analysis, approximately 65–1,000 Hz fundamental
  range (C2–B5), stable notes at least 60 ms. No live streaming.
* Mono downmix can cancel out-of-phase stereo or combine unrelated sources.
* PCM input is deterministic for identical samples/options on the same runtime;
  browser/FFmpeg decoding and floating arithmetic across hardware are not promised
  to produce bit-identical observations. Source emission from a fixed score is stable.

## Algorithms and confidence

YIN cumulative mean normalized difference uses a 40 ms window, 10 ms hop, 20 ms
integration length, first local minimum below 0.15 and parabolic lag interpolation.
See [the original YIN paper](https://doi.org/10.1121/1.1458024). Frequency maps to
the nearest equal-tempered MIDI pitch with A4=440 Hz. A three-frame median reduces
isolated pitch glitches. Silence uses a floor of RMS 0.003 or 3.5% of maximum
frame RMS. Pitch transitions and energy attacks segment notes; an energy rise
above 1.8 times the previous hop after 80 ms can mark a repeated note.

Automatic tempo searches integer 60–180 BPM for onset intervals fitting a sixteenth
grid, with a small whole-beat and 120 BPM prior. Fewer than three notes use a labeled
120 BPM fallback. This is a hypothesis, with unavoidable half/double-time ambiguity;
meter, key and tempo changes are not inferred. Boundaries within 0.065 beats of a
quarter-beat grid are snapped; others preserve six-decimal beat timing. Original
measured seconds are always retained. Velocity is a bounded square-root RMS mapping
for playback, not inferred MIDI velocity or an acoustic loudness calibration.

* **Pitch periodicity**: each note's arithmetic mean of `1 - CMND(selected lag)`;
  report value is the mean across notes. It is periodicity, not probability that
  the entire score is correct. Harmonic mixtures can have high periodicity.
* **Timing grid fit**: mean `max(0, 1 - abs(4*b - round(4*b))/0.5)` over measured
  note boundaries `b` in beats. Articulation may lower fit even at the right tempo.
  This is also the automatic tempo evidence field; it is not tempo certainty.
* **Clipping**: more than 0.1% samples have absolute value >=0.999.
* **Weak periodicity**: fewer than 80% of above-threshold frames have a YIN pitch;
  noise, weak fundamentals and mixtures are possible causes, not diagnoses.
* Instrument/key/meter/overall correctness percentages are deliberately absent:
  there is no defensible estimator for those properties in this release.

## Musical comparison

Reference notes are matched one-to-one to the nearest unused detected onset within
150 ms. Pitch does not influence matching. Metrics are:

* Pitch accuracy/recall: exact MIDI matches / reference count; missed notes penalize it.
* Missed/extra: unmatched reference/detected notes; matched wrong pitches score zero pitch credit.
* Onset/duration MAE: mean absolute seconds error over matched notes; null if none.
* Repeated notes: adjacent same-pitch reference pairs for which both notes were
  matched with correct pitch (use with onset error to judge repeated attack quality).
* Contour: fraction of adjacent matched pairs with the same pitch-change sign;
  null when no consecutive pairs can be compared.
* Rest IoU: silence intersection duration / silence union duration across all note
  boundaries through the last note ending; trailing file padding is excluded.
  Null when neither timeline contains silence in that domain.
* Tempo error: absolute BPM difference when both reference and estimate exist.

Round-trip validation constructs source from AST, reparses through the actual parser,
renders using Wave, then independently analyzes the rendered audio. Its reference is
the canonical score's beat schedule. CLI reports also compare source observations
with rendered observations; source observations are a proxy, not ground truth.
For the original fixture corpus, independently specified notes are the ground truth.
Renderer release envelopes explain the measured additional 40–91 ms duration error.
No optimization uses waveform similarity; no iterative refinement is implemented.

## Extending the subsystem

`ITranscriptionInputAdapter<T>` isolates decoding into `AnalysisAudio`.
`IMusicalAnalyzer` replaces signal analysis without coupling callers to a provider.
`MonophonicTranscriber.Interpret` maps observations to `MusicalScore`.
`ITranscriptionOutput<T>` consumes that score; the current AST writer, source printer,
timeline and analysis JSON keep output separate from decoding. Existing MIDI/Wave
paths consume emitted AST/source. Future symbolic inputs can construct a score
directly and use the same outputs without pretending to be audio observations.

The canonical score allows tempo maps, nullable meter/key, sections, roles, notes,
rests, chords, expression and legitimate text. Unsupported structured features are
rejected by the first source writer rather than silently discarded. Implement a
score importer for MIDI, a live PCM input adapter for microphones, an extended
analyzer for separated sources, or a new writer for MusicXML as independent additions.

## Validation

Initialize the existing Wordbank test dependency with
`git submodule update --init --recursive`, then run `dotnet test SoundScript.sln`.
Run browser bridge checks with `node --test scripts/transcription-browser-input.test.cjs`.
Real MP3/MP4 tests require FFmpeg; they explicitly skip if it cannot be found.
See [engineering report](transcription-engineering-report.md) and
[fixture ground truth and measurements](../src/SoundScript.Tests/Golden/transcription/measurements.json).

[Mixed audio / musical roles](mixed-audio-transcription.md) supports selected melody, harmony and bass estimates without claiming audio stem separation.
