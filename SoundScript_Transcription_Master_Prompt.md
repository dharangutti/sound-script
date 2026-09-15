# SoundScript Transcription — Master Implementation Prompt

## Mission

Implement a new, extensible **SoundScript Transcription** subsystem that converts supported media and musical inputs into a canonical musical representation and then into valid SoundScript and other high-level outputs.

This is a **new feature/project inside the existing SoundScript repository and solution**, not a separate repository and not a collection of one-off converters.

The long-term product direction is bidirectional:

```text
External music/media → Understand/Transcribe → SoundScript → Perform/Render → Media
```

SoundScript already handles the code-to-media direction. This project introduces the inverse direction while preserving the existing architecture, determinism, compatibility, and tests.

---

## 1. Architectural Principle

Create a new project in the existing solution, preferably:

```text
src/SoundScript.Transcription/
```

with appropriate tests, for example:

```text
tests/SoundScript.Transcription.Tests/
```

Follow the repository's actual current project/test layout if it differs.

Do **not** place transcription algorithms directly into `SoundScript.Wave`, `SoundScript.Midi`, the CLI, or Playground.

Transcription is conceptually the inverse of the existing engine:

```text
Existing:
SoundScript DSL → AST → interpretation/performance → MIDI/Wave/Media

New:
Media/Musical Input → analysis → canonical musical model → SoundScript AST/DSL
```

The subsystem must be designed around **extensible input and output boundaries**. WAV/MP3/MP4 are only the first input family. SoundScript DSL is the primary output, but it must not be the only possible output.

---

## 2. High-Level Architecture

Target this conceptual architecture:

```text
INPUTS
  │
  ├── Audio: WAV / MP3 / other supported audio
  ├── Video: MP4 / WebM / other containers with audio
  ├── MIDI
  ├── Microphone / live humming
  ├── Voice / isolated vocal melody
  └── Future input adapters
          │
          ▼
   INPUT ADAPTER LAYER
          │
          ▼
 MUSICAL OBSERVATION MODEL
          │
          ▼
 TRANSCRIPTION / INTERPRETATION ENGINE
          │
          ▼
 CANONICAL MUSICAL MODEL
          │
          ├── SoundScript DSL / source
          ├── SoundScript AST
          ├── MIDI
          ├── MusicXML (future-ready; implement only if justified)
          ├── Note/Event Timeline
          ├── Analysis JSON
          ├── Confidence / diagnostic report
          └── Reconstructed preview/audio
```

Do not create pairwise converters such as `Mp3ToMidi`, `VideoToSoundScript`, `WavToMusicXml`, etc. Inputs normalize into common internal models; outputs consume the canonical model.

Define clean abstractions/interfaces appropriate to the existing codebase. Names such as `ITranscriptionInput`, `ITranscriptionInputAdapter`, `ITranscriptionOutput`, or `ITranscriptionOutputWriter` are suggestions, not mandatory names.

The architecture, not the exact interface names, is the requirement.

---

## 3. Canonical Musical Representation

Create or reuse a canonical representation capable of carrying, where known:

```text
Song / Composition
 ├── Tempo / tempo map
 ├── Time signature
 ├── Key / scale (with confidence)
 ├── Sections (future-ready)
 ├── Tracks / voices
 │    ├── Melody
 │    ├── Harmony
 │    ├── Bass
 │    ├── Percussion
 │    └── Vocal
 ├── Notes
 │    ├── Pitch
 │    ├── Start time / beat
 │    ├── Duration
 │    ├── Velocity / intensity
 │    ├── articulation/expression observations
 │    └── confidence/provenance
 ├── Rests / silence
 ├── Chords (when confidently detected)
 ├── Lyrics/text only when legitimately available and supported
 ├── Expression/performance observations
 └── Analysis confidence/provenance
```

Do not force uncertain observations into confident musical facts. Confidence and provenance should be first-class where useful.

Keep **transcription** and **performance interpretation** separate:

- Transcription determines **WHAT was played**.
- Existing `perform expressive` determines **HOW SoundScript performs the resulting score**.

Do not duplicate or bury performance interpretation inside transcription.

---

## 4. Initial Input Support

The end-to-end first release should support practical media inputs including:

- WAV
- MP3 where existing/runtime media support permits it
- MP4/video with an audio stream
- other formats naturally available through the repository's existing FFmpeg/media strategy

Reuse existing FFmpeg integration where appropriate. Do not introduce a second unrelated media pipeline without a strong reason.

For video/container inputs:

```text
Container → extract audio → normalize analysis representation → transcribe
```

Normalize analysis audio into a documented PCM/internal representation. Avoid unnecessary repeated encoding/decoding.

Design the adapter boundary now so future inputs can include:

