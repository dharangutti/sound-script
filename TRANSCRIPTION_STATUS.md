# Transcription status

Branch: `codex/transcription`. Specification: `docs/SoundScript_Transcription_Master_Prompt.md`.

## Milestone 1: boundaries and native output complete

New Transcription project: evidence-bearing canonical model, observation/input/output contracts,
strict PCM WAV decoder and resampler, typed AST/source writer and timeline/JSON outputs.
Five model/decoding/render tests pass. Existing AST printing gains only rest/instrument support.
Initial baseline: 1,020 pass / 11 fail because Wordbank submodule was uninitialized.
Restored the existing pinned submodule via `git submodule update --init --recursive`.

## Remaining

Analysis/fixture validation, CLI + dedicated Playground tab, final regression and report.
Architecture rationale: `docs/transcription-architecture.md`. No new packages.
