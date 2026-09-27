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
| 1 — editing/text/callouts | Complete | PASS; PHASE_1_REPORT.md | Yes | PR 147 awaiting merge/deploy |
| 2 — typed audience variants | Complete | PASS; PHASE_2_REPORT.md | Yes | PR 147 awaiting merge/deploy |
| 3 — external annotations | Complete | PASS; PHASE_3_REPORT.md | Yes | PR 147 awaiting merge/deploy |
| 4 — ranked editor improvements | Not authorized | No | No | No |
| 5 — explicit variant generation | Not authorized | No | No | No |
| 6 — build artifacts/provenance | Not authorized | No | No | No |
| 7 — industrial reference scenario | Not authorized | No | No | No |
| 8 — profile-guided longer-form backend | Conditional future work | No | No | No |

## Restart here

1. Read this file, MASTER_ROADMAP.md and completed phase reports. Inspect git status
   and log; preserve in-progress user work. Do not repeat completed native gates.
2. Publication PR is open: https://github.com/dharangutti/sound-script/pull/147.
   Main branch codex/videolab-phase3-publication, commit 15c7f1a; only
   docs/labs-manifest.json and docs/labs-hosting.md change. It is mergeable.
3. Immutable tag labs-videolab-mvp-web-v0.4.0 pins
   e5ec72604c1dcb23c859f2674824dbcc7a834990. Experimental branch and tag are pushed.
4. Local fresh site build validated remote provenance and preserved unrelated
   production files. All 27 staging/integrity tests, docs drift and staged Labs,
   Playground startup and runtime-media browser checks passed. PR CI was running
   when this checkpoint was written; inspect GitHub for its current status.
5. Stop for the user's merge/testing decision. Do not merge automatically.
   Use TEST_AFTER_DEPLOYMENT.md after merge; do not start Phase 4 without approval.

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

## Publication references

Artifact SHA256: 5dfec0a84685e0bc22b035fbdabf73c859cdd0f17068d020f219aa94e7a4eb45.
Evidence SHA256: e763eb4dbc2de54173540c57fc52b43575625052904f5bd0ea295f69dce31940.
Publication checkout: C:/Users/dhara/.codex/worktrees/labs-hosting/VideoLabs.
Build log: artifacts/phase3-site-build.log in that checkout. Browser tests include
normal/503/corrupt Playground startup and runtime-media behavior.

The native local test was also rerun with actual playback before screenshot capture
to verify the annotation preview reaches the compositor. It passed. This test-only
follow-up does not change the immutable artifact or move its release tag.
