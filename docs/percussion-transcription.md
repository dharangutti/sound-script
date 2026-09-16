# Percussion / rhythm transcription — experimental

Milestone 4 lives on `codex/percussion-rhythm-transcription`, based on mixed-role
commit `2d1ba42`. See the [phased plan](percussion-transcription-plan.md).

```sh
soundscript transcribe drums.wav --mode percussion --out drums.ss --report drums.json --preview drums-preview.wav
soundscript transcribe recording.mp3 --mode percussion --start 10 --duration 10 --tempo 120 --out rhythm.ss
soundscript wave rhythm.ss --out rhythm.wav
```

Input, source, report and preview must use distinct filenames.

The Playground has a separate **Percussion / Rhythm (Experimental)** mode with
detected hits, observed seconds, reconstructed beats, grid origin and fit, tempo
alternatives, classification evidence, editable rhythm code, playback and exports.
The pitched-instrument selector is inactive in this mode; playback uses a
deterministic synthetic kit. The general music editor also routes hit syntax
directly to the wave backend.

## Dedicated unpitched path

Normalized PCM passes through a separate three-band envelope/transient detector,
then an onset-grid interpreter. Neither analysis nor generated-audio validation
calls the pitched-note transcriber. Canonical percussion tracks have an explicit
hit list and **empty pitched-note lists**. No MIDI pitch is inferred from drum
resonances. The shared wave event container marks percussion explicitly and sets
its pitch frequency to zero; a separate renderer synthesizes the drum class.

Onsets have 10 ms observation resolution. Candidates need a sufficient rise over
recent energy, an absolute activity floor and observed decay; a 70 ms refractory
period suppresses duplicate attacks. Attack-band dominance produces coarse kick,
snare or hat estimates. Ambiguous attacks become generic clicks. Scores describe
energy dominance and onset strength, **not calibrated instrument probabilities**.
Silence, continuous noise, sustained tones and attacks without enough decay
observation are rejected in the authored tests.

The independent rhythm interpreter searches tempo/grid hypotheses. It exposes
half/double-time alternatives and fit, retains original onset seconds and only
snaps reconstruction beats when timing evidence is sufficiently strong. Fewer
than three hits use an explicitly labelled 120 BPM playback fallback with zero
tempo evidence. No meter is inferred.

## SoundScript representation

```soundscript
tempo 120
track drums {
    hit kick :0.5 v90
    hit hat :0.5 v65
    hit snare :0.5 v85
    hit click :0.5 v60
}
```

`hit kick|snare|hat|click` requires a positive beat duration (numeric `:beats` or
an existing duration alias), followed by optional velocity `v1`–`v127`. Duration
advances the track cursor; it is not a measured drum decay. Parallel tracks
preserve independent timing. Hits work at top level, in ordinary tracks,
sequences, blocks, loops and phrases; vocal `sing` bodies do not accept hits.
The AST/printer/parser preserve this construct without converting it to NoteNode.
Wave playback supports it. MIDI export explicitly rejects it until a dedicated
percussion mapping is implemented. CLI inspection automatically selects Wave
and reports hits in event counts, without inflating pitched-note counts.

## Measurements and limitations

Authored tests cover kick-only, snare-only, hats, alternating kick/snare, mixed
percussion, resonant pitched percussion, noisy transients, silence, sustained
tones, continuous noise, weak pitched accompaniment, truncated attacks,
single-hit tempo fallback, cancellation, deterministic sync/async output,
syntax, timing and wave rendering. Each of the isolated class fixtures contains
five expected hits; recovery is checked within 20 ms with the expected class.
Production synthesis is separate from fixture synthesis. Generated reanalysis
uses percussion onset/class comparison, with one-to-one matching within 50 ms.

The original `alban_gogh-funk-drums-solo-301208.mp3` recording was measured at
0–10, 10–20 and 20–30 seconds. The excerpts produced 52, 50 and 38 estimated hits.
Onset-grid fit was 92.5%, 67.7% and 70.4%; the latter excerpts chose near-double
tempo hypotheses (239 and 237 BPM). This ambiguity remains visible. No hat
classes were estimated in these excerpts; source instruments are not verified.
See [machine-readable measurements](percussion-measurements.json).

Round-trip onset recall was 92.3%, 100% and 100%, with 100% onset precision;
matched-class agreement was 100%, 92% and 89.5%. These compare the generated
schedule with reanalysis of synthetic playback, **not ground truth from the
original recording**. Simultaneous hits may merge into one class, close rolls and
quiet hits can be missed, and tonal attacks can receive a coarse percussion
label. This does not claim drum stem isolation or full kit reconstruction.

Human listening acceptance remains deferred at the user's request. Original
media, previews and a combined mixed-role/rhythm listening page live in ignored
local artifacts. No recognizable-playback verdict has been recorded.

## Verification result

All 1,161 .NET regression tests and 20 Node tests passed. After final small
tempo/display refinements, all 19 focused percussion tests passed again.
Release publication/integrity and browser startup checks passed. Browser checks
cover all transcription modes, CLI/browser score parity, hit and note playback,
role filtering, original excerpts, editing, exports, rejection, cancellation and
mobile layout. The percussion check also verifies the general music editor
selects the Wave backend for hit syntax.
