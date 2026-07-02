# SoundScript Documentation (v1–v3)

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
Just **text → parsed → interpreted → MIDI**.

Everything is intentionally small, deterministic, and hobby-friendly.

---

## Project Structure

```
/src
    SoundScript.Core/      # Models: Token, ParsedNote, MelodyProgram, TimedNote
    SoundScript.Parser/    # Tokenizer + Parser
    SoundScript.Midi/      # Interpreter + MIDI generator (DryWetMIDI)
    SoundScript.Cli/       # CLI runner (executable: soundscript)
    SoundScript.Web/       # Blazor WASM web demo

/examples
    melody.ss              # v1 example — notes + BPM
    durations.ss           # v3 example — note durations
```

| Project | Role |
|---------|------|
| **SoundScript.Core** | Shared types used across the pipeline |
| **SoundScript.Parser** | Lexical analysis and parsing into `MelodyProgram` |
| **SoundScript.Midi** | Beat timing and MIDI file export |
| **SoundScript.Cli** | Command-line entry point |
| **SoundScript.Web** | Browser-based demo (client-side WASM) |

---

## How to Build

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- (Optional) VS Code or JetBrains Rider
- (Optional) A modern browser for the Web UI

### Build everything

```bash
dotnet build
```

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

### Run the durations example

```bash
dotnet run --project src/SoundScript.Cli -- run examples/durations.ss
```

---

## How to Use (Web Version)

### Run the Blazor WASM app

```bash
dotnet run --project src/SoundScript.Web
```

Open the URL shown in the console.

You will see:

- A **textarea** with DSL pre-filled (the default melody example)
- A **Generate MIDI** button
- A **Download MIDI** link (appears after generation)

Everything runs **client-side in the browser** — no server processing, no filesystem access. MIDI bytes are generated in memory and offered as a base64 download link.

---

## Language Reference (v1–v3)

Every script is a single `melody { ... }` block. Whitespace separates tokens.

### 1. Notes

Write pitch letter + octave number:

```
C4 E4 G4 C5
```

**Format:** `[A–G][#|b]?[octave]`

| Example | Meaning |
|---------|---------|
| `C4` | Middle C |
| `F#4` | F sharp, octave 4 |
| `Bb3` | B flat, octave 3 |

Notes are case-insensitive for the pitch letter (`c4` and `C4` are equivalent).

### 2. BPM

Set tempo inside the melody block:

```
bpm 120
```

- Must be a **positive integer**
- Default if omitted: **120 BPM**

### 3. Bar separator

```
C4 E4 | G4 C5
```

The `|` token is **recognized but ignored** — reserved for future bar/timing features.

### 4. Durations (v3)

Durations are measured in **beats** (quarter-note beats at the current BPM).

#### Default — 1 beat

```
C5
```

#### Verb form

```
C4 for 2
```

#### Compact form

```
G4:4
```

| Note in `durations.ss` | Duration |
|------------------------|----------|
| `C4 for 2` | 2 beats |
| `E4 for 1` | 1 beat |
| `G4:4` | 4 beats |
| `C5` | 1 beat (default) |

Duration values must be **positive numbers**.

### Full durations example

```
melody {
    bpm 100
    C4 for 2
    E4 for 1
    G4:4
    C5
}
```

### Recognized but unused (reserved)

The tokenizer recognizes `play` as a keyword, but the parser does not use it yet. It is reserved for future features.

---

## How It Works (Pipeline)

```
DSL script
    ↓
Tokenizer          →  Token stream (melody, notes, bpm, for, :, |, numbers, …)
    ↓
Parser             →  MelodyProgram { Bpm, List<ParsedNote> }
    ↓
Interpreter        →  List<TimedNote> (beats → milliseconds)
    ↓
MidiGenerator      →  output.mid  (via DryWetMIDI)
```

### Core models

| Type | Fields / purpose |
|------|------------------|
| `ParsedNote` | Pitch, accidental, octave, `DurationBeats` (default `1`) |
| `MelodyProgram` | `Bpm`, ordered list of `ParsedNote` |
| `TimedNote` | `MidiNumber`, `StartBeat`, `DurationBeats`, `DurationMs` |

### Timing

The interpreter converts beats to milliseconds:

```
durationMs = (60_000 / bpm) × durationBeats
```

Notes are placed **sequentially**: each note starts where the previous one ends.  
The MIDI generator writes a **single track** at 480 ticks per quarter note.

### MIDI note mapping

Pitch letters map to standard MIDI note numbers. For example, at octave 4:

| Note | MIDI number |
|------|-------------|
| C4 | 60 |
| E4 | 64 |
| G4 | 67 |
| C5 | 72 |

---

## Design Philosophy

- **Tiny language** — minimal syntax, no AST complexity
- **Deterministic behavior** — same input always produces the same MIDI
- **MIDI-first** — no audio synthesis
- **Incremental growth** — one small feature at a time

### Not supported yet

- Multi-track
- Loops
- Instruments / program changes
- Chords
- Rests
- Velocity
- Bar-aware timing (beyond token recognition)

---

## Future Roadmap (Recommended Order)

These are planned directions, not implemented features.

### Phase 4 — Instruments

```
instrument piano
instrument violin
instrument 41
```

### Phase 5 — Loops

```
loop 4 {
    C4 E4
}
```

### Phase 6 — Rests

```
rest for 2
```

### Phase 7 — Chords

```
[C4 E4 G4] for 2
```

### Phase 8 — Multi-track

```
track piano {
    C4 E4 G4
}

track violin {
    G3 A3 B3
}
```

### Phase 9 — Velocity

```
C4 @80
```

### Phase 10 — Web Syntax Highlighting

Minimal, deterministic, offline.

---

## Quick Reference

| Task | Command |
|------|---------|
| Build | `dotnet build` |
| CLI — default example | `dotnet run --project src/SoundScript.Cli -- run examples/melody.ss` |
| CLI — durations example | `dotnet run --project src/SoundScript.Cli -- run examples/durations.ss` |
| Web UI | `dotnet run --project src/SoundScript.Web` |

---

## License

See [LICENSE](../LICENSE) in the repository root.
