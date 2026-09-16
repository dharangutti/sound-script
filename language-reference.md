# SoundScript Language Reference

For editor-focused authoring features, see [Authoring ergonomics](authoring.md).

## Audio/Visual media (V10/V11)

Visual blocks also support explicit shapes and static appearance declarations:
`shape rectangle`, `fill "#6ee7b7"`, `stroke "#ffffff"`, `strokeWidth 2`,
`text "READY"`, and `fontSize 42`. Text/font size apply to `shape text`.
See [Media Primitives](media-primitives.md) for all supported shapes and validation rules.

The audio grammar below remains available. Media programs add top-level
`visual "name" for 2s`, `wait 1s`, `visual "name" for 2s at 0s`, and
`sync audio`. Visual blocks accept linear numeric curves such as
`animate opacity 0 -> 1 over 1s`. V11 renders these states and the shared
Wave PCM audio to WebM; it adds no frame or codec syntax to the language.
See the [complete temporal media reference](visual-temporal.md) for all
time-unit aliases, validation rules, primitives, renderer properties,
audio timing, and executable examples.

Complete syntax reference for the SoundScript DSL. Whitespace separates tokens. Statements appear at the top level or inside blocks.

> V2 extends [v1.2](whats-new-v1.2.md) with imports, blocks, metadata, patterns, phrases, and orchestration. → [whats-new-v2.md](whats-new-v2.md)

## Lexical Rules

- **Comments:** `//` line comments and `/* ... */` block comments. `#` remains a
  literal outside quoted colors and is not a comment token.
- **Case:** keywords are case-insensitive (`Melody`, `melody`, `MELODY` are equivalent).
- **Strings:** `"relative/path.ss"` for imports.
- **Numbers:** integers or decimals (`120`, `0.5`, `1.5`).
- **Bar line:** `|` — measure boundary when `time` is declared.
- **Tie:** `~` — merges adjacent notes of the same pitch.
- **Tempo arrow:** `->` or Unicode `→` — connects start and end BPM in tempo ramps.

## Program Structure

```
program ::= statement*
```

Top-level statements: `perform expressive`, `import`, `block`, `pattern`, `track`, `melody`, `sequence`, `loop`, `play`, tempo/time, instrument/layer metadata, orchestration, notes, chords, rests, dynamics.

## Performance interpretation

```ss
perform expressive
tempo 72
track lead {
    instrument flute
    phrase {
        articulation legato
        crescendo
        G4 h A4 h B4 w
    }
    rest q
}
```

`perform expressive` is a **top-level, program-wide opt-in** for MIDI, Wave,
and Playground playback. It applies to all instrumental tracks regardless of
where the declaration appears. It is not valid inside a track or phrase;
`expressive` is the only mode. Omit the declaration to retain legacy output.

The performance layer connects suitable adjacent melodic notes, shapes phrase
velocity, and keeps repeated pitches, accents, large leaps, and rests articulated.
It preserves written melodic pitches instead of applying legacy octave/contour
correction. Explicit staccato retains its MIDI duration and velocity.

Wave and the Playground's MIDI sample player also use gentler connected attacks,
bounded releases, sustained-note evolution, and continuous phrase dynamics.
External MIDI players receive the shaped note timing and velocity; the additional
audio envelopes are SoundScript metadata, not portable MIDI controller automation.
Each MIDI track/layer gets an independent melodic channel (maximum 15).

See [Performance interpretation](performance-interpretation.md) for the precise
rules, renderer differences, reproducible comparisons, and current limitations.

## Notes

**Format:** `[A–G][accidental]?[octave]`

| Accidental | Syntax | Example |
|------------|--------|---------|
| Sharp | `#` | `F#4` |
| Flat | `b`, `B`, `♭` | `Bb3`, `Db4` |
| Natural | `♮` | `C♮4` |
| None | (omit) | `C4` |

| Example | Meaning |
|---------|---------|
| `C4` | Middle C (MIDI 60) |
| `F#4` | F sharp, octave 4 |
| `Bb3` | B flat, octave 3 |

- MIDI octave range: **-1–9** (4 = middle-C octave). The valid MIDI boundary is
  C-1 (0) through G9 (127); pitches above G9 are rejected.
