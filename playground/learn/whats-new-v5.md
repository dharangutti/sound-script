# What's New in V5

SoundScript **V5** adds **word-level prosody** — a new `SoundScript.Prosody`
subsystem that shapes Text-to-Melody pitch top-down (phrase → word →
syllable) instead of per phoneme category. It runs entirely alongside the
existing V3.1 `PhonemeComposer`; nothing about that pipeline changes.

## The fix

V3.1's `PhonemeMapper` assigns pitch **per phoneme category** — every `/p/` is
always A3, every `/aa/` is always D4, regardless of the word it's in or where
that word sits in the phrase. The result has rhythm and pattern, but no
speech-like contour: real prosody is shaped by stress, word category, and
sentence position, not by which consonant happens to occur. V5 computes pitch
top-down instead:

```
Words → base pitch (content/function, phrase position)
     → phrase contour (statement falls, question rises)
     → per-syllable micro-pitch (stress-driven)
     → phonemes (rhythm/articulation only, via the existing PhonemeMapper.Kind/Duration)
```

## New subsystem: `SoundScript.Prosody`

| Module | Purpose |
|--------|---------|
| `WordTokenizer` | Text → words, each with its syllable breakdown (reuses the existing `Syllabifier`) |
| `FunctionWords` / `WordCategory` | Closed-class content vs. function word classification |
| `StressDetector` | Lightweight rule-based per-syllable stress (primary/secondary/unstressed) |
| `WordPitchTable` | Declarative base-pitch band per category + phrase position |
| `WordProsodyPlanner` | Resolves each word's base pitch from category, position, and stress |
| `PhraseContourEngine` | Statement/question/list sentence-level pitch ramp |
| `SyllableContourGenerator` | Stress-driven ±3-semitone micro-pitch per syllable |
| `ProsodyClamp` | Safety-net pass bounding adjacent jumps (≤5 semitones) and phrase range (≤7 semitones) |
| `ProsodyNoteBuilder` / `ProsodyPhraseAssembler` | Prosody pitch → AST notes/phrases (one phrase per syllable) |
| `ProsodyComposer` | Facade: `Compose` / `ComposeProgram` / `AppendTo` / `BuildAst`, mirroring `PhonemeComposer`'s shape |

## New CLI verb

```bash
dotnet run --project src/SoundScript.Cli -- prosody "Twinkle twinkle little star" twinkle.mid
```

Same flags as `compose` (`--append <script.ss>`). The existing `compose` verb
and `PhonemeComposer` are untouched — V5 is reached only through this new
verb, opt-in.

The existing `render` verb needs no changes: it works on any MIDI file
regardless of which composer produced it, so a prosody-composed MIDI renders
through the same V4 SoundCSS timbre pass as a `compose`-produced one:

```bash
soundscript prosody "Twinkle twinkle little star" twinkle.mid
soundscript render twinkle.mid --css examples/default.ssc --out twinkle.wav --text "Twinkle twinkle little star"
```

The Playground gained a matching **Render Audio (Prosody)** button alongside
**Compose with Prosody**.

## Determinism

No randomness anywhere in the new pipeline: word classification, stress
detection, pitch tables, and the phrase contour ramp are all pure functions
of the input text. Identical text produces identical MIDI bytes, verified the
same way as V3.1 (SHA-256 comparison of repeated runs).

→ [docs/word-prosody.md](word-prosody.md) · [docs/v5-prosody-architecture.md](v5-prosody-architecture.md)

## Tests

New `WordProsodyTests.cs`:

- `WordProsodyPlannerTests` — determinism, category/position pitch bands
- `SyllableContourGeneratorTests` — stress ordering, ±3 semitone bound
- `PhraseContourEngineTests` — statement/question contour direction
- `ProsodyClampTests` — adjacent-jump and phrase-range bounds
- `ProsodyComposerTests` — end-to-end "Twinkle twinkle little star" pitch
  sequence, per-syllable single-pitch property, determinism, byte-identical
  MIDI

All existing tests (`PhonemeComposerTests`, `TimbreTests`, `TimbreTuningTests`,
`CycleSynthesisTests`, `MusicalIntelligenceTests`, ...) continue to pass
unmodified.

## Protected subsystems

Core, Parser, Interpreter, Voice, MIDI generator, and `PhonemeComposer` (plus
its siblings `PhonemeMapper`, `PhonemeSplitter`, `GestureBuilder`,
`PhraseAssembler`) are **unchanged** — V5 is a new, additive project with zero
edits to any existing file, aside from a new opt-in CLI verb in `Program.cs`.

## Previous releases

→ [What's new in V4.1.1](whats-new-v4.1.1.md) — timbre quality tuning
→ [What's new in V4.1](whats-new-v4.1.md) — cycle-accurate timbre reconstruction
→ [What's new in V4](whats-new-v4.md) — offline timbre synthesis (SoundCSS)
