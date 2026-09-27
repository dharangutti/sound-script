# VideoLab roadmap — durable restart record

Last updated: 2026-09-28. Development branch: experiments/videolab.

## Authorized scope and current state

Phases 0–3 are complete and validated. The user first requested Phases 0/1, then
explicitly authorized Phases 2/3, and finally requested a deployment PR after
Phase 3. Stop for user testing after that PR. Phase 4+ remains unauthorized.
Do not merge experimental implementation into main or advance later phases.

| Phase | Implemented | Validated | MVP-worthy | Publicly hosted |
| --- | --- | --- | --- | --- |
| 0 — baseline audit | Complete | PASS; exact artifact reproduced | Audit only | v0.3.0 baseline |
| 1 — editing/text/callouts | Complete | PASS; PHASE_1_REPORT.md | Yes | Publication PR pending |
| 2 — typed audience variants | Complete | PASS; PHASE_2_REPORT.md | Yes | Publication PR pending |
| 3 — external annotations | Complete | PASS; PHASE_3_REPORT.md | Yes | Publication PR pending |
| 4 — ranked editor improvements | Not authorized | No | No | No |
| 5 — explicit variant generation | Not authorized | No | No | No |
| 6 — build artifacts/provenance | Not authorized | No | No | No |
| 7 — industrial reference scenario | Not authorized | No | No | No |
| 8 — profile-guided longer-form backend | Conditional future work | No | No | No |

## Restart here

1. Read this file, MASTER_ROADMAP.md and completed phase reports. Inspect git status
   and log; preserve in-progress user work. Do not repeat completed native gates.
2. Current task: publish a v0.4.0 immutable artifact tag, pin it on
   codex/videolab-phase3-publication, validate staging/site and raise its PR to main.
3. Publication checkout: C:/Users/dhara/.codex/worktrees/labs-hosting/VideoLabs.
   It is based on origin/main 83ed217 and is isolated from experimental code.
4. Update this file with the final tag/commit/PR/checks after creating the PR.
5. Stop for the user's testing decision. Do not merge the PR automatically.

## Validated milestones and evidence

- Baseline db9ebebb778fd823f490cc3a886ab8ea0f4dab77, immutable tag
  labs-videolab-mvp-web-v0.3.0. PR 146 was merged by the user.
- Phase 1 completed at 605dd25; core f36122c. 186 native checks and 36 gallery pairs.
- Phase 2 completed/pushed at 5720653; core 0208518. 239 native checks, 42 gallery
  pairs and both browser suites. Initial FFmpeg startup failure was rerun fully.
- Phase 3 core pushed at 57e5693. Final gate: 294 native checks, 48 repeated/decode
  pairs, ten demos / 26 bindings / 52 exports. Both browser suites, typed/annotation
  CLI suites, twelve encoder repeats and three disconnect/shutdown cases passed.
- All 49 Phase 2 video assets remain byte-identical. Static artifact/evidence hashes
  are in web/publication-evidence.json. Completed phase reports explain limitations.
- Private real-media validation passed 17/17 at Phase 1; it was not required again
  for annotation-only changes. The 48 normalization regression checks remain green.

## Commands and local evidence

Run from experiments/VideoLab. Validated .NET 10.0.303, FFmpeg/ffprobe 9.0.1,
Playwright 1.63.0 Chromium. PLAYWRIGHT_MODULE is available at
C:/Users/dhara/.codex/worktrees/labs-hosting/VideoLabs/scripts/node_modules/playwright.

Final native/gallery run: dotnet artifacts/phase3-final-bin/VideoLab.dll webproof.
Log: artifacts/roadmap-phase3-final.log (ignored). Final local-adapter build is
artifacts/phase3-verified-bin/VideoLab.dll; set VIDEOLAB_DLL to this for Node tests.
Tests: web/smoke.cjs, web/local.test.cjs, web/typed-cli.test.cjs,
web/annotations-cli.test.cjs, web/encoder.test.cjs, web/shutdown.test.cjs.

A new checkout can run dotnet run -c Release -- webproof, then these Node tests.
Private original downloads/logs are ignored and must not be committed. Public
samples and the final static outputs are versioned. No background server is
required to preserve this state. See phase reports for scoped reproducibility.
