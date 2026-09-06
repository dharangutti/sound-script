# SoundScript Use Cases

SoundScript is an independent open-source audio and media programming language
for versioned musical scores, repeatable audio cues, and timed Audio/Visual programs.

## Audio/Visual programming

| Use case | Implemented building blocks | Runnable example |
|---|---|---|
| Short scored presentations | Sequential cues, waits, overlap, radius and opacity | [Showcase](../examples/visual-temporal.ssv) |
| Motion studies and teaching | Linear position and radius curves, exact scrubbing, piano audio | [Moving orb](../examples/visual-motion.ssv) |
| Timed instructions and story cards | Named cards, explicit gaps, resizing, fades | [Ready, Go, Done](../examples/visual-story.ssv) |
| Layered product cue prototypes | Card, orb, star, absolute placement, millisecond timing | [Layered product cue](../examples/visual-overlays.ssv) |

Load an example in the Playground's **Audio/Visual** selector or **Example
Library**, then play, pause, scrub, and export WebM. These are authored
timelines; external live data and event integrations must be supplied by a host
application. The renderer uses built-in visual treatments and named cards.
See [all supported constructs and properties](visual-temporal.md).

## Audio workflows
## More application options with explicit primitives

| Use case | Primitives | Runnable demo |
|---|---|---|
| Timed progress and loading presentations | Rectangle, rounded panel, text | [Progress indicator](../examples/visual-progress.ssv) |
| Process explanations and training | Panels, labels, arrows, dividers | [Process diagram](../examples/visual-process.ssv) |
| Direction and instructional cues | Rotating triangle, sequential captions | [Instruction sequence](../examples/visual-instructions.ssv) |
| Status and monitoring prototypes | Ring, circle, ellipse, status label | [Status display](../examples/visual-status.ssv) |

Set fills and outlines to suit the application, then animate position, dimensions,
opacity, and rotation. These examples play authored timelines; an application
must supply any external events or live data. Text uses a portable uppercase
bitmap font. See [the complete primitives reference](media-primitives.md).

## Audio workflows

- **Developers and music technology:** diffable compositions, reusable blocks,
  phrases, patterns, orchestration, MIDI, and direct WAV synthesis.
- **Education and research:** inspect notation, harmony, synthesis, and
  deterministic text-to-melody or word-prosody pipelines.
- **Vocals:** karaoke MIDI, WordBank vocal rendering, and recorded stem mixes.
- **Accessibility and industrial audio:** non-speech machine-state,
  conveyor-drift, motion, temperature-trend, and spatial cue prototypes.
  Explore the [Industrial Audio Toolkit](/industrial/).

See the [user guide](user-guide.md), [language reference](language-reference.md),
and [example catalog](examples.md) for the existing audio workflows.
