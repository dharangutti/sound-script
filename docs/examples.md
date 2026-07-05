# SoundScript Examples (V6)

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
| [phrases-v3.ss](../examples/phrases-v3.ss) | Phrase engine v3 (aliases, envelopes, timing) | `... run examples/phrases-v3.ss` |
| [patterns.ss](../examples/patterns.ss) | Arp, strum, rhythm | `... run examples/patterns.ss` |
| [orchestration.ss](../examples/orchestration.ss) | Orchestration helpers | `... run examples/orchestration.ss` |
| [full-v2-showcase.ss](../examples/full-v2-showcase.ss) | Combined V2 demo | `... run examples/full-v2-showcase.ss` |
| [vocal-song.ss](../examples/vocal-song.ss) | Vocal track — lyrics + phonetic syllable alignment | `... run examples/vocal-song.ss` |

## Text-to-Melody Examples (V3.1)

`compose` takes plain text instead of a script — no `.ss` file needed. Each
command is deterministic: the same text always produces the same MIDI bytes.

| Text | Run |
|------|-----|
| Twinkle twinkle little star | `dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star"` |
| Hello world | `dotnet run --project src/SoundScript.Cli -- compose "Hello world"` |
| SoundScript makes music deterministic | `dotnet run --project src/SoundScript.Cli -- compose "SoundScript makes music deterministic"` |

Append the composed track to an existing script's output:

```bash
dotnet run --project src/SoundScript.Cli -- compose "How I wonder what you are" out.mid --append examples/vocal-song.ss
```

→ [text-to-melody.md](text-to-melody.md) · [cli.md](cli.md)

## Text-to-Melody: `.ss` Export (V6)

Add `--emit-ss <path>` to `compose`/`prosody` to also write the composed AST
as human-editable `.ss` source, alongside the `.mid` file:

```bash
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star" --emit-ss twinkle.ss
```

```
tempo 96

track phonemes {
    phrase {
        crescendo
        B3 e staccato
        C4 e v58
        E4 q legato
        C4 q v58
    }
    ...
}
```

Hand-edit a note (change a pitch, a duration, a velocity) and run it through
the existing parser to hear exactly that edit and nothing else:

```bash
dotnet run --project src/SoundScript.Cli -- run twinkle.ss twinkle-viass.mid
```

`twinkle-viass.mid` is byte-identical to what `compose "Twinkle twinkle
little star" twinkle.mid` (no flag) produces directly, as long as no hand
edits were made — tempo, pitches, durations, and velocities all round-trip.

→ [whats-new-v6.md](whats-new-v6.md) · [cli.md](cli.md#--emit-ss--export-the-composed-ast-as-ss-source-v6)

## Industrial Audio Cue Examples (V3)

Complete scripts backing the [Industrial Applications](https://soundscript.net/industrial/) case studies.

| Example | Scenario | Run |
|---------|----------|-----|
| [industrial-blind-assist.ss](../examples/industrial-blind-assist.ss) | Blind operator spatial awareness | `... run examples/industrial-blind-assist.ss` |
| [industrial-machine-state.ss](../examples/industrial-machine-state.ss) | Machine state (idle / running / critical) | `... run examples/industrial-machine-state.ss` |
| [industrial-conveyor-drift.ss](../examples/industrial-conveyor-drift.ss) | Conveyor timing drift (swing / push / pull) | `... run examples/industrial-conveyor-drift.ss` |
| [industrial-temperature-trend.ss](../examples/industrial-temperature-trend.ss) | Temperature trend (rising / stable / falling) | `... run examples/industrial-temperature-trend.ss` |
| [industrial-robotic-arm.ss](../examples/industrial-robotic-arm.ss) | Robotic arm motion phases (approach / grip / release) | `... run examples/industrial-robotic-arm.ss` |

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
| Text-to-Melody | `compose` input + *Compose from text* (V3.1) |
| Melody | Basic melody (v1.2) |
| Articulations | Staccato, legato, accent |
| Dynamics | p → mf → f |
| Chords | Chord progressions |
| Intelligence | Sequences + contour |
| Multi-track | Melody + bass |
| Playback | Shaping pipeline |

## Related

- [whats-new-v2.md](whats-new-v2.md)
- [whats-new-v6.md](whats-new-v6.md) — `.ss` export
- [language-reference.md](language-reference.md)
