# SoundScript Examples (v1.2)

Runnable example scripts in the `examples/` directory. Run any example with:

```bash
dotnet run --project src/SoundScript.Cli -- run examples/<name>.ss
```

## v1.2 Feature Examples

| File | Feature | Engine phase |
|------|---------|--------------|
| [melody.ss](../examples/melody.ss) | Basic melody and tempo | — |
| [rests.ss](../examples/rests.ss) | Rests | Phase 3 |
| [ties.ss](../examples/ties.ss) | Ties (`~`) | Phase 3 |
| [articulations.ss](../examples/articulations.ss) | Staccato, legato, accent | Phase 3 + 5 |
| [dynamics.ss](../examples/dynamics.ss) | `p`, `mp`, `mf`, `f` | Phase 3 + 5 |
| [chord-voicing.ss](../examples/chord-voicing.ss) | Low-root chord voicing | Phase 1 |
| [harmonic-spacing.ss](../examples/harmonic-spacing.ss) | Wide chord spacing | Phase 4 |
| [melodic-contour.ss](../examples/melodic-contour.ss) | Wide melodic leaps | Phase 4 |
| [phrase-smoothing.ss](../examples/phrase-smoothing.ss) | Sequence phrase boundaries | Phase 4 |
| [dynamic-ramping.ss](../examples/dynamic-ramping.ss) | Abrupt dynamic changes | Phase 4 |
| [multitrack-sync.ss](../examples/multitrack-sync.ss) | Multi-track alignment | Phase 1 |
| [playback-shaping.ss](../examples/playback-shaping.ss) | Full shaping pipeline | Phase 5 |

## Language Feature Examples

| File | Feature |
|------|---------|
| [durations.ss](../examples/durations.ss) | Duration syntax (`for`, `:`, aliases) |
| [instruments.ss](../examples/instruments.ss) | Instrument selection |
| [tempo-time.ss](../examples/tempo-time.ss) | Tempo and time signature |
| [chords.ss](../examples/chords.ss) | Chord progressions |
| [sequences.ss](../examples/sequences.ss) | Sequences and `play` |
| [loops.ss](../examples/loops.ss) | Loops |
| [velocity.ss](../examples/velocity.ss) | Velocity control |
| [multitrack.ss](../examples/multitrack.ss) | Multi-track blocks |

## Showcase

| File | Description |
|------|-------------|
| [full.ss](../examples/full.ss) | Combined v1.2 showcase |

## Playground Presets

The [browser playground](https://soundscript.net/playground/) includes presets matching these examples:

- Melody
- Articulations & Dynamics
- Chords
- Musical Intelligence
- Multi-track
- Playback Shaping

## Run All Examples

```bash
for f in examples/*.ss; do
  echo "=== $f ==="
  dotnet run --project src/SoundScript.Cli -- run "$f" /tmp/test.mid
done
```

## Related

- [language-reference.md](language-reference.md) — Syntax reference
- [whats-new-v1.2.md](whats-new-v1.2.md) — v1.2 changelog
