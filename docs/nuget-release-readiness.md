# NuGet release readiness

This report is the release handoff for the V13 SoundScript package. It records
repository evidence only; local packing does not publish to nuget.org.

## Branch

- Branch: codex/nuget-docs-applications
- HEAD: fill from the final release commit.

## Packaging

- Package ID: SoundScript
- Version: 13.0.0
- Project: src/SoundScript/SoundScript.csproj
- Target framework: net10.0
- Package size: fill from the final nupkg.
- Contents: facade and bundled SoundScript libraries, transcription, XML docs,
  README, icon, licenses, repository metadata, and the packaged wordbank corpus.
- XML documentation: verify the facade XML is present in the package; bundled
  library XML status should be recorded from package inspection.

## Public API

The primary entry points are SoundScriptEngine.Compile, CompileFile,
SoundScriptCompilation.RenderWave, RenderStereoWave, and RenderMidi.
Transcription uses PcmWaveInput, DesktopMediaInput, TranscriptionEngine,
TranscriptionMode, and SoundScriptOutput.

The facade is additive. CLI internals and command syntax remain unchanged.

## Documentation and samples

The developer documentation is indexed at docs/documentation.md and includes
quick start, common tasks, .NET API, NuGet, application samples, language, CLI,
transcription, and architecture guides. The application samples are
DynamicAudio, DevOpsSonification, and TestFixtureGenerator; each consumes the
public facade.

## Validation evidence

Record exact results from the final validation run:

- dotnet pack: fill result and package path.
- Package size and file list: fill from inspection.
- Fresh local consumer install and execution: fill result.
- .NET tests: fill exact passed/failed/skipped totals.
- Node/browser tests: fill exact totals.
- Sample builds and runs: fill exact results.
- Release build and package validation: fill exact results.
- CLI smoke tests: fill exact commands and results.
- Determinism checks: fill exact comparison/hash evidence.

## Publishing

The package was not published as part of this work unless the final validation
record explicitly says otherwise. The manual workflow
.github/workflows/publish-nuget.yml is the controlled publication path; its
publish input defaults to false and publication requires the NUGET_API_KEY
repository secret.

## Known limitations

Transcription is bounded to normalized mono PCM and a maximum 120-second
analysis input. Monophonic is the supported baseline. Extract Melody,
Polyphonic / Piano, Mixed Audio / Roles, and Percussion / Rhythm are
experimental. Mixed roles are symbolic estimates, not isolated stems.
Percussion emits unpitched hit events and MIDI explicitly rejects those events.
Reconstruction consistency does not establish ground-truth accuracy.

## Git handoff

- Final HEAD: fill after all milestone commits.
- Working tree: fill after final git status --short.
- Suggested PR title: Prepare SoundScript for NuGet distribution, developer docs and real-world samples
