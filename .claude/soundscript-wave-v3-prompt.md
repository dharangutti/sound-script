# SoundScript.Wave — v3 Build Prompt (Backend Only)

## Context

v1 (oscillator/envelope/mixer/WAV writer) and v2 (wavetable/DDS, band-limited
oscillators, stereo panning) are complete. v3 pulls three items out of the
parking lot, each gated through the safeguards doc's grammar extension rule:

> `.ssw` may introduce new grammar constructs when (a) the feature cannot be
> expressed in `.ss`, AND (b) the new construct is isolated to the wave
> backend without breaking `.ss` compatibility.

**Backend only. No UI, no CLI polish, no front-end.** Mark all new/changed
files with `// UNDER DEVELOPMENT — v3` at the top.

## Build order (dependency-driven, not arbitrary)

1. Effects chain
2. Seeded jitter (extends existing `humanize`, does not add new grammar)
3. Phoneme/prosody tone mapping (built last, consumes the seed mechanism
   from step 2 so its variation is deterministic from day one)

---

## 1. Effects chain (reverb / delay / filters)

**Gate check:** passes both conditions — MIDI has no post-processing concept
to reuse, and this is a pure post-mix stage fully isolated to
`SoundScript.Wave`. New grammar justified.

- Post-processes the final mixed buffer only — does not touch `NoteEvent`,
  `TimbreParams`, the adapter, or the parser/AST for anything upstream
- v3 scope: **delay** (simple feedback delay line) and **a basic low-pass /
  high-pass filter** (single-pole IIR is enough for v3). Reverb is
  algorithmically heavier (Schroeder/Freeverb-style) — include only if time
  allows; otherwise defer reverb specifically to the parking lot as
  "reverb — deferred from v3, delay/filter shipped instead," don't silently
  drop it
- New grammar, isolated to `.ssw`:
  ```
  effect delay time=0.25 feedback=0.4 mix=0.3
  effect filter type=lowpass cutoff=2000
  ```
- Effects apply per-track before the final mix-down, or on the master buffer
  post-mix — pick one and document why (per-track is more flexible, master
  is simpler and sufficient for v3; recommend master-only for v3, per-track
  as a parking-lot follow-up)
- Must remain fully deterministic: no time-based/wall-clock state, same
  input buffer + same effect parameters = same output buffer, always

## 2. Seeded jitter / humanization (extends existing shared construct)

**Gate check:** fails the "cannot be expressed in `.ss`" condition —
`humanize` already exists as a shared directive. This is an **extension**,
not new grammar. Do not introduce a new keyword for this.

- Add an explicit `seed` parameter to the existing `humanize` directive:
  ```
  humanize timing=0.02 velocity=0.1 seed=42
  ```
- If `seed` is omitted, derive one deterministically from file content
  (e.g. a hash of the track/pattern name) — never fall back to wall-clock
  or unseeded `Random`, per the safeguards doc's determinism rule
- Wave backend consumes the same seed to jitter `NoteEvent.StartTimeSeconds`
  and `NoteEvent.Velocity` within the humanize bounds, using a seeded PRNG
  local to that note/track — must produce identical jitter across runs and
  platforms given the same seed
- This is also the mechanism prosody (item 3) will reuse for tone variation,
  so build the seeded-PRNG utility as a small shared internal helper now
  rather than duplicating it in the prosody module later

## 3. Phoneme / prosody tone mapping

**Gate check:** passes — no equivalent in `.ss`/MIDI, isolated to the wave
backend. This is the largest new subsystem in the project so far; treat it
as its own sub-scope.

- New subsystem: text → phoneme sequence → base frequency range per phoneme
  → `NoteEvent` sequence, sitting alongside (not replacing) the existing
  AST-to-`NoteEvent` adapter
- Start narrow: a small, fixed phoneme-to-frequency-range table (not a full
  linguistic model) — enough to demonstrate "same word, different tone,"
  not a production TTS engine
- Tone variation within a phoneme's allowed range uses the seeded jitter
  utility from item 2 — same word + same seed = same tone, always;
  different seed = different but still deterministic tone
- New grammar, isolated to `.ssw` (justified: no `.ss`/MIDI equivalent
  exists for phoneme-level pitch mapping):
  ```
  speak "hello world" voice=default seed=7
  ```
- Explicitly out of scope for v3: full formant synthesis, multi-language
  phoneme sets, anything resembling real TTS quality — this is a
  proof-of-concept demonstrating the tone-variation mechanism, not a
  speech synthesizer

---

## Non-goals for v3 (still in parking lot / deferred)

- Reverb (deferred from effects chain unless time allows — see note above)
- Per-track effect routing (master-only for v3)
- Multi-language or production-quality phoneme/prosody modeling
- Any UI, playback preview, or visualization tooling

## Determinism requirement (unchanged, re-verified for v3)

- Effects chain: same input buffer + params → same output buffer
- Seeded jitter: same seed → byte-identical jitter, across platforms
- Prosody: same text + same seed → byte-identical `NoteEvent` sequence
- Extend the CI checksum suite to cover all three new paths independently,
  plus one combined test (effects + jitter + prosody together) to catch
  interaction bugs

## Deliverable checklist

- [ ] `effect delay` and `effect filter` grammar + post-mix processing stage
- [ ] Reverb: implemented, or explicitly deferred with parking-lot note
- [ ] `humanize ... seed=N` extension (no new keyword — extends existing)
- [ ] Shared seeded-PRNG helper used by both jitter and prosody
- [ ] Phoneme-to-frequency-range table (small, fixed, documented as v3-scope
      "proof of concept," not production TTS)
- [ ] `speak "..." voice=... seed=...` grammar + adapter path to `NoteEvent`
- [ ] Determinism regression tests for all three features, independently
      and combined
- [ ] Zero modifications to `SoundScript.Core`, `SoundScript.Midi`,
      `SoundScript.Timbre`, or existing `.ss` grammar/behavior
- [ ] Every new/changed file marked `// UNDER DEVELOPMENT — v3`
- [ ] Safeguards doc updated: remove these three items from the parking
      lot list, note what shipped vs. what was deferred (reverb, per-track
      routing, full prosody)
