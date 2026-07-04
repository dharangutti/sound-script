# SoundScript Release Notes

## V3.1 — Text-to-Melody (PhonemeComposer)

- **New subsystem:** `SoundScript.Compose` — a deterministic text-to-melody
  engine (`PhonemeComposer`, `PhonemeSplitter`, `PhonemeMapper`,
  `GestureBuilder`, `PhraseAssembler`). Plain text → syllables → phonemes →
  musical gestures → AST → MIDI.
- **New CLI verb:** `soundscript compose "<text>" [output.mid]`, with
  `--append <script.ss>` to add the composed track to an existing script's
  output.
- **New library API:** `PhonemeComposer.ComposeProgram(text)`,
  `Compose(text)`, `AppendTo(program, text)`, `BuildAst(text)`.
- **Playground:** new **Text-to-Melody** input with a **Compose from text**
  button; the browser output is byte-identical to the CLI output.
- **No breaking changes:** grammar, parser, interpreter, vocal subsystem, and
  MIDI generator are unmodified; existing scripts compile byte-identically.
- **Determinism verified:** identical text produces identical MIDI bytes,
  confirmed by SHA-256 comparison of repeated CLI runs and byte-equality tests.

Details: [docs/whats-new-v3.1.md](docs/whats-new-v3.1.md) ·
[docs/text-to-melody.md](docs/text-to-melody.md) ·
[docs/phoneme-composer.md](docs/phoneme-composer.md)

## V3 — Expressive Phrases and Industrial Audio Cues

- Curve and transition aliases (`curve gentle`, `transition sharp`)
- New curves (`swell`, `fade`, `expressive`) and dynamic envelopes
  (`crescendo`, `decrescendo`)
- Phrase articulation defaults and timing modifiers (`swing`, `push`, `pull`)
- Industrial audio cue examples and case studies

Details: [docs/whats-new-v3.md](docs/whats-new-v3.md)

## V2 — Composition and Production Features

- Multi-file imports, named blocks, patterns, phrases, orchestration helpers
- Track metadata (gain, humanize), tempo automation, instrument layers
- Deterministic humanization, advanced chord voicing

Details: [docs/whats-new-v2.md](docs/whats-new-v2.md)

## v1.2 — Foundation

- Notation engine, expressive notation, stabilization, musical intelligence,
  playback quality

Details: [docs/whats-new-v1.2.md](docs/whats-new-v1.2.md)