- MIDI import
- microphone/live recording
- humming
- isolated vocals
- instrument recordings
- additional media formats
- future score/image/music-recognition inputs

Do not implement all future adapters merely to satisfy architecture. Make them addable without rewriting the transcription core.

---

## 5. First Quality Target — Monophonic Transcription

Excellent monophonic transcription is the first acceptance target.

Prioritize:

- clean synthesized melody
- humming
- flute
- violin
- isolated vocal melody where practical
- monophonic piano/keyboard line
- synthesized lead

Detect at minimum:

- pitch/fundamental frequency
- note onset
- note ending
- duration
- silence/rests
- repeated notes
- approximate tempo
- beat/rhythmic position

Investigate and select appropriate deterministic or controllably deterministic algorithms for:

- pitch detection
- onset detection
- silence segmentation
- tempo/beat estimation
- duration quantization

Do not hard-code results for validation fixtures.

Convert detected frequencies into correct musical pitches/octaves and detected timing into useful SoundScript durations.

Quantize intelligently. Prefer conventional SoundScript musical durations when the evidence supports them, but preserve sufficient information when timing does not cleanly map to a simple value.

---

## 6. Polyphonic and Mixed-Audio Roadmap

Once monophonic transcription is demonstrably working, investigate progressively harder material:

1. melody + simple accompaniment
2. piano melody/chords
3. bass + melody
4. multiple instruments
5. vocals + instruments
6. dense mixed recordings

Do not claim arbitrary commercial-song reconstruction is solved unless measured evidence demonstrates it.

If source separation, instrument recognition, harmony detection, or chord detection is uncertain, report uncertainty rather than inventing confident results.

The architecture should allow later addition of source-separation or ML-backed analysis components without forcing the rest of SoundScript to depend on one particular model/provider.

---

## 7. SoundScript as the Primary Native Output

The primary product output is valid, editable SoundScript.

Example shape only — generate syntax according to the repository's real current language grammar:

```text
perform expressive
tempo 76
time 4/4

track melody {
    instrument flute
    mf
    phrase {
        articulation legato
        G4 q B4 q D5 h |
        E5 h D5 q B4 q |
        A4 h rest h |
    }
}
```

Generated SoundScript must:

1. be syntactically valid,
2. parse through the real SoundScript parser,
3. use real supported language constructs,
4. render through existing SoundScript paths,
5. remain editable by a human.

Do not produce pseudo-SoundScript merely for display.

Prefer generating through AST/model-aware components instead of uncontrolled string concatenation.

Use existing constructs such as tempo, time signature, tracks, notes, rests, ties, phrases, articulations, dynamics, chords, instruments, and `perform expressive` only when justified by the observed music and supported by current grammar.

---

## 8. High-Level Output Architecture

SoundScript DSL is the native output, but output architecture must allow additional consumers of the canonical musical model.

Design for outputs such as:

### Required / first-class

- SoundScript source/DSL
- SoundScript AST or equivalent structured representation
- note/event timeline
- analysis/confidence report
- reconstructed SoundScript playback/render for validation

### Supported or future-ready where appropriate

- MIDI
- analysis JSON
- MusicXML
- other notation/interchange formats
- reconstructed WAV/audio preview
- future visualization/timeline outputs

Do not implement every possible output simply to check a box. Establish a clean output boundary and implement the outputs needed for the end-to-end feature and validation.

---

## 9. CLI Experience

Provide an intuitive CLI workflow consistent with existing SoundScript conventions, for example:

```text
soundscript transcribe input.wav --output melody.ss
soundscript transcribe input.mp3 --output melody.ss
soundscript transcribe input.mp4 --output score.ss
```

The exact syntax should follow the repository's established CLI design.

Useful options may include, if justified:

```text
--mode monophonic
--instrument flute
--tempo auto
--quantize auto
--report transcription.json
--preview output.wav
```

Do not create unnecessary options before the core workflow is stable.

CLI output should clearly report success, detected musical information, confidence, warnings, and generated output path.

---

## 10. Playground Experience

If technically feasible without destabilizing the Playground, provide an end-to-end browser workflow:

```text
Upload Media
   ↓
Analyze / Transcribe
   ↓
Generated SoundScript appears in editor
   ↓
Play
   ↓
Edit
   ↓
Replay / Export
```

The user should be able to hear the reconstructed result immediately using the existing SoundScript playback system.

Do not compromise existing Playground startup, playback, export, or deterministic behavior.

If browser limitations make some formats impractical, document them and preserve the architecture for future support rather than adding brittle hacks.

---

## 11. Round-Trip Validation — Core Requirement

Round-trip validation is a defining feature, not an optional demo.

For each transcription:

