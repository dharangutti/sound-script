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
- Package size: pending the authoritative final package-validation run.
- Contents: facade and bundled SoundScript libraries, transcription, XML docs,
  README, icon, licenses, repository metadata, and the packaged wordbank corpus.
- XML documentation: final acceptance requires SoundScript.xml and the XML/PDB
  metadata for bundled public assemblies in lib/net10.0. The pre-fix package
  inspection is not release evidence because component XML/PDB coverage was
  incomplete.

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

- dotnet pack: pending authoritative package-validation report.
- Package size and file list: fill from inspection.
- Fresh local consumer install and execution: fill result.
- .NET tests: pending authoritative final report.
- Node/browser tests: pending authoritative final report.
- Sample builds and runs: pending authoritative final report.
- Release build and package validation: pending authoritative final report.
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

## Ready-to-paste PR description

### What user problem does this solve?

SoundScript now has a discoverable .NET package and an onboarding path for
developers who want deterministic audio/media generation or editable source
from suitable recordings.

### What changed?

The V13 SoundScript facade bundles the reusable libraries behind stable compile,
render, and transcription entry points. The CLI and Playground continue to use
the existing language and rendering behavior.

### NuGet packaging

Package ID SoundScript, version 13.0.0, targets net10.0. It includes the
facade, bundled libraries, transcription, package README, icon, license
metadata, repository metadata, and wordbank corpus. The manually triggered
publishing workflow defaults publish to false.

### Documentation

The documentation hub now routes readers through quick start, common tasks,
.NET API, NuGet, application samples, language, CLI, transcription, and
architecture guides. README and homepage copy describe both source-to-media
and audio-to-editable-source workflows.

### Application demos

DynamicAudio demonstrates event-driven cues; DevOpsSonification maps test/build
state to deterministic audio; TestFixtureGenerator creates repeatable WAV/MIDI
fixtures through the facade.

### Validation

Replace this line with the exact package, consumer, sample, CLI, .NET, Node,
browser, Release, and determinism results from the final validation run.

### Compatibility and determinism

The change is additive. Existing CLI commands, language semantics, Playground
behavior, and renderer paths remain in use. Identical source, options, assets,
and engine version produce repeatable output.

### Known limitations

Transcription modes beyond Monophonic are experimental. Mixed roles are
symbolic estimates rather than isolated stems, percussion is Wave-only because
MIDI rejects unpitched hits, and round-trip consistency is not ground-truth
accuracy.
