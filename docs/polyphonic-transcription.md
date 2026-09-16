# Polyphonic / Piano (Experimental) — Milestone 2

Implemented on `codex/polyphonic-piano-transcription`, based on `6e1a0c6`.
[Plan](polyphonic-transcription-plan.md), [synthetic measurements](polyphonic-synthetic-measurements.json),
[real piano measurements](polyphonic-real-measurements.json).

## Usage and shared architecture

```powershell
dotnet run --project src/SoundScript.Cli -- transcribe piano.mp3 --mode polyphonic --start 30 --duration 10 --out piano.ss --report piano.json --preview piano.wav
```

CLI and Playground call `TranscriptionEngine` with `Polyphonic`. Existing input
adapters normalize PCM; `PolyphonicAnalyzer` records simultaneous candidates;
`PolyphonicTranscriber` segments each pitch and allocates intervals to parallel
tracks. Existing `SoundScriptOutput` creates the real AST, printer and parser output.
The existing Wave renderer performs playback. No syntax, renderer, monophonic or
melody-extractor thresholds changed. Piano is the new mode's default playback
instrument; users can override it. Excerpts, atomic outputs and rejection exit 1
follow existing CLI behavior.

Playground adds **Polyphonic / Piano (Experimental)**, note/group counts, maximum
simultaneity, polyphony density/coverage, rejected-frame and octave evidence. It
supports selected original-excerpt playback, generated playback, editing, source/
WAV/report exports, cancellation and option-change invalidation. Previous editor
text remains inactive after failure. Only implemented modes are displayed.

## Representation and analyzer

Existing `MusicalNote` observations retain physical seconds and notation beats.
Existing tracks represent independent durations/overlap, with role `polyphonic-voice`.
No `MusicalScore` constructor change was necessary. New optional `Polyphony` evidence
on `TranscriptionResult` contains dedicated multi-pitch frames, raw frequency,
fundamental/residual spectral share, harmonic evidence, rejection reasons and
simultaneous pitch groups. Old results omit this null property when serialized.

Named chord AST nodes expand predetermined voicings and the printer does not
serialize them. Parallel note tracks already preserve arbitrary inversions and
independent offsets, so the implementation uses those. Groups describe observed
pitch sets, not inferred named chords or roots. Interval partitioning preserves
every accepted note; it does not collapse a chord into a melody. Six-decimal beat
rounding avoids sub-microbeat rests that the existing printer would round to zero.

The independent local analyzer uses 4096-sample Hann windows at 16 kHz (256 ms),
20 ms hops, interpolated spectral peaks and joint harmonic accounting. Fundamental
range is C2–C7, at most six candidates per frame. A candidate requires measurable
fundamental energy (1.8% of frame spectral energy). Energy explainable by lower
fundamentals' first six harmonics is conservatively subtracted. Octave candidates
require an upper overtone before tracking can continue with strong residual
fundamental evidence. This is a bounded heuristic, not a learned piano model.

Diffuse spectra (<70% concentrated peak energy), excessive strong peaks, >6
candidates and strong 2:3:5 families without their lower fundamental reject.
Accepted notes last at least 100 ms; rising pitch-energy evidence can split repeated
attacks. Generation requires >=50% stable active coverage and <50% rejected active
frames. All accepted results are Experimental. These gates are independent of the
first two modes. No new model, package, native binary, network service or license
is introduced; normalized-PCM analysis works offline in .NET/WASM.

## Metrics and evidence

Per-note confidence is mean residual fundamental spectral energy / total frame
energy, not probability of a correct piano key. `MeanFundamentalShare` averages
that residual measure. Stable coverage counts active frames covered by retained
notes. Polyphonic coverage counts active frames with >=2 retained notes. Mean
active polyphony is notes per active frame; maximum simultaneity is the peak count.
ChordCount counts contiguous distinct simultaneous pitch sets, including changing
release combinations; it is not the number of written chord attacks.

`OctaveAmbiguousFraction` counts frames where an octave alternative needed scrutiny,
including alternatives retained or suppressed. High values do not demonstrate
correct octave doubling. Frame-level rejections and candidate evidence remain in
the report. Legacy monophonic periodicity fields are not measurements for this mode.

`PolyphonicComparison` matches same-pitch onsets one-to-one within 150 ms, reports
precision/recall, missed/extra notes, separately unmatched octave errors, onset and
duration MAE. Chord-tone precision/recall integrates overlapping pitch sets over
time; grouping agreement measures time with exactly matching simultaneous sets.
The greedy nearest-onset assignment is not an optimal transcription alignment.
No named-root or melody-contour inference is claimed for a general polyphonic set.
Round trips use polyphonic reanalysis of the generated schedule, not monophonic
recall. They are self-consistency measurements, not independent source truth.

