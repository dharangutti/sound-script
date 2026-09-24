# SoundScript documentation

SoundScript turns editable source into deterministic audio and queryable media.
Choose a path by what you want to build. This is the canonical documentation index.

<!-- GENERATED:CURRENT_PUBLIC_RELEASE_START -->
Current public version: **15.0.0**. Publication channels are recorded in `docs/release-state.json`.
<!-- GENERATED:CURRENT_PUBLIC_RELEASE_END -->

## Start here

- [Quick start](quick-start.md) — hear your first script and choose browser, package or CLI.
- [Playground](PLAYGROUND.md) — edit, preview and export without installation.
- [User guide](user-guide.md) — learn the workflow from notes to complete media.
- [Common tasks](common-tasks.md) — find a short recipe for WAV, MIDI, vocals or transcription.

## Build with .NET

- [NuGet](nuget.md) — install the public library or validate a local development package.
- [.NET API](dotnet-api.md) — compile in memory, load files, render bytes and handle errors.
- [Application samples](application-samples.md) — integrate cues, monitoring and media fixtures.
- [Programmable media runtime](programmatic-media-runtime.md) — query synchronized media from a host clock.

## Use the CLI

- [Installation and distribution](cli.md#installation-and-automation) — choose a release archive or source checkout.
- [CLI reference](cli.md) — discover commands, options, diagnostics and exit codes.
- [Automation](cli.md#installation-and-automation) — validate inputs and consume JSON in CI.
- [Common CLI tasks](common-tasks.md) — render, inspect and transcribe from a shell.

## Author audio and music

- [Language reference](language-reference.md) — look up supported musical and media syntax.
- [Wave grammar](wave-grammar.md) — author direct audio effects and samples.
- [SoundCSS](soundcss.md) — define deterministic timbre and synthesis settings.
- [Vocal](vocal.md) — generate speech cues and mix vocal stems.
- [Text to melody](text-to-melody.md) and [prosody](word-prosody.md) — turn words into musical phrasing.
- [Examples](examples.md) — adapt runnable scores and Playground presets.

## Programmable media

- [Temporal visuals](visual-temporal.md) — author seekable scenes and audio synchronization.
- [Programmable media tutorial](tutorials/programmable-media.md) — build a host-driven experience.
- [Audio/visual compositions](audio-visual-compositions.md) — learn from complete synchronized examples.
- [Media primitives](media-primitives.md) — choose shapes, text, fills and animation.
- [CLI WebM export](cli.md) — preflight FFmpeg and produce synchronized video.

## Transcription

- [Transcription guide](transcription.md) — choose inputs and understand the monophonic baseline.
- [Melody extraction](melody-extraction.md) — assess dominant-line estimates in suitable recordings.
- [Polyphonic/piano](polyphonic-transcription.md) — inspect experimental simultaneous-note analysis.
- [Mixed roles](mixed-audio-transcription.md) — interpret symbolic role estimates and their limits.
- [Percussion](percussion-transcription.md) — recover experimental unpitched rhythm events.

## Automation and testing

- [Deterministic fixtures](../samples/TestFixtureGenerator/README.md) — generate reproducible test media.
- [DevOps sonification](../samples/DevOpsSonification/README.md) — map build states to cues.
- [CI workflow](../.github/workflows/tests.yml) — inspect the OS/configuration validation matrix.
- [NuGet end to end](nuget-end-to-end.md) — exercise complete consumer workflows.
- [Release checklist](releasing.md) — prepare and validate without implicitly publishing.
- [Documentation maintenance](documentation-maintenance.md) — update owned facts, classify files and check drift.

## Architecture and internals

- [Architecture](architecture.md) and [pipeline](pipeline.md) — locate compiler and renderer responsibilities.
- [Runtime contracts](programmatic-media-runtime.md) — understand time, serialization and host boundaries.
- [Package structure](nuget.md#package-metadata) — inspect bundled assemblies and metadata.
- [Transcription architecture](transcription-architecture.md) — understand analysis and output boundaries.

## Reference

- [Language reference](language-reference.md) and [CLI reference](cli.md) — resolve syntax and command questions.
- [Supported platforms](../README.md#supported-platforms) — check OS, browser and framework expectations.
- [Dependencies and limits](programmatic-media-runtime.md) — separate core rendering from optional FFmpeg export.

## Historical engineering evidence

These reports describe recorded versions; package state and test counts are
evidence, not current installation advice.

- [V15 documentation reliability](v15-documentation-reliability-report.md) — inspect development acceptance and requirements.
- [V14 acceptance](v14-acceptance-report.md) — review media compatibility and validation.
- [V13 hardening](v13-reliability-hardening.md) and [readiness](v13-release-readiness.md) — inspect earlier reliability evidence.
- [NuGet validation](nuget-end-to-end-validation.md) — review the pinned consumer baseline.
- [Release history](../RELEASE_NOTES.md) — follow earlier release-specific changes.
