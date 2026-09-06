# Practical Audio/Visual compositions

SoundScript is an independent open-source audio and media programming language.
These six-second compositions apply its primitives, score notation, Wave
synthesis, and temporal model to communication and teaching. Each source is an
editable template, not a special diagram renderer.

Open **Playground → Audio/Visual** and choose a category in the selector. The
Example Library also groups the sources in collapsed sections. The default
example remains the original showcase.

| Composition | Useful outcome / style | Audio |
|---|---|---|
| [Team responsibilities](../examples/visual-org-chart.ssv) | Organization chart with staged teams; light business | Musical entries |
| [Request to delivery](../examples/visual-delivery-flow.ssv) | Process explanation with captions; warm | Synthetic speak cues |
| [Cache request sequence](../examples/visual-sequence-diagram.ssv) | Actor lifelines and message flow; technical dark | Request cues |
| [Service boundaries](../examples/visual-architecture.ssv) | Architecture with nested containers and connectors | Synthetic speak cues |
| [Signal conditioning](../examples/visual-block-diagram.ssv) | Block diagram with travelling signal | Musical stages |
| [Release milestones](../examples/visual-release-timeline.ssv) | Delivery timeline; minimal light | Milestone cues |
| [Service health](../examples/visual-status-dashboard.ssv) | Compact status snapshot with degraded service | Alert at 4s |
| [Deployment progress](../examples/visual-progress-dashboard.ssv) | Three coordinated progress bars; dark | Stage cues |
| [Support handoff](../examples/visual-information-cards.ssv) | Owner, priority and next action | Silent |
| [Latency comparison](../examples/visual-comparison.ssv) | Proportional before/after bars; light | Silent |
| [Device startup](../examples/visual-startup-explainer.ssv) | Instructional explainer with captions and symbols | Synthetic speak cues |
| [Experiment title sequence](../examples/visual-title-captions.ssv) | Three timed messages; presentation dark | Opening score |
| [Quarterly review](../examples/visual-presentation.ssv) | Three slide-like intervals; business | Synthetic speak cues |
| [Review and revision](../examples/visual-workflow.ssv) | Approval and feedback paths | Musical stages |
| [Dependency map](../examples/visual-network.ssv) | Network of components with a central highlight | Silent |
| [Incident response](../examples/visual-step-by-step.ssv) | Step-by-step process with border highlights | Musical stages |
| [Delivery scorecard](../examples/visual-kpi.ssv) | KPI values and targets; weekly snapshot | Silent |
| [Distance, speed and time](../examples/visual-education.ssv) | Educational motion and metre scale | One-second ticks |
| [Score, Wave and Voice](../examples/visual-mixed-audio.ssv) | Notes + voice/sing + speak + effect | Shared Wave mix |
| [48-service rollout](../examples/visual-scale-study.ssv) | Repeated widgets and six batches; dense scale study | Completion cue |

The first 18 cover the requested application types. Other natural applications
include release announcements, test-result briefings, reproducible experiment
comparisons, and staged onboarding. These authored snapshots do not connect to
live telemetry.

## Customize ordinary source

The [primitive/property tables](media-primitives.md) and
[temporal reference](visual-temporal.md) describe the complete implemented
surface. The only new visual syntax is `set`, a shorter constant curve:

```ss
visual "panel" for 6s at 0s {
    shape roundedRectangle
    fill "#17263c"
    stroke "#38bdf8"
    strokeWidth 2
    set x 640
    set y 360
    set width 320
    set height 120
}
visual "label" for 6s at 0s {
    shape text
    text "SERVICE READY"
    fontSize 28
    fill "#e2e8f0"
    set x 640
    set y 360
}
```

`set` accepts `x`, `y`, `width`, `height`, `size`, `radius`, `rotation`, and
`opacity`, case-insensitively. `set x 640` compiles exactly like
`animate x 640 -> 640 over 6s` inside a six-second visual. A property may be
set or animated once, not both. Appearance settings retain their existing
declarations. Unknown `set` properties are rejected; unknown numeric `animate`
curves retain their legacy inspectable behavior.

Edit text, colors, positions, dimensions, intervals and audio directly.
Coordinates are centres in a 1280×720 logical scene: 0–1 means normalized,
larger values mean pixels. For a left-anchored growing bar, move its x centre
by half the width increase. Lines/arrows use centre, width, and clockwise
rotation; 270 degrees points upward. Source numeric literals are non-negative.
The renderer can clamp signed values supplied through APIs, but the tokenizer
does not accept signed literals. The source has no comment syntax.

Cards combine separate panel, text and indicator intervals. Duplicate source
blocks and adjust their centres to create a row/grid. Visual nesting, named
visual components, shared style objects and parameterized visual templates have
not been added. Audio `block`/`loop` does not accept visual statements. The
optional maintainer helper `node scripts/build-av-examples.cjs` recreates and
overwrites these 20 files; it is not needed to run or edit them.

Painter order is start time, then source order. Draw backgrounds, connectors,
panels and labels in that order at a common start. Use an unfilled outline for a
later highlight to keep text readable. Consecutive half-open intervals change
captions. There is no explicit z-index or attached connector feature.

## Audio and synchronization

Independent note/voice tracks share t=0 with the visuals. At tempo 120,
`rest:4` advances a track two seconds; `at 2s` pins a visual there. `sync audio`
marks the narrative cursor; it does not retime the score. For tempo ramps,
`TemporalAudioRenderer.BuildTempoMap(program)` and `StateAtAudioBeat` inspect the
same clock used by the Wave adapter. Visual seconds remain independent of FPS.

Media reuses `WaveRenderer`, its track mixer, prosody generator and voice/sing
adapter. Notes, `voice { sing ... }`, `speak`, and Wave master effects coexist
in one AST. Preview and both exporters use the same fitted mono PCM: long audio
is trimmed and short audio is padded. Pause/scrub stops playback; Resume seeks
into that buffer.

MIDI-compatible scores still export with `run source.ssv output.mid`. Media
synthesizes their notes through Wave; it does not capture a MIDI device or
promise General MIDI instrument timbres. MIDI-only shaping/gain and SoundCSS
options are not all implemented by Wave. Existing Music, Timbre, Compose and
Prosody workflows remain available for those capabilities.

**Voice limitation:** `speak` and `sing` produce deterministic synthetic phoneme
audio, not natural narration. The four explainer trials retain complete captions
and use short synthetic cues. The separate browser Web Speech preview is not
captured in media export. External recordings, WordBank assets and sample paths
are not packaged into browser media by this exercise; missing samples are
skipped by the shared profile. These examples do not establish natural narrated
video support.

## Export

```bash
dotnet run --project src/SoundScript.Cli -- visual examples/visual-architecture.ssv --at 2 --at 4
dotnet run --project src/SoundScript.Cli -- video examples/visual-architecture.ssv --output architecture.webm --fps 24
```

Browser Export Clip and CLI video sample the same canonical scene and PCM.
CLI requires FFmpeg; the browser requires Canvas capture, Web Audio and
MediaRecorder. Browser encoding runs in real time and can miss deadlines under
load. Encoded bytes and antialiasing need not match. Prefer CLI for offline
batch production. Long, text-heavy clips materialize large plans; see the
[exercise report and reproducible checks](av-stress-test.md).
