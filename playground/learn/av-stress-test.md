# Audio/Visual use-case exercise

## Plan and pre-change capability audit

Branch: `codex/audio-visual-use-case-stress-test`. Preserve `StateAt(t)`, exact
ticks, half-open intervals, existing audio interpreters, and renderer-only FPS.

1. Audit the existing implementations (matrix below).
2. Author all 18 requested compositions with existing syntax; compile and sample
   them before deciding on additions.
3. Record recurring friction, classify A–G, and implement only small shared fixes.
4. Integrate representative sources into the existing embedded Playground catalog.
5. Verify source → timeline → canonical scene → PCM → preview/export, run audio
   regressions, and measure a larger composition.
6. Publish supported syntax, use cases, measured results, and explicit limitations.

| Area | Existing implementation | Practical limit before this exercise |
|---|---|---|
| Primitives | Nine explicit shapes in `VisualPresentation`, shared polygon/glyph paths | No arbitrary paths, imports, or diagram-specific primitives |
| Styling | RGB fill/stroke, stroke width, text size, opacity | No shared style object or font family; source values are repeated |
| Position/dimensions | Centre x/y, width/height, size, radius | Constants require equal-endpoint `animate` curves; 0–1 means normalized |
| Motion | Linear local curves, rotation, opacity; target holds | No easing or delayed local curves; use separate intervals |
| Text | Shared 5×7 uppercase bitmap geometry, fitted single line | 120 ASCII characters; no rich text, wrapping, or multilingual glyphs |
| Timing | Absolute `at`, sequential intervals, `wait`, `sync audio` | Marker does not retime audio; seconds independent of beats |
| Layering | Stable start-time then source-order painter order | Later highlights can cover earlier labels; source order alone cannot fix it |
| Reuse/layout | Audio blocks, loops, sequences, patterns; manual visual arithmetic | Parser does not allow visuals in audio block bodies; no visual nesting/templates |
| MIDI | Existing interpreter, tempo map, MIDI generator, player | Media uses Wave synthesis of score notes, not recorded MIDI instruments |
| Wave/PCM | AST adapter, deterministic synthesis, track mixing, effects, fitted WAV | Media profile is mono; unsupported MIDI shaping remains unsupported by Wave |
| Voice | `VoiceNode`/`sing` handled by Wave; Voice has lyric/timing interpretation | Synthetic phoneme audio, not natural narration; Web Speech is a separate preview |
| Speak | Seeded prosody/phoneme tones and speech timing in Wave adapter | MIDI rejects `speak`; Playground incorrectly asks MIDI for visual tempo map |
| Timbre | Existing SoundCSS/MIDI timbre renderer, Wave phoneme timbres | SoundCSS rendering is a separate workflow, not automatically used by media |
| Prosody/Compose | Existing text-to-note and prosody generators, emitted source | Generated ordinary source can be combined with visuals; no new text engine needed |
| Multiple audio sources | Independent track/voice cursors and existing Wave mixer | External sample policy skips missing files; no cross-host asset packaging |
| Preview | Canonical scene canvas and deterministic WAV seek/pause/resume | UI updates sample state; browser scheduling is not hard real-time |
| Browser WebM | Sampled canonical scene + shared WAV, Canvas/MediaRecorder | Real-time encoding and browser support required; all samples materialized |
| CLI WebM | Same scene/PCM, software rasterizer, FFmpeg VP9/Opus and decode check | FFmpeg needed; glyph paths multiply export memory and raster work |
| Examples | 28 embedded `.ssv` presets plus extensive audio corpus | Practical compositions are grouped so the default view remains compact |

Audit sources: `Parser.ParseVisualStatement`, `VisualInterpreter`,
`VisualTimeline.StateAt`, `TemporalVisualSceneBuilder`, `TemporalShapeGeometry`,
`TemporalAudioRenderer`, `AstToNoteEventAdapter`, `VocalInterpreter`,
`VocalSpeechTimeline`, `WaveSpeechTimeline`, Playground playback and exporter JS.

No new visual abstraction is assumed by this plan. Audio loops/blocks do not
imply visual reuse: attempting `block card { visual ... }` is rejected by the
current block grammar. Templates initially mean editable ordinary `.ssv` files.

## Existing-construct experiment and decisions

Before language changes, all 20 new compositions passed 25 tests: parsing,
six-second duration, interval boundaries, arbitrary-time repeatability, canonical
preview/export scene equality, geometry bounds, deterministic PCM, rasterization,
and five speech-onset checks. The authoring helper's `--legacy` switch reproduces
that original equal-endpoint syntax. Initial authoring errors (unsupported comment
syntax and negative rotation) were resolved using plain source and equivalent
positive angles, without extending the tokenizer.

| Rank | Repeated friction | Class | Decision |
|---|---|---|---|
| 1 | Wave `speak` and `effect` compile for CLI video but fail in Playground's MIDI-based tempo probe (four explainers and mixed audio) | C/F | Expose the Wave adapter's existing tempo map; use it in the media bridge and Playground |
| 2 | Every fixed position/dimension repeats a value and duration in `animate` (all 20 compositions) | D | Add contextual `set property value`, lowered to the same constant `VisualAutomationNode` |
| 3 | Card = panel + text; repeated coordinates/styles (org chart, network, dashboards) | E | Keep editable source templates for now; no evidence that a new scene graph is worth its cost |
| 4 | Later filled highlights hide earlier text (workflow and incident response trials) | A/B | Use outline-only overlays and deliberate interval/source order; no z-index needed |
| 5 | Manual connector endpoints/turns (org chart, sequence, architecture, workflow) | B | Fixed layouts work with lines/arrows and positive rotation; no attachment or layout engine |
| 6 | Voice is synthetic rather than intelligible natural narration (four explainers) | B | Keep concise captions; honestly label synthetic speech, reuse Wave's existing engine |

The production changes are syntax sugar and a clock bridge. No diagram primitive,
CSS/theme engine, visual template language, mixer, or replacement audio engine is
introduced. The maintainer JS authoring helper is optional; committed `.ssv`
sources run independently and are the user-facing templates.
