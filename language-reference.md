# SoundScript Language Reference (V2)

Complete syntax reference for the SoundScript DSL. Whitespace separates tokens. Statements appear at the top level or inside blocks.

> V2 extends [v1.2](whats-new-v1.2.md) with imports, blocks, metadata, patterns, phrases, and orchestration. → [whats-new-v2.md](whats-new-v2.md)

## Notes & Melody Blocks

```
melody {
    tempo 120
    C4 q E4 q G4 q | C5 h
}
```

**Note format:** `[A–G][#|b]?[octave]`

| Example | Meaning |
|---------|---------|
| `C4` | Middle C (MIDI 60) |
| `F#4` | F sharp, octave 4 |
| `Bb3` | B flat, octave 3 |

## Imports (V2)

```ss
import "lib.ss"
```

Relative paths only. See [imports.md](imports.md).

## Named Blocks (V2)

```ss
block intro { C4 q E4 q G4 q }
play intro
```

See [blocks.md](blocks.md).

## Phrases (V2)

```ss
phrase {
    curve soft
    transition smooth
    mf
    C4 q E4 q G4 q
}
```

See [phrases.md](phrases.md).

## Patterns (V2)

```ss
pattern arp { up }
play arp Cmaj q
```

See [patterns.md](patterns.md).

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

See [track-metadata.md](track-metadata.md), [layers.md](layers.md), [orchestration.md](orchestration.md).

## Tempo Automation (V2)

```ss
time 4/4
tempo 120 → 140 over 4 bars
```

See [tempo-automation.md](tempo-automation.md).

## Durations

| Syntax | Beats |
|--------|-------|
| `C5` | 1 (default) |
| `C4 for 2` | 2 |
| `G4:4` | 4 |
| `C4 q` | 1 (quarter) |
| `D4 h` | 2 (half) |
| `E4 e` | 0.5 (eighth) |
| `F4 w` | 4 (whole) |

## Instruments

`piano`, `bass`, `violin`, `flute`, `guitar`, `trumpet`, `cello`, `organ`, `synth`

## Chords

```
Cmaj q
Dm h
G7 q
Fmaj7 w
```

### Advanced Voicing (V2)

```
Cmaj drop2 q
Cmaj inv1 h
Cmaj spread q
```

| Modifier | Effect |
|----------|--------|
| `drop2` / `drop3` | Drop voicing |
| `inv1` / `inv2` | Inversions |
| `spread` | Widen upper voices |

See [advanced-chords.md](advanced-chords.md).

## Sequences & Loops

```
sequence intro { C4 q D4 q }
play intro

loop 4 { C4 q D4 q }
```

## Dynamics

| Marking | Base velocity |
|---------|---------------|
| `p` | 48 |
| `mp` | 64 |
| `mf` | 80 |
| `f` | 96 |

## Rests, Ties, Articulations

```
rest q
C5 q ~ C5 q
staccato C4 q
C4 q legato
accent C4 q
```

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

## Related

- [whats-new-v2.md](whats-new-v2.md)
- [pipeline.md](pipeline.md)
- [examples.md](examples.md)
