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

## Hearing the lyrics

MIDI lyric events are **metadata** — they carry no audio. Players that don't
understand them (including simple soundfont players) will play only the melody.
SoundScript gives you two ways to actually hear the words:

### In the Playground

The [Playground](https://soundscript.net/playground/) overlays the browser's
**speech synthesis** (Web Speech API) on top of the melody: each lyric word is
spoken at its note's onset, pitched coarsely toward the sung note, and paced to
the note's duration. Click **Run** on the *Voice* preset and you'll hear the
melody with the words spoken over it. Change the lyric string and the spoken
words change with it.

Notes on the preview:

- Speech voices vary by browser and OS — this playback layer is a *preview*,
  not part of the deterministic artifact. The exported MIDI is bit-identical
  everywhere.
- If a browser has no speech synthesis, the playground warns and plays melody only.
- The sung melody itself plays through the soundfont; vocal programs (52–54)
  fall back to the closest available sample set.

### In a DAW or singing synthesizer

Download the MIDI from the playground (or render with the CLI) and open it in
any tool that understands lyric events: most DAWs show them in the event list,
karaoke players scroll them, and singing synthesizers (VOCALOID, Synthesizer V,
Sinsy) will actually *sing* them with a vocal model.

## How to test

**Playground:** open the [Playground](https://soundscript.net/playground/),
click the **Voice** preset, then **Run**. Edit the string after `sing` and run
again — the spoken words follow your edit.

**CLI:**

```bash
dotnet run --project src/SoundScript.Cli -- run examples/vocal-song.ss vocal-song.mid
```

Expected output:

```
Wrote 24 notes across 1 track(s) and 14 sung syllable(s) across 1 voice(s) to vocal-song.mid at 100 BPM.
```

Then verify the lyric events, e.g. open `vocal-song.mid` in a DAW's event list,
or dump them quickly with Python:

```bash
pip install mido
python -c "import mido; [print(m) for m in mido.MidiFile('vocal-song.mid') if m.type == 'lyrics']"
```

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
no audio payload, so voice rendering belongs to the playback layer — which is
exactly what the playground's Web Speech preview does. Natural next steps on
this track:

- phoneme-level output (`FF 05` events carrying phonemes instead of syllables),
- per-syllable speech scheduling with formant-based pitch tracking,
- stress-aware velocity accents from the syllabifier.
