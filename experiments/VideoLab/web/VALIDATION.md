# UI and discoverability validation — 2026-09-27

Environment: Windows x64, .NET SDK 10.0.303, FFmpeg/ffprobe 9.0.1 full_shared,
Playwright 1.63.0 Chromium. Scope stays under experiments/VideoLab on its own branch.

## Delivered

- Real-footage first composition, playback/time/status, MP4/WebM exports.
- Seven thumbnail cards; preview, read-only tracks, parameters and source stay synced.
- Two video and two music selections in the public showcase, with two verified
  runtime bindings per combination. Total: 18 snapshots, 36 media outputs.
- Exact engine frame inspection, keyboard scrubbing and source/default/current values.
- Optional loopback workbench using the same compiler/runtime/FFmpeg: numeric and
  slider edits, personal video/music substitutions, render/cancel and reset.
- Local metadata/type/size/span validation, replacement preservation, temporary files,
  origin/token checks and explicit shutdown. No public upload service or accounts.
- Compact main homepage callout and featured Labs card are a separate site promotion.
  Signal Lab remains POC/unpublished; other Lab maturity is unchanged.

Main implementation files: WebProof.cs, LocalWorkbench.cs, Program.cs, Ffmpeg.cs,
AssetProbe.cs, examples/showcase.json, web/index.html, web/app.js, web/style.css,
web/samples/, generated web/site/. Usage and boundaries are in web/README.md;
sample origins/license are in web/samples/CREDITS.md.

## Checks

- Existing native suite: 143 checks pass, including frame/transition/audio assertions
  and repeat-render comparisons. Additional gallery: 32 MP4/WebM binding pairs are
  rendered twice, compared byte-for-byte and fully decoded.
- web/smoke.cjs: all seven cards, every sample/binding/format, actual playback,
  exports, exact scenes, synchronized source, reset, keyboard seeking, missing-data
  errors and 390/768/1360-pixel layouts pass. Desktop/mobile screenshots inspected.
- web/local.test.cjs: native parameter binding and invalid-binding preservation;
  MP4/WebM/WAV/MP3 selections; malformed, wrong-extension, empty, oversized and
  too-short rejection; failed replacement preservation; repeated MP4, WebM decode,
  byte ranges, cancel, browser edits/upload/render/reset and shutdown cleanup pass.
- web/encoder.test.cjs: 12 independent data-sequence MP4 renders match exactly.
- Production Release solution builds with zero warnings/errors. All 1,296 production
  tests pass after initializing the pinned wordbank and publishing Playground.
  Initial failures were missing checkout prerequisites, not accepted regressions.
- Site checks: 14 Labs tests, 52 documentation/release tests, 26 browser-input/integrity
  tests pass. Documentation drift/code UX and v15 acceptance validation pass.
  Published Playground browser startup passes normal, 503 and corruption handling.

## Reproducibility finding

Repeated data-sequence renders exposed two x264 MP4 hashes while eight raw YUV
filter outputs matched. Single-thread flags alone did not resolve it. Enabling
x264 cpu-independent=1 produced stable output across repeated independent renders;
the original assertions remain enforced. Some Lab MP4 bytes change relative to
older milestones; those tags remain frozen. No cross-machine/tool-version equality
claim is made and no production SoundScript encoder was changed.

## Intentional limits

Public hosting is static, so personal files and arbitrary numeric edits require the
local workbench. The UI explains this before file selection. User files reach only
the loopback process's temporary session on the same computer. One chosen video
fills the existing timed spans; one audio choice replaces the main music track.
No source editor, independent multi-file clip authoring, editable timeline, cloud
storage, account system, new codecs or production integration was introduced.
Normal reset/shutdown removes temporary files; crash/forced termination may leave
OS-temp files, as documented. Native FFmpeg handles trusted local media, not a public
hostile-file service. All production NuGet/CLI/core APIs and behavior are unchanged.

The checked-in publication evidence and production manifest identify the exact
approved artifact. A merged site promotion and normal Pages deployment are required
before these changes appear on the live URL.
