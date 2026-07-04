# SoundScript Language Reference (V2)

Complete syntax reference for the SoundScript DSL. Whitespace separates tokens. Statements appear at the top level or inside blocks.

> V2 extends [v1.2](whats-new-v1.2.md) with imports, blocks, metadata, patterns, phrases, and orchestration. → [whats-new-v2.md](whats-new-v2.md)

## Lexical Rules

- **Comments:** none — `#` is not a comment token.
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

Top-level statements: `import`, `block`, `pattern`, `track`, `melody`, `sequence`, `loop`, `play`, tempo/time, instrument/layer metadata, orchestration, notes, chords, rests, dynamics.

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

- Octave range: **0–8** (4 = middle-C octave).
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

**Dotted suffix notation** (e.g. `q.`) is not supported. Use numeric forms for fractional beats (`C4 for 1.5`, `D4:1.5`).

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

| Articulation | Syntax | Effect |
|--------------|--------|--------|
| Staccato | `staccato C4 q` | ~47% duration, slightly softer |
| Legato | `C4 q legato` | ~97% duration |
| Accent | `accent C4 q` | ~110% velocity, ~102% duration |

One articulation per note, as prefix or suffix (not both).

→ [expressive-notation.md](expressive-notation.md) · [playback-quality.md](playback-quality.md)

## Dynamics

| Marking | Base velocity |
|---------|---------------|
| `p` | 48 |
| `mp` | 64 |
| `mf` | 80 |
| `f` | 96 |

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

Supported: `piano`, `bass`, `violin`, `flute`, `guitar`, `trumpet`, `cello`, `organ`, `synth`

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
| Dynamics | `p`, `mp`, `mf`, `f` (scoped to phrase) |

Phrase blocks set **phrase boundaries** on exit (same as `play` block/sequence). Nested `phrase` inside `phrase` is not supported.

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
- [cli.md](cli.md) — CLI reference (`run`, `compose`)
- [text-to-melody.md](text-to-melody.md) — Text-to-melody engine (V3.1)
- [pipeline.md](pipeline.md) — Interpreter pipeline
- [examples.md](examples.md) — Example catalog
