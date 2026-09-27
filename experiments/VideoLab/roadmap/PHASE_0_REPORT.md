# Phase 0 — PASS, 2026-09-27

## Summary and baseline

The existing product was audited and reproduced before changing product behavior.
Baseline commit: db9ebebb778fd823f490cc3a886ab8ea0f4dab77.
Preserved tag: labs-videolab-mvp-web-v0.3.0. Branch: experiments/videolab.
Live https://soundscript.net/labs/videolab/publication.json matches this commit and
artifact SHA256 24c9ae42feb38e98871ff5c6c2f90795f109447dc2c92bbad7ba33bf002280e9.
PR 146 was merged by the user before this roadmap request. A live Chromium check
confirmed seven cards, three timeline lanes and actual video playback.

## Architecture and compatibility

Strict JSON compiles into immutable Composition, typed decimal/boolean expression
ASTs and visual/audio programs. Atomic Runtime.SetMany produces immutable Snapshot;
SceneAt(frame) is the random-access semantic oracle. Legacy scripts use efficient
lowering; programmable scripts use bounded per-frame reference lowering. FFmpeg
handles normalized video, frame-aligned audio, MP4/H.264/AAC and WebM/VP9/Opus.
There are no production project references or solution/package entries.

Authoring supports trim/place/sequence/crossfade; shapes; position/size/scale/crop/
rotation/opacity/anchor; keyframes with easing; numeric runtime parameters;
conditions; reusable effects; structured sequences and batch outputs. Text and
callouts are absent. Browser has playback, seven cards, read-only tracks, frame and
source inspection, sample swaps and fixed public bindings. Local mode adds editable
numeric bindings and one personal video/music substitution with native rendering.

Limits: 64 parameters/elements/assets, canvas axes <=4096 (even), general timeline
<=216000 frames; programmable <=600 frames, <=4096 element-frames, <=100M canvas
pixels and <=100M transformed-layer pixels. Outputs: 24/25/30/50/60 fps, 48kHz stereo.
Supported local UI inputs: MP4/WebM, WAV/MP3, <=50 MiB each. Native ingestion supports
validated SDR progressive square-pixel CFR sources including fractional CFR rates;
detected VFR, HDR/wide-gamut/high-bit-depth and interlace are rejected. Legacy
normalization, bounds and failure rules remain as documented in SEMANTICS.md.

Publication: main stages only pinned Git static artifacts with evidence/tree hashes
under /labs/videolab/. Native adapter and implementation stay on the experiment
branch. The public site does not accept media uploads or execute native FFmpeg.
Some architecture prose predates optional cancellation/local-server support; Phase 1
documentation will correct that stale description without changing guarantees.

## Files changed

Only roadmap documentation: MASTER_ROADMAP.md (saved request), PROGRESS.md (durable
restart/status), this report, PHASE_1_DESIGN.md (bounded implementation decisions).
No product behavior or publication metadata changed in this phase.

## Tests and demonstration

From experiments/VideoLab, using the baseline Release executable:

- `dotnet bin/Release/net10.0/VideoLab.dll webproof`: 143 native checks and 32
  additional gallery repeat/decode pairs PASS. Full regenerated static tree hash
  exactly matches the published baseline, including media, source/UI and scene data.
- `node web/smoke.cjs`: all seven demos/18 bindings/36 formats and export paths,
  exact scenes, source/timeline, reset, keyboard, errors and responsive layout PASS.
- `node web/local.test.cjs`: origin/token, native bindings, four input formats,
  invalid/empty/oversized/too-short replacements, rendering/repeat/decode, cancel,
  browser selection/reset and shutdown cleanup PASS.
- `node web/encoder.test.cjs`: 12 independent MP4 repeats PASS.
- `dotnet bin/Release/net10.0/VideoLab.dll realtest real-media/manifest.json`:
  17/17 private cases PASS (including expected rejection). Inputs stay ignored.
- Live Chromium smoke: seven cards, three lanes, playable first composition PASS.

Tools: .NET 10.0.303, FFmpeg/ffprobe 9.0.1 full_shared, Playwright 1.63.0 Chromium,
Windows x64. Repeatability remains same-input/tool/environment only.

## Public deployment recommendation

KEEP PRIVATE/BRANCH-ONLY for roadmap work. The existing baseline is already public;
this audit does not authorize publishing future phases. Preserve its pin and tags.

## Next phase

Phase 1 is explicitly authorized by the supplied roadmap: controlled-font text,
simple callouts and recognizable editing/timeline example. Phase 2 is not authorized.
