# Example learning guide

All 79 sources in `examples/` and five downloadable website demos retain their
filenames and compiled behavior. The 39 embedded Playground and SoundCSS examples
also have purpose comments and compilation coverage. Short documentation snippets
are explanations or fragments; the runnable sources are indexed here.

Levels describe what to learn, not a required backend. Some Visual exercises
include a music track, and larger compositions combine several topics.

## Choosing an entry point

Begin with literal pitches and durations. Move on to sequences, blocks, and
tracks before trying the larger arrangements. [Authoring conveniences](authoring.md)
are optional: use named values when their shared meaning helps an edit, and keep
short notation visible when it teaches the feature more directly.

- `.ss`: use the CLI `run` command for MIDI; `full-song-wave.ss` and
  `speech-only-wave.ss` demonstrate the `wave` command.
- `.ssw`: use `wave`. The vocal-stem examples also have preparation commands in
  the [example catalog](examples.md#v8-offline-vocal-stems-cli).
- `.ssv`: use `visual` for timeline inspection or `video` for export; CLI
  video encoding requires FFmpeg. The Playground loads these same source files.
- `.ssc`: use as the stylesheet for `render`; it is SoundCSS, not a musical score.
- `import-lib.ss` supplies definitions to the importing examples and has no
  standalone performance. Browser presets keep their definitions inline.

## Timing and backend details

Music durations and rests are in beats. Visual durations and markers use seconds
or milliseconds. At 120 BPM, four beats equal two seconds; the mixed-audio example
names the beat delays and visual entry times separately to make that relationship
visible. Changing tempo alone will not retime a visual marker.

Sequential visual cues advance the cursor, `wait` creates a gap, and explicit
`at` placement permits independent overlap. `sync audio` records an audio
synchronization point; it does not generate music. Several visual-only dashboards
therefore have a silent audio rail. Fixed animations in the earlier visual examples
are retained to preserve their existing timelines.

Wave renders notes, pitched lyrics, and seeded synthetic speech. Some MIDI
instrument and phrase-shaping directives do not shape Wave output. WordBank and
external vocal-stem workflows require their documented render options;
`speak` alone does not select a natural voice. The bundled
`vocal-stems/hello-world.wav` is a demonstration tone.

Named `speak` parameters such as `gain=1.1` currently require literals.
The review preserves them instead of extending the language. The short
`melody.ss` and `tempo-time.ss` exercises retain their existing measure-warning
behavior; meter and bar separators do not silently rewrite durations.

## Beginner

| Source | Demonstrates |
|---|---|
| [authoring.ss](../examples/authoring.ss) | name a tempo and a rest length with compile-time constants. |
| [blocks.ss](../examples/blocks.ss) | define named musical sections and play them in order. |
| [chords.ss](../examples/chords.ss) | combine major, minor, and seventh chords into a progression. |
| [durations.ss](../examples/durations.ss) | compare explicit beat lengths, colon notation, and a default duration. |
| [dynamics.ss](../examples/dynamics.ss) | change dynamic markings as a melody rises. |
| [instruments.ss](../examples/instruments.ss) | choose a piano instrument for a short melody. |
| [loops.ss](../examples/loops.ss) | repeat a two-note motif a fixed number of times. |
| [melody.ss](../examples/melody.ss) | write pitches, short duration symbols, and a bar separator in a melody. |
| [multitrack.ss](../examples/multitrack.ss) | play a melody and bass together on independent tracks. |
| [rests.ss](../examples/rests.ss) | place explicit silence between notes. |
| [sequences.ss](../examples/sequences.ss) | reuse named note sequences in a chosen order. |
| [tempo-time.ss](../examples/tempo-time.ss) | declare tempo and meter independently of note durations. |
| [velocity.ss](../examples/velocity.ss) | combine a default velocity with individual note overrides. |
| [wave-speak.ssw](../examples/wave-speak.ssw) | render a short phrase as seeded synthetic speech tones through Wave. |

## Music

| Source | Demonstrates |
|---|---|
| [articulations.ss](../examples/articulations.ss) | compare staccato, legato, and accent in prefix and suffix notation. |
| [chord-voicing.ss](../examples/chord-voicing.ss) | compare chord registers and the resulting voicings. |
| [dynamic-ramping.ss](../examples/dynamic-ramping.ss) | demonstrate playback shaping across changes in dynamic level. |
| [harmonic-spacing.ss](../examples/harmonic-spacing.ss) | demonstrate harmonic spacing across seventh chords. |
| [humanization.ss](../examples/humanization.ss) | apply deterministic timing and velocity variation to a short piano line. |
| [layers.ss](../examples/layers.ss) | play one chord progression through piano and cello layers. |
| [melodic-contour.ss](../examples/melodic-contour.ss) | demonstrate register shaping across large melodic leaps. |
| [metadata.ss](../examples/metadata.ss) | contrast track gain and humanization settings with a dry piano track. |
| [multitrack-sync.ss](../examples/multitrack-sync.ss) | align melody, harmony, and bass on the same four-beat timeline. |
| [patterns.ss](../examples/patterns.ss) | apply arpeggio, strum, and rhythm patterns to chords. |
| [phrase-smoothing.ss](../examples/phrase-smoothing.ss) | smooth a phrase transition around a reusable block and a register change. |
| [phrases.ss](../examples/phrases.ss) | compare smooth and abrupt phrase transitions. |
| [playback-shaping.ss](../examples/playback-shaping.ss) | combine articulation, dynamics, and a chord to demonstrate playback shaping. |
| [tempo-automation.ss](../examples/tempo-automation.ss) | declare a four-bar tempo ramp over a sustained note. |
| [ties.ss](../examples/ties.ss) | join repeated pitches into sustained notes with ties. |

## Visual

| Source | Demonstrates |
|---|---|
| [visual-comparison.ssv](../examples/visual-comparison.ssv) | before and after latency bars using the same scale. |
| [visual-information-cards.ssv](../examples/visual-information-cards.ssv) | reusable cards for a staged support-handoff brief. |
| [visual-kpi.ssv](../examples/visual-kpi.ssv) | staged metrics and targets in an authored scorecard snapshot. |
| [visual-motion.ssv](../examples/visual-motion.ssv) | animate position and radius with a rotating overlay over a piano score. |
| [visual-network.ssv](../examples/visual-network.ssv) | shared dependencies and an animated outline highlight. |
| [visual-overlays.ssv](../examples/visual-overlays.ssv) | overlap three cues and place a short sparkle using milliseconds. |
| [visual-story.ssv](../examples/visual-story.ssv) | sequence Ready, Go, and Done cards with an intentional gap and fades. |

## Audio-Visual

| Source | Demonstrates |
|---|---|
| [visual-architecture.ssv](../examples/visual-architecture.ssv) | service boundaries, routed connectors, and synthetic speech cues. |
| [visual-block-diagram.ssv](../examples/visual-block-diagram.ssv) | a moving signal through three processing blocks. |
| [visual-delivery-flow.ssv](../examples/visual-delivery-flow.ssv) | process stages and captions aligned with synthetic speech cues. |
| [visual-education.ssv](../examples/visual-education.ssv) | distance, speed, and time illustrated with one-second musical ticks. |
| [visual-instructions.ssv](../examples/visual-instructions.ssv) | coordinate three timed instructions with a rotating direction cue. |
| [visual-org-chart.ssv](../examples/visual-org-chart.ssv) | staged team cards and reporting lines with musical entry cues. |
| [visual-presentation.ssv](../examples/visual-presentation.ssv) | three timed slides with captions and synthetic speech cues. |
| [visual-process.ssv](../examples/visual-process.ssv) | connect two labelled panels with an animated arrow over music. |
| [visual-progress-dashboard.ssv](../examples/visual-progress-dashboard.ssv) | three progress bars that complete in sequence. |
| [visual-progress.ssv](../examples/visual-progress.ssv) | build a growing progress bar with a panel and portable text over music. |
| [visual-release-timeline.ssv](../examples/visual-release-timeline.ssv) | release milestones introduced with musical cues. |
| [visual-sequence-diagram.ssv](../examples/visual-sequence-diagram.ssv) | request and response arrows along three actor lifelines. |
| [visual-startup-explainer.ssv](../examples/visual-startup-explainer.ssv) | timed startup symbols and captions with synthetic speech. |
| [visual-status-dashboard.ssv](../examples/visual-status-dashboard.ssv) | a service-health snapshot with a delayed alert cue. |
| [visual-status.ssv](../examples/visual-status.ssv) | combine animated status indicators with a rotating badge and label. |
| [visual-step-by-step.ssv](../examples/visual-step-by-step.ssv) | coordinated captions and highlights for incident-response stages. |
| [visual-temporal.ssv](../examples/visual-temporal.ssv) | combine sequential cues, a wait, pinned overlays, and reusable authoring values. |
| [visual-title-captions.ssv](../examples/visual-title-captions.ssv) | a three-part title sequence over a short musical opening. |
| [visual-workflow.ssv](../examples/visual-workflow.ssv) | an approval workflow with an explicit revision path. |

## Advanced

| Source | Demonstrates |
|---|---|
| [advanced-chords.ss](../examples/advanced-chords.ss) | compare drop voicing, inversion, and spreading on the same chord. |
| [full-v2-showcase.ss](../examples/full-v2-showcase.ss) | combine imports, patterns, layers, tempo automation, and orchestration. |
| [full.ss](../examples/full.ss) | arrange imported material with phrases, patterns, layers, and chord helpers. |
| [orchestration.ss](../examples/orchestration.ss) | enrich a chord with octave doubling, bass reinforcement, and top-note emphasis. |
| [phrases-v3.ss](../examples/phrases-v3.ss) | combine phrase curves, transitions, envelopes, articulation, and swing. |
| [visual-scale-study.ssv](../examples/visual-scale-study.ssv) | 48 service cards with six timed batches for timeline and export inspection. |

## Integration / Export

| Source | Demonstrates |
|---|---|
| [default.ssc](../examples/default.ssc) | shape plosive and vowel timbres for offline SoundCSS rendering. |
| [full-song-wave.ss](../examples/full-song-wave.ss) | render a reusable hook, harmony, bass, and sung voice through Wave. |
| [import-lib.ss](../examples/import-lib.ss) | provide shared definitions for the import examples; no notes play on their own. |
| [imports.ss](../examples/imports.ss) | resolve a local library and reuse its melody and bass definitions. |
| [industrial-blind-assist.ss](../examples/industrial-blind-assist.ss) | sketch contrasting spatial-awareness cues through register, articulation, and dynamics. |
| [industrial-conveyor-drift.ss](../examples/industrial-conveyor-drift.ss) | contrast swing, push, and pull as conveyor-drift audio cues. |
| [industrial-machine-state.ss](../examples/industrial-machine-state.ss) | distinguish idle, running, and critical states with reusable musical cues. |
| [industrial-robotic-arm.ss](../examples/industrial-robotic-arm.ss) | represent approach, grip, and release with swell, accent, and fade. |
| [industrial-temperature-trend.ss](../examples/industrial-temperature-trend.ss) | represent rising, stable, and falling temperature with melodic phrases. |
| [jingle-bells-vocal.ssw](../examples/jingle-bells-vocal.ssw) | combine a song arrangement with speech phrases for offline vocal-stem rendering. |
| [jingle-bells-wordbank.ssw](../examples/jingle-bells-wordbank.ssw) | arrange Jingle Bells for the WordBank vocal-stem workflow. |
| [phase8-wordbank-vocal.ssw](../examples/phase8-wordbank-vocal.ssw) | exercise corpus-backed words and an uncommon-word fallback in vocal generation. |
| [speech-only-wave.ss](../examples/speech-only-wave.ss) | render sung lyrics and synthetic speech over a chord pad without a MIDI step. |
| [visual-mixed-audio.ssv](../examples/visual-mixed-audio.ssv) | score notes, voice, speech, and an effect in one timed composition. |
| [vocal-song.ss](../examples/vocal-song.ss) | align sung lyric syllables with pitches over a piano accompaniment. |
| [wave-effects.ssw](../examples/wave-effects.ssw) | combine seeded humanization, synthetic speech, delay, and filtering. |
| [wave-humanize.ssw](../examples/wave-humanize.ssw) | render repeatable timing and velocity variation with synthetic speech. |
| [wave-vocal-stem.ssw](../examples/wave-vocal-stem.ssw) | mix a bundled vocal-stem sample with a chord pad. |

## Downloadable website demos

These are separate published-source variants; their arrangements and visual
appearance remain distinct from similarly named library examples.

| Source | Demonstrates |
|---|---|
| [orchestra.ss](assets/demos/orchestra.ss) | layer violin, cello, and piano tracks in the website orchestra demo. |
| [piano.ss](assets/demos/piano.ss) | shape the website piano demo with a gentle, legato phrase. |
| [visual-temporal.ssv](assets/demos/visual-temporal.ssv) | demonstrate sequential timing, pinned overlays, and fades in the website export demo. |
| [vocal-midi.ss](assets/demos/vocal-midi.ss) | export pitched lyrics with a piano accompaniment as MIDI. |
| [vocal.ssw](assets/demos/vocal.ssw) | provide the website song source for WordBank vocal-stem rendering. |

## Embedded Playground examples

The main selector includes 20 music presets, including the full MIDI Jingle Bells
arrangement. The Wave selector supplies ten presets, including the full Wave
arrangement, effects, speech, and vocal workflows. The initial script is another
small layered phrase. Studio supplies three paired Wave/SoundCSS examples; the
word-style syntax example and default timbre stylesheet complete the 39 embedded
sources. Each keeps its existing selection key and behavior.

## Maintenance and compatibility

The larger org-chart, information-card, and scale-study examples share card
styles. The org-chart and scale-study name reusable values and visual entry times;
mixed-audio names beat delays and visual markers. The full-song Wave example names
its repeated tempo. Existing authoring and temporal examples retain their current
`let`, `marker`, `style`, and `use` demonstrations.

The other sources retain literal notation, with purpose comments and selected
intent notes. Beginner examples remain short; feature-specific examples keep the
feature being taught visible. Sample assets and pre-rendered media are unchanged.
The generated visual library can be reproduced with
`node scripts/build-av-examples.cjs`; its comments and shared styles are emitted
by the same helper.

Run `dotnet test SoundScript.sln` to validate the whole repository. The
[example regression tests](../src/SoundScript.Tests/ExampleCompilationTests.cs)
compile all 123 sources and compare their lowered programs against the original
[compatibility baselines](../src/SoundScript.Tests/Golden/example-programs.md).
Existing deterministic output and timeline tests remain in place.
