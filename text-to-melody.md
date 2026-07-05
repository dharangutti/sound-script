# Text-to-Melody (V3.1)

SoundScript V3.1 adds a deterministic **text-to-melody engine**: give it a plain
English string and it composes a melody from the sounds of the words. No speech,
no audio synthesis, no randomness — text in, standard MIDI out.

```bash
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star"
```

```
Composed 7 syllable(s) into 24 note(s) to output.mid at 96 BPM.
```

## The pipeline

The engine is a third pipeline branch beside the instrumental interpreter and
the vocal subsystem. Nothing existing changed — the composer builds standard AST
nodes in code and rides the same interpreter and MIDI generator as every script:

```
plain text
    ↓
words → syllables            Syllabifier        (existing, SoundScript.Voice)
    ↓
syllable → phonemes          PhonemeSplitter    (new)
    ↓
phoneme → musical gesture    PhonemeMapper      (new, pure data table)
    ↓
gestures → phrases → AST     GestureBuilder + PhraseAssembler (new)
    ↓
AST → InterpretedTrack       Interpreter        (existing, unchanged)
    ↓
InterpretedTrack → MIDI      MidiGenerator      (existing, unchanged)
```

## Stage by stage

### 1. Words → syllables

The existing `Syllabifier` (the same engine that aligns lyrics in `voice`
blocks) splits each word using nucleus detection, maximal onset, and sonority
sequencing — no dictionary, no randomness:

```
"Twinkle twinkle little star" → Twin · kle · twin · kle · lit · tle · star
```

### 2. Syllable → phonemes

`PhonemeSplitter` scans each syllable left to right, consuming known digraphs
first (`sh`, `ch`, `th`, `ee`, `oo`, `ai`, ...) and normalising every remaining
letter to a canonical phoneme symbol:

```
star → /s/ /t/ /aa/ /r/
twin → /t/ /w/ /ai/ /n/
kle  → /k/ /l/ /ee/
```

This is a rule-based grapheme approximation, not a pronunciation dictionary:
the same syllable always yields the same phonemes, on every platform.

### 3. Phoneme → gesture

`PhonemeMapper` looks each phoneme up in a fixed table. Every entry is pure
data — a gesture kind, a pitch, and a duration:

```
/s/  → fade     D4 e
/t/  → staccato B3 e
/aa/ → legato   D4 q
/r/  → accent   D4 e
```

Plosives map to staccato, nasals to swell, fricatives to fade, liquids to
accent, vowels to legato. Every pitch sits within a perfect fifth (A3–E4), so
adjacent phonemes in a word move by a step or two instead of leaping across
octaves — closer to how spoken pitch contour actually moves. (The fifth-wide
band isn't arbitrary: the shared `MelodicContour` shaping step would octave-
correct any wider leap, so anything much narrower or wider gets distorted.)
The full table is in [phoneme-composer.md](phoneme-composer.md).

### 4. Gestures → phrases → AST

Each syllable becomes one `PhraseNode` — a musical micro-phrase. Staccato,
legato, and accent gestures set the standard per-note articulation; swell and
fade gestures set the phrase's existing crescendo/decrescendo envelope. The
assembled AST is an ordinary SoundScript program (`TempoNode` + `TrackNode`),
indistinguishable from one produced by the parser.

### 5. AST → MIDI

The existing interpreter turns the AST into an `InterpretedTrack` named
`phonemes`, applying the same deterministic shaping as any script (articulation
shaping, phrase envelopes, octave smoothing, melodic contour). The existing
MIDI generator writes the file. No generator changes were needed — the composed
track is just one more track.

## Recursive composition

The composition algorithm is a deterministic recursion over the syllable list —
each step splits one syllable into phonemes, maps each phoneme to a gesture,
appends the gestures to the current phrase, and recurses on the remaining
syllables:

```
compose(syllables, index):
    if index = length(syllables): return
    begin phrase
    for each phoneme in split(syllables[index]):
        append map(phoneme)
    end phrase
    compose(syllables, index + 1)
```

There is no randomness at any stage: no seeds, no time-dependent state, no
platform-dependent string handling.

## Determinism guarantee

Identical input text produces identical MIDI bytes — the same guarantee the
rest of SoundScript makes for scripts. This is verified in the test suite by
composing the same text twice and comparing the raw MIDI byte streams, and can
be checked from the shell:

```bash
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star" a.mid
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star" b.mid
sha256sum a.mid b.mid   # identical hashes
```

## Usage

**CLI** (see [cli.md](cli.md)):

```bash
# standalone: compose text into its own MIDI file
dotnet run --project src/SoundScript.Cli -- compose "Hello world" hello.mid

# append: add the composed track to an existing script's output
dotnet run --project src/SoundScript.Cli -- compose "Hello world" out.mid --append examples/vocal-song.ss
```

**Library** (`SoundScript.Compose`):

```csharp
using SoundScript.Compose;

// text → complete program (tempo map included), ready for MidiGenerator.Write
var program = PhonemeComposer.ComposeProgram("Twinkle twinkle little star");

// text → just the interpreted track
var track = PhonemeComposer.Compose("Twinkle twinkle little star");

// add the composed track to an existing interpreted program
PhonemeComposer.AppendTo(existingProgram, "Twinkle twinkle little star");
```

**Playground:** type text into the **Text-to-Melody** input box and press
**Compose from text** — the composed MIDI plays in the browser and can be
downloaded. The browser output is byte-identical to the CLI output for the
same text.

## V4 extension: MIDI → audio

V4 adds an offline timbre pass **after** MIDI generation. The MIDI file remains
the backbone for pitch, duration, timing, and articulation; SoundCSS supplies
spectral styling:

```
composed MIDI
    ↓
MidiToTimbreTimeline     align phonemes, build 8 ms frame grid
    ↓
SpectralEngine + SoundCSS    formants, noise, bursts
    ↓
WAV / OGG
```

```bash
# compose, then render
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star" twinkle.mid
dotnet run --project src/SoundScript.Cli -- render twinkle.mid \
  --css examples/default.ssc --out twinkle.wav \
  --text "Twinkle twinkle little star"
```

The playground **Render Audio** button runs the same timbre pipeline in WASM.

→ [v4-architecture.md](v4-architecture.md) · [soundcss.md](soundcss.md) · [timbre-engine.md](timbre-engine.md)

## Related

- [phoneme-composer.md](phoneme-composer.md) — module documentation, mapping table, splitter rules
- [cli.md](cli.md) — CLI reference
- [architecture.md](architecture.md) — where the composer sits in the system
- [whats-new-v3.1.md](whats-new-v3.1.md) — V3.1 changelog
- [whats-new-v4.md](whats-new-v4.md) — V4 timbre synthesis changelog
