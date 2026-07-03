# SoundScript Documentation

A tiny, deterministic music DSL that turns simple text into MIDI.  
Built in C#, designed for curiosity, creativity, and play.

---

## What SoundScript Is

SoundScript is a micro-language that lets you write music like code:

```
melody {
    bpm 120
    C4 E4 G4 | C5
}
```

The engine parses this DSL and generates a MIDI file — no DAW, no plugins, no audio synthesis.  
Just **text → tokenizer → parser → AST → interpreter → MIDI**.

Everything is intentionally small, deterministic, and hobby-friendly.

---

## Project Structure

```
/src
    SoundScript.Core/      # AST nodes, tokens, interpreted program models
    SoundScript.Parser/    # Tokenizer + Parser
    SoundScript.Midi/      # Interpreter + MIDI generator (DryWetMIDI)
    SoundScript.Cli/       # CLI runner (executable: soundscript)
    SoundScript.Web/       # Blazor WASM web demo (dev)
    SoundScript.Playground/ # Blazor WASM playground (GitHub Pages)

/examples
    melody.ss              # Phase 1 — notes + BPM
    durations.ss           # Phase 3 — note durations
    instruments.ss         # Phase 4 — instrument selection
    tempo-time.ss          # Phase 5 — tempo & time signature
    chords.ss              # Phase 6 — chords
    sequences.ss           # Phase 7 — sequences & blocks
    loops.ss               # Phase 8 — loops
    velocity.ss            # Phase 9 — velocity control
    multitrack.ss          # Phase 10 — multi-track support
    full.ss                # Combined example
```

