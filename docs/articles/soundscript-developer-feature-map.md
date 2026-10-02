# What If Your Next Media Asset Was a Pull Request?

*SoundScript, mapped by what a developer wants to build—not by release numbers.*

A notification sound needs changing. A visual cue needs moving. A melody needs a different instrument.

What would you rather review: another exported file, or the lines that explain what changed?

That is the idea behind **SoundScript**: audio and timed visuals authored as readable source, with a .NET toolkit for compiling, inspecting, and rendering them. The project is MIT-licensed, with local workflows and a client-side browser Playground.

This is not a tour of every API method. It is a map of the implemented capabilities: **what you can ask your code to do, and what is already there to help.**

*Feature snapshot: 2 October 2026. Based on the public site and project documentation; experimental capabilities are marked separately.*

## One value. Two senses.

Suppose a status indicator should become more visible **and** its cue louder as a value increases:

```text
param intensity = 0.25
perform expressive
tempo 120

track cue { gain intensity C4 q E4 q G4 h }

visual "status" for 4s {
    shape circle
    set opacity intensity
}
```

With that script in `source`, a .NET host can update the parameter without reparsing the source:

```csharp
using SoundScript;

var runtime = SoundScriptEngine.CompileRuntime(source);
runtime.Set("intensity", 0.90m);
var snapshot = runtime.Bind();

File.WriteAllBytes("status.wav", snapshot.RenderAudio());
var scene = snapshot.SceneAt(TimeSpan.FromSeconds(1));
```

One parameter influences something you hear and something you see. The host owns playback; each bound snapshot supplies a complete audio render and queryable visual state—not streaming synthesis.