- At most one accidental per note.

## Durations

| Syntax | Beats | Notes |
|--------|-------|-------|
| *(omitted)* | 1.0 | Default quarter-note length |
| `q`, `quarter` | 1.0 | Quarter note |
| `h`, `half` | 2.0 | Half note |
| `e`, `eighth` | 0.5 | Eighth note |
| `w`, `whole` | 4.0 | Whole note |
| `for N` | N | Numeric beats (`C4 for 2`) |
| `:N` | N | Colon form (`G4:4`, `G4:0.5`) |

 Dotted suffix notation multiplies a written duration by 3/2 (`C4 q.`). Numeric
 forms remain available for arbitrary beats (`C4 for 1.5`). `triplet e C4`
 applies a 2/3 multiplier, while `tuplet 5 in 4 C4 e` supports arbitrary
 n-in-the-time-of-m tuplets. `grace C5 e` emits a short 1/4-duration grace
 event. These modifiers resolve to the same deterministic beat representation
 used by ordinary notes.

Repeated single-letter aliases (`qq`, `hh`) are rejected.

→ [notation.md](notation.md) for the internal `NotatedNote` model.

## Rests

```
rest q
rest e
rest for 2
rest:4
```

Rests advance the beat clock; no MIDI note is emitted. Duration syntax matches notes.

→ [expressive-notation.md](expressive-notation.md)

## Ties

```
C5 q ~ C5 q
C5 q ~ C5 q ~ C5 h
```

- `~` ties adjacent notes of the **same pitch**.
- Durations merge into one sustained note.
- Mismatched pitches error: `Invalid tie: pitches differ`.
- Chords cannot be tied.

## Articulations

| Articulation | Syntax | Legacy MIDI shaping |
|--------------|--------|--------|
| Staccato | `staccato C4 q` | ~47% duration, slightly softer |
| Legato | `C4 q legato` | ~97% duration |
| Accent | `accent C4 q` | ~110% velocity, ~102% duration |

One articulation per note, as prefix or suffix (not both).

The 97% legato duration leaves a 3% note-off gap; it does not mean overlapping
notes. With `perform expressive`, eligible connections replace that gap with
up to 40 ms of note overlap. Legacy Wave ignores these articulation modifiers;
expressive Wave honors them. Articulation velocity factors above are intermediate
values, before later instrument and velocity curves.

→ [expressive-notation.md](expressive-notation.md) · [playback-quality.md](playback-quality.md)

## Dynamics

| Marking | Base velocity |
|---------|---------------|
| `p` | 48 |
| `mp` | 64 |
| `mf` | 80 |
| `f` | 96 |
| `ppp` | 32 |
| `ff` / `fff` | 112 |
| `sfz` | 120 |
| `fp` | 104 |

Dynamics persist on the track until changed. Per-note `vN` overrides apply before shaping.

## Velocity

```
velocity 90        // track-scoped default (1–127)
C4 q v100          // per-note override
```

## Chords

```
Cmaj q
Dm h
G7 q
Fmaj7 w
Cmaj drop2 q
Cmaj inv1 h
Cmaj spread q
```

| Suffix | Quality | Intervals (semitones) |
|--------|---------|------------------------|
| *(none)* / `maj` | Major | 0, 4, 7 |
| `m` / `min` | Minor | 0, 3, 7 |
| `dim` | Diminished | 0, 3, 6 |
| `aug` | Augmented | 0, 4, 8 |
| `maj7` | Major 7 | 0, 4, 7, 11 |
| `7` | Dominant 7 | 0, 4, 7, 10 |
| `sus2` / `sus4` | Suspended | 0, 2, 7 / 0, 5, 7 |
| `major6` / `m6` | Sixth | 0, 4, 7, 9 / 0, 3, 7, 9 |
| `dim7` / `halfdim7` | Diminished / half-diminished 7 | 0, 3, 6, 9 / 0, 3, 6, 10 |
| `m7` / `min7` | Minor 7 | 0, 3, 7, 10 |
| `maj9` / `min9` / `9` | Ninth chords | 0, 4, 7, 11, 14 / 0, 3, 7, 10, 14 / 0, 4, 7, 10, 14 |
| `dom9` | Explicit dominant 9 | 0, 4, 7, 10, 14 |
| `11` / `13` | Extended dominant harmony | 0, 4, 7, 10, 14, 17 / plus 21 |
| `add9` | Added ninth | 0, 4, 7, 14 |

