# SoundScript.Wave — Safeguards & Maximum-Derivation Principles (v1.1 addendum)

## Purpose

`.ssw` must remain a **strict, minimal superset** of `.ss`. This document is
the checklist to apply before adding any new construct, feature, or runtime
behavior to the wave backend, so scope stays controlled as the project grows.

## The core test before adding anything new

Before adding a keyword, directive, or runtime parameter, answer:

1. **Can this be expressed with existing `.ss` grammar?**
   If yes — do not add new syntax. Reuse the existing construct.
2. **Does this exist only because WAV output physically requires it
   (frequency, envelope, sample rate) and MIDI has no equivalent concept?**
   If yes — it qualifies as a legitimate new construct.
3. **Does this break a `.ss` file if someone mistakenly runs it through the
   wave backend?**
   Must always be "no." Fallback to sane defaults, never error on missing
   wave-specific directives.

## Grammar extension rule (v3 upgrade)

Formalizing rule 2 above into an explicit, standing permission:

> `.ssw` may introduce new grammar constructs when (a) the feature cannot be
> expressed in `.ss`, AND (b) the new construct is isolated to the wave
> backend without breaking `.ss` compatibility.

Both conditions are required, not either/or. A construct that's wave-only
but *could* have been expressed with existing grammar still fails the test —
isolation to the wave backend is necessary but not sufficient on its own.
This clause exists so future grammar additions (e.g. an effects-chain
directive, a prosody/tone directive) have a clear, pre-agreed bar to clear
instead of being argued case-by-case from first principles each time.

## Safeguards for determinism (non-negotiable)

- No unseeded randomness anywhere in the wave pipeline. Every source of
  variation (tone jitter, humanization, detune drift) takes an explicit or
  derived seed.
- Same seed + same input file = byte-identical `.wav`, forever, across
  machines/OS/CPU. This is a regression test, not a suggestion — add a
  checksum test to CI once merged.
- Any new "human variation" feature (per earlier tone/prosody discussion)
  must expose its seed as a first-class parameter in the `.ssw` syntax, not
  bury it in code. Users should be able to reproduce or intentionally vary
  a take.

## Safeguards for backward compatibility

- `SoundScript.Wave` must remain addable/removable without touching
  `SoundScript.Core`, `SoundScript.Midi`, or `SoundScript.Timbre`.
- A `.ss` file with zero wave-specific directives must still render (sine +
  neutral ADSR default) rather than fail, if ever pointed at the wave
  backend.
- New wave-only keywords must be rejected gracefully (clear error, not a
  crash) if encountered by the MIDI backend — the parser should recognize
  them as valid tokens even where the MIDI generator doesn't act on them.

## Safeguards for scope creep

Maintain a running "parking lot" of ideas considered but deliberately not
built yet. Every idea from the SoundCSS-to-frequency/prosody discussion goes
here until there's real user demand.

Shipped (no longer parked):

- Stereo panning, band-limited/anti-aliased oscillators, wavetable/DDS
  lookup-table oscillator — shipped in v2
- Effects chain — **delay + single-pole low/high-pass filter shipped in v3**
  (`effect delay time= feedback= mix=`, `effect filter type= cutoff=`),
  master-only, post-mix
- Seeded jitter — shipped in v3 as the named-parameter extension of the
  existing `humanize` directive (`timing= velocity= seed=`), consumed by the
  wave adapter via the shared `DeterministicRandom` helper
- Phoneme-level frequency/prosody mapping (seeded tone variation) — shipped
  in v3 as `speak "..." voice= seed=`, proof-of-concept scope only

Still parked:

- Reverb — deferred from v3, delay/filter shipped instead
  (Schroeder/Freeverb-class algorithms are meaningfully heavier than the v3
  window allowed; the parser rejects `effect reverb` with a message pointing
  here rather than silently ignoring it)
- Per-track effect routing — v3 shipped master-only effects (documented
  decision in `MasterEffectChain`); the per-track variant reuses the same
  grammar and DSP when demand appears
- Full prosody/multi-language phoneme modeling, formant synthesis, anything
  approaching production TTS quality — v3 shipped a small fixed
  grapheme-driven English-ish table (`PhonemeFrequencyTable`) purely to prove
  the deterministic tone-variation mechanism
- Steeper filter slopes (biquad/multi-pole) — v3 shipped single-pole,
  6 dB/octave
- Additional `speak` voices — v3 ships `voice=default` only; other names are
  rejected with a clear error

Nothing moves out of the parking lot without a specific, named use case
(a user request, a concrete bug like platform-dependent float drift, etc.)
— not "this would be a cool feature."

## Safeguards for messaging (from novelty discussion)

- Never claim invention of an underlying DSP technique (wavetable synthesis,
  ADSR envelopes, prosody modeling) — these predate the project by decades.
- Do claim the actual differentiators, and only these:
  1. One shared grammar, two independent backends (MIDI and direct WAV)
  2. Byte-identical determinism as a first-class, tested guarantee
  3. Zero external dependencies — no DAW, no soundfont, no MIDI stack required
  4. Small, embeddable, readable-as-code syntax (vs. Csound/SuperCollider's
     steeper learning curve)

## Review checklist before merging any `.ssw` feature

- [ ] Passes the three-question test above
- [ ] No unseeded randomness introduced
- [ ] Determinism regression test still passes (byte-identical output)
- [ ] `.ss` files unaffected, still parse and render via MIDI path unchanged
- [ ] New construct documented with a one-line "why MIDI can't express this"
      justification in the changelog/PR description
- [ ] If deferred rather than built: added to the parking lot list above,
      not silently dropped
