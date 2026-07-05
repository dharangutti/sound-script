# SoundScript Release Notes

## V6 — Editable .ss Export (`--emit-ss`)

- **New library API:** `SoundScript.Parser.SsPrinter.Print(ProgramNode)` —
  serializes a pre-interpretation AST back into `.ss` DSL source text that
  the existing `Tokenizer`/`Parser` can re-parse, with no second informal
  reader.
- **New CLI flag:** `--emit-ss <path>` on both `compose` and `prosody`,
  alongside the existing `--append` (the two are mutually exclusive — see
  [docs/whats-new-v6.md](docs/whats-new-v6.md) for why).
- **Playground:** both compose buttons now also produce a `.ss` export — an
  inline **View .ss source** panel and a **Download .ss** button.
- **No breaking changes:** default `compose`/`prosody` output is unchanged;
  `run`'s existing `.ss` → `.mid` path is unchanged; SoundCSS/`.ssc` handling
  is unchanged.
- **Determinism verified:** `compose --emit-ss` → `run` produces MIDI
  byte-identical to the direct `compose` path, including the composed tempo
  at beat 0.

Details: [docs/whats-new-v6.md](docs/whats-new-v6.md) ·
[docs/text-to-melody.md](docs/text-to-melody.md) ·
[docs/cli.md](docs/cli.md)

## V5 — Word-Level Prosody (ProsodyComposer)

- **New subsystem:** `SoundScript.Prosody` — pitch is planned top-down
  (phrase → word → syllable) instead of per phoneme category:
  `WordTokenizer`, `StressDetector`, `WordPitchTable`/`WordProsodyPlanner`,
  `PhraseContourEngine`, `SyllableContourGenerator`, `ProsodyClamp`,
  `ProsodyComposer`. Runs entirely alongside V3.1's `PhonemeComposer`.
- **New CLI verb:** `soundscript prosody "<text>" [output.mid]`, with the
  same `--append <script.ss>` support as `compose`.
- **New library API:** `ProsodyComposer.ComposeProgram(text)`,
  `Compose(text)`, `AppendTo(program, text)`, `BuildAst(text)`.
- **Playground:** new **Compose with Prosody** button next to **Compose from
  text**, using the same text input.
- **No breaking changes:** Core, Parser, Interpreter, Voice, MIDI generator,
  and `PhonemeComposer` (plus its Compose siblings) are unmodified.
- **Determinism verified:** identical text produces identical MIDI bytes,
  confirmed by byte-equality tests, the same guarantee `PhonemeComposer` makes.

Details: [docs/whats-new-v5.md](docs/whats-new-v5.md) ·
[docs/word-prosody.md](docs/word-prosody.md) ·
[docs/v5-prosody-architecture.md](docs/v5-prosody-architecture.md)

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