`C6` and `Cmaj6` remain unambiguous MIDI pitches/chords from earlier syntax;
write `Cmajor6` when a major-sixth chord is intended.

### Dominant-7 Disambiguation

Tokens like `G7` are lexed as notes. The parser reinterprets them as dominant-7 chords **only when a duration follows**:

```
G7 q      ← dominant-7 chord (octave 4)
B7        ← note B, octave 7 (no duration)
C7 h      ← dominant-7 chord
```

Dominant-7 chords cannot specify octave and cannot be tied.

### Advanced Voicing (V2)

| Modifier | Effect |
|----------|--------|
| `drop2` / `drop3` | Drop voicing |
| `inv1` / `inv2` | Inversions |
| `spread` | Widen upper voices |

→ [advanced-chords.md](advanced-chords.md)

## Time Signature & Measures

```
time 4/4
melody {
    C4 q E4 q G4 q |
    C4 h |
}
```

When `time` is declared and bar lines (`|`) are used, measure durations are validated. Warnings are non-blocking:

- `Measure N incomplete: expected X beats, got Y`
- `Measure N exceeds expected duration`

## Tempo

```
bpm 120
tempo 120
tempo 120 → 140 over 4 bars
```

- `bpm` and `tempo` set instant tempo.
- Only `tempo` supports ramps (`→` or `->`, `over N bar` / `over N bars`).
- Multiple top-level ramps chain sequentially.

→ [tempo-automation.md](tempo-automation.md)

## Instruments & Layers

```
instrument piano
layer piano
layer cello
```

All 128 General MIDI Level 1 programs are available by compact names (for
example `acousticgrand`, `electricguitarclean`, `altosax`,
`trombone`, `leadsquare`, and `fxscifi`) or by program number `0`–`127`.
The original names `piano`, `bass`, `violin`, `flute`, `guitar`, `trumpet`,
`cello`, `organ`, and `synth` retain their existing mappings. General MIDI
percussion uses channel 10 and meaningful drum names are a recommended future
extension; custom Wave/SoundCSS timbres remain supported.

→ [layers.md](layers.md)

## Imports (V2)

```ss
import "lib.ss"
```

Relative paths only. Nested imports allowed; circular imports error. Later definitions override earlier.

→ [imports.md](imports.md)

## Named Blocks (V2)

```ss
block intro { C4 q E4 q G4 q }
play intro
```

Blocks expand inline. Recursion is rejected. Body allows notes, chords, dynamics, articulations, rests, bar lines, `phrase`, `orchestration`, and nested `play`. Blocks do **not** allow loops, tempo/time, or track metadata.

→ [blocks.md](blocks.md)

## Sequences & Loops

```
sequence intro { C4 q D4 q }
play intro

loop 4 { C4 q D4 q }
```

| | `block` | `sequence` |
|---|---------|------------|
| Body | Musical events, phrase, play | Full track body |
| Recursion guard | Yes | No |
| Loops inside | No | Yes |

`loop` is allowed in `track`, `sequence`, and top-level — not in `melody` or nested inside another `loop`.

## Phrases (V2 + V3)

```ss
phrase {
    curve soft              // soft | hard | balanced | expressive | swell | fade
    curve gentle            // V3 alias for soft
    transition smooth       // smooth | abrupt | soft | expressive
    transition sharp        // V3 alias for abrupt
    crescendo               // V3 dynamic envelope
    decrescendo             // V3 dynamic envelope
    articulation legato     // V3 phrase default (staccato | legato | accent | detached)
    swing 0.67              // V3 timing (0.0–1.0)
    push 0.02               // V3 timing (beats ahead)
    pull 0.01               // V3 timing (beats behind)
    mf
    C4 q E4 q G4 q
}
```

