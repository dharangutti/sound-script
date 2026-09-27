# Phase 1 — PASS, 2026-09-27

## Summary
Deterministic titles and label/pointer callouts now share the existing transform,
condition, animation and immutable snapshot model. The browser offers an eighth
ordinary editing example, six readable lanes and an editing-properties inspector.

## Architecture
Optional texts/callouts compile to existing visual programs. DejaVu Sans 2.37 is
embedded with its license and fixed hash. Literal caption files use FFmpeg
expansion=none; generated font/text resources are isolated and cleaned. Caption
planes are rasterized once then transformed by the bounded reference renderer.
No system font discovery, host scripting or production dependencies were added.

## Files changed
Model.cs, Programming.cs, Captions.cs, Ffmpeg.cs, ProgrammableBackend.cs,
EditingProof.cs, Program.cs, VideoLab.csproj, fonts/, examples/editing.json,
LocalWorkbench.cs, WebProof.cs, web source/tests/site and semantics/architecture/docs.
Core checkpoint: f36122c. Browser integration is the following milestone commit.

## Compatibility
All 143 existing native checks remain green. All 38 baseline MP4/WebM files
(including two input samples) retain their exact SHA256 values. Existing JSON
remains valid; caption metadata is omitted when absent. Conditions remain numeric
in this milestone. Single-line printable ASCII, one bundled font, no wrapping,
no text editing widgets or drag-and-drop. Callout targets are local to their plane.

## Tests
- Full webproof: 186 native checks (143 existing + 43 editing), PASS.
- 36 gallery format/binding pairs rendered twice, byte-identical and fully decoded.
- Eight demos, 20 snapshots, 40 exported videos; public browser suite PASS.
- Local API/media/atomic bindings/cancellation/render/reset/cleanup suite PASS,
  including editing controls, native render and mobile overflow regression.
- 12 independent encoder regression renders byte-identical, PASS.
- Private real-media validation: 17/17 PASS.
- Desktop/mobile screenshots and decoded acceptance image visually inspected.
- Mobile export checksum wrapping and concurrent bind/upload ordering were fixed
  after the browser tests exposed them; both browser suites passed after fixes.

Commands run from the Lab directory using artifacts/phase1-final-bin/VideoLab.dll:
webproof, editingtest, realtest real-media/manifest.json; node web/smoke.cjs,
node web/local.test.cjs and node web/encoder.test.cjs with VIDEOLAB_DLL override.
Native log: artifacts/roadmap-phase1-final.log (ignored).
Real-media log: artifacts/roadmap-phase1-real.log (ignored).

## Demonstration
examples/editing.json combines two trimmed clips, crossfade, title, animated
callout, shape, audio gain and bounded position/scale/rotation/opacity/crop controls.
Load “Titles, callouts and familiar edits” in the private local browser workbench.
Acceptance MP4 SHA256:
E7E1ABA432392E78F12238E276AA07FEE36990B96CBA85A5BB976DF40EE8CCB7.

## Public deployment recommendation
KEEP PRIVATE/BRANCH-ONLY. The milestone passes its gate, but the user wants to
review the completed roadmap work before merging. Public v0.3.0 provenance and
hosting remain unchanged. New gallery outputs are development artifacts only.

## Next phase
The user explicitly authorized Phases 2 and 3 after this gate: typed audience
parameters/groups/batch variants, then bounded external annotation datasets.
Phase 4 and later remain unauthorized. Keep separate reports and checkpoints.
