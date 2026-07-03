# What's New in SoundScript V2

SoundScript V2 extends the v1.2 engine with compositional syntax, track control, and advanced harmonic tooling — while keeping all v1.2 scripts valid.

## V2 Features

| Feature | Syntax | Doc |
|---------|--------|-----|
| **Imports** | `import "lib.ss"` | [imports.md](imports.md) |
| **Named blocks** | `block intro { }` + `play intro` | [blocks.md](blocks.md) |
| **Track metadata** | `gain`, `humanize` | [track-metadata.md](track-metadata.md) |
| **Tempo automation** | `tempo 120 → 140 over 4 bars` | [tempo-automation.md](tempo-automation.md) |
| **Instrument layers** | `layer piano` / `layer cello` | [layers.md](layers.md) |
| **Humanization** | Deterministic timing + velocity jitter | [humanization.md](humanization.md) |
| **Advanced chords** | `Cmaj drop2`, `inv1`, `spread` | [advanced-chords.md](advanced-chords.md) |
| **Phrase engine v2** | `phrase { curve soft ... }` | [phrases.md](phrases.md) |
| **Pattern engine** | `pattern arp { up }` + `play arp Cmaj q` | [patterns.md](patterns.md) |
| **Orchestration** | `double octave`, `reinforce bass`, `brighten top` | [orchestration.md](orchestration.md) |

## Updated Pipeline

V2 adds stages between voicing and playback:

```
ChordVoicing → AdvancedChordVoicing → ChordOrchestration → HarmonicSpacing
Note emit:   MusicalIntelligence → PhraseShaper → PlaybackShaper → HumanizeApplicator
Pattern play: PatternExpander → EmitNote (full note pipeline)
Multi-layer:  per-layer PlaybackShaper + MIDI channel
```

→ [pipeline.md](pipeline.md)

## New Examples

| Example | Feature |
|---------|---------|
| [imports.ss](../examples/imports.ss) | Multi-file projects |
| [blocks.ss](../examples/blocks.ss) | Named blocks |
| [metadata.ss](../examples/metadata.ss) | Gain + humanize |
| [tempo-automation.ss](../examples/tempo-automation.ss) | Tempo ramps |
| [layers.ss](../examples/layers.ss) | Instrument layers |
| [humanization.ss](../examples/humanization.ss) | Deterministic jitter |
| [advanced-chords.ss](../examples/advanced-chords.ss) | drop2, inv1, spread |
| [phrases.ss](../examples/phrases.ss) | Phrase shaping |
| [patterns.ss](../examples/patterns.ss) | Arp, strum, rhythm |
| [orchestration.ss](../examples/orchestration.ss) | Orchestration helpers |
| [full-v2-showcase.ss](../examples/full-v2-showcase.ss) | Combined V2 demo |

## Updated Playground

The browser playground includes V2 presets for blocks, metadata, tempo automation, layers, humanization, advanced chords, phrases, patterns, and orchestration. Imports use the CLI (`ProgramLoader`) only.

→ [soundscript.net/playground](https://soundscript.net/playground/)

## Backward Compatibility

All v1.2 syntax remains valid. V2 adds optional statements and modifiers; existing `melody`, `track`, `sequence`, and `play` workflows are unchanged.

## Language Reference

→ [language-reference.md](language-reference.md) — Complete V2 syntax

## v1.2 Foundation

V2 builds on the five-phase v1.2 engine:

- Phase 2 — Notation
- Phase 3 — Expressive notation
- Phase 1 — Stabilization
- Phase 4 — Musical intelligence
- Phase 5 — Playback quality

→ [whats-new-v1.2.md](whats-new-v1.2.md)