| Statement | Values |
|-----------|--------|
| `curve` | `soft`/`gentle`, `hard`/`strong`/`aggressive`, `balanced`, `expressive`, `swell`, `fade` |
| `transition` | `smooth`, `abrupt`/`sharp`, `soft`, `expressive` |
| `crescendo` / `decrescendo` | Phrase velocity ramp |
| `articulation` | `staccato`, `legato`, `accent`, `detached` |
| `swing` / `push` / `pull` | Deterministic timing offsets |
| Dynamics | `ppp`, `p`, `mp`, `mf`, `f`, `ff`, `fff`, `sfz`, `fp` (scoped to phrase) |

Phrase blocks set **phrase boundaries** on exit (same as `play` block/sequence). Nested `phrase` inside `phrase` is not supported.

In legacy MIDI, `transition` and `crescendo`/`decrescendo` shape **note-on
velocity**, not audio crossfades or a held note's volume. Legacy Wave enters
phrase bodies but skips their shaping directives. `perform expressive` adds
relationship-based connections and continuous amplitude evolution on supported
audio paths; rests still separate musical phrases.

→ [phrases.md](phrases.md) · [phrases-v3.md](phrases-v3.md)

## Patterns (V2)

```ss
pattern arp { up }
play arp Cmaj q
```

| Body | Kind | Behavior |
|------|------|----------|
| `up` | Arpeggio | Ascending; duration split evenly |
| `down` | Arpeggio | Descending |
| `updown` | Arpeggio | Ascend then descend |
| `strum` | Strum | Staggered chord tones (0.05 beat offset) |
| `rhythm e e q` | Rhythm | Custom durations per voice |

Pattern play does **not** set phrase boundaries.

→ [patterns.md](patterns.md)

## Track Metadata (V2)

```ss
track piano {
    instrument piano
    gain 0.9
    humanize 0.03
    layer piano
    layer cello
    double octave
    reinforce bass
    brighten top
    C4 q
}
```

| Statement | Range | Effect |
|-----------|-------|--------|
| `gain N` | 0.0–1.0 | Velocity multiplier after playback shaping |
| `humanize N` | ≥ 0 | Deterministic timing + velocity jitter |

→ [track-metadata.md](track-metadata.md) · [humanization.md](humanization.md) · [layers.md](layers.md) · [orchestration.md](orchestration.md)

## Orchestration (V2)

```
double octave
reinforce bass
brighten top
```

Track-scoped, sticky flags affecting all subsequent chords.

→ [orchestration.md](orchestration.md)

## Context Matrix

| Statement | Top | Melody | Track/Seq | Block | Phrase |
|-----------|-----|--------|-----------|-------|--------|
| `import` | ✓ | | | | |
| `block` / `pattern` def | ✓ | | | | |
| `track` / `melody` / `sequence` | ✓ | | | | |
| `loop` | ✓ | ✗ | ✓ | ✗ | ✗ |
| `phrase` | | ✓ | ✓ | ✓ | ✗ |
| `instrument` / `layer` / `gain` / `humanize` / `velocity` | ✓ | ✓ | ✓ | ✗ | ✗ |
| `tempo` / `bpm` / `time` | ✓ | ✓ | ✓ | ✗ | ✗ |
| `orchestration` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `play` | ✓ | ✓ | ✓ | ✓ | ✓ |
| note / chord / rest / dynamic / bar | ✓ | ✓ | ✓ | ✓ | ✓ |

## Melody & Track Blocks

```
melody {
    tempo 120
    C4 q E4 q G4 q | C5 h
}

track melody {
    instrument piano
    mf
    C4 q
}
```

## Text-to-Melody: `compose` (V3.1)

`compose` is a top-level **CLI verb** beside `run` — it takes plain text, not a
script, so it adds nothing to the grammar above:

```bash
soundscript compose "Twinkle twinkle little star" [output.mid]
soundscript compose "Twinkle twinkle little star" out.mid --append file.ss
```

