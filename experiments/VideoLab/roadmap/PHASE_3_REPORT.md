# Phase 3 — PASS, 2026-09-28

## Summary
One base project accepts three external review datasets. Strict bounded annotation
data generates ordinary titles, callouts and highlight outlines. CLI, batch and
browser inspection expose the same native result; source footage is unchanged.

## Architecture
Annotations.Expand is pure data-to-primitives lowering followed by normal compiler
validation. Categories/severity select a fixed palette. Generated groups retain
annotation IDs and enum audience predicates. Each dataset compiles once per batch
and shares immutable snapshots across its bindings. File reading is explicit in
adapters, bounded in size; all datasets validate before any batch output is written.
Cross-record media/dataset output protection supplements existing atomic replacement.

## Files changed
Annotations.cs, AnnotationProof.cs, Model.cs, Batches.cs, Program.cs,
LocalWorkbench.cs, WebProof.cs; one annotations.json base, three external dataset
files and batch example; browser source/tests/site, semantics, usage and evidence.
Core checkpoint: 57e5693. No production SoundScript runtime or package changes.

## Compatibility
All 239 earlier native checks remain green. All 49 Phase 2 MP4/WebM assets retain
identical hashes. Existing decimal/typed APIs and render paths are preserved.
Limits remain explicit: 24 records, printable ASCII text, fixed font size/palette,
three styles and existing aggregate bounds. No arbitrary annotation scripting.

## Tests
- Full webproof: 294 native checks (239 prior + 55 annotations), PASS.
- 48 gallery format/binding repeat/decode pairs, PASS.
- Ten-card public browser suite, 26 snapshots / 52 exports, PASS.
- Local API/browser suite including dataset selection/source inspection, native
  annotation render, typed reset, media validation and cleanup, PASS.
- CLI annotation loading and three external-dataset batch renders, PASS.
- Invalid later dataset prevents all batch writes, PASS.
- 12 independent encoder repeats, PASS.
- Three graceful stops with disconnected in-flight responses, PASS.
- Three decoded annotation views and local browser screenshot visually reviewed.

The local test caught a shutdown response race: an already-sent/closed response
could fail again while sending an error. Error-response handling now tolerates
transport teardown while preserving unexpected task faults; the local suite and
three explicit disconnect/shutdown regression runs passed. Renderer code and the
validated static gallery did not change for that adapter fix.

Build/test commands are recorded in web/publication-evidence.json. Full native log:
artifacts/roadmap-phase3-final.log. Final adapter binary:
artifacts/phase3-verified-bin/VideoLab.dll. Logs/media intermediates stay ignored.

## Demonstration
examples/annotations.json remains the common base. Files under examples/annotations
contain shop-floor instructions, QA comments and engineering review. Use
examples/annotations-batch.json, or the browser's “Review notes become video” card.
The inspector separates base JSON, selected dataset and generated composition.

## Public deployment recommendation
READY FOR LAB PUBLICATION. The user explicitly requested a deployment PR after
Phase 3. Publish only the validated static artifact via an immutable v0.4.0 tag
and main-owned manifest/evidence pin; keep native implementation on the experiment
branch. A PR merge and normal Pages deployment are still required for the live URL.

## Next phase
Stop after the publication PR for user testing. Phase 4 (ranked editor improvements)
and later phases are not authorized. Preserve all phase reports and resume state.
