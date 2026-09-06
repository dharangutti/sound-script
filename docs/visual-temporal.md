# Temporal Visual Programs

SoundScript visual source describes events and state over continuous time. It
does not describe frames. A downstream renderer can sample the same program at 24,
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
exactly `t = 3s`. That removes endpoint ambiguity and keeps adjacent cues
deterministic.

## Language surface

| Form | Meaning |
|------|---------|
| `visual "name" for 4s` | Add an interval at the current visual narrative cursor, then advance it. |
| `wait 1s` | Advance only that cursor. It creates no visual. |
| `visual "badge" for 2s at 6s` | Pin an absolute overlay without moving the narrative cursor. |
| `animate radius 20 -> 200 over 3s` | Add a local, deterministic linear property curve inside a visual block. |
| `set x 640` | Constant property; lowered to an equal-endpoint curve over the visual duration. |
| `sync audio` | Declare that the current visual cursor is an audio-clock synchronization marker. |

Seconds accept `s`, `sec`, `secs`, `second`, or `seconds`; milliseconds accept `ms`, `millisecond`, or `milliseconds`, and are stored as exact
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

## Video export

The Playground's **Export Clip** action is a downstream browser renderer. It
builds an immutable export plan by evaluating `VisualTimeline.StateAt(t)` at
24, 30, or 60 samples per second (30 is the default), projects each supplied
state into the canonical Playground scene profile, and rasterizes those same
primitives to a canvas. The browser uses the same deterministic SoundScript.Wave
PCM rail as the CLI video exporter, so the live stage, browser WebM, and CLI
WebM share visual layout, labels, opacity, and audio timing. Browsers with
Canvas capture and MediaRecorder support (Chrome, Edge, and Firefox) download
a playable WebM clip with audio and visuals.

```text
Authoring: VisualState = StateAt(t)
Rendering: Frame[n] = StateAt(n / outputFPS)
```

The second equation exists only in `SoundScript.Media`, the export-plan adapter.
There are no FPS, frames, frame tracks, codecs, or canvas concepts in the parser,
AST, `VisualTimeline`, or visual DSL. The encoded WebM bytes may vary by browser
and media encoder; the source timeline states and export plan are deterministic.

The command line can render the same plan into a real WebM through a deliberately
separate FFmpeg adapter:

```bash
dotnet run --project src/SoundScript.Cli -- video examples/visual-temporal.ssv \
  --output demo.webm --fps 30
```

The CLI rasterizer consumes only the canonical scene plan—never timeline
internals—and the shared SoundScript.Wave renderer supplies the same fitted PCM
audio as the browser. FFmpeg is required to encode VP9/Opus WebM; after
encoding, the command decode-verifies both streams. FFmpeg may vary encoded
bytes across versions, but the temporal states, scene primitives, and PCM
source remain deterministic.

## Playground playback

The Playground's Visual Timeline tab uses shared deterministic SoundScript.Wave
PCM for notes, voice/sing, speak and Wave effects. Its beat probe uses the Wave
adapter's tempo map, so Wave-only syntax can compile.
**Play** starts audio and a monotonic visual clock together; **Pause** stops both
while keeping the exact current `t`; **Resume** schedules audio from that time;
**Restart** returns to `t = 0`. Dragging or typing in the scrubber pauses
playback, evaluates `StateAt(t)` immediately, and leaves the next Play/Resume at
that same time.

The browser repaint interval is only a display mechanism. It is not stored in
the source, does not create authored frames, and does not change the temporal
meaning of the program. Audio seeking starts the same PCM buffer at the selected
offset, so sustained content and export audio cannot drift onto a second timing
model.

## Canonical presentation profile

The Media layer projects each sampled state into ordered primitives in a logical
1280×720 viewport. The Playground canvas, browser exporter, and CLI rasterizer
all consume these primitives. Known names (`intro`, `circle`, `product`, and
`sparkle`) use the demo's pill/orb/card/star treatment; other names use the
generic card fallback. `opacity` is applied uniformly, while `x`, `y`, `width`,
`height`, `size`, and `rotation` are renderer-profile properties. This keeps the
demo and exported clips visually aligned without adding frames or FPS to the
language.

