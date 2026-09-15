# Expressive performance: specification and engineering report

`perform expressive` opts an instrumental score into deterministic performance
interpretation. It changes relationships between notes as well as their individual
envelopes. Existing scripts retain their legacy path when the declaration is absent.

**Validation status (2026-09-15):** all 1,031 tests pass, including 23 focused
performance tests. Five real Playground presets and 334 browser-scheduled notes
were verified. After the before/after listening comparison, the user reported:
“Improved; articulation remains clear.” This records listening acceptance in
addition to automated checks, without claiming orchestral realism. No external
high-quality MIDI synthesizer was available for this evaluation.

## Syntax and scope

```ss
perform expressive
tempo 60
track melody {
    instrument flute
    phrase {
        articulation legato
        crescendo
        C4 h D4 h E4 w
    }
    rest q
}
```

The declaration is top-level and applies to the entire program, including tracks
and layers. Its position does not limit its scope. `expressive` is the only mode;
there is no track-local override or `perform off`. `perform` remains a contextual
word, so a sequence named `perform` can still be declared and played.

The feature is aimed at instrumental melody. It does not provide portamento,
breath synthesis, bow-change samples, or natural sung phoneme transitions.

## 1. Baseline and root causes

The original slow diagnostic is a flute playing `C4 h legato D4 h legato` at
60 BPM, plus the complete original ballad [Harbor Lights](../examples/performance-harbor.ss).
For each comparison, the legacy input is exactly the same score with the
performance declaration removed. No copyrighted cinematic melody or lyric was
used or committed.

| Layer | Observed implementation | Consequence |
|-------|-------------------------|-------------|
| Parser / AST | Pitch and written duration are represented; same-pitch ties merge into one note | A tie gives one attack; repeated untied pitches remain separate events |
| MIDI interpretation | Legato duration is `written × 0.97`; the next onset advances by the full written duration | 60 ms gap between half notes at 60 BPM |
| MIDI interpretation | Legacy octave/contour smoothing can change written melodic pitches | Expressive mode bypasses those pitch corrections, so interval decisions use the written melody |
| Phrase shaping | Curves, transitions, crescendo and decrescendo affect note-on velocity | They do not connect attacks or evolve one held note |
| Instrument routing | Legacy tracks independently reuse channel 0 | Simultaneous program changes can override other instruments |
| MIDI serialization | 480 ticks/quarter; duration truncates to whole ticks | The diagnostic gap becomes 29 ticks, or 60.417 ms |
| Legacy Wave adapter | Uses written durations and a default sine timbre; skips articulation, phrase shaping and layers | MIDI articulation behavior cannot explain this path's sound; phrase and instrument instructions were lost here |
| Wave synthesis | Every note has a new oscillator/envelope; default attack 10 ms, decay 50 ms, sustain 0.8, release 100 ms | Repeated independent envelopes and static sustain rather than connected performance |
| Legacy browser MIDI parser | Pairs notes by channel/pitch across tracks; at equal ticks processes note-on before note-off | Repeated or simultaneous same-pitch events can overwrite pending notes |
| Legacy browser samples | Source duration is capped by sample length/pitch-shift rate; final gain fade is fixed at 20 ms | A long written note can run out of sound early |
| Legacy browser tempo | Uses the last tempo event for all ticks | Tempo changes do not follow the exported MIDI tempo map |

These are code and numeric findings. A MIDI note-off gap is not automatically
acoustic silence: a receiving synthesizer may continue a release tail. Likewise,
Wave's full written duration does not imply a convincingly connected sound.

## 2. Architecture

