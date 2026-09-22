# Experimental melody extraction (Milestone 1)

Branch: `codex/experimental-melody-extraction`, based on `a303776`.

The supplied advanced transcription roadmap is design input. This implementation
covers its first milestone only; polyphonic scores, separation, percussion, live
capture and symbolic import remain separate milestones.

## Phases and acceptance criteria

1. Shared engine: explicit modes, deterministic spectral extraction, measurable
   dominance/coverage/ambiguity and rejected intervals. Reuse the V13 interpreter.
   Existing default monophonic observations and generated sources must be identical.
2. CLI and Playground: opt-in mode, structured reports, experimental labels,
   original/generated playback, invalidation on option changes. Both callers use
   the same engine; unsupported modes fail explicitly.
3. Focused tests: dominant harmonic melody with accompaniment must recover known
   pitches; equal voices, dense mixtures, silence and noise must reject. Verify
   synchronous/asynchronous parity, cancellation, valid source and audible render.
4. Final validation: full .NET suite, browser tests/startup, CLI smoke, original
   local piano excerpts if available. Record results and limitations honestly;
   synthetic success alone does not establish real piano acceptance.

Run only checks necessary to resolve each phase's risks, reserving broad regression
for the integrated implementation. No new model, service or package is planned.

## Outcome

Core, CLI, Playground and automated verification are implemented. Final validation:
1,103 .NET tests and 20 Node tests passed; Release browser workflow and all three
startup scenarios passed. The publish-artifact test initially failed because its
default directory was absent; rerunning with `SOUNDSCRIPT_PLAYGROUND_PUBLISH_DIR`
set to the new verified Release artifact passed the full suite.

External acceptance is partial: one of ten original piano excerpts yields an
experimental candidate; nine reject. Jazz and drum excerpts reject. Recognizable
real-piano reconstruction remains unverified by listening/independent source truth.
See [implementation, measurements and limitations](melody-extraction.md). No later
milestone or public deployment is included in this branch.
