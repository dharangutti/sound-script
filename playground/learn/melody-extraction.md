# Experimental dominant melody extraction

Milestone 1 implementation, 2026-09-16. See the [phased plan](melody-extraction-plan.md).
Default transcription remains V13 monophonic. This page records the extraction
milestone; V13 now also exposes experimental polyphonic, mixed-role and percussion
modes (see [transcription](transcription.md)). Mixed roles do not provide isolated stems.

```powershell
dotnet run --project src/SoundScript.Cli -- transcribe input.mp3 --mode extract-melody --start 30 --duration 10 --out melody.ss --report analysis.json --preview melody.wav
```

`--mode monophonic` is equivalent to omitting the flag. Unknown modes are usage
errors. Extraction rejection uses the existing input-error exit code 1, writes the
requested diagnostic report, and does not write source or preview. Existing files
at those output paths are not deleted; the CLI explicitly warns about earlier runs.

In Playground, select **Extract Melody from Complex Audio (Experimental)**. Review
coverage, ambiguity and rejected intervals before playing/exporting. The original
recording has its own full-file player; its time is absolute within the file.
Generated playback starts at the selected excerpt. Report/rejected-section times
are relative to that excerpt. Changing mode, tempo, instrument or excerpt
invalidates the previous result and disables generated playback/export. Preserved
editor text is inactive until a new analysis succeeds. Rejected analyses remain
exportable as reports.

## Shared implementation and evidence

`TranscriptionEngine` dispatches both CLI and Playground to the same calculations.
The existing monophonic analyzer is unchanged. Extraction produces pitch frames
for the existing interpreter, canonical score, AST/printer/parser and renderer.
The interpreter accepts an optional spectral evidence value; its original YIN
evidence and serialized default results remain unchanged.

The new local managed C# extractor uses a 2048-sample Hann window, radix-2 FFT,
10 ms hops and interpolated spectral peaks at the normalized 16 kHz boundary.
It selects the strongest peak only when:

- its frequency is 65–1500 Hz;
- its three-bin energy is at least 35% of total spectral energy;
- every other local peak has less than 40% of its energy;
- no octave-related peak (within 0.7 semitones) has at least 25% of its energy.

Out-of-range peaks still compete. RMS silence gating, minimum stable duration
(60 ms), note segmentation and timing interpretation follow the existing path.
Generation requires at least one stable note and 50% stable coverage of active
audio. Accepted extraction is always **Experimental**. No baseline threshold was
relaxed, and no recording-specific parameters are used.

`Extraction` in the report includes:

| Field | Meaning |
|---|---|
| ExtractedNoteCount | Stable note segments, including candidates in rejected results |
| MelodyCoverage | Active frames covered by stable extracted notes / active frames |
| MeanSpectralShare | Average strongest-peak energy share across active frames, including rejected frames |
| CompetingPitchFraction | Active frames with another peak at least 40% as energetic |
| OctaveUncertainFraction | Active frames with an octave-related peak at least 25% as energetic |
| RejectedSections | Contiguous rejected frame intervals, with reason and excerpt-relative seconds |

Per-note evidence measures mean spectral share for that segment. `PitchConfidence`
retains the common result field name, but its meaning is defined by each note's
`PitchEvidence.Method`. Extraction frames use `SpectralShare`; they do not report
spectral energy as YIN periodicity. Legacy suitability periodicity/fundamental fields
are zero (not measured) in extraction mode and are not shown in that mode's UI.
Short pitched fragments discarded by the interpreter do not count as stable
coverage; `RejectedSections` describes the spectral gate, not every discarded
sub-60-ms fragment. No value is a calibrated correctness probability.

There is no model, cloud service, package, native binary or license added. Analysis
works offline on .NET and browser WASM. The optional existing FFmpeg decoder remains
necessary for desktop compressed media. Async analysis yields every 20 frames for
browser cancellation and uses the same calculations as synchronous analysis.
Determinism is tested for identical normalized PCM/options on the same runtime;
different codecs/platform floating-point behavior need not be byte-identical.

