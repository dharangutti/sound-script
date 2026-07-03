# What's New in SoundScript v1.2

SoundScript v1.2 completes the engine evolution with notation, expressiveness, stabilization, musical intelligence, and playback quality — while keeping all existing scripts compatible.

## Phase 2 — Notation Engine

- Canonical `NotatedNote` model with pitch class, accidental, and octave
- Duration aliases: `q` (quarter), `h` (half), `e` (eighth), `w` (whole)
- Accidentals: `#`, `b`, `n`
- Consistent MIDI number resolution

## Phase 3 — Expressive Notation

- **Rests** — `rest q` advances time without notes
- **Ties** — `C5 q ~ C5 q` merges durations
- **Articulations** — `staccato`, `legato`, `accent`
- **Dynamics** — `p`, `mp`, `mf`, `f`
- **Measure validation** — warnings for incomplete or excess measures

## Phase 1 — Stabilization

- **BeatMath** — deterministic beat rounding (1e-9 grid)
- **ChordVoicing** — raises low roots, spreads wide chords
- **InstrumentGainMap** — per-instrument velocity balancing
- **GlobalBeatClock** — multi-track sync with drift correction
- **SequenceContext** — reliable sequence play state
- **Loop alignment** — beat-grid loop boundaries

## Phase 4 — Musical Intelligence

- **OctaveSmoother** — reduces extreme octave jumps
- **MelodicContour** — corrects wide melodic leaps
- **HarmonicSpacing** — refines chord register
- **PhraseSmoother** — smooths sequence phrase boundaries
- **DynamicContext** — ramps abrupt dynamic changes over 3 notes

## Phase 5 — Playback Quality

- **DynamicShaper** — dynamic-level velocity curves
- **ArticulationShaper** — refined staccato/legato/accent shaping
- **InstrumentGainRefiner** — soft-velocity boost, hot-velocity reduction
- **ExpressiveCurve** — balanced phrasing curves
- **DurationNormalizer** — beat-grid duration rounding
- **ChordBalancer** — per-voice chord velocity balance

## New Interpreter Pipeline

```
Tokenizer → Parser → AST
    → Intelligence (contour, spacing, dynamics)
    → PlaybackShaper (6-stage refinement)
    → MidiGenerator
```

See [pipeline.md](pipeline.md) for the full diagram.

## New Examples

12 new v1.2 examples covering rests, ties, articulations, dynamics, voicing, spacing, contour, phrase smoothing, dynamic ramping, multi-track sync, and playback shaping.

See [examples.md](examples.md).

## New Documentation

| Document | Topic |
|----------|-------|
| [language-reference.md](language-reference.md) | Complete syntax |
| [notation.md](notation.md) | Notation engine |
| [expressive-notation.md](expressive-notation.md) | Rests, ties, articulations |
| [stabilization.md](stabilization.md) | Timing and voicing |
| [musical-intelligence.md](musical-intelligence.md) | Contour and spacing |
| [playback-quality.md](playback-quality.md) | Shaping pipeline |
| [architecture.md](architecture.md) | System architecture |
| [pipeline.md](pipeline.md) | Interpreter flow |

## New Diagrams

- Notation model diagram ([notation.md](notation.md))
- Interpreter pipeline ([pipeline.md](pipeline.md))
- Playback shaping pipeline ([playback-quality.md](playback-quality.md))
- Musical intelligence flow ([musical-intelligence.md](musical-intelligence.md))
- Stabilization modules ([stabilization.md](stabilization.md))
- Multi-track sync ([pipeline.md](pipeline.md))

## Backward Compatibility

- All Phase 1–10 language syntax remains valid
- No new keywords required for existing scripts
- `melody { bpm 120; C4 E4 G4 }` still works unchanged
- `G7` alone still parses as G octave 7 (use `G7 q` for dominant 7th chord)

## Getting Started

```bash
dotnet build
dotnet run --project src/SoundScript.Cli -- run examples/melody.ss
```

Or try the [Playground](https://soundscript.net/playground/).
