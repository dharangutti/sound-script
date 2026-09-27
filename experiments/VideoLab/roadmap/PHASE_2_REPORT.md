# Phase 2 — PASS, 2026-09-28

## Summary
One compiled synthetic assembly composition produces shop-floor, QA and engineering
videos through bounded enum/boolean bindings. Each view has distinct titles,
instructions, callouts and markers. Source footage stays identical.

## Architecture
Optional typedParameters adds enums and booleans beside unchanged decimal
parameters. SetBindings validates and publishes mixed updates atomically;
Snapshot retains immutable typed values. The existing closed typed AST supports
enum/boolean equality and logical composition. Named groups apply native
predicates to visuals and audio; group identity remains inspectable. Existing
batch rendering reuses one compiled structure for all three views.

## Files changed
TypedParameters.cs, Model.cs, Expressions.cs, Programming.cs, Batches.cs,
Program.cs, AudienceProof.cs, LocalWorkbench.cs, WebProof.cs; audience examples,
synthetic assembly asset, browser typed controls/tests/site and documentation.
Core checkpoint 0208518; intermediate remote checkpoint 60ef404.

## Compatibility
All 186 prior native checks pass. All 42 Phase 1 MP4/WebM assets (including two
source samples) retain identical hashes. Decimal SetMany remains supported.
No production SoundScript changes or new packages. Enum strings are bounded
symbols; arbitrary runtime strings are intentionally absent. No nested groups.

## Tests
- Final resumed full build: 239 native checks (186 prior + 53 audience), PASS.
- 42 gallery binding/format pairs repeated byte-identically and decoded, PASS.
- Nine-card public browser suite: all bindings/formats/source/scenes, PASS.
- Local API/browser suite: typed values, invalid types, enum/boolean controls,
  native audience rendering, reset, existing media flows and cleanup, PASS.
- CLI enum/boolean/decimal parsing and invalid/duplicate assignments, PASS.
- Twelve independent encoder regression outputs remain identical, PASS.
- Three decoded audience images and local QA browser screenshot reviewed.

The first gallery run failed with Windows FFmpeg startup error 0xC0000142. Its
partial staging directory was never published. FFmpeg launched normally on
resume; a fresh build and full rerun passed without skipped assertions. The
local browser test exposed overlapping upload busy-state restoration; reference
counting fixed it, and both browser suites passed after the fix.

Commands: dotnet build -c Release -o artifacts/phase2-resume-bin;
dotnet artifacts/phase2-resume-bin/VideoLab.dll webproof;
node web/smoke.cjs; node web/local.test.cjs; node web/typed-cli.test.cjs;
node web/encoder.test.cjs. Set VIDEOLAB_DLL to that binary for native Node tests.
Durable log location: artifacts/roadmap-phase2-resume.log (ignored).

## Demonstration
examples/audience.json + examples/audience-batch.json generate three outputs.
The private gallery has nine demos, 23 snapshots and 46 exports. Local audience
and showSafety selectors change the native scene and rendered output. The assembly
schematic and instructions are illustrative, not instructions for a real product.

## Public deployment recommendation
KEEP PRIVATE/BRANCH-ONLY. Gate passed, but the user has reserved the merge decision.
Public v0.3.0 pins/tags are unchanged. No production merge or deployment.

## Next phase
Phase 3 is explicitly authorized: bounded external annotation data, pure lowering
to existing primitives, one base project with three datasets and batch outputs.
See PHASE_3_DESIGN.md. Stop after Phase 3's report; Phase 4+ is not authorized.
