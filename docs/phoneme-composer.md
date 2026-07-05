# PhonemeComposer Module Reference

`SoundScript.Compose` is the V3.1 text-to-melody subsystem. It converts plain
text into a standard SoundScript AST and lets the existing interpreter and MIDI
generator do the rest. This page documents each module, the mapping tables, and
the determinism rules. For the conceptual overview see
[text-to-melody.md](text-to-melody.md).

## Modules

| Module | Role |
|--------|------|
| `PhonemeComposer` | Facade: text → syllables → phonemes → gestures → AST → `InterpretedTrack` |
| `PhonemeSplitter` | Rule-based syllable → phoneme symbols |
| `PhonemeMapper` | Pure-data phoneme → `MusicalGesture` table |
| `GestureBuilder` | Gesture → existing AST nodes (`NoteNode`, `PhraseEnvelopeNode`) |
| `PhraseAssembler` | Gestures → per-syllable `PhraseNode`s → program AST |

The project references `SoundScript.Core` (AST), `SoundScript.Voice`
(`Syllabifier`), and `SoundScript.Midi` (`Interpreter`). Nothing references it
back — it is a leaf branch of the pipeline.

## Public API

```csharp
// complete program with tempo map, ready for MidiGenerator.Write
InterpretedProgram PhonemeComposer.ComposeProgram(string text, int tempo = 96)

// just the interpreted track (named "phonemes")
InterpretedTrack PhonemeComposer.Compose(string text, int tempo = 96)

// add the composed track to an existing interpreted program,
// using the host program's tempo
void PhonemeComposer.AppendTo(InterpretedProgram program, string text)

// the program AST without interpreting it
ProgramNode PhonemeComposer.BuildAst(string text, int tempo = 96)

// words → syllables via the existing Syllabifier
IReadOnlyList<string> PhonemeComposer.SplitSyllables(string text)
```

## Splitter rules (`PhonemeSplitter`)

The splitter is a grapheme-driven approximation, not a dictionary G2P. It scans
each syllable left to right:

1. **Normalise** — keep letters only, lowercase (ordinal, culture-independent).
2. **Collapse doubled consonants** — `tt`, `ss`, `ll` → one phoneme.
3. **Digraphs first** — two-letter graphemes take priority over single letters:

| Grapheme | Phoneme | Grapheme | Phoneme |
|----------|---------|----------|---------|
| `sh` | /sh/ | `aa` | /aa/ |
| `ch` | /ch/ | `ee`, `ea`, `ey`, `ie` | /ee/ |
| `th` | /th/ | `oo`, `oa` | /oo/ |
| `ph` | /f/ | `ai`, `ay` | /ai/ |
| `wh` | /w/ | `au`, `ou`, `ow` | /au/ |
| `ng` | /ng/ | | |
| `ck` | /k/ | | |
| `qu` | /k/ /w/ | | |

4. **Single-letter fallbacks** — vowels normalise to the canonical long-vowel
   symbols; irregular consonants collapse:

| Letter | Phoneme | Letter | Phoneme |
|--------|---------|--------|---------|
| `a` | /aa/ | `c`, `q` | /k/ |
| `e`, `y` | /ee/ | `x` | /k/ /s/ |
| `i` | /ai/ | all others | themselves (`b` → /b/, `m` → /m/, ...) |
| `o` | /au/ | | |
| `u` | /oo/ | | |

Examples:

```
star  → /s/ /t/ /aa/ /r/
shine → /sh/ /ai/ /n/ /ee/
queen → /k/ /w/ /ee/ /n/
sing  → /s/ /ai/ /ng/
```

## Mapping table (`PhonemeMapper`)

Every phoneme maps to a `MusicalGesture` — a gesture kind, pitch, octave, and
duration (`e` = eighth, `q` = quarter). The table is pure data; extending it
means adding rows.

Every gesture's pitch sits within a perfect fifth — A3 to E4 (7 semitones) —
rather than spanning multiple octaves. Two things make this width the right
one, not just "narrower": real speech F0 moves in small steps around a
baseline pitch, so a wide-ranging table (an earlier version spanned C3–D5,
over two octaves) made adjacent phonemes in a word leap across octaves and
read as an arpeggiated tune instead of spoken prosody; and the shared
Interpreter step `MelodicContour` octave-shifts any note-to-note leap over 7
semitones to smooth "wide" melodic jumps in hand-written music — a table
with gaps wider than a fifth would get silently re-widened by that shared
correction. Keeping every entry within A3–E4 avoids triggering it:

