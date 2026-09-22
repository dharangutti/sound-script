# Tempo Automation

Linear tempo ramps with beat-accurate duration integration.

## Syntax

```ss
time 4/4
tempo 120 → 140 over 4 bars
```

- `→` (arrow) connects start and end BPM
- `over N bars` sets ramp duration in measures
- Multiple ramps chain sequentially at the top level

## Behavior

1. `TempoAutomationMap` stores linear ramps
2. Note durations integrate BPM changes beat-by-beat
3. MIDI tempo map exports discretized change points
4. In-track ramps schedule at the current global beat

## Tempo Map Diagram

```
BPM
140 ┤                    ╭────────
    │                  ╱
130 ┤               ╱
    │            ╱
120 ┤───────────╯
    └──────────────────────── beats
    0           8           16
         4 bars @ 4/4 = 16 beats
```

## Example

→ [examples/tempo-automation.ss](../examples/tempo-automation.ss)

```bash
dotnet run --project src/SoundScript.Cli -- run examples/tempo-automation.ss
```

## Related

- [language-reference.md](language-reference.md) — tempo syntax
- [pipeline.md](pipeline.md) — TempoAutomationMap in interpreter
