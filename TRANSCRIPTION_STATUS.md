# Transcription status

Branch: `codex/transcription`. Specification: `docs/SoundScript_Transcription_Master_Prompt.md`.

## Milestone 1: boundaries and native output complete

New Transcription project: evidence-bearing canonical model, observation/input/output contracts,
strict PCM WAV decoder and resampler, typed AST/source writer and timeline/JSON outputs.
Five model/decoding/render tests pass. Existing AST printing gains only rest/instrument support.
Initial baseline: 1,020 pass / 11 fail because Wordbank submodule was uninitialized.
Restored the existing pinned submodule via `git submodule update --init --recursive`.

## Milestone 2: deterministic analysis and round-trip corpus complete

YIN pitch, energy segmentation, repeated attacks, onset tempo grid, conservative
quantization, comparison metrics and existing-renderer reanalysis implemented.
Five original synthetic fixtures: all pitches matched, no missed/extra source notes,
7.5–10 ms mean onset error, 5–10 ms duration error. Round-trip pitch recall 100%.
Rubato fixture's nominal tempo is not recovered (95 vs 120 BPM); documented.
Full regression after submodule initialization: **1,049 passed, 0 failed**.
Measurements and distributable media: `src/SoundScript.Tests/Golden/transcription/`.

## Remaining

CLI + dedicated Playground tab, compressed-media/browser verification, final regression and report.
Architecture rationale: `docs/transcription-architecture.md`. No new packages.