| Phoneme | Gesture | Phoneme | Gesture |
|---------|---------|---------|---------|
| /p/ | staccato A3 e | /s/ | fade D4 e |
| /t/ | staccato B3 e | /sh/ | fade D4 e |
| /k/ | staccato B3 e | /th/ | fade D4 e |
| /b/ | staccato A3 e | /f/ | fade C4 e |
| /d/ | staccato B3 e | /v/ | fade D4 e |
| /g/ | staccato B3 e | /z/ | fade D4 e |
| /ch/ | staccato B3 e | /h/ | fade C4 e |
| /m/ | swell C4 q | /r/ | accent D4 e |
| /n/ | swell C4 q | /l/ | accent C4 e |
| /ng/ | swell C4 q | /j/ | accent D4 e |
| /w/ | swell C4 e | | |

Vowels map to legato notes:

| Phoneme | Gesture |
|---------|---------|
| /aa/ | legato D4 q |
| /ee/ | legato E4 q |
| /oo/ | legato B3 q |
| /ai/ | legato E4 q |
| /au/ | legato C4 q |

Phonemes without a row fall back to a default gesture (legato C4 e), so the
mapping is total — any input text composes.

## Gesture semantics (`GestureBuilder` + `PhraseAssembler`)

Gestures are expressed entirely through **existing** AST constructs — no new
grammar, no new node types:

| Gesture kind | AST representation |
|--------------|--------------------|
| staccato | `NoteNode` with `Articulation = Staccato` (~47% duration) |
| legato | `NoteNode` with `Articulation = Legato` (~97% duration) |
| accent | `NoteNode` with `Articulation = Accent` (~110% velocity) |
| swell | note velocity 58 + phrase `crescendo` envelope |
| fade | note velocity 52 + phrase `decrescendo` envelope |

Each **syllable becomes one `PhraseNode`**. The first swell or fade gesture in
a syllable decides that phrase's envelope; a single MIDI note cannot change
velocity mid-sound, so swell and fade shape velocity *across* the syllable's
notes using the existing phrase envelope machinery.

## Example AST output

`compose "star"` (one syllable, phonemes /s/ /t/ /aa/ /r/) builds:

```
ProgramNode
├── TempoNode { Bpm = 96 }
└── TrackNode { Name = "phonemes" }
    └── PhraseNode                                    ← syllable "star"
        ├── PhraseEnvelopeNode { Decrescendo }        ← from /s/ (fade)
        ├── NoteNode D4 e  velocity 52                ← /s/  fade
        ├── NoteNode B3 e  staccato                   ← /t/  staccato
        ├── NoteNode D4 q  legato                     ← /aa/ legato
        └── NoteNode D4 e  accent                     ← /r/  accent
```

The interpreter then applies its normal deterministic shaping (articulation
shaping, phrase envelope, octave smoothing, melodic contour) exactly as it
would for a hand-written script, and emits the `phonemes` track.

## Determinism rules

- **Pure data tables** — the splitter and mapper are lookup tables with no
  state and no randomness.
- **Ordinal string handling** — normalisation is culture-independent; results
  do not vary with system locale.
- **No seeds, no clocks, no platform branches** — the same text yields the
  same syllables, phonemes, gestures, AST, and MIDI bytes on every platform.
- **Existing pipeline** — interpretation and MIDI generation reuse the same
  deterministic code paths as every SoundScript script.

Verified in `src/SoundScript.Tests/PhonemeComposerTests.cs`: splitter rules,
the mapping table, AST shape, note-level equality, and MIDI byte equality for
repeated composition of the same text.

## Related

- [text-to-melody.md](text-to-melody.md) — pipeline overview
- [cli.md](cli.md) — `compose` verb reference
- [architecture.md](architecture.md) — system architecture
- [vocal.md](vocal.md) — the vocal subsystem (a separate branch)
