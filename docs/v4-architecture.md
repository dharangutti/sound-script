# SoundScript V4 Architecture

V4 extends the V3.1 text-to-melody pipeline with a third dimension: **offline
timbre synthesis**. The full deterministic chain is:

```
Text
  → Syllables        (Syllabifier — Voice)
  → Phonemes         (PhonemeSplitter — Compose)
  → Gestures         (PhonemeMapper — Compose)
  → AST              (GestureBuilder + PhraseAssembler — Compose)
  → MIDI             (Interpreter + MidiGenerator — unchanged)
  → TIMBRE           (SoundScript.Timbre — new)
  → AUDIO            (WAV / OGG)
```

## Design principles

| Principle | V4 behaviour |
|-----------|--------------|
| MIDI as backbone | Pitch, duration, timing, articulation, and track layout come **only** from MIDI |
| Additive only | No changes to Core, Parser, Interpreter, Voice, MIDI, or PhonemeComposer |
| Deterministic | Pure data tables, fixed frame grid, SHA-256 regression tests |
| Offline | No real-time engine; slow-motion frame synthesis |
| Declarative timbre | SoundCSS (`.ssc`) styles phonemes like CSS styles elements |

## Subsystem map

```
SoundScript.Core          AST, notation, interpreted program types
SoundScript.Parser        Tokenizer, parser, ProgramLoader
SoundScript.Midi          Interpreter, MidiGenerator
SoundScript.Voice         Syllabifier, VocalInterpreter
SoundScript.Compose       PhonemeComposer branch (V3.1)
SoundScript.Timbre        SoundCSS + offline renderer (V4)  ← new
SoundScript.Cli           run | compose | render
SoundScript.Playground    browser MIDI + offline WAV preview
```

## MIDI → timbre contract

The timbre engine **reads** MIDI; it never writes or patches MIDI files.

| From MIDI | Used for |
|-----------|----------|
| Note on/off times | Frame alignment, segment duration |
| Pitch | Fundamental frequency |
| Velocity | Amplitude |
| Tempo map | Millisecond conversion |
| Track selection | Prefer `phonemes` track when named |

| From SoundCSS / mapper | Used for |
|--------------------------|----------|
| Phoneme label | Profile lookup |
| `TimbreProfile` | Formants, noise, burst, nasal, brightness |

Phoneme **identity** is not stored in standard MIDI today. V4 resolves it via,
in order:

1. `@phonemes` directive in the stylesheet
2. `--text` on the CLI / compose text in the playground
3. Deterministic MIDI signature guessing (`PhonemeTimbreMapper.GuessPhoneme`)

## Integration surfaces

### CLI

```bash
soundscript render file.mid --css style.ssc --out output.wav --text "hello"
```

### Playground

**Render Audio** composes text to MIDI, then runs `OfflineRenderer` in WASM and
plays the resulting WAV through Web Audio.

### Tests

`TimbreTests.cs` covers SoundCSS parsing, timeline alignment, spectral output,
and SHA-256 determinism.

## Version history

| Version | Addition |
|---------|----------|
| V3.1 | PhonemeComposer — text → MIDI |
| V4 | SoundScript.Timbre — MIDI → audio |

→ [What's new in V4](whats-new-v4.md)

## See also

- [SoundCSS](soundcss.md)
- [Timbre engine](timbre-engine.md)
- [Text-to-melody (V3.1)](text-to-melody.md)
- [PhonemeComposer](phoneme-composer.md)