## Synthetic and real results

Thirteen authored CC0 fixtures cover dyads, major/minor triads, sustained/repeated
chords (including no gap), arpeggios, octave doubling, block/broken accompaniment,
overlap, solo notes and harmonic false positives. All have note precision/recall
1.0, no missed/extra notes or octave errors on this corpus. Onset MAE ranges 0–40 ms,
duration MAE 0–83.3 ms; simultaneous grouping agreement ranges 82.7–100% where
defined. Ground truth and per-fixture metrics are committed. These timbres are
simple synthetic signals, not acoustic piano validation. Dense chords, dense
ensemble, missing-fundamental, noise, drum-noise and silence fixtures reject.

Round-trip limitations are visible: octave fixture recall is 50%, repeated-legato
recall 88.9%, and melody/block-chord precision/recall 83.3%; other fixtures reach
100% note precision/recall. Rendered timbre/harmonics and release tails differ from
the source; rendering semantics were not changed to improve these measurements.

Thirty local runs compare all three modes on 10-second excerpts at 0,30,60,90,110 s
from both original piano recordings. Monophonic rejects all ten. Extract Melody
rejects nine; emotional piano at 90 s emits 23 notes at 55.3% stable coverage.
Polyphonic emits 36–116 candidates with 5–6 maximum simultaneous notes on all ten,
with stable coverage 76.3–96.7%. Its rendered-note precision is 87.2–98.6% and recall
66.7–91.6%; these are not source accuracy. Octave ambiguity is especially high on
the alanajordan recording (83.0–94.6%), so harmonics/over-segmentation remain a
material risk. More output is not proof of better resemblance.

Original recordings, excerpts and derived source/previews remain outside Git.
`artifacts/polyphonic-review/index.html` provides side-by-side original, Extract
Melody (when accepted) and Polyphonic playback, with identical piano playback
instrument for the generated modes. It can export human listening verdicts.
**Human listening: deferred by the user ("I will check later").** No improved/partially improved/not-recognizable
verdict has been supplied. Recognizable improvement and absence of spurious
real-piano notes are therefore not established. Implementation is suitable for a
focused draft PR; the listening acceptance gate remains open.

## Validation and limitations

27 added .NET cases; full suite **1,130 passed, 0 failed, 0 skipped**. Twenty Node
tests pass. Release publish/integrity and startup normal/retry/corruption checks
pass; the existing melody browser workflow passes. Old CLI reports/source for eight
representative input/mode pairs are byte-identical to pre-Milestone-2 snapshots.
Existing golden monophonic and rendering/parser regressions also pass.

Polyphonic browser verification covers source/score parity, actual simultaneity,
original-excerpt playback, editing/replay, downloads, rejection, invalidation,
cancellation and 390 px layout. Source, timing, pitches and tracks match exactly;
FFT evidence differences between WASM/desktop are bounded to 1e-12 (observed around
1e-16). Same-runtime sync/async/repeated runs serialize identically.

Not supported: full 88-key coverage, dense arrangements, inferred pedal, reliable
missing fundamentals, instrument identity, exact dynamics, tempo maps, or guaranteed
harmonic/octave separation. Pure sine octave doubling can be indistinguishable from
a harmonic timbre. Weak notes and transient details can disappear; spectral windows
smear timing. Generic ensembles and pitched percussion can sometimes look harmonic;
this is not an instrument classifier. Human review is mandatory for real output.

```powershell
dotnet test SoundScript.sln
node --test scripts/*.test.cjs
dotnet publish src/SoundScript.Playground -c Release
# Set SOUNDSCRIPT_PLAYGROUND_PUBLISH_DIR if using a nondefault publish directory.
node scripts/verify-playground-startup.cjs artifacts/playground
node scripts/verify-transcription-playground.cjs artifacts/playground
node scripts/verify-polyphonic-playground.cjs artifacts/playground
./scripts/measure-polyphonic-acceptance.ps1 -RecordingsDirectory C:/Users/dhara/Downloads -OutputDirectory artifacts/new-piano-review
```

Set `SOUNDSCRIPT_POLYPHONIC_ARTIFACTS` to an artifact directory during the focused
tests to regenerate original synthetic WAVs, previews, sources and full metrics.
The real-input script requires an empty output directory and local FFmpeg; it
does not fetch or commit recordings.