| Project | Role |
|---------|------|
| **SoundScript.Core** | AST nodes, tokens, `InterpretedProgram`, `TimedNote` |
| **SoundScript.Parser** | Lexical analysis and parsing into `ProgramNode` |
| **SoundScript.Midi** | Beat scheduling, instrument/velocity handling, MIDI export |
| **SoundScript.Cli** | Command-line entry point |
| **SoundScript.Web** | Local browser demo (client-side WASM) |
| **SoundScript.Playground** | Hosted playground for [soundscript.net/playground](https://soundscript.net/playground/) |

---

## Try in Browser

Open the **SoundScript Playground** — no install required:

**[https://soundscript.net/playground/](https://soundscript.net/playground/)**

The playground is a client-only Blazor WebAssembly app that:

- Runs the full tokenizer → parser → AST → interpreter → MIDI pipeline in your browser
- Plays MIDI through Web Audio with a **local soundfont** (no CDN, no API calls)
- Works offline once loaded
- Deploys as static files on GitHub Pages

---

## How to Build the Playground

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)

### Build everything

```bash
dotnet build
```

### Publish playground to `docs/playground/`

```bash
dotnet publish src/SoundScript.Playground/SoundScript.Playground.csproj -c Release
```

Output is written to `docs/playground/` (configured in the project file). This folder is deployed to GitHub Pages at `/playground/`.

### Run locally during development

```bash
dotnet run --project src/SoundScript.Playground
```

Open `http://localhost:5180/playground/` (uses `--pathbase=/playground` to match production).

### GitHub Pages deployment

A GitHub Actions workflow (`.github/workflows/deploy-pages.yml`) runs on every push to `main`:

1. Publishes `SoundScript.Playground` into `docs/playground/`
2. Publishes the entire `docs/` folder to the `gh-pages` branch

**Pages settings** (one-time):

1. Go to **Settings → Pages**
2. Set **Source** to **Deploy from a branch**
3. Branch: `gh-pages`, folder: `/ (root)`
4. Custom domain: `soundscript.net` (CNAME file is in `docs/CNAME`)

Site layout:

```
/docs/index.html              → https://soundscript.net/
/docs/playground/index.html   → https://soundscript.net/playground/
/docs/playground/soundfont/   → local WAV samples
```

See [PLAYGROUND.md](PLAYGROUND.md) for a full verification checklist.

---

## How to Use (CLI)

### Run a script

```bash
dotnet run --project src/SoundScript.Cli -- run examples/melody.ss
```

This writes `output.mid` in the current directory.

### Custom output filename

```bash
dotnet run --project src/SoundScript.Cli -- run examples/melody.ss my-song.mid
```

### Run all examples

```bash
dotnet run --project src/SoundScript.Cli -- run examples/chords.ss
dotnet run --project src/SoundScript.Cli -- run examples/multitrack.ss
```

---

## How to Use (Web Version — Local Dev)

```bash
dotnet run --project src/SoundScript.Web
```

Open the URL shown in the console. For the hosted public playground, use [soundscript.net/playground](https://soundscript.net/playground/) instead.

---

## Language Reference

Whitespace separates tokens. Statements can appear at the top level or inside blocks.

### Phase 1 — Notes & Melody Blocks

Every legacy script can still be a single `melody { ... }` block:

```
melody {
    bpm 120
    C4 E4 G4 | C5
}
```

**Note format:** `[A–G][#|b]?[octave]`

| Example | Meaning |
|---------|---------|
| `C4` | Middle C (MIDI 60) |
| `F#4` | F sharp, octave 4 |
| `Bb3` | B flat, octave 3 |

Notes are case-insensitive for the pitch letter.

### Phase 2 — Bar Separator

```
C4 E4 | G4 C5
```

The `|` token is recognized but ignored — reserved for future bar-aware features.

### Phase 3 — Durations

Durations are measured in **beats** (quarter-note beats at the current tempo).

| Syntax | Beats |
|--------|-------|
| `C5` | 1 (default) |
| `C4 for 2` | 2 |
| `G4:4` | 4 |
| `C4 q` | 1 (quarter) |
| `D4 h` | 2 (half) |
| `E4 e` | 0.5 (eighth) |
| `F4 w` | 4 (whole) |

Legacy `bpm` and new `tempo` both set beats per minute (default **120**).

### Phase 4 — Instruments

```
instrument piano
instrument violin
instrument flute
```

| Instrument | GM Program |
|------------|------------|
| `piano` | 0 (Acoustic Grand Piano) |
| `bass` | 32 (Acoustic Bass) |
| `violin` | 40 |
| `flute` | 73 |
| `guitar` | 24 |
| `trumpet` | 56 |
| `cello` | 42 |
| `organ` | 19 |
| `synth` | 80 |

Default instrument: **acoustic grand piano (0)**.  
The interpreter emits a MIDI program change before subsequent notes on that track.

### Phase 5 — Tempo & Time Signature

```
tempo 120
time 4/4
time 3/4
```

- `tempo` (or legacy `bpm`) affects all note durations.
- `time` sets time signature metadata in the MIDI file (no rhythmic validation).

### Phase 6 — Chords

```
Cmaj
Dm
G7 q
Fmaj7 h
```

Chords expand into multiple simultaneous notes.

| Syntax | Quality | Intervals (semitones) |
|--------|---------|----------------------|
| `Cmaj`, `C` + maj | Major | 0, 4, 7 |
| `Dm`, `D` + m | Minor | 0, 3, 7 |
| `Cdim` | Diminished | 0, 3, 6 |
| `Caug` | Augmented | 0, 4, 8 |
| `G7 q` | Dominant 7th | 0, 4, 7, 10 |
| `Fmaj7` | Major 7th | 0, 4, 7, 11 |

Optional octave suffix: `Cmaj4`, `Dm5` (default octave: 4).

**Backward compatibility:** `G7` alone inside a `melody` block is still parsed as **G at octave 7**.  
Write `G7 q` (with a letter duration) to play a G dominant 7th chord.

### Phase 7 — Sequences & Blocks

```
sequence intro {
    C4 q
    D4 q
    E4 h
}

sequence chorus {
    G4 q
    A4 q
    B4 h
}

play intro
play chorus
```

- `sequence` defines a reusable named block.
- `play` expands the sequence inline at the current position.

### Phase 8 — Loops

```
loop 4 {
    C4 q
    D4 q
}
```

Repeats the block **N** times. Nested loops are not supported.

### Phase 9 — Velocity Control

```
velocity 80

C4 q v100
D4 q v60
E4 q
```

- `velocity N` sets the global default (1–127, default **64**).
- `vN` on a note overrides the global for that note.

### Phase 10 — Multi-Track Support

```
track melody {
    instrument flute
    C5 q
    D5 q
}

track bass {
    instrument bass
    C2 h
    G2 h
}
```

Each `track` block becomes a separate MIDI track. The interpreter merges all tracks into a single MIDI file.

---

## How It Works (Pipeline)

```
DSL script
    ↓
Tokenizer          →  Token stream
    ↓
Parser             →  ProgramNode (AST)
    ↓
Interpreter        →  InterpretedProgram (tracks, timed notes, program changes)
    ↓
MidiGenerator      →  output.mid  (via DryWetMIDI)
```

### AST node types

| Node | Purpose |
|------|---------|
| `ProgramNode` | Root container for all statements |
| `MelodyNode` | Legacy melody block (backward compatible) |
| `TrackNode` | Named multi-track block |
| `SequenceNode` | Reusable named sequence |
| `PlayNode` | Inline sequence expansion |
| `LoopNode` | Repeat block N times |
| `InstrumentNode` | MIDI program change |
| `TempoNode` / `BpmNode` | Tempo setting |
| `TimeSignatureNode` | Time signature metadata |
| `VelocityNode` | Global velocity setting |
| `NoteNode` | Single note with pitch, duration, optional velocity |
| `ChordNode` | Chord expanded to simultaneous notes |
| `BarNode` | Ignored bar separator |

### Timing

```
durationMs = (60_000 / tempo) × durationBeats
```

Monophonic tracks place notes sequentially. Chords place all notes at the same start beat.  
The MIDI generator writes **480 ticks per quarter note**.

---

## Design Philosophy

- **Tiny language** — human-readable, hobby-grade syntax
- **Deterministic behavior** — same input always produces the same MIDI
- **MIDI-first** — no audio synthesis
- **Additive growth** — phases 1–3 syntax remains valid
- **No randomness, no external DSL dependencies**

---

## Quick Reference

| Task | Command |
|------|---------|
| Build | `dotnet build` |
| Phase 1 example | `dotnet run --project src/SoundScript.Cli -- run examples/melody.ss` |
| Phase 3 example | `dotnet run --project src/SoundScript.Cli -- run examples/durations.ss` |
| Phase 6 chords | `dotnet run --project src/SoundScript.Cli -- run examples/chords.ss` |
| Phase 10 multi-track | `dotnet run --project src/SoundScript.Cli -- run examples/multitrack.ss` |
| **Browser playground** | [soundscript.net/playground](https://soundscript.net/playground/) |
| Web UI (local) | `dotnet run --project src/SoundScript.Web` |

---

## License

See [LICENSE](../LICENSE) in the repository root.