## Measured validation

The original CC0 synthetic mixture has a decaying harmonic melody over two quieter
accompaniment pitches. All four pitches (72, 76, 79, 74) are recovered, with no extra
or missed extracted notes, 5 ms onset MAE, 15 ms duration MAE and 98% stable coverage.
These numbers describe that fixture, not arbitrary recordings. Equal voices,
accompaniment-heavy triads, dense mixtures, octave doubling, noise, noise percussion
and silence reject. The five committed V13 fixtures retain their existing source
and serialized results. Sync/async, repeated runs, CLI and Release browser output
are tested for equality.

The synthetic generated-score/rendered-audio comparison yields 75% pitch recall,
one extra detected note and 30 ms onset MAE. Existing renderer release overlap and
monophonic reanalysis remain imperfect; source extraction success is not a promise
of waveform-equivalent reconstruction. Rendering semantics were not changed.

Local external acceptance used ten-second excerpts at 0, 30, 60, 90 and 110 seconds
from each original piano recording, and 30–40 seconds from the jazz/drum recordings.
Recordings and derived audio/source are kept outside Git. Exact results are in
[the measurements](melody-extraction-measurements.json).

| Recording | Extraction result | Stable coverage |
|---|---|---|
| alanajordan-piano-solo-245698 | All five excerpts rejected | 19.0–42.7% |
| music_for_videos-emotional-solo-piano-166032 | Four rejected; 90–100 s experimental, 23 notes | 55.3% for the accepted excerpt |
| alex-morgan-latin-jazz-solo-sunny-cafe-560053 | Rejected | 29.2% |
| alban_gogh-funk-drums-solo-301208 | Rejected | 19.5% |

Monophonic mode rejects all twelve of those excerpts. The accepted piano excerpt's
generated-score/rendered-audio comparison is 82.6% pitch recall, four missed notes,
zero extras. There is no independently annotated source truth for these recordings;
source pitch accuracy and recognizable melody acceptance are **not established**.
Human listening review remains required before treating that partial candidate as
a useful reconstruction. This is an implemented experimental mode, not completion
of the roadmap's broader real-piano quality target.

## Limitations

This is dominant spectral line extraction, not a trained predominant-melody model
or stem separator. It can follow a louder accompaniment/bass line. Rich harmonics,
sustain, close voices, octave doubling and percussion resonances are difficult.
The 128 ms window smears boundaries, and weak or brief notes disappear. Drum-only
noise fixtures reject, but no claim is made that all pitched percussion rejects.
Meter, key and tempo maps remain unknown; automatic tempo retains V13 ambiguity.
Most real piano excerpts tested still reject. Do not interpret an experimental
result as a full piano score or an exact reconstruction.

## Reproduce checks

```powershell
dotnet publish src/SoundScript.Playground -c Release -p:PublishDir=C:/Workspace/SoundScript/V13/sound-script/artifacts/melody-playground/
$env:SOUNDSCRIPT_PLAYGROUND_PUBLISH_DIR = (Resolve-Path artifacts/melody-playground).Path
dotnet test SoundScript.sln
node --test scripts/*.test.cjs
npm ci --prefix scripts
node scripts/verify-playground-startup.cjs artifacts/melody-playground
node scripts/verify-transcription-playground.cjs artifacts/melody-playground
```

The browser verification requires installed Playwright Chromium (or `CHROMIUM_PATH`)
and a Debug CLI build, which `dotnet test` provides. It creates ignored synthetic
fixtures/screenshots under `artifacts/melody-browser`. Tests cover original playback,
mode/instrument/excerpt invalidation, rejected output, cancellation and a 390 px
mobile viewport. Release startup tests cover normal loading, transient 503 recovery
and refusal of corrupt framework assets.