The text is split into syllables (the vocal engine's `Syllabifier`), each
syllable into phonemes, each phoneme mapped to a musical gesture, and the
gestures assembled into ordinary AST nodes (`PhraseNode`, `NoteNode`,
`PhraseEnvelopeNode`) that the existing interpreter turns into a track named
`phonemes`.

Programmatic equivalents in `SoundScript.Compose`:

| API | Result |
|-----|--------|
| `PhonemeComposer.ComposeProgram(text, tempo = 96)` | Complete `InterpretedProgram` (tempo map included), ready for `MidiGenerator.Write` |
| `PhonemeComposer.Compose(text, tempo = 96)` | Just the `InterpretedTrack` named `phonemes` |
| `PhonemeComposer.AppendTo(program, text)` | Adds the composed track to an existing `InterpretedProgram`, using its tempo |
| `PhonemeComposer.BuildAst(text, tempo = 96)` | The program AST without interpreting it |

**Determinism:** the mapping tables are pure data, string handling is
culture-independent, and there is no randomness — identical text produces
identical MIDI bytes on every platform, the same contract as scripts.

→ [text-to-melody.md](text-to-melody.md) · [phoneme-composer.md](phoneme-composer.md) · [cli.md](cli.md)

## Text-to-Melody: `prosody` (V5)

Same CLI shape as `compose`, but pitch follows word stress and sentence contour
via the [`ProsodyComposer`](word-prosody.md):

```bash
soundscript prosody "Twinkle twinkle little star" [output.mid]
soundscript prosody "Twinkle twinkle little star" out.mid --append file.ss
soundscript prosody "Twinkle twinkle little star" --emit-ss twinkle-prosody.ss
```

→ [word-prosody.md](word-prosody.md) · [cli.md](cli.md#prosody--word-level-text-to-melody-v5)

## Offline timbre: `render` (V4)

Renders an existing MIDI file with a [SoundCSS](soundcss.md) (`.ssc`) stylesheet:

```bash
soundscript render file.mid --css examples/default.ssc [--out output.wav] [--text "source text"]
```

→ [soundcss.md](soundcss.md) · [timbre-engine.md](timbre-engine.md) · [cli.md](cli.md#render--midi-to-audio-v4)

## Direct wave output: `wave` (V7)

Renders a `.ss` or `.ssw` script directly to WAV via [SoundScript.Wave](wave-grammar.md)
— no MIDI step. Wave-only grammar (`effect`, `speak`, named `humanize`) is
rejected by `run`; use `wave` instead:

```bash
soundscript wave examples/wave-effects.ssw [output.wav] [--stereo]
soundscript wave examples/full-song-wave.ss jingle.wav
```

→ [wave-grammar.md](wave-grammar.md) · [whats-new-v7.md](whats-new-v7.md) · [cli.md](cli.md#wave--script-to-wav-v7)

## AST Node Types

| Node | Purpose |
|------|---------|
| `ImportNode` | File import |
| `BlockNode` | Named reusable block |
| `PatternNode` | Pattern definition |
| `PhraseNode` | Phrase block |
| `OrchestrationNode` | Orchestration helper |
| `LayerNode` | Instrument layer |
| `GainNode` / `HumanizeNode` | Track metadata |
| `TempoRampNode` | Tempo automation |
| `ProgramNode` | Root container |
| `TrackNode` / `MelodyNode` | Track blocks |
| `NoteNode` / `ChordNode` | Musical events |
| `RestNode` | Rest |
| `DynamicNode` | Dynamic marking |
| `PlayNode` | Block / sequence / pattern invocation |
| `LoopNode` | Loop |
| `PhraseCurveNode` / `PhraseTransitionNode` | Phrase shaping |
| `PhraseArticulationNode` / `PhraseEnvelopeNode` | V3 phrase defaults and envelopes |
| `PhraseSwingNode` / `PhrasePushNode` / `PhrasePullNode` | V3 timing modifiers |

## Related

- [notation.md](notation.md) — Notation engine (Phase 2)
- [expressive-notation.md](expressive-notation.md) — Rests, ties, articulations (Phase 3)
- [whats-new-v2.md](whats-new-v2.md) — V2 changelog
- [cli.md](cli.md) — CLI reference (`run`, `compose`, `prosody`, `render`, `wave`)
- [text-to-melody.md](text-to-melody.md) — Text-to-melody engine (V3.1)
- [pipeline.md](pipeline.md) — Interpreter pipeline
- [examples.md](examples.md) — Example catalog

## Unpitched percussion (experimental)

`hit kick|snare|hat|click :beats [vN]` represents a genuine percussion event
without a note pitch. It uses the Wave backend; MIDI mapping is not implemented.
See [percussion syntax and transcription](percussion-transcription.md).
