# VideoLab roadmap — durable restart record

Last updated: 2026-09-28. Branch: `experiments/videolab`.
Baseline: `db9ebebb778fd823f490cc3a886ab8ea0f4dab77`, tag
`labs-videolab-mvp-web-v0.3.0`. PR 146 was merged by the user; live provenance
currently matches this baseline. Do not retag it or update public hosting during
roadmap development. The user will decide when to merge after completion.

## Authorized scope

Initial roadmap scope was Phases 0 and 1 only. On 2026-09-27 the user explicitly
extended it: complete the current milestone, then the next two phases (2 and 3).
Finish and report each gate before starting the next. Phase 4+ remains queued.
Keep all work in experiments/VideoLab on the experimental branch, without merging
or deploying; the user will decide when to merge.

| Phase | Implemented | Validated | MVP-worthy | Publicly hosted |
| --- | --- | --- | --- | --- |
| 0 — baseline audit | Complete | PASS; exact baseline artifact reproduced | Audit only | Baseline v0.3.0 already live |
| 1 — text/callouts/editing foundation | Complete | PASS; see PHASE_1_REPORT.md | Yes, bounded Lab scope | No |
| 2 — typed audience variants | Core implemented; browser gate running | 53 focused checks PASS | Pending full gate | No |
| 3 — structured annotations | Authorized after Phase 2 gate | Pending | Pending | No |
| 4 — ranked editor improvements | Not authorized | No | No | No |
| 5 — explicit variant generation | Not authorized | No | No | No |
| 6 — build artifacts/provenance | Not authorized | No | No | No |
| 7 — industrial reference scenario | Not authorized | No | No | No |
| 8 — profile-guided longer-form backend | Conditional future work | No | No | No |

## Current checkpoint / restart here

1. Read this file, MASTER_ROADMAP.md and completed phase reports.
2. Inspect git status/log; preserve in-progress work. No merge/deployment.
3. Phase 0 and Phase 1 gates passed. Do not repeat the baseline audit.
4. Implement Phase 2 next: bounded enum/boolean parameters, typed conditions,
   named composition groups and three synthetic audience outputs from one source.
5. Record Phase 2 gate/report, then implement Phase 3 external annotations.
6. Stop after Phase 3 report. Update this file and commit each coherent slice.

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

### Phase 1 checkpoint — complete

See PHASE_1_REPORT.md and phase1-evidence.json. Full native/gallery, browser,
local API, encoder and private real-media checks passed. Core commit f36122c.
Final binary is artifacts/phase1-final-bin/VideoLab.dll. Set VIDEOLAB_DLL for
local/encoder tests when using that isolated output. Public pins remain unchanged.

- Source JSON remains authoritative; no drag-and-drop editor or typed audience
  parameters in Phase 1.
- Preserve immutable Composition/Runtime/Snapshot/SceneAt semantics and bounded
  reference lowering. Text must not rely on system font discovery or unsafe FFmpeg
  string interpolation.
- Existing local workbench may be running on port 8745 from the previous task.
  Test servers own port 18745. Do not confuse either with the deployed public site.
- Public provenance was fetched directly over HTTPS; it pins db9ebeb and artifact
  SHA256 24c9ae42feb38e98871ff5c6c2f90795f109447dc2c92bbad7ba33bf002280e9.

### Phase 2 active checkpoint

TypedParameters.cs adds bounded enum/boolean declarations; legacy decimal API is
retained. SetBindings publishes mixed values atomically. Group predicates compile
into typed ASTs and appear in SceneAt. One audience.json and audience-batch.json
produce three synthetic views; 53 focused checks including six repeated/decoded
outputs passed. Full native/gallery run uses artifacts/phase2-final-bin and log
artifacts/roadmap-phase2-final.log. Browser suites still pending. Do not start
Phase 3 until this gate/report is complete.

Final focused Phase 2 binary: artifacts/phase2-verified-bin/VideoLab.dll;
53/53 checks PASS on the synthetic assembly fixture. Full gallery run uses the
earlier phase2-final-bin (51 checks); final parser compatibility correction is
covered by the extra focused check. Native gates will run again with Phase 3.

### Remote preservation checkpoint — 2026-09-28

Phase 1 complete at 605dd25; Phase 2 core at 0208518. Final focused audience
proof passed 53 checks, including all six MP4/WebM outputs repeated and decoded.
Evidence is saved in phase2-core-evidence.json. Browser source integration is
implemented but NOT gate-approved. Full gallery build failed while launching
FFmpeg for transforms-B.webm with Windows exit -1073741502 (0xC0000142).
The atomic gallery publish did not occur, so web/site remains the verified
eight-card Phase 1 artifact. Do not claim Phase 2 gallery or browser completion.

Restart: verify FFmpeg can launch, rebuild current source to a fresh output, rerun
webproof with line-flushed log, then run both browser suites with VIDEOLAB_DLL
pointing to that build. Preserve all regression assertions. Finish Phase 2 report
and checkpoint before Phase 3. User explicitly requested this intermediate state
be pushed to origin/experiments/videolab. No merge or public deployment.