That is the [adaptive runtime](https://soundscript.net/doc.html?p=runtime-parameters.md). Here is the wider map.

## 1. “I need a melody, not a recording session.”

Write musical intent directly: which notes, how long, how they relate, and who plays them.

```text
Compose music
├── Notes → pitches, accidentals, rests and ties
├── Rhythm → standard/numeric durations, dots, triplets/tuplets, grace notes
├── Musical time → time signatures, bar checks and tempo ramps
├── Harmony → basic/extended chords, inversions, drop/spread voicings
├── Patterns → ascending/descending arpeggios, strums and rhythms
├── Structure → tracks, melodies, sequences, loops and reusable blocks
├── Reuse → relative file imports and named sections
└── Arrangement → 128 GM programs, layers and orchestration helpers
```

The same [music language](https://soundscript.net/doc.html?p=language-reference.md) can describe a short application cue or a multi-track arrangement. General MIDI program selection is not a promise that every renderer has identical instrument realism.

## 2. “Correct notes. Now make them feel connected.”

Separate the written score from its performance: shape emphasis, phrasing, and timing without giving up repeatability.

```text
Shape the performance
├── Level → track gain, dynamics and per-note velocity
├── Articulation → legato, staccato, accent and detached phrases
├── Phrasing → curves, transitions, crescendos and decrescendos
├── Timing → swing, push, pull and deterministic humanization
├── Orchestration → octave doubling, bass reinforcement, brighter tops
└── Expressive mode → connected notes and supported audio envelopes
```

[`perform expressive`](https://soundscript.net/doc.html?p=performance-interpretation.md) is opt-in. MIDI and audio renderers preserve different parts of the performance; a MIDI export is not the same thing as exporting every audio envelope.

## 3. “I need an actual sound file—and control over its character.”

Choose a rendering path rather than treating MIDI and audio as interchangeable.

```text
Render and design sound
├── MIDI → standard musical-event files
├── Wave → direct mono/stereo WAV from scripts
│   ├── Effects → master delay and lowpass/highpass filters
│   ├── Recordings → WAV samples, vocal stems and timed overlays
│   └── Variation → repeatable timing/velocity and synthesis seeds
└── SoundCSS → style MIDI-to-WAV/OGG timbre
    ├── Tone → harmonics, brightness and harmonic rolloff
    ├── Voice color → formants, bandwidth, resonance and openness
    └── Texture → noise, transients and cycle/frame smoothing
```

Think of [SoundCSS](https://soundscript.net/doc.html?p=soundcss.md) as a stylesheet for sound character. The [Wave path](https://soundscript.net/doc.html?p=wave-grammar.md) also lets recordings and generated material share a mix.

## 4. “Can words become part of the composition?”

Words can supply melody, phrasing, lyric timing, synthetic cues, or recorded vocal material—different jobs, different paths.

```text
Work with words and vocals
├── Compose → text → syllables/phonemes → musical gestures
├── Prosody → word stress and phrase contour shape melody
├── Editable export → composed output returns to .ss source
├── Vocal tracks → lyrics bound to notes; karaoke MIDI events
├── Speak → deterministic synthetic phoneme/prosody cues
├── Vocal stems → generate one phrase or batch a script
├── WordBank → recorded words with rule-based/fallback engines
│   └── Corpus tools → normalization and opt-in missing-word generation
├── Word styling → delivery, accent, speed, pitch, energy and timbre
│   └── More controls → persona, emotion, breath, vibrato, age/gender presets
└── Continuous vocals → cross-word fades and pitch/formant smoothing
```

These [vocal and word-styling tools](https://soundscript.net/doc.html?p=soundcss.md) are not a promise of natural singing or a general-purpose neural text-to-speech system. Optional eSpeak workflows require a separate installation.

## 5. “My visuals should follow the same clock as my audio.”

Describe what should exist at a time, then query that time directly. Playback and export consume the resulting scene.

```text
Program timed visuals
├── Sequence → timed cues, waits and absolute overlapping placements
├── Synchronize → shared audio time and tempo-aware beat queries
├── Shapes → rectangle, rounded rectangle, ellipse, circle, triangle
│   └── Also → line, arrow, ring and text; built-in named treatments
├── Appearance → fills, strokes, text, font size and reusable styles
├── Motion → linear position, size, radius, rotation and opacity curves
├── Inspection → exact StateAt(t), SceneAt(t), duration and timeline
├── Scene output → typed primitives, versioned JSON and SVG
└── Clip output → synchronized WebM at 24, 30 or 60 FPS
```

The [media runtime](https://soundscript.net/doc.html?p=programmatic-media-runtime.md) is queryable, not just playable. Core visuals use built-in primitives; imported-footage composition belongs to VideoLab, below.

## 6. “My application state changes. The composition should respond.”

Keep the structure fixed; update the supported values. A warning cue does not need source-string replacement for every state change.

```text
Bind application state
├── Parameters → named decimals with defaults and discovered bounds
├── Audio binding → direct track gain in expressive mode
├── Visual bindings → x, y, width, height, rotation and opacity
├── State operations → get, set, atomic batch updates and reset
├── Isolation → independent instances from one compiled structure
├── Snapshots → earlier bindings stay stable after later updates
├── Outputs → audio, MIDI and scenes from the bound snapshot
└── Tools → CLI overrides and generated Playground controls
```

[Runtime parameters](https://soundscript.net/doc.html?p=runtime-parameters.md) do not dynamically rewrite notes, tempo, timing, imports, or track structure. This is controlled adaptation, not unrestricted live coding.

## 7. “I have a recording. Give me something editable.”

Start with suitable audio, inspect the evidence, and edit the recovered source. Analysis is an estimate—not the original score rediscovered with certainty.

```text
Transcribe audio
├── Monophonic → the supported single-melody baseline
├── Extract melody [experimental] → estimate a dominant line
├── Polyphonic / piano [experimental] → estimate simultaneous notes
├── Mixed roles [experimental] → symbolic melody/harmony/bass estimates
├── Percussion / rhythm [experimental] → unpitched rhythm events
├── Inputs → WAV; MP3, MP4, M4A, WebM, Ogg, FLAC, AAC and MOV*
├── Evidence → suitability checks, diagnostics and reconstruction analysis
└── Workflow → editable .ss, playback, WAV and analysis export
```

The [transcription subsystem](https://soundscript.net/doc.html?p=transcription.md) analyzes clips up to 120 seconds. *Desktop compressed-media decoding uses FFmpeg; browser support depends on available codecs.* Mixed-role estimates are **not separated audio stems**.

Experimental percussion also has a Wave-only `hit` construct for kick, snare, hat, and click events; MIDI mapping is not implemented.

## 8. “Let me explore without building an entire application first.”

The Playground brings source editing, musical audition, media inspection, and exports into one browser workflow.

```text
Author and explore
├── Source conveniences → comments, constants, arithmetic and time markers
├── Visual reuse → named styles with local overrides
├── Editor → highlighting, formatting, completion and hover help
├── Navigation → outlines, bracket selection and find/replace
├── Refactoring → constant rename, transpose and duration edits
│   └── Visual helpers → color editing and visual duplication
├── Diagnostics → clickable errors/warnings and structural summaries
├── Audition → selected ranges, mute/solo and note/instrument previews
├── Playback → play, pause, resume, restart and timeline scrubbing
└── Workspaces → Music & Wave, Audio/Visual, adaptive media, transcription
```

[Authoring tools](https://soundscript.net/doc.html?p=authoring.md), example presets, source downloads, and copyable CLI equivalents help turn experiments into reusable files. File imports remain a CLI or filesystem-capable host workflow, not a browser feature.

## 9. “Now put it in my application—or my build.”

Use the library when media belongs inside a .NET process; use the CLI when it belongs in a shell pipeline.

```text
Integrate and automate
├── .NET 10 → local Windows, macOS and Linux workflows
├── Library → compile strings or trusted source files
│   ├── Render → MIDI, mono/stereo WAV and synchronized media
│   └── Inspect → warnings, timelines, scenes, JSON and SVG
├── CLI: music → run, compose, prosody, render, wave
├── CLI: media → visual, video, transcribe
├── CLI: validation → validate, inspect
├── CLI: vocals → vocal generate/batch, wordbank ensure/normalize
├── Automation → JSON diagnostics, source locations and exit codes
├── Export operations → FFmpeg preflight, progress, parallel frames, cancel
└── Examples → test fixtures, DevOps cues and monitoring applications
```

The [.NET package](https://soundscript.net/doc.html?p=dotnet-api.md) does not shell out to the CLI for ordinary compilation. The [CLI](https://soundscript.net/doc.html?p=cli.md) supports source-checkout workflows; do not confuse the library package with a separately published CLI tool.

## Beyond the main product: the Labs bench

These are implemented experiments, **not stable features bundled into the supported SoundScript package**.

```text
SoundScript Labs
├── VideoLab — published experimental MVP
│   ├── Compose → video, audio, shapes, titles and callouts
│   ├── Edit → trims, sequences, crossfades and transforms
│   ├── Program → expressions, easing and reusable effects
│   ├── Vary → typed parameters, conditions, groups and audience versions
│   ├── Generate → data-driven sequences and parameterized batches
│   ├── Annotate → external review datasets become visible notes/markers
│   ├── Inspect/export → exact frame state; MP4 and WebM
│   └── Access → public verified demos; private local workbench for files
├── Signal Lab — preserved command-line proof of concept
│   ├── Generate → sine, square, chirp and sweep signals
│   ├── Analyze → FFT, spectral peaks, RMS and peak-frequency estimates
│   ├── Validate → units, sample budgets and Nyquist constraints
│   └── Export → JSON, WAV, PCM16 and Float32 with provenance/hashes
└── Runtime Binding Lab — preserved research prototype
    └── Compile-once media and immutable bindings; no public runnable app
```

[VideoLab](https://soundscript.net/labs/videolab/) is a bounded composition experiment, not a full nonlinear editor. Personal files and arbitrary supported bindings use its local .NET/FFmpeg workbench. [Signal Lab](https://github.com/dharangutti/sound-script/blob/codex/soundscript-labs-poc/experiments/SoundScript.Labs/README.md) simulates signals; it does not capture from or control hardware. The [Labs index](https://soundscript.net/labs/) records each experiment’s publication status.

## The feature underneath the features

For me, the interesting part is not just that code can produce a sound or a clip.

It is that **the explanation of the media can live beside the code that uses it**.

A changed note is a diff. A parameter update is data. A scene at two seconds is something an application can inspect. A generated cue can become a reproducible test fixture.

One important boundary: repeatability assumes fixed source, assets, options, and renderer versions. It is not a blanket promise that encoded video—or transcription through different decoders—will have identical bytes everywhere.

Start with one small requirement: a success cue, a timed status display, a repeatable media fixture, or a short solo melody to transcribe.

Then ask a different question:

**What should this media do—and which parts should be code?**

[Try the Playground](https://soundscript.net/playground/) · [Read the documentation](https://soundscript.net/doc.html?p=documentation.md) · [Explore the source](https://github.com/dharangutti/sound-script) · [Visit Labs](https://soundscript.net/labs/)