The [architecture overview](architecture.md#opt-in-performance-interpretation)
shows both rendering paths. The central implementation is
[`PerformancePlanner`](../src/SoundScript.Core/Performance/PerformancePlanner.cs),
a pure function over a voice's notes expressed in seconds. Each note also retains
its score onset/duration, phrase identifier, articulation, and instrument.
Score adjacency is checked separately from humanized playback time.

MIDI attaches that intent during expansion, plans after humanization, converts
durations back through the integrated tempo map, and assigns independent melodic
channels to instrumental tracks/layers. It emits ordinary notes plus versioned
text metadata for SoundScript audio envelopes. Wave adapts the AST independently
and calls the same planner; it does not depend on Midi or Parser.

The browser uses the metadata only for opted-in files. It preserves track identity
when pairing events, orders note-off before note-on at equal ticks, integrates
tempo events, and schedules continuous gain. Sustained instrument samples loop
through a region with a 30 ms blended seam, instead of stopping at source length.

## 3. Performance rules

These are conservative, explicit engineering limits, not claims of universal
musical optimality. They passed the five-case listening review above; other
instruments and styles may warrant further tuning.

| Situation | Rule |
|-----------|------|
| Connected melody | Same phrase and instrument; exactly adjacent score events; interval 1–5 semitones; both written durations at least 180 ms; no staccato/accent on either side; outgoing legato or a sustained instrument family |
| Overlap | `min(40 ms, 8% of each neighbouring duration)` beyond the next onset; audio release adds at most 20 ms, so total overlap is at most 60 ms |
| Repeated pitch / large leap | Fresh attack; no connection. Non-staccato notes leave up to 20 ms separation in addition to fitting their release before the next attack |
| Short notes | Below 180 ms, connection is disabled to protect rhythmic articulation |
| Staccato | MIDI retains its shaped 47% duration and velocity; audio uses a short attack and bounded 12 ms release |
| Accent | Fresh 4 ms attack; no connection into or out of the accented note |
| Connected sustained attack | 35 ms, versus 18 ms for a new sustained phrase; duration caps attack at one quarter of the note |
| Other attacks | 6 ms for ordinary percussive notes; 4 ms for explicit staccato/accent |
| Phrase velocity | Non-accented/non-staccato notes receive `0.94 + 0.10 × sin(π × position)`; a multi-note phrase ending receives an additional ×0.96 |
| Rest / phrase ending | Connection stops; note plus release is fitted within the written boundary. Master delay or other effects may still have independent tails |
| Humanization | Start offsets are bounded to the note's neighbourhood; a phrase's first attack cannot shift into the preceding rest |
| Sustained evolution | Sustained-family notes at least 600 ms long receive a deterministic 4% sine-shaped amplitude arch |
| Continuous dynamics | An end/start gain ratio follows the phrase crescendo/decrescendo position; this is evaluated within the held note, including a one-note phrase |
| Chords | Tone envelopes are supported; chords are excluded from melodic legato connections |
| Instrument layers | Plan independently; MIDI assigns distinct channels and skips percussion channel 10. More than 15 instrumental voices produces an explicit error |

Sustained families are GM programs 16–23, 40–44, 48–54, 56–79, and 88–95
(zero-based). This classification is a timbre heuristic, not an instrument-specific
physical model. A piano can still use explicit `legato`, but its sample is not
automatically sustained like a flute or violin.

## 4. Measured before/after results

| Diagnostic | Legacy | Expressive |
|------------|--------|------------|
| Half-note connection at 60 BPM | 1.94 s duration, 60 ms event gap | 2.04 s duration, 40 ms event overlap |
| Same diagnostic in MIDI | 931 ticks long; 29-tick gap | 979 ticks long; 19-tick overlap (39.583 ms) |
| Single held crescendo | Constant note-on velocity during sustain | Continuous end/start gain ratio `1.15 / 0.85 ≈ 1.353` |
| Single held decrescendo | Constant note-on velocity during sustain | Continuous end/start gain ratio `0.85 / 1.15 ≈ 0.739` |
| Staccato eighth at 60 BPM | 235 ms MIDI duration | 235 ms MIDI duration; same shaped velocity |
| Browser long sample | Stops at available sample duration | Loops until planned note-off plus bounded release |

The browser counts below come from the real MIDI parser used by the Playground,
not just from the AST. The exported MIDI note counts themselves do not change.

| Validation score / Playground preset | MIDI notes | Legacy browser notes | Expressive browser notes | Connected transitions | Maximum event overlap |
|--------------------------------------|-----------:|---------------------:|-------------------------:|----------------------:|----------------------:|
| Harbor Lights (`performance-harbor`) | 58 | 53 | 58 | 19 | 40 ms |
| Jingle Bells (`performance-bells`) | 82 | 70 | 82 | 0 | 0 ms |
| Ode to Joy (`performance-ode`) | 80 | 79 | 80 | 59 | 40 ms |
| Clockwork Garden (`performance-clockwork`) | 86 | 86 | 86 | 0 | 0 ms |
| Floating Lanterns (`performance-lanterns`) | 28 | 28 | 28 | 21 | 40 ms |

- **Harbor Lights:** original slow flute/piano/cello ballad; exposed legato gaps,
  instrument-channel collisions, phrase breaths, and short sample sustain.
- **Jingle Bells:** Pierpont's public-domain refrain; exposed repeated-note pairing
  losses. The performance planner adds no connected overlaps to this piano/bass score.
- **Ode to Joy:** Beethoven's public-domain theme; exercises repeated pitches among
  small lyrical intervals, explicit phrase endings, and violin/cello colors.
- **Clockwork Garden:** original fast piano/bass scherzo; checks that staccato pulse,
  rests, and deliberate leaps receive no legato overlap.
- **Floating Lanterns:** original layered flute/violin and cello miniature; exercises
  multi-second sustain, independent layers, breaths, crescendo and decrescendo.

All five presets were loaded and played from both the development build and the
published Release build in headless Edge. Ten before/after browser-sample recordings rendered through real Web Audio
without clipping. Wave before/after recordings and event JSON are also generated.
The listening page applies the same additional gain to each pair so differences
remain comparable. The user's subsequent listening feedback confirmed improvement
with clear articulation.

## 5. Reproduce validation

From the repository root, in PowerShell:

```powershell
$env:SOUNDSCRIPT_PERFORMANCE_ARTIFACTS = Join-Path $PWD 'artifacts/performance'
dotnet test src/SoundScript.Tests/SoundScript.Tests.csproj --filter FullyQualifiedName~ExpressivePerformanceTests
node scripts/verify-performance.cjs
```

For real-browser checks, start the compiled Playground in a separate terminal.
Use `--no-launch-profile`: the repository launch profile sets a `/playground`
path base while the page uses `/` on localhost.

```powershell
dotnet run --project src/SoundScript.Playground --no-launch-profile --urls http://127.0.0.1:5194
```

With Playwright available in Node's module path and its Chromium browser installed:

```powershell
# Optional on Windows with installed Edge:
$env:PLAYWRIGHT_CHANNEL = 'msedge'
node scripts/verify-performance-playground.cjs http://127.0.0.1:5194/
node scripts/performance-report.cjs
```

Open `artifacts/performance/index.html` for the listening pairs. The directory also
contains scores, MIDI, Wave PCM, browser-sample recordings, and measured event JSON.
These generated artifacts are ignored by Git. The tests do not require an external
synthesizer or a copyrighted reference recording.

The automated coverage includes parser scope/round-trip, measured legacy gaps,
bounded overlap, repeated pitches, leaps, accents, rest silence, phrase endings,
staccato preservation, deterministic held-note envelopes, crescendo/decrescendo,
tempo changes, humanized breaths, layers, independent MIDI channels, chord boundaries,
ties, channel exhaustion, MIDI metadata, byte determinism, and all five scores.
The existing example inventory adds only five new fingerprints; earlier values
and existing tests are retained.

Final full-suite result: **1,031 passed, 0 failed, 0 skipped**. The browser schedule
checks cover **334 notes**. Real-browser validation produced **10 recordings**
without clipping and exercised all **5 presets**. Three pre-existing xUnit analyzer
warnings remain in `VisualTimelineTests.cs`.

## 6. Remaining limitations and acceptance

- Listening acceptance comes from the user's five-case comparison. Unit tests and
  waveform energy alone do not establish perceptual quality, and this is not a
  calibrated listening study across instruments, soundfonts, and devices.
- Wave provides simple sine/triangle instrument-family colors, not sampled
  orchestral realism. MIDI sample playback retains the repository's limited GM
  sample set and pitch-shifted octave-3 recordings. Missing sample programs fall
  back to piano; a blended loop can still sound synthetic.
- External MIDI players do not read SoundScript's per-note amplitude metadata.
  They get shaped timing and velocity; continuous dynamics depend on the receiving
  synthesizer. No channel-wide expression CCs are emitted, avoiding changes to
  overlapping voices on the same channel.
- Phrase velocity processing and advanced chord voicing remain backend-specific.
  Wave still lacks the MIDI engine's general arpeggio expansion, chord voicing,
  gain refinement, and musical-intelligence modules. This feature does not claim
  full renderer parity.
- Vocal/speech articulation, physical bow/breath transitions, vibrato, and pitch
  slides are outside this performance layer. A held instrument tone is not sung speech.
- Explicit notation still matters: use suffix articulations or phrase defaults
  for dense runs. The existing parser can consume an articulation following a
  note as that note's suffix, even if it was intended as the next note's prefix.
- Humanization is deterministic within each backend; legacy MIDI and Wave use
  different seed policies. This change does not promise identical humanized samples
  or timbres across backends.

## 7. Files changed

- **Core and parser:** `Ast/PerformNode.cs`, `Performance/PerformancePlanner.cs`,
  `TimedNote.cs`, `InterpretedProgram.cs`, `Parser.cs`, and `SsPrinter.cs`.
- **MIDI:** `Interpreter.cs`, `ExpressivePerformance.cs`, and `MidiGenerator.cs`.
- **Wave:** `AstToNoteEventAdapter.cs`, `ExpressiveWaveAdapter.cs`, `NoteEvent.cs`,
  and `NoteRenderer.cs`.
- **Playground:** `midi-player.js`, `soundfont-loader.js`, editor keywords,
  `PerformanceExamples.cs`, preset catalog, page selection/menu, and embedded resources.
- **Verification:** `ExpressivePerformanceTests.cs`, five additive example
  fingerprints, `verify-performance.cjs`, `verify-performance-playground.cjs`,
  and `performance-report.cjs`.
- **Examples:** `performance-harbor.ss`, `performance-bells.ss`, `performance-ode.ss`,
  `performance-clockwork.ss`, and `performance-lanterns.ss`.
- **Documentation:** language reference, architecture, playback quality, expressive
  notation, phrase V3 reference, user guide, Wave grammar, example index, documentation
  index, and this engineering report.
