# Milestone 2 — Polyphonic / Piano

Branch `codex/polyphonic-piano-transcription`, based on Milestone 1 commit `6e1a0c6`.
Only this milestone is in scope; both earlier analysis paths remain unchanged.

## Implementation phases

1. Add independent simultaneous-pitch observations and a conservative spectral
   analyzer. Preserve pitch-level timing/evidence, reject diffuse/dense ambiguity,
   and measure dyads, triads, repeated attacks, octaves and sustained overlap.
2. Interpret each pitch independently and allocate overlapping notes to existing
   parallel SoundScript tracks. The current named-chord AST expands fixed voicings
   and cannot preserve arbitrary inversions/independent offsets; parallel note
   tracks already can. No language or rendering changes are needed.
3. Expose the same engine through CLI and Playground, with polyphonic diagnostics,
   appropriate round-trip comparison, excerpt playback and stale-output protection.
4. Run focused checks during development, then complete full .NET, Node, Release
   integrity/startup and real-browser checks. Measure multiple original piano
   excerpts against both existing modes, retaining third-party audio outside Git.
5. Generate a local listening comparison page and record human review separately.
   Automated simultaneous-note output is not proof of improved resemblance.

## Measurable gates

- Existing monophonic/extraction source and analysis baselines must remain stable.
- Known synthetic note sets must be measured using one-to-one pitch/onset matching,
  precision/recall, timing errors and simultaneous pitch-set agreement.
- Generated source must use the existing AST/printer/parser, represent overlaps,
  and render non-silent audio with the existing renderer.
- Unsupported material must reject or explicitly expose partial evidence.
- Sync/async results and CLI/browser output must agree for the same normalized PCM.
- Listening acceptance remains pending until a human reviews original/melody/
  polyphonic examples; no fabricated listening verdict.