## Supported presentation primitives and properties (V11)

### Explicit shapes and appearance

Visual blocks now accept optional `shape`, `fill`, `stroke`, `strokeWidth`,
`text`, and `fontSize` declarations alongside `animate`. The explicit shapes are
`rectangle`, `roundedRectangle`, `ellipse`, `circle`, `triangle`, `line`, `arrow`,
`ring`, and `text`. These are opt-in; existing names retain their presentations.
See [Media Primitives](media-primitives.md) for the complete grammar, defaults,
property ranges, portable text rules, and four new application demos.

### Legacy named treatments (without `shape`)

| Visual name (case-insensitive) | Presentation |
|---|---|
| `intro` | Pill labelled “A visual idea begins” |
| `circle` | Orb with radius-controlled bounds |
| `product` | Card labelled “PRODUCT” |
| `sparkle` | Star glyph |
| Any other quoted name | Generic card labelled with that name |

These are conventions for `visual "name"`, not separate shape keywords.
Arbitrary names do not load files or generate imagery.

| Animated property | Current renderer behavior |
|---|---|
| `opacity` | All primitives; clamped to 0–1, default 1 |
| `x`, `y` | Centre position; values from 0 to 1 are normalized, others are logical pixels |
| `width`, `height` | Clamped to 8–1280 and 8–720 logical pixels respectively |
| `size` | Fallback for each dimension when explicit width/height is absent |
| `radius` | Circle bounds only; clamped to 12–220, default 72; `size` is a fallback radius before dimension overrides |
| `rotation` | Degrees, clamped to −360–360 |

The logical viewport is 1280×720. Explicit width/height override bounds derived
from radius or size. Properties are case-insensitive. Other numeric property
curves can be stored and inspected, but the scene renderer does not display them.

Media constructs are top-level `visual`, `wait`, and `sync audio` statements.
A visual block accepts `animate property from -> to over duration`
directives (`→` is also accepted) and optional [static presentation settings](media-primitives.md).
Animated values interpolate linearly from the visual's
local start, then hold their target until the interval ends. For a constant
property, use `set x 0.5` or equal endpoints: `animate x 0.5 -> 0.5 over 2s`.
Durations must be positive; absolute placement may be zero. Each property
may appear once per visual, with a curve no longer than its visual interval.
Intervals with equal start times retain source order in the scene.

`sync audio` records a marker at the narrative cursor; it does not delay,
stretch, or retime the score. Tempo changes affect the beat-to-time bridge,
while visual durations remain seconds-based. The media PCM track is fitted
to the visual timeline: longer audio is trimmed, shorter audio is padded with
silence. Missing external sample files are skipped by the shared media profile.

This surface does not include image/video imports, arbitrary drawing commands,
visual loops or nested scenes, easing selectors, or authored frame tracks.
Output FPS and codecs belong to the exporter.

## Audio/Visual Example Library

Use the Playground's **Audio/Visual** selector, or **Example Library →
Audio/Visual** in Music & Wave. The original eight examples include piano scores. The four
original demos below are joined by [four shape-and-style application demos](media-primitives.md):

| Example | What to inspect |
|---|---|
| [Showcase](../examples/visual-temporal.ssv) | Sequential cues, waits, overlap, radius and opacity; 12 seconds |
| [Moving orb](../examples/visual-motion.ssv) | Position, radius, and rotating sparkle; 6 seconds |
| [Ready, Go, Done](../examples/visual-story.ssv) | Named cards, a one-second gap, width animation, and fades; 7 seconds |
| [Layered product cue](../examples/visual-overlays.ssv) | Overlap, dimensions, rotation, size, millisecond timing; 8 seconds |

See [20 practical compositions](audio-visual-compositions.md) for categorized
diagrams, dashboards, teaching examples and the limits of synthetic Voice/speak
audio in preview and export.

```bash
dotnet run --project src/SoundScript.Cli -- visual examples/visual-motion.ssv --at 3
dotnet run --project src/SoundScript.Cli -- video examples/visual-motion.ssv --output motion.webm --fps 30
```
