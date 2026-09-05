# Temporal Visual Programs

SoundScript visual source describes events and state over continuous time. It
does not describe frames. A future renderer can sample the same program at 24,
30, or 60 FPS; the authored meaning stays unchanged.

```ss
tempo 120

track music {
    instrument piano
    mf
    C4 h E4 h G4 h C5 h
    G4 h E4 h C4 h G4 h
    C5 h G4 h E4 h C4 h
}

sync audio
visual "intro" for 3s
wait 1s
visual "product" for 4s

visual "circle" for 8s at 0s {
    animate radius 28 -> 170 over 3s
    animate opacity 0.35 -> 1 over 1.5s
}

visual "sparkle" for 2s at 4.5s {
    animate opacity 0 -> 1 over 0.4s
}

visual "outro" for 4s {
    animate opacity 1 -> 0 over 2s
}
```

The example describes this program:

```text
audio:   MIDI score at 120 BPM, sharing the same t = 0
intro:   [0s, 3s)
wait:    [3s, 4s)
product: [4s, 8s)
circle:  [0s, 8s), radius(t) = 28 → 170 during its first 3s
sparkle: [4.5s, 6.5s)
outro:   [8s, 12s), opacity fades during its first 2s
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
t=0s => intro + circle(radius=28, opacity=0.35)
  t=1.5s => intro + circle(radius=99, opacity=1)
t=3s => circle(radius=170, opacity=1)
t=4s => product + circle(radius=170, opacity=1)
t=4.5s => product + circle + sparkle(opacity=0)
t=5s => product + circle + sparkle(opacity=1)
t=8.75s => outro(opacity=1)
t=12s => no visual (the final interval is half-open)
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

## Playground playback

The Playground's Visual Timeline tab compiles the source once, then uses the
existing local MIDI/Web Audio player for the `track music` rail. **Play** starts
audio and a monotonic visual clock together; **Pause** stops both while keeping
the exact current `t`; **Resume** schedules audio from that time; **Restart**
returns to `t = 0`. Dragging or typing in the scrubber pauses playback, evaluates
`StateAt(t)` immediately, and leaves the next Play/Resume at that same time.

The browser repaint interval is only a display mechanism. It is not stored in
the source, does not create authored frames, and does not change the temporal
meaning of the program. Audio seeking is implemented by rescheduling the
remaining MIDI notes from the selected offset; sustained notes are restarted
from their remaining duration.
