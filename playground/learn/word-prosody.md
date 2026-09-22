# Word-Level Prosody (V5)

SoundScript V5 adds a second deterministic text engine, **`ProsodyComposer`**,
that shapes Text-to-Melody pitch the way speech actually works: top-down,
phrase → word → syllable, instead of one fixed pitch per phoneme category.
It runs alongside V3.1's `PhonemeComposer` — nothing about that engine
changes; V5 is reached through its own CLI verb and library facade.

```bash
dotnet run --project src/SoundScript.Cli -- prosody "Twinkle twinkle little star"
```

```
Composed 7 syllable(s) into 24 note(s) to output.mid at 96 BPM.
```

## Why word-level

V3.1's `PhonemeMapper` gives every phoneme category one fixed pitch (every
`/p/` is A3, every `/aa/` is D4), so the melody follows *which sounds occur*,
not *how the sentence is spoken*. Real prosody is shaped by:

- **Word category** — content words (nouns, verbs, adjectives) carry more
  pitch prominence than function words (articles, prepositions, auxiliaries).
- **Stress** — the stressed syllable of a word rises above its neighbors.
- **Phrase position and sentence type** — statements fall toward the end,
  questions rise.

V5 computes pitch from these three inputs first, and only afterward asks
"which phonemes make up this syllable" — and at that point, phonemes
contribute rhythm and articulation (via the existing `PhonemeMapper.Kind` /
`Duration`, reused unchanged), never pitch.

## The pipeline

```
plain text
    ↓
words → syllables            WordTokenizer      (reuses the existing Syllabifier)
    ↓
word → category               FunctionWords / WordCategory
    ↓
word → stress pattern         StressDetector
    ↓
word → base pitch             WordProsodyPlanner + WordPitchTable
    ↓
sentence → phrase contour     PhraseContourEngine
    ↓
syllable → micro-pitch        SyllableContourGenerator
    ↓
phrase-wide safety clamp       ProsodyClamp
    ↓
syllable → phonemes           PhonemeSplitter            (existing, reused)
    ↓
phoneme → rhythm/articulation  PhonemeMapper.Kind/Duration (existing, reused — pitch ignored)
    ↓
notes → phrases → AST         ProsodyNoteBuilder + ProsodyPhraseAssembler
    ↓
AST → InterpretedTrack        Interpreter        (existing, unchanged)
    ↓
InterpretedTrack → MIDI       MidiGenerator      (existing, unchanged)
```

## Stage by stage

### 1. Words → syllables

`WordTokenizer` splits text into words, then syllabifies each with the same
`Syllabifier` used by `PhonemeComposer` and vocal lyric alignment — but,
unlike `PhonemeComposer.SplitSyllables`, it keeps word boundaries, since
prosody needs to know which syllables belong to which word.

### 2. Word → category and stress

`FunctionWords` is a closed, deterministic set of English articles,
prepositions, conjunctions, pronouns, and auxiliaries; anything else is a
content word. `StressDetector` then assigns one `StressLevel` (`Primary` /
`Secondary` / `Unstressed`) per syllable using simple rules — trochaic for
two-syllable words (first syllable stressed, unless it's a common unstressed
prefix like *re-*, *un-*, *con-*), first-syllable-primary for longer words.
This is a lightweight heuristic, not a pronunciation dictionary.

### 3. Word → base pitch

`WordPitchTable` is a small declarative table of semitone offsets from C4,
keyed by category and phrase position:

| Category | Position | Offset | Pitch |
|----------|----------|--------|-------|
| Content | Start | +4 | E4 |
| Content | Middle | 0 | C4 |
| Content | End | −3 | A3 |
| Function | any | −7 | F3 |

`WordProsodyPlanner` resolves each word's `PhrasePosition` from its index and
looks up this table. These offsets are wide enough that the word/phrase
contour reads as a real melodic arc rather than collapsing into the same
narrow band V3.1's fixed `PhonemeMapper` table occupies — see the note on
`ProsodyClamp` below.

### 4. Sentence → phrase contour

`PhraseContourEngine` detects sentence type from trailing punctuation
(`?` → question, else statement) and computes one semitone delta per word as
a linear ramp: statements fall from +2 to −4 across the phrase, questions
rise from −3 to +5. This delta is added on top of each word's base pitch.