```text
SOURCE MEDIA
    ↓
TRANSCRIBE
    ↓
CANONICAL MUSICAL MODEL
    ↓
GENERATE SOUNDSCRIPT
    ↓
PARSE WITH REAL PARSER
    ↓
RENDER/PERFORM
    ↓
COMPARE MUSICAL RESULT TO SOURCE
```

Where useful, allow iterative refinement:

```text
source
→ transcription
→ SoundScript
→ render
→ musical comparison
→ refine transcription
→ render again
```

Do **not** optimize solely for waveform similarity. The same melody rendered with different instruments can have very different waveforms.

Prioritize musical similarity metrics such as:

- pitch correctness
- note onset accuracy
- duration accuracy
- missed notes
- extra notes
- repeated-note accuracy
- rest/silence alignment
- tempo error
- beat/rhythmic alignment
- melodic contour similarity

Document how each metric is calculated.

---

## 12. Confidence and Diagnostics

Return meaningful transcription diagnostics.

Example presentation:

```text
Notes detected: 42
Estimated tempo: 78 BPM
Pitch confidence: 96%
Timing confidence: 91%
Instrument confidence: 63%
Overall transcription confidence: 89%
```

Do not manufacture arbitrary percentages. Every confidence metric must have a defensible calculation or clearly documented interpretation.

If a particular property cannot be confidently inferred, say so.

Warnings should distinguish cases such as:

- noisy recording
- multiple simultaneous sources
- weak fundamental pitch
- uncertain tempo
- uncertain instrument
- clipping
- insufficient duration
- silence/no musical content

---

## 13. Validation Corpus

Create at least five contrasting legal-to-commit validation fixtures.

At minimum cover:

1. clean synthetic monophonic melody
2. hummed/vocal-style melody fixture
3. sustained flute/violin-style melody
4. piano/keyboard melody with repeated/articulated notes
5. melody containing meaningful rests and rhythmic/tempo variation

If the implementation reaches sufficient maturity, add simple polyphonic fixtures.

Use original, generated, public-domain, or appropriately licensed material. Do not commit copyrighted commercial recordings, extracted stems, lyrics, or reconstructed protected scores as repository fixtures.

For synthetic/generated fixtures, preserve known ground truth so transcription accuracy can be measured objectively.

---

## 14. Tests

Add focused automated tests for at least:

- pitch detection
- pitch-to-note mapping
- onset detection
- note ending/duration
- rests/silence
- repeated notes
- duration quantization
- tempo estimation
- deterministic analysis
- malformed input
- unsupported media
- silent input
- noisy input
- very short input
- clipped input
- MP4 audio extraction
- generated DSL parsing
- canonical model → SoundScript generation
- round-trip rendering
- musical comparison metrics
- confidence calculations
- CLI behavior
- browser upload/transcription where implemented
- backward compatibility with existing SoundScript behavior

Run the full existing repository test suite.

Do not weaken existing baselines or tests merely to make the new feature pass.

Existing scripts and existing deterministic outputs must remain unchanged unless an intentional compatibility change is explicitly documented and approved.

---

## 15. Determinism

SoundScript's deterministic behavior is a core product characteristic.

Where transcription algorithms are deterministic, identical inputs/configuration should produce identical canonical results and generated SoundScript.

If a dependency introduces nondeterminism, randomness, model variation, hardware variation, or provider variation:

- isolate it behind an adapter,
- document it,
- seed/configure it where possible,
- preserve deterministic downstream generation from a fixed observation/canonical model,
- add appropriate tests.

Do not silently weaken SoundScript's determinism guarantees.

---

## 16. Dependencies

Prefer existing repository capabilities and well-justified libraries.

Before introducing any dependency, document:

- what problem it solves,
- why existing code cannot reasonably solve it,
- license compatibility,
- platform support,
- runtime/deployment impact,
- determinism implications,
- browser implications if applicable.

Avoid coupling the architecture to one cloud AI provider or proprietary service.

A future ML implementation should be replaceable through the analysis/input architecture.

---

## 17. Copyright and Product Safety

The engine is general-purpose and may process user-provided media, but repository fixtures and shipped examples must be safe to distribute.

Do not commit copyrighted commercial songs, protected lyrics, unauthorized extracted stems, or reconstructed note-for-note commercial scores as examples/tests.

Use original/public-domain/licensed fixtures for regression and demonstrations.

Do not advertise arbitrary copyrighted-song cloning as the feature's purpose.

The product capability should be described as transcription, musical analysis, conversion, editing, and reconstruction of media the user is authorized to process.

---

## 18. Scope Discipline

This is an end-to-end implementation task, but quality matters more than pretending every music-recognition problem is solved in one iteration.

Do not stop after:

- architecture only,
- pitch detection only,
- CLI shell only,
- one hard-coded demo,
- pseudo-SoundScript output.

