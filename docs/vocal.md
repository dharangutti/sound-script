# Vocal Track (Voice Engine)

SoundScript's parallel vocal subsystem: write lyrics next to pitches and get a
karaoke-compatible MIDI file with sung syllables aligned to notes by a
deterministic phonetics engine.

```ss
voice lead {
    vocal choir
    mf
    sing "Twinkle twinkle little star" C4 q C4 q G4 q G4 q A4 q A4 q G4 h
}
```

## Why a parallel track?

The instrumental engine (tracks, layers, phrases, patterns) is untouched. The
voice engine is a **separate pipeline branch** that shares only the parse step
and the tempo map:

```
DSL script
    ↓
Tokenizer → Parser → AST
    ├── Interpreter (tracks)          → InterpretedTrack[]
    └── VocalInterpreter (voices)     → InterpretedVocalTrack[]
            ├── Syllabifier (phonetics)
            └── LyricAligner (syllable ↔ note binding)
    ↓
MidiGenerator → output.mid  (notes + FF 05 lyric meta events)
```

- Scripts without `voice` blocks compile byte-identically to before.
- Vocal notes use reserved MIDI **channel 15**, so they never collide with
  instrument layers (which allocate channels from 0 upward).
- Each voice becomes its own MIDI track chunk with a track-name meta event.

## Syntax

### `voice` block

A top-level named block, parallel to `track`:

```ss
voice lead {
    vocal choir
    mf
    sing "..." <notes>
    rest q
}
```

Voice bodies accept: `vocal`, `sing`, `rest`, dynamics (`p mp mf f`), and
`velocity`. Instrumental statements (`instrument`, `layer`, `phrase`, chords)
are rejected with a clear error — voices are melodic, one pitch at a time.

### `vocal` — timbre

Selects the General MIDI vocal program:

| Name | GM Program |
|------|------------|
| `choir` / `aahs` | 52 — Choir Aahs (default) |
| `oohs` | 53 — Voice Oohs |
| `synthvoice` | 54 — Synth Voice |

### `sing` — lyric line

```ss
sing "How I wonder what you are" F4 q F4 q E4 q E4 q D4 q D4 q C4 h
```

A lyric string followed by one or more pitched notes (same note syntax as
tracks: pitch + octave, duration alias or `:beats`, optional `v<velocity>`).

## The phonetics engine

`sing` binds **syllables** to notes, not words. The `Syllabifier` splits each
word deterministically using three phonetic rules — no dictionary, no
randomness, identical output on every platform:

1. **Nucleus detection** — every syllable has one vowel group. Silent final
   *e* is ignored (*shine* → 1 syllable); consonant + *le* endings form a
   syllabic nucleus (*twin-kle*, *lit-tle*, *ta-ble*); *y* is a vowel when not
   word-initial (*hap-py*).
2. **Maximal onset** — consonants between vowels attach to the following
   syllable as far as English phonotactics allow (*shi-ning*, not *shin-ing*),
   using a legal-onset table (`bl`, `str`, `th`, …).
3. **Sonority sequencing** — clusters that cannot legally start a syllable
   stay in the previous coda (*win-dow*, *hel-lo*).

### Syllable-to-note alignment

| Case | Behavior |
|------|----------|
| syllables == notes | One syllable per note |
| syllables < notes | **Melisma** — the last vowel is held across the remaining notes (standard vocal writing) |
| syllables > notes | Tail syllables merge onto the final note + a compiler warning |

```ss
sing "Ah" C4 q E4 q G4 q      # 1 syllable, 3 notes → melisma over the triad
```

## MIDI output

For each voice, the generator writes a dedicated track chunk containing:

- `FF 03` track name (the voice name),
- a program change to the vocal timbre on channel 15,
- note on/off events for every sung pitch,
- `FF 05` **lyric meta events** at each syllable onset — word-final syllables
  get a trailing space, melisma continuations get no event. This is the
  standard karaoke convention understood by MIDI players, DAWs, and singing
  synthesizers (VOCALOID, Synthesizer V, Sinsy) that accept lyric-annotated MIDI.

## Determinism

The same script always produces the same syllables, the same alignment, and
bit-identical MIDI — the vocal engine follows the same contract as the rest of
SoundScript. Vocal timing uses the shared tempo map, so tempo ramps shape
sung durations exactly like instrumental ones.

## Example

→ [examples/vocal-song.ss](../examples/vocal-song.ss)

```bash
dotnet run --project src/SoundScript.Cli -- run examples/vocal-song.ss
```

## Scope and roadmap

The engine emits **lyric-annotated MIDI**, not rendered speech audio: MIDI has
no audio payload, so actual voice rendering belongs to the playback layer.
The current playground approximates vocal timbre with its soundfont fallback.
Natural next steps on this track:

- phoneme-level output (`FF 05` events carrying phonemes instead of syllables),
- browser playback via Web Speech / formant synthesis keyed off the lyric events,
- stress-aware velocity accents from the syllabifier.