### 5. Syllable → micro-pitch

`SyllableContourGenerator` adds a small stress-driven offset on top of the
word's resolved pitch: +2 semitones for the primary-stressed syllable, +1 for
secondary, 0 for unstressed — always within the required ±3 semitone bound.

### 6. Safety clamp

`ProsodyClamp` walks the full per-syllable pitch sequence once, left to
right, and pulls back any adjacent jump over 5 semitones or any running
phrase range over 14 semitones. The 5-semitone adjacent-jump bound is what
keeps the shared Interpreter's `MelodicContour` step (which octave-corrects
leaps over 7 semitones) from ever firing on prosody-composed pitch; the
14-semitone range bound is a much looser backstop for pathological inputs —
it's deliberately wide enough to let the word/phrase/syllable contour above
produce its full natural range (worst case around 13 semitones) instead of
collapsing every phrase into the same narrow band as V3.1's fixed
`PhonemeMapper` table. For "Twinkle twinkle little star" the clamp is a
no-op either way.

### 7. Syllable → phonemes (timbre/rhythm only)

Each syllable's final pitch is shared by **every phoneme in that syllable** —
this is the concrete difference from V3.1: phonemes no longer carry pitch.
`PhonemeSplitter` and `PhonemeMapper` are reused exactly as before, but only
their `GestureKind` (articulation) and `Duration` are read; `Pitch`/`Octave`
are discarded in favor of the syllable's prosody-resolved pitch.

### 8. Notes → phrases → AST → MIDI

`ProsodyNoteBuilder` and `ProsodyPhraseAssembler` build the same shape of AST
as `PhonemeComposer` (one `PhraseNode` per syllable, `TempoNode` + `TrackNode`
at the top), just with prosody-resolved, potentially chromatic pitches
(spelled with sharps). From there the existing `Interpreter` and
`MidiGenerator` take over unchanged.

## Determinism guarantee

Every stage above is a pure function of the input text — no seeds, no
randomness, no platform-dependent behavior. Identical text produces identical
MIDI bytes, the same guarantee `PhonemeComposer` makes, verified the same way:

```bash
dotnet run --project src/SoundScript.Cli -- prosody "Twinkle twinkle little star" a.mid
dotnet run --project src/SoundScript.Cli -- prosody "Twinkle twinkle little star" b.mid
sha256sum a.mid b.mid   # identical hashes
```

## Usage

**CLI:**

```bash
dotnet run --project src/SoundScript.Cli -- prosody "Hello world" hello.mid
dotnet run --project src/SoundScript.Cli -- prosody "Hello world" out.mid --append examples/vocal-song.ss
```

**Library** (`SoundScript.Prosody`):

```csharp
using SoundScript.Prosody;

var program = ProsodyComposer.ComposeProgram("Twinkle twinkle little star");
var track = ProsodyComposer.Compose("Twinkle twinkle little star");
ProsodyComposer.AppendTo(existingProgram, "Twinkle twinkle little star");
```

## V4 audio rendering

The V4 `render` CLI verb and `OfflineRenderer` are composer-agnostic — they
work on any MIDI file, regardless of which engine produced it. A
prosody-composed MIDI renders through the exact same SoundCSS timbre pass as
a `PhonemeComposer` one:

```bash
dotnet run --project src/SoundScript.Cli -- prosody "Twinkle twinkle little star" twinkle.mid
dotnet run --project src/SoundScript.Cli -- render twinkle.mid \
  --css examples/default.ssc --out twinkle.wav \
  --text "Twinkle twinkle little star"
```

`ProsodyComposer` names its track `"prosody"` (rather than `PhonemeComposer`'s
`"phonemes"`), so code calling `OfflineRenderer`/`MidiToTimbreTimeline`
directly (as opposed to the `render` CLI verb, which reads any single-track
file regardless of its name) should pass
`RenderOptions { PreferredTrackName = "prosody" }` explicitly — the Playground's
**Render Audio (Prosody)** button does this.

## Related

- [text-to-melody.md](text-to-melody.md) — V3.1 phoneme-level engine this runs alongside
- [architecture.md](architecture.md) — where the prosody branch sits in the system
- [v5-prosody-architecture.md](v5-prosody-architecture.md) — component map and layer diagram
- [whats-new-v5.md](whats-new-v5.md) — V5 changelog
