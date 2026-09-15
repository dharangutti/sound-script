# Transcription architecture and implementation plan

Authoritative requirements: `SoundScript_Transcription_Master_Prompt.md`.

```text
WAV / FFmpeg desktop media / browser decoded audio / future adapters
  -> mono 16 kHz floating PCM -> IMusicalAnalyzer -> MusicalObservations
  -> MonophonicTranscriber -> MusicalScore (seconds + beats + evidence)
  -> ITranscriptionOutput<T> -> typed AST -> SsPrinter -> real parser
  -> existing Wave / MIDI rendering -> musical comparison
```

## Repository fit

The existing Core AST represents executable notation, not uncertain measurements.
Transcription therefore owns a separate evidence-bearing canonical score. It references
Parser and Wave for output and validation; no transcription algorithm enters those projects.
CLI and Playground are orchestration only. The desktop media adapter uses the Media
project's existing FFmpeg process runner, executable selection, cancellation and timeout.
The browser adapter uses Web Audio and supplies identical normalized PCM to the core.

The canonical model carries tempo points, nullable meter/key, sections, tracks/roles,
notes, rests, optional chords/text/expression and provenance. Unknown properties stay
unknown. Output adapters must reject unsupported score features instead of dropping them.
Transcription does not enable expressive performance automatically.

## Milestones

1. Models, adapter contracts, native PCM decoding, typed AST and source output.
2. Deterministic monophonic pitch/onset/offset/tempo/quantization, five original
   generated fixtures, musical metrics and parse/render/reanalysis validation.
3. CLI media decoding/report/preview and independent Playground Transcription tab.
4. Full regression, real compressed-media/browser validation, engineering report.

## Dependencies and algorithm decision

No new NuGet/runtime dependency. A local implementation of the YIN cumulative mean
normalized difference estimator provides frame pitch and periodicity; see
[de Cheveigné and Kawahara (2002)](https://doi.org/10.1121/1.1458024).
This avoids a model download, service, license addition or provider nondeterminism.
Existing optional FFmpeg installation remains external; codecs depend on that build.
Native C# analysis runs on desktop and WASM. Browser decoding depends on installed
browser codecs and is not promised to be byte-identical to FFmpeg decoding.

Dense polyphony, instrument recognition, key/meter inference, source separation,
speech recognition, MusicXML and live input are future work, with extension boundaries
present now. Synthetic vocal and instrument fixtures are not evidence of accuracy on
real singers or acoustic instruments. Quality claims must follow measured results.
