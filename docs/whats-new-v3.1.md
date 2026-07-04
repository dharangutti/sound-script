# What's New in V3.1

V3.1 adds the **PhonemeComposer** — a deterministic text-to-melody engine —
without changing any existing subsystem.

## Text-to-Melody Engine

| Addition | Description | Doc |
|----------|-------------|-----|
| **`compose` verb** | `soundscript compose "Twinkle twinkle little star"` — plain text to MIDI | [cli.md](cli.md) |
| **`--append`** | `compose "text" out.mid --append file.ss` — add the composed track to a script's output | [cli.md](cli.md) |
| **`SoundScript.Compose`** | New project: `PhonemeComposer`, `PhonemeSplitter`, `PhonemeMapper`, `GestureBuilder`, `PhraseAssembler` | [phoneme-composer.md](phoneme-composer.md) |
| **Library API** | `ComposeProgram(text)`, `Compose(text)`, `AppendTo(program, text)`, `BuildAst(text)` | [phoneme-composer.md](phoneme-composer.md) |
| **Playground** | **Text-to-Melody** input + **Compose from text** button | [PLAYGROUND.md](PLAYGROUND.md) |

## Pipeline Addition

```
Tokenizer → Parser → AST
    ├── Interpreter        (tracks)   — unchanged
    ├── VocalInterpreter   (voices)   — unchanged
    └── PhonemeComposer    (text)     — new
            ├── Syllabifier      (reused from SoundScript.Voice)
            ├── PhonemeSplitter  (syllable → phonemes)
            ├── PhonemeMapper    (phoneme → gesture, pure data)
            ├── GestureBuilder   (gesture → AST nodes)
            └── PhraseAssembler  (phrases → program AST)
    ↓
MidiGenerator → output.mid
```

The composer builds a standard AST in code and reuses the existing interpreter
and MIDI generator end to end.

## Compatibility

- **No breaking changes** — grammar, parser, interpreter, vocal subsystem, and
  MIDI generator are unmodified. Existing scripts compile byte-identically.
- **Deterministic** — identical text produces identical MIDI bytes, verified by
  SHA-256 comparison of repeated CLI runs and by byte-equality tests in
  `PhonemeComposerTests`.
- **No randomness, no platform dependence** — pure data mapping tables and
  ordinal string handling throughout.

## Example

```bash
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star"
```

```
Composed 7 syllable(s) into 24 note(s) to output.mid at 96 BPM.
```

## Related

- [text-to-melody.md](text-to-melody.md) — pipeline overview
- [phoneme-composer.md](phoneme-composer.md) — module reference
- [cli.md](cli.md) — CLI reference
- [whats-new-v3.md](whats-new-v3.md) — V3 changelog
