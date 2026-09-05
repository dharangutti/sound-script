# Temporal Visual Programs

SoundScript visual source describes events and state over continuous time. It
does not describe frames. A future renderer can sample the same program at 24,
30, or 60 FPS; the authored meaning stays unchanged.

```ss
sync audio
visual "intro" for 4s
wait 1s
visual "product" for 5s

visual "circle" for 5s at 0s {
    animate radius 20 -> 200 over 3s
}
```

The example describes this program:

```text
intro:   [0s, 4s)
wait:    [4s, 5s)
product: [5s, 10s)
circle:  [0s, 5s), radius(t) = 20 → 200 during its first 3s
```

`[start, end)` is a half-open interval, so `intro` is no longer active at
exactly `t = 4s`. That removes endpoint ambiguity and keeps adjacent cues
deterministic.

## Language surface

| Form | Meaning |
|------|---------|
| `visual "name" for 4s` | Add an interval at the current visual narrative cursor, then advance it. |
| `wait 1s` | Advance only that cursor. It creates no visual. |
| `visual "badge" for 2s at 6s` | Pin an absolute overlay without moving the narrative cursor. |
| `animate radius 20 -> 200 over 3s` | Add a local, deterministic linear property curve inside a visual block. |
| `sync audio` | Declare that the current visual cursor is an audio-clock synchronization marker. |

Seconds accept `s`, `sec`, `seconds`, or `ms`, and are stored as exact
`TimeSpan` ticks. Animation duration must fit inside its visual interval;
duplicate property curves on the same visual are rejected instead of guessed.

## Inspecting a program

The `visual` CLI verb produces a friendly temporal storyboard and can query
any instant without rendering a frame:

```bash
dotnet run --project src/SoundScript.Cli -- visual examples/visual-temporal.ssv \
  --at 0 --at 1.5 --at 4 --at 4.5 --at 5 --at 8.75
```

Expected highlights:

```text
t=0s => intro + circle(radius=20)
t=1.5s => intro + circle(radius=110)
t=4s => circle(radius=200)
t=5s => product
t=8s => product + sparkle(opacity=1)
t=8.75s => product
```

## Audio synchronization

The visual timeline shares `t = 0` with SoundScript audio. Its
`StateAtAudioBeat(beat, TempoAutomationMap)` bridge asks the existing,
tempo-ramp-aware `TempoAutomationMap` for elapsed milliseconds and evaluates
the same absolute visual clock. In other words, audio sample rate and video
FPS are both output samplers—not authoring primitives.

The initial capability intentionally stops before MP4/FFmpeg integration.
`SoundScript.Visual` is a small Core-only temporal rail, ready for future text,
procedural graphics, transitions, and renderer/export adapters.