The minimum end-to-end success path is:

```text
REAL MEDIA FILE
   ↓
DECODE / EXTRACT AUDIO
   ↓
ANALYZE
   ↓
TRANSCRIBE MONOPHONIC MELODY
   ↓
CANONICAL MUSICAL MODEL
   ↓
GENERATE VALID SOUNDSCRIPT
   ↓
PARSE
   ↓
RENDER / PLAY
   ↓
MEASURE ROUND-TRIP MUSICAL ACCURACY
```

A high-quality monophonic implementation with clean extension points is preferable to unreliable claims of perfect dense-song reconstruction.

However, continue as far into polyphonic/mixed transcription as the architecture, time, and objective results responsibly allow.

---

## 19. Backward Compatibility

This must be an additive subsystem.

Existing SoundScript parsing, rendering, MIDI, Wave, browser playback, CLI commands, visual/media behavior, and `perform expressive` semantics should remain compatible.

Do not modify established semantics merely to make transcription easier.

Generated scripts may opt into existing features such as `perform expressive`, but transcription must use the actual language rather than changing the language around the transcription implementation.

---

## 20. Documentation

Update appropriate documentation with:

- transcription architecture
- input adapter architecture
- canonical musical model
- output architecture
- CLI usage
- Playground usage
- supported formats
- supported transcription modes
- accuracy/confidence interpretation
- determinism guarantees
- limitations
- dependency requirements such as FFmpeg where applicable
- extension guidance for adding future input/output adapters

Include an architecture/data-flow diagram in repository-friendly form if appropriate.

---

## 21. Definition of Done

Do not declare completion until all applicable items below are satisfied.

### Architecture

- New transcription project exists inside the current SoundScript solution.
- Input formats are isolated behind extensible adapters.
- A canonical musical representation exists.
- Output generation is separated from input decoding/analysis.
- SoundScript is the primary native output.
- Future input/output formats can be added without rewriting core transcription algorithms.

### End-to-end functionality

- Real WAV/audio can be transcribed.
- MP3 works where supported by the chosen media path.
- MP4/video audio can be extracted and transcribed.
- Monophonic transcription works end-to-end.
- Notes, timing, durations, repeated notes, and rests are represented.
- Generated SoundScript is valid and parseable.
- Generated SoundScript can be played/rendered.
- Round-trip musical validation works.
- Confidence/diagnostic information is produced.

### Product integration

- CLI transcription workflow works.
- Playground upload/transcription workflow is implemented if technically feasible without destabilization.
- Generated source is editable and replayable.

### Quality

- At least five contrasting validation fixtures exist.
- Objective accuracy metrics are reported.
- Deterministic behavior is tested.
- Full existing regression suite passes.
- New focused tests pass.
- Existing behavior is not silently changed.

### Documentation

- Architecture is documented.
- CLI/product usage is documented.
- Supported inputs/outputs are documented.
- Limitations are explicit.
- Future polyphonic/input/output extension points are documented.

---

## 22. Final Engineering Report

At completion, produce a detailed engineering report containing:

1. architecture implemented,
2. new projects/components/files,
3. canonical musical model design,
4. input adapters implemented,
5. output adapters/writers implemented,
6. algorithms used for pitch/onset/tempo/duration/quantization,
7. dependencies introduced and justification,
8. supported input formats,
9. supported output formats,
10. CLI usage examples,
11. Playground behavior,
12. five validation fixtures and their ground truth,
13. measured accuracy for every fixture,
14. round-trip comparison results,
15. confidence metric definitions,
16. determinism results,
17. tests added and final full-suite test count,
18. compatibility impact,
19. known limitations,
20. current polyphonic/mixed-audio capability,
21. recommended next steps.

Clearly distinguish:

- implemented and objectively verified,
- implemented but experimental,
- architecturally supported for later,
- not yet implemented.

Do not claim exact reconstruction of arbitrary music unless objective validation demonstrates it.

---

## Final Product Principle

The purpose of this subsystem is not merely:

```text
MP3 → SoundScript
```

It is to establish a general, extensible **music/media understanding and import layer** for SoundScript:

```text
MANY INPUTS
    ↓
MUSICAL UNDERSTANDING
    ↓
CANONICAL MUSIC
    ↓
MANY OUTPUTS
```

with SoundScript as the native programmable representation.

Combined with the existing rendering and `perform expressive` capabilities, the long-term loop becomes:

```text
Listen / Import
      ↓
Understand
      ↓
SoundScript Code
      ↓
Edit / Program
      ↓
Perform Expressively
      ↓
Render / Export
      ↓
Compare / Refine
```

Build the first end-to-end implementation now, while keeping this broader architecture intact for future inputs, outputs, transcription models, and musical capabilities.
