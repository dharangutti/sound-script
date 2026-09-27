# VideoLab roadmap — durable restart record

Last updated: 2026-09-27. Branch: `experiments/videolab`.
Baseline: `db9ebebb778fd823f490cc3a886ab8ea0f4dab77`, tag
`labs-videolab-mvp-web-v0.3.0`. PR 146 was merged by the user; live provenance
currently matches this baseline. Do not retag it or update public hosting during
roadmap development. The user will decide when to merge after completion.

## Authorized scope

Implement Phase 0 and Phase 1 from [the saved roadmap](MASTER_ROADMAP.md).
Its final execution instruction explicitly excludes Phase 2. Report and stop after
the Phase 1 gate. Track all later phases here, but do not implement them yet.
All implementation stays under experiments/VideoLab; no production solution,
NuGet, SoundScript CLI/runtime, release or deployment changes.

| Phase | Implemented | Validated | MVP-worthy | Publicly hosted |
| --- | --- | --- | --- | --- |
| 0 — baseline audit | Complete | PASS; exact baseline artifact reproduced | Audit only | Baseline v0.3.0 already live |
| 1 — text/callouts/editing foundation | In progress | Not started | Not assessed | No |
| 2 — typed audience variants | Not authorized | No | No | No |
| 3 — structured annotations | Not authorized | No | No | No |
| 4 — ranked editor improvements | Not authorized | No | No | No |
| 5 — explicit variant generation | Not authorized | No | No | No |
| 6 — build artifacts/provenance | Not authorized | No | No | No |
| 7 — industrial reference scenario | Not authorized | No | No | No |
| 8 — profile-guided longer-form backend | Conditional future work | No | No | No |

## Current checkpoint / restart here

1. Read this file, MASTER_ROADMAP.md and phase reports before editing.
2. Inspect `git status` and the latest commits; preserve in-progress user work.
3. Baseline validation passed as `dotnet bin/Release/net10.0/VideoLab.dll webproof`
   from the Lab directory. Log: ignored `artifacts/roadmap-phase0-native.log`.
   If interrupted, rerun it and the browser/local/encoder checks below. Never infer
   success from an empty/partial log. Compare rebuilt web/site bytes to baseline.
4. Phase 0 is complete; see PHASE_0_REPORT.md. All checks passed and the complete
   artifact hash exactly matched the published baseline. Do not redo the audit.
5. Phase 1 work: controlled-font deterministic text; text+shape callouts; ordinary
   editing example and readable browser tracks/controls; regression and render gates.
6. After every completed slice, update this checklist and record commands/results.
   Commit coherent milestones on the experimental branch. Do not merge or deploy.

## Validation commands

Run from `experiments/VideoLab`. .NET 10.0.303 and FFmpeg/ffprobe 9.0.1 validated.
Playwright 1.63.0 is installed at
`C:/Users/dhara/.codex/worktrees/labs-hosting/VideoLabs/scripts/node_modules/playwright`;
set PLAYWRIGHT_MODULE to this path if this checkout has no installation.

```powershell
dotnet run -c Release -- webproof
node web/smoke.cjs
node web/local.test.cjs
node web/encoder.test.cjs
dotnet run -c Release -- realtest real-media/manifest.json
```

Private real-media validation is optional when those ignored files are unavailable;
record a skip explicitly. The baseline expects 143 native checks, 32 gallery
repeat/decode pairs, public/local browser suites and 12 encoder repeats. Do not
weaken them. Refresh publication evidence only for an actual approved publication,
not an in-progress development build.

## Decisions and known constraints

- Source JSON remains authoritative; no drag-and-drop editor or typed audience
  parameters in Phase 1.
- Preserve immutable Composition/Runtime/Snapshot/SceneAt semantics and bounded
  reference lowering. Text must not rely on system font discovery or unsafe FFmpeg
  string interpolation.
- Existing local workbench may be running on port 8745 from the previous task.
  Test servers own port 18745. Do not confuse either with the deployed public site.
- Public provenance was fetched directly over HTTPS; it pins db9ebeb and artifact
  SHA256 24c9ae42feb38e98871ff5c6c2f90795f109447dc2c92bbad7ba33bf002280e9.
