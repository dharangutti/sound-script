# SoundScript Examples (V2)

Runnable example scripts for every major feature.

## V2 Feature Examples

| Example | Feature | Run |
|---------|---------|-----|
| [imports.ss](../examples/imports.ss) | Multi-file imports | `dotnet run --project src/SoundScript.Cli -- run examples/imports.ss` |
| [import-lib.ss](../examples/import-lib.ss) | Shared library (imported) | (used by imports.ss) |
| [blocks.ss](../examples/blocks.ss) | Named blocks | `... run examples/blocks.ss` |
| [metadata.ss](../examples/metadata.ss) | Gain + humanize | `... run examples/metadata.ss` |
| [tempo-automation.ss](../examples/tempo-automation.ss) | Tempo ramps | `... run examples/tempo-automation.ss` |
| [layers.ss](../examples/layers.ss) | Instrument layers | `... run examples/layers.ss` |
| [humanization.ss](../examples/humanization.ss) | Deterministic jitter | `... run examples/humanization.ss` |
| [advanced-chords.ss](../examples/advanced-chords.ss) | drop2, inv1, spread | `... run examples/advanced-chords.ss` |
| [phrases.ss](../examples/phrases.ss) | Phrase engine v2 | `... run examples/phrases.ss` |
| [patterns.ss](../examples/patterns.ss) | Arp, strum, rhythm | `... run examples/patterns.ss` |
| [orchestration.ss](../examples/orchestration.ss) | Orchestration helpers | `... run examples/orchestration.ss` |
| [full-v2-showcase.ss](../examples/full-v2-showcase.ss) | Combined V2 demo | `... run examples/full-v2-showcase.ss` |

## Updated v1.2 Examples

| Example | V2 updates |
|---------|------------|
| [full.ss](../examples/full.ss) | imports, blocks, phrases, patterns, layers, orchestration |
| [phrase-smoothing.ss](../examples/phrase-smoothing.ss) | phrase blocks + block play |

## Core Language Examples

| Example | Demonstrates |
|---------|--------------|
| [melody.ss](../examples/melody.ss) | Basic melody |
| [rests.ss](../examples/rests.ss) | Rests |
| [ties.ss](../examples/ties.ss) | Ties |
| [articulations.ss](../examples/articulations.ss) | Staccato, legato, accent |
| [dynamics.ss](../examples/dynamics.ss) | Dynamic markings |
| [chords.ss](../examples/chords.ss) | Chord progressions |
| [sequences.ss](../examples/sequences.ss) | Sequences |
| [loops.ss](../examples/loops.ss) | Loops |
| [multitrack.ss](../examples/multitrack.ss) | Multi-track |
| [velocity.ss](../examples/velocity.ss) | Velocity control |
| [instruments.ss](../examples/instruments.ss) | Instruments |
| [tempo-time.ss](../examples/tempo-time.ss) | Tempo and time signature |
| [durations.ss](../examples/durations.ss) | Duration syntax |

## Engine Intelligence Examples

| Example | Demonstrates |
|---------|--------------|
| [chord-voicing.ss](../examples/chord-voicing.ss) | Phase 1 voicing |
| [harmonic-spacing.ss](../examples/harmonic-spacing.ss) | Harmonic spacing |
| [melodic-contour.ss](../examples/melodic-contour.ss) | Melodic contour |
| [dynamic-ramping.ss](../examples/dynamic-ramping.ss) | Dynamic ramping |
| [multitrack-sync.ss](../examples/multitrack-sync.ss) | Multi-track sync |
| [playback-shaping.ss](../examples/playback-shaping.ss) | Playback shaping |

## Playground Presets (V2)

| Preset | Content |
|--------|---------|
| V2 Showcase | Combined imports, patterns, phrases, layers |
| Imports | Multi-file import demo |
| Blocks | Named block expansion |
| Metadata | Gain + humanize |
| Tempo | Tempo automation ramp |
| Layers | Piano + cello layers |
| Humanize | Deterministic jitter |
| Advanced Chords | drop2, inv1, spread |
| Phrases | Phrase curves + transitions |
| Patterns | Arp, strum, rhythm |
| Orchestration | double octave, bass, top |
| Melody | Basic melody (v1.2) |
| Articulations | Staccato, legato, accent |
| Dynamics | p → mf → f |
| Chords | Chord progressions |
| Intelligence | Sequences + contour |
| Multi-track | Melody + bass |
| Playback | Shaping pipeline |

## Related

- [whats-new-v2.md](whats-new-v2.md)
- [language-reference.md](language-reference.md)
