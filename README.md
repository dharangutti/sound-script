# SoundScript

An open-source, deterministic music language that turns simple text into professional-sounding MIDI.  
Built in C# — runs on Windows, macOS, and Linux, with a browser playground that works in any modern browser (Chrome, Edge, Firefox, Safari).

```
import "lib.ss"

block intro {
    phrase {
        mf
        play arp Cmaj q
    }
}

pattern arp { up }

track melody {
    layer piano
    layer cello
    gain 0.9
    humanize 0.02
    play intro
}
```

**Text → import loader → tokenizer → parser → AST → interpreter → shaping → MIDI.**

## What is SoundScript?

SoundScript is a micro-language for writing music like code. Describe notes, chords, dynamics, patterns, and orchestration in plain text; the engine parses your script, applies musical intelligence and playback refinement, and emits a standard MIDI file. The same script always produces the same MIDI — on every platform.

## Vision

Music should be as writable, versionable, and reproducible as software. SoundScript makes sound a first-class engineering artifact:

- **Deterministic by design** — the same script yields bit-identical MIDI, every run, everywhere. Compositions belong in version control.
- **General-purpose, not a toy** — from melody sketches and full arrangements to [expressive industrial audio cues](https://soundscript.net/industrial/) for machine states, robotics, and accessibility workflows.
- **Runs everywhere** — a cross-platform .NET CLI (Windows, macOS, Linux) and a WebAssembly playground that works in any modern browser. SoundScript is not tied to any single browser or vendor.
- **Minimal surface** — no DAW, no plugins, no server. Plain text in, standard MIDI out.

## Platform Support

| Surface | Requirements |
|---------|--------------|
| **CLI** (`SoundScript.Cli`) | .NET 8 SDK — Windows, macOS, Linux |
| **Playground** (`SoundScript.Playground`) | Any modern browser with WebAssembly and Web Audio — Chrome, Edge, Firefox, Safari (desktop and mobile). Runs fully client-side; no account, no server, no installation. |

## V2 Overview

V2 extends the v1.2 five-phase engine with compositional and production features:

| Feature | Syntax |
|---------|--------|
| **Imports** | `import "lib.ss"` |
| **Blocks** | `block intro { }` + `play intro` |
| **Metadata** | `gain 0.9`, `humanize 0.03` |
| **Tempo automation** | `tempo 120 → 140 over 4 bars` |
| **Layers** | `layer piano` / `layer cello` |
| **Humanization** | Deterministic timing + velocity jitter |
| **Advanced chords** | `Cmaj drop2`, `inv1`, `spread` |
| **Phrases** | `phrase { curve soft ... }` · [V3 aliases & envelopes](docs/phrases-v3.md) |
| **Patterns** | `pattern arp { up }` + `play arp Cmaj q` |
| **Orchestration** | `double octave`, `reinforce bass`, `brighten top` |

All v1.2 syntax remains valid.

## Interpreter Pipeline (V2)

```
DSL script
    ↓
ProgramLoader (imports)
    ↓
Tokenizer → Parser → AST   ←── PhonemeComposer (V3.1: plain text → syllables → phonemes → gestures → AST)
    ↓
Interpreter
    ├── PatternExpander (pattern play)
    ├── Chord: Voicing → AdvancedVoicing → Orchestration → Spacing
    ├── Note: Intelligence → PhraseTimingShaper (V3) → PhraseShaper → PlaybackShaper
    ├── Layers (per-channel shaping)
    └── HumanizeApplicator (post-pass)
    ↓
MidiGenerator → output.mid
```

→ [docs/pipeline.md](docs/pipeline.md) · [docs/architecture.md](docs/architecture.md)

## V2 Examples

| Example | Demonstrates |
|---------|--------------|
| [examples/imports.ss](examples/imports.ss) | Multi-file imports |
| [examples/blocks.ss](examples/blocks.ss) | Named blocks |
| [examples/metadata.ss](examples/metadata.ss) | Gain + humanize |
| [examples/tempo-automation.ss](examples/tempo-automation.ss) | Tempo ramps |
| [examples/layers.ss](examples/layers.ss) | Instrument layers |
| [examples/humanization.ss](examples/humanization.ss) | Deterministic jitter |
| [examples/advanced-chords.ss](examples/advanced-chords.ss) | drop2, inv1, spread |
| [examples/phrases.ss](examples/phrases.ss) | Phrase engine v2 |
| [examples/phrases-v3.ss](examples/phrases-v3.ss) | Phrase engine v3 |
| [examples/patterns.ss](examples/patterns.ss) | Arp, strum, rhythm |
| [examples/orchestration.ss](examples/orchestration.ss) | Orchestration helpers |
| [examples/full-v2-showcase.ss](examples/full-v2-showcase.ss) | Combined V2 demo |
| [examples/industrial-blind-assist.ss](examples/industrial-blind-assist.ss) | Industrial cue — blind operator spatial awareness |
| [examples/industrial-machine-state.ss](examples/industrial-machine-state.ss) | Industrial cue — machine states (idle / running / critical) |
| [examples/industrial-conveyor-drift.ss](examples/industrial-conveyor-drift.ss) | Industrial cue — conveyor timing drift |
| [examples/industrial-temperature-trend.ss](examples/industrial-temperature-trend.ss) | Industrial cue — temperature trend |
| [examples/industrial-robotic-arm.ss](examples/industrial-robotic-arm.ss) | Industrial cue — robotic arm motion phases |
| [examples/vocal-song.ss](examples/vocal-song.ss) | Vocal track — lyrics bound to pitches via phonetics |
| [examples/default.ssc](examples/default.ssc) | SoundCSS timbre stylesheet (V4) |
| [examples/wave-effects.ssw](examples/wave-effects.ssw) | Wave grammar — combined humanize + speak + effects |
| [examples/wave-speak.ssw](examples/wave-speak.ssw) | Wave grammar — `speak` prosody tones |
| [examples/wave-humanize.ssw](examples/wave-humanize.ssw) | Wave grammar — seeded humanize + speak |
| [examples/full-song-wave.ss](examples/full-song-wave.ss) | Four-part song rendered via the wave backend |
| [examples/speech-only-wave.ss](examples/speech-only-wave.ss) | Speech + vocal song without a MIDI step |
| [examples/wave-vocal-stem.ssw](examples/wave-vocal-stem.ssw) | V8: `speak sample=` vocal stem mixing |
| [examples/jingle-bells-vocal.ssw](examples/jingle-bells-vocal.ssw) | V8: Jingle Bells + offline vocal stems |

→ [docs/examples.md](docs/examples.md)

```bash
dotnet run --project src/SoundScript.Cli -- run examples/full-v2-showcase.ss
```

## v1.2 Foundation

| Phase | Focus |
|-------|--------|
| **Phase 2** | Notation engine |
| **Phase 3** | Expressive notation |
| **Phase 1** | Stabilization |
| **Phase 4** | Musical intelligence |
| **Phase 5** | Playback quality |

→ [docs/whats-new-v1.2.md](docs/whats-new-v1.2.md)

## Text-to-Melody (PhonemeComposer)

V3.1 adds a deterministic **text-to-melody engine**: give the CLI a plain
English string and it composes a melody from the sounds of the words — no
script required, no randomness, no audio synthesis.

```bash
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star"
```

```
Composed 7 syllable(s) into 24 note(s) to output.mid at 96 BPM.
```

```
Text → Syllables → Phonemes → Gestures → AST → MIDI
       Syllabifier  PhonemeSplitter  PhonemeMapper  PhraseAssembler  MidiGenerator
```

Each syllable becomes a musical micro-phrase: plosives map to staccato notes,
nasals to swells, fricatives to fades, liquids to accents, vowels to legato
pitches. The composer builds a standard AST and reuses the existing interpreter
and MIDI generator, so identical text always produces byte-identical MIDI —
verified by SHA-256. Use `--append file.ss` to add the composed track to an
existing script's output.

→ [docs/text-to-melody.md](docs/text-to-melody.md) · [docs/phoneme-composer.md](docs/phoneme-composer.md) · [docs/cli.md](docs/cli.md)

## Wordbank integration

Linguistic tables (function words, grapheme rules, phoneme mappings, prosody
offsets, legal syllable onsets, locale syllabification rules, timbre profiles,
and optional per-word overrides) load at runtime from embedded JSON sourced from
the companion
[soundscript-wordbank](https://github.com/dharangutti/soundscript-wordbank)
repository (English, Spanish, and French locale packs in v0.5.0; corpus pilot
`2026.07.0`). The wordbank
is vendored as a git submodule at `wordbank/` and copied into
`src/SoundScript.Wordbank/Data/` before build. Sync updated data with:

```bash
git submodule update --init --recursive   # first clone
./scripts/sync-wordbank.sh
./scripts/bump-wordbank-submodule.sh      # update submodule + sync
```

Load an external wordbank checkout at runtime (overrides embedded packs):

```bash
export WORDBANK_DIR=./wordbank
dotnet run --project src/SoundScript.Cli -- prosody "Hola mundo" out.mid --locale es
# or pass --wordbank-dir ./wordbank on compose / prosody
```

See [wordbank VERSIONING.md](https://github.com/dharangutti/soundscript-wordbank/blob/main/docs/VERSIONING.md) for the engine ↔ package ↔ corpus contract (`8.0.x` / `>= 0.5.0` / `2026.07.0`).

Select a locale for `compose` or `prosody`:

```bash
dotnet run --project src/SoundScript.Cli -- prosody "Hola mundo" out.mid --locale es
```

## V4: Offline timbre synthesis

V4 adds **SoundScript.Timbre** — deterministic MIDI → WAV/OGG using
[SoundCSS](docs/soundcss.md) stylesheets. MIDI remains the backbone; timbre is
a read-only leaf branch.

```bash
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star" twinkle.mid
dotnet run --project src/SoundScript.Cli -- render twinkle.mid \
  --css examples/default.ssc --out twinkle.wav --text "Twinkle twinkle little star"
```

## V4.1: Cycle-accurate timbre synthesis

V4.1 adds cycle-by-cycle waveform reconstruction inside each frame. MIDI and
SoundCSS remain the backbone; synthesis quality improves via harmonic series
per pitch period.

→ [docs/v4.1-cycle-synthesis.md](docs/v4.1-cycle-synthesis.md) · [docs/whats-new-v4.1.md](docs/whats-new-v4.1.md)

## V4.1.1: Timbre quality tuning

V4.1.1 tunes harmonic balance, formant Q, noise shaping, transients, and
cycle/frame continuity on top of the V4.1 engine — additive and
deterministic, no new modules.

→ [docs/v4.1.1-timbre-tuning.md](docs/v4.1.1-timbre-tuning.md) · [docs/whats-new-v4.1.1.md](docs/whats-new-v4.1.1.md)

## Vocal Track (New)

SoundScript now has a parallel **voice engine**: write lyrics beside pitches and a
deterministic phonetics engine (syllabification + maximal-onset alignment) binds
each syllable to a note, exporting karaoke-standard MIDI lyric events.

```
voice lead {
    vocal choir
    mf
    sing "Twinkle twinkle little star" C4 q C4 q G4 q G4 q A4 q A4 q G4 h
}
```

The instrumental pipeline is untouched — voices interpret in a separate branch and
render onto a reserved MIDI channel. In the [Playground](https://soundscript.net/playground/),
lyrics are **spoken aloud** over the melody via the browser's speech synthesis (click the
*Voice* preset and press Run); the exported MIDI carries them as standard karaoke lyric
events for DAWs and singing synthesizers.

```bash
# try it from the CLI
dotnet run --project src/SoundScript.Cli -- run examples/vocal-song.ss vocal-song.mid
```

→ [docs/vocal.md](docs/vocal.md)

## Architecture

```
/src
    SoundScript.Core/       # AST, TempoAutomationMap, InstrumentMap
    SoundScript.Parser/     # Tokenizer, Parser, ProgramLoader
    SoundScript.Midi/       # Interpreter, PatternExpander, PhraseShaper, ChordOrchestration
    SoundScript.Voice/      # Vocal engine: Syllabifier, LyricAligner, VocalInterpreter
    SoundScript.Compose/    # Text-to-melody: PhonemeComposer, PhonemeSplitter, PhonemeMapper
    SoundScript.Wordbank/   # Embedded linguistic data from soundscript-wordbank
    SoundScript.Timbre/      # Offline timbre synthesis (SoundCSS)
    SoundScript.Wave/        # Direct AST → WAV synthesis (V7)
    SoundScript.Cli/        # CLI (run, compose, prosody, render, wave)
    SoundScript.Playground/ # Browser playground

/docs                       # Documentation + website
/examples                   # Example scripts
```

## Getting Started

```bash
git clone https://github.com/dharangutti/sound-script.git
cd sound-script
dotnet build
dotnet run --project src/SoundScript.Cli -- run examples/blocks.ss
```

## Playground

Try SoundScript in your browser — works in Chrome, Edge, Firefox, and Safari, fully client-side:

**[soundscript.net/playground](https://soundscript.net/playground/)**

## What's New in V8

- **Vocal stems in Wave export** — `sample`, `speak sample=`, CLI `--vocal` / `--tts-dir` / `--offline-tts`
- **`soundscript vocal`** — `generate` and `batch` for offline stem WAVs (`composite` default: corpus + G2P; `espeak`/`prosody` optional)
- **Phase 8 wordbank vocal** — curated CC0/CC-BY pronunciation audio with G2P fallback → [docs/phase8-wordbank-vocal.md](docs/phase8-wordbank-vocal.md)
- Example: [examples/jingle-bells-vocal.ssw](examples/jingle-bells-vocal.ssw)

→ [docs/whats-new-v8.md](docs/whats-new-v8.md) · [RELEASE_NOTES.md](RELEASE_NOTES.md)

## What's New in V7

- **SoundScript.Wave** — render `.ss` / `.ssw` directly to deterministic WAV (no MIDI step)
- **`wave` CLI verb** — `soundscript wave script.ssw output.wav`
- Playground auto-routes wave-only grammar (`speak`, `effect`, named `humanize`)

→ [docs/whats-new-v7.md](docs/whats-new-v7.md) · [docs/wave-grammar.md](docs/wave-grammar.md)

## What's New in V3.1

- **PhonemeComposer** — deterministic text-to-melody engine (`SoundScript.Compose`)
- **`compose` CLI verb** — `soundscript compose "Twinkle twinkle little star"`, with `--append file.ss`
- **Playground Text-to-Melody** — type text, press *Compose from text*
- No breaking changes; identical text → identical MIDI bytes (SHA-256 verified)

→ [docs/whats-new-v3.1.md](docs/whats-new-v3.1.md) · [docs/text-to-melody.md](docs/text-to-melody.md) · [RELEASE_NOTES.md](RELEASE_NOTES.md)

## What's New in V3

- Curve and transition aliases (`curve gentle`, `transition sharp`)
- New curves: `swell`, `fade`, `expressive`
- Dynamic envelopes: `crescendo`, `decrescendo`
- Phrase articulation: `articulation legato`
- Timing modifiers: `swing`, `push`, `pull` (via `PhraseTimingShaper`)
- Industrial audio cue showcase: [soundscript.net/industrial](https://soundscript.net/industrial/)

→ [docs/whats-new-v3.md](docs/whats-new-v3.md) · [docs/phrases-v3.md](docs/phrases-v3.md)

## What's New in V2

- Multi-file imports with `ProgramLoader`
- Named reusable blocks
- Track metadata: gain, humanize
- Tempo automation with linear ramps
- Instrument layers with per-channel MIDI
- Deterministic humanization
- Advanced chord voicing (drop2, inv1, spread)
- Phrase engine v2 with curves and transitions
- Pattern engine (arp, strum, rhythm)
- Orchestration helpers

→ [docs/whats-new-v2.md](docs/whats-new-v2.md)

## Documentation

| Document | Description |
|----------|-------------|
| [docs/user-guide.md](docs/user-guide.md) | Hands-on user guide with runnable examples |
| [docs/language-reference.md](docs/language-reference.md) | Complete syntax (V2) |
| [docs/cli.md](docs/cli.md) | CLI reference (`run`, `compose`, `prosody`, `render`, `wave`, `vocal`) |
| [docs/whats-new-v8.md](docs/whats-new-v8.md) | V8 changelog — vocal stems in Wave export |
| [docs/whats-new-v7.md](docs/whats-new-v7.md) | V7 changelog — SoundScript.Wave |
| [docs/wave-grammar.md](docs/wave-grammar.md) | Wave grammar (`.ssw`) |
| [docs/text-to-melody.md](docs/text-to-melody.md) | Text-to-melody pipeline (V3.1) |
| [docs/phoneme-composer.md](docs/phoneme-composer.md) | PhonemeComposer module reference |
| [docs/whats-new-v3.1.md](docs/whats-new-v3.1.md) | V3.1 changelog |
| [docs/whats-new-v3.md](docs/whats-new-v3.md) | V3 changelog |
| [docs/whats-new-v2.md](docs/whats-new-v2.md) | V2 changelog |
| [docs/imports.md](docs/imports.md) | Import system |
| [docs/blocks.md](docs/blocks.md) | Named blocks |
| [docs/track-metadata.md](docs/track-metadata.md) | Gain + humanize |
| [docs/tempo-automation.md](docs/tempo-automation.md) | Tempo ramps |
| [docs/layers.md](docs/layers.md) | Instrument layers |
| [docs/humanization.md](docs/humanization.md) | Deterministic jitter |
| [docs/advanced-chords.md](docs/advanced-chords.md) | Chord voicing |
| [docs/phrases.md](docs/phrases.md) | Phrase engine v2 |
| [docs/phrases-v3.md](docs/phrases-v3.md) | Phrase engine v3 |
| [docs/patterns.md](docs/patterns.md) | Pattern engine |
| [docs/orchestration.md](docs/orchestration.md) | Orchestration helpers |
| [docs/vocal.md](docs/vocal.md) | Vocal track + phonetics engine |
| [docs/pipeline.md](docs/pipeline.md) | Interpreter pipeline |
| [docs/architecture.md](docs/architecture.md) | System architecture |
| [docs/examples.md](docs/examples.md) | Example catalog |

## License

See [LICENSE](LICENSE).
