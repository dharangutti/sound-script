# SoundScript User Guide

**From your first note to expressive, production-ready cues — a hands-on tour of the whole language.**

SoundScript is a deterministic music language: you write plain text, the engine compiles it to a standard MIDI file, and the same script always produces the same output — on every platform, every time. This guide takes you from a three-note melody to multi-track arrangements and industrial audio cues, with a complete, runnable script at every step.

> Every code block in this guide is a full program. Paste any of them into the [Playground](https://soundscript.net/playground/) and press compile, or save them as a `.ss` file and run them with the CLI.

---

## Contents

1. [Setup — Playground or CLI](#1-setup--playground-or-cli)
2. [Your First Melody](#2-your-first-melody)
3. [Durations, Rests, and Ties](#3-durations-rests-and-ties)
4. [Dynamics and Articulations](#4-dynamics-and-articulations)
5. [Chords and Voicings](#5-chords-and-voicings)
6. [Tracks, Instruments, and Layers](#6-tracks-instruments-and-layers)
7. [Tempo, Time Signatures, and Ramps](#7-tempo-time-signatures-and-ramps)
8. [Reuse: Blocks, Sequences, Loops, and Imports](#8-reuse-blocks-sequences-loops-and-imports)
9. [Patterns: Arpeggios, Strums, and Rhythms](#9-patterns-arpeggios-strums-and-rhythms)
10. [Phrases: Expressive Shaping (V2 + V3)](#10-phrases-expressive-shaping-v2--v3)
11. [Production Polish: Gain, Humanize, Orchestration](#11-production-polish-gain-humanize-orchestration)
12. [Putting It Together: An Industrial Audio Cue](#12-putting-it-together-an-industrial-audio-cue)
13. [Quick Reference Card](#13-quick-reference-card)

---

## 1. Setup — Playground or CLI

**No install:** open the [Playground](https://soundscript.net/playground/). It runs entirely in your browser (Chrome, Edge, Firefox, Safari — desktop and mobile), compiles your script client-side, and plays the result with Web Audio.

**CLI (Windows, macOS, Linux):** requires the .NET 8 SDK.

```bash
git clone https://github.com/dharangutti/sound-script.git
cd sound-script
dotnet build
dotnet run --project src/SoundScript.Cli -- run examples/melody.ss my-output.mid
```

The CLI prints a summary and writes a standard `.mid` file you can open in any DAW, sequencer, or MIDI player:

```
Wrote 4 notes across 1 track(s) to my-output.mid at 120 BPM.
```

A few things worth knowing before you write your first line:

- **Whitespace separates tokens.** Newlines and indentation are free-form.
- **Keywords are case-insensitive** — `tempo`, `Tempo`, and `TEMPO` are equivalent.
- **There are no comments** in the language. Every token is meaningful.
- **Warnings never abort.** The engine's musical intelligence adjusts and reports; it does not reject your script.

## 2. Your First Melody

A note is a pitch letter (`A`–`G`), an optional accidental (`#`, `b`), and an octave (`0`–`8`, where `4` is the middle-C octave). A duration letter follows the note.

```
tempo 120
instrument piano

C4 q E4 q G4 q C5 h
```

That's a complete program: quarter notes walking up a C-major triad, landing on a half-note C5, played on piano at 120 BPM. Accidentals work as you'd expect — `F#4` is F sharp, `Bb3` is B flat.

You can also wrap notes in a `melody` block, which is handy once scripts grow:

```
melody {
    tempo 120
    C4 q E4 q G4 q C5 h
}
```

## 3. Durations, Rests, and Ties

| Syntax | Beats | Meaning |
|--------|-------|---------|
| `q` or `quarter` | 1 | Quarter note |
| `h` or `half` | 2 | Half note |
| `e` or `eighth` | 0.5 | Eighth note |
| `w` or `whole` | 4 | Whole note |
| `for N` | N | Numeric beats — `C4 for 1.5` |
| `:N` | N | Colon form — `G4:0.5` |
| *(omitted)* | 1 | Defaults to a quarter note |

Dotted-note suffixes (`q.`) are not supported — use the numeric forms for fractional beats.

**Rests** advance time without sounding a note, and **ties** (`~`) merge adjacent notes of the same pitch into one sustained note:

```
tempo 100
instrument piano

C4 q E4 e G4 e rest q C5 for 1.5 B4:0.5
C5 q ~ C5 q ~ C5 h
```

The last line sounds as a single C5 held for four beats. Tying different pitches is an error — ties are for sustain, not slurs.

## 4. Dynamics and Articulations

Dynamics set loudness and **persist until changed**:

| Marking | Velocity | Feel |
|---------|----------|------|
| `p` | 48 | quiet |
| `mp` | 64 | moderate-quiet |
| `mf` | 80 | moderate-loud |
| `f` | 96 | loud |

Articulations shape individual notes, written as a prefix or suffix (one per note):

| Articulation | Effect |
|--------------|--------|
| `staccato` | Short and detached (~47% length) |
| `legato` | Smooth and connected (~97% length) |
| `accent` | Emphasized (~110% velocity) |

```
tempo 110
instrument violin

p C4 q D4 q
mf staccato E4 q staccato F4 q
f accent G4 h
```

For surgical control there is also per-note velocity: `C4 q v100` overrides the current dynamic for that note only, and `velocity 90` sets a track-scoped default.

## 5. Chords and Voicings

Write a root, a quality suffix, and a duration:

```
tempo 90
instrument piano

Cmaj q Am q Fmaj q G7 h
Cmaj drop2 h Fmaj inv1 h Cmaj spread w
```

| Suffix | Quality |
|--------|---------|
| *(none)* / `maj` | Major |
| `m` / `min` | Minor |
| `dim` | Diminished |
| `aug` | Augmented |
| `maj7` | Major seventh |
| `7` | Dominant seventh |

The second line shows **advanced voicings**: `drop2`/`drop3` lower an inner voice for a wider, warmer spread; `inv1`/`inv2` are inversions; `spread` widens the upper voices. The engine's voicing intelligence also smooths transitions between chords automatically.

One subtlety: `G7 q` is a dominant-seventh chord because a duration follows; a bare `B7` is the note B in octave 7.

## 6. Tracks, Instruments, and Layers

Tracks give each musical voice its own instrument, dynamics, and metadata. Layers double one track across several instruments, each on its own MIDI channel:

```
tempo 100

track lead {
    instrument flute
    mf
    C5 q D5 q E5 q G5 h
}

track pad {
    layer piano
    layer cello
    gain 0.8
    mp
    Cmaj w
}

track bass {
    instrument bass
    mf
    C2 h G2 h
}
```

Supported instruments: `piano`, `bass`, `violin`, `flute`, `guitar`, `trumpet`, `cello`, `organ`, `synth`.

All tracks start at beat zero and play in parallel — arrangement is simply what you make coincide.

## 7. Tempo, Time Signatures, and Ramps

`tempo` (or `bpm`) sets the pulse. `time` declares a signature, and once bar lines (`|`) are used, the engine validates each measure and warns — without stopping — if a bar is short or long:

```
time 4/4
tempo 96
instrument piano

melody {
    C4 q E4 q G4 q C5 q |
    B4 h G4 h |
}
```

Tempo can also **ramp linearly** over a number of bars — an accelerando in one line:

```
tempo 100 -> 132 over 4 bars
instrument piano

track build {
    mf
    loop 4 {
        C4 e E4 e G4 e C5 e G4 e E4 e C4 e G3 e
    }
}
```

Both `->` and the Unicode arrow `→` are accepted. Multiple top-level ramps chain one after another.

## 8. Reuse: Blocks, Sequences, Loops, and Imports

A `block` is a named musical fragment; `play` expands it inline wherever you need it. Define once, reuse everywhere:

```
tempo 112
instrument guitar

block hook {
    mf
    E4 e G4 e A4 q
    G4 e E4 e D4 q
}

block turnaround {
    f
    Cmaj q G7 q
}

track song {
    play hook
    play turnaround
    play hook
}
```

`sequence` is similar but allows full track-body statements (including `loop`), and `loop N { ... }` repeats its body N times:

```
tempo 124
instrument synth

track pulse {
    mf
    loop 4 {
        C3 e C3 e G3 e C3 e
    }
}
```

When a script grows past one file, `import` splits it into libraries. Imports are relative paths, may nest, and circular imports are rejected with a clear error:

```
import "riffs.ss"

track main {
    instrument guitar
    play hook
}
```

Blocks defined later override earlier ones by name (with a warning) — imports behave like layered configuration.

## 9. Patterns: Arpeggios, Strums, and Rhythms

Patterns turn a single chord into motion. Define the pattern once, then apply it to any chord and duration with `play <pattern> <chord> <duration>`:

```
tempo 104
instrument guitar

pattern arp { up }
pattern cascade { down }
pattern wave { updown }
pattern brush { strum }
pattern gallop { rhythm e e q }

track textures {
    mf
    play arp Cmaj q
    play cascade Am q
    play wave Fmaj q
    play brush Cmaj h
    play gallop Dm h
}
```

| Directive | Behavior |
|-----------|----------|
| `up` | Ascending arpeggio, duration split evenly |
| `down` | Descending arpeggio |
| `updown` | Ascend then descend |
| `strum` | Chord tones staggered like a guitar strum |
| `rhythm e e q` | Chord tones cycled over your custom rhythm |

## 10. Phrases: Expressive Shaping (V2 + V3)

The `phrase` block is where SoundScript stops sounding mechanical. Everything inside a phrase is shaped as one musical gesture — velocity curves, note-to-note transitions, dynamic envelopes, a default articulation, and deterministic timing feel:

```
tempo 108
instrument violin

track melody {
    phrase {
        curve gentle
        transition smooth
        articulation legato
        mf
        C4 q E4 q G4 q C5 h
    }
    phrase {
        curve swell
        crescendo
        mf
        C4 q E4 q G4 q C5 q
    }
    phrase {
        curve fade
        decrescendo
        f
        C5 q G4 q E4 q C4 q
    }
    phrase {
        swing 0.67
        push 0.02
        mf
        C4 e E4 e G4 e E4 e C4 e E4 e G4 e E4 e
    }
}
```

**Curves** shape velocity across the phrase:

| Curve | Alias | Character |
|-------|-------|-----------|
| `soft` | `gentle` | Rounded, understated |
| `hard` | `strong`, `aggressive` | Accentuated, assertive |
| `balanced` | — | Neutral blend |
| `expressive` | — | Linear/soft blend, singing quality |
| `swell` | — | Rises across the phrase |
| `fade` | — | Falls across the phrase |

**Transitions** control the seam between notes: `smooth`, `abrupt` (alias `sharp`), `soft`, `expressive`.

**Envelopes** — `crescendo` ramps velocity up across the phrase's notes; `decrescendo` ramps down.

**Phrase articulation** — `articulation legato` (or `staccato`, `accent`, `detached`) sets the default for every note in the phrase; per-note articulations still override.

**Timing modifiers** add groove — deterministically, so the "feel" is identical on every run:

| Keyword | Range | Effect |
|---------|-------|--------|
| `swing 0.67` | 0.0–1.0 | Delays off-beat notes into a swung groove |
| `push 0.02` | beats ≥ 0 | Leans ahead of the beat |
| `pull 0.03` | beats ≥ 0 | Sits behind the beat |

The full pipeline is documented in [phrases.md](phrases.md) and [phrases-v3.md](phrases-v3.md).

## 11. Production Polish: Gain, Humanize, Orchestration

Three track-level tools take output from correct to produced:

```
tempo 92

track keys {
    instrument piano
    gain 0.85
    humanize 0.02
    double octave
    reinforce bass
    brighten top
    mf
    Cmaj w Fmaj w Cmaj spread w
}
```

- **`gain 0.85`** — scales the track's velocity (0.0–1.0) after all shaping; your mix balance.
- **`humanize 0.02`** — adds subtle timing and velocity jitter. Crucially, it is **seeded and deterministic**: the "human" imperfection is the same on every compile, so humanized scripts remain reproducible.
- **Orchestration helpers** — `double octave` doubles chords an octave up, `reinforce bass` adds a root an octave down, `brighten top` lifts the top voice. Sticky flags: they affect every chord that follows in the track.

## 12. Putting It Together: An Industrial Audio Cue

SoundScript's determinism makes it useful beyond composition — auditable audio cues for machines, robots, and accessibility workflows. Here is a machine-state cue built from everything above: named blocks as a shared vocabulary, phrase shaping for meaning, one track sequencing the states:

```
tempo 100
instrument organ

block idle {
    phrase {
        curve gentle
        articulation legato
        mp
        C4 q E4 q G4 h
    }
}

block running {
    phrase {
        curve strong
        articulation accent
        mf
        C3 e G3 e C4 e G3 e
    }
}

block critical {
    phrase {
        transition sharp
        articulation staccato
        f
        G4 e G4 e G4 e rest e G4 e G4 e G4 e
    }
}

track machine {
    play idle
    play running
    play critical
}
```

Idle breathes gently at mezzo-piano; running drives with accented energy; critical fires sharp staccato bursts at forte. An operator learns the vocabulary once and hears state changes peripherally — before looking at a dashboard.

The [Industrial Applications](https://soundscript.net/industrial/) page walks through five complete scenarios — blind-operator spatial awareness, machine states, conveyor timing drift, temperature trends, and robotic arm motion — each backed by a runnable script in [examples/](examples.md):

```bash
dotnet run --project src/SoundScript.Cli -- run examples/industrial-machine-state.ss
```

## 13. Quick Reference Card

| I want to… | Write |
|------------|-------|
| Set the tempo | `tempo 120` |
| Ramp the tempo | `tempo 100 -> 132 over 4 bars` |
| Play a note | `C4 q` · `F#3 h` · `Bb4 e` |
| Custom length | `C4 for 1.5` · `G4:0.5` |
| Rest | `rest q` |
| Sustain across notes | `C5 q ~ C5 h` |
| Set loudness | `p` `mp` `mf` `f` |
| Shape one note | `staccato C4 q` · `C4 q legato` · `accent C4 q` |
| Play a chord | `Cmaj q` · `Am h` · `G7 q` |
| Voice a chord | `Cmaj drop2 q` · `Fmaj inv1 h` · `Cmaj spread w` |
| Pick an instrument | `instrument flute` |
| Stack instruments | `layer piano` + `layer cello` |
| Reuse a fragment | `block hook { ... }` + `play hook` |
| Repeat | `loop 4 { ... }` |
| Split into files | `import "lib.ss"` |
| Arpeggiate | `pattern arp { up }` + `play arp Cmaj q` |
| Shape a gesture | `phrase { curve gentle ... }` |
| Build / release intensity | `crescendo` / `decrescendo` in a phrase |
| Add groove | `swing 0.67` · `push 0.02` · `pull 0.03` |
| Balance the mix | `gain 0.85` |
| Sound human (reproducibly) | `humanize 0.02` |
| Thicken chords | `double octave` · `reinforce bass` · `brighten top` |

### Where next

- **[Playground](https://soundscript.net/playground/)** — try everything in this guide in your browser
- **[language-reference.md](language-reference.md)** — the complete, precise syntax
- **[examples.md](examples.md)** — 39 runnable scripts covering every feature
- **[phrases-v3.md](phrases-v3.md)** — the full expressive-shaping reference
- **[Industrial Applications](https://soundscript.net/industrial/)** — deterministic audio cues for real-world processes
- **[SoundScript.md](SoundScript.md)** — the documentation hub
