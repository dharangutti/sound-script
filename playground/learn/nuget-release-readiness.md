# NuGet release readiness

Current distribution (checked 2026-09-21): the [SoundScript 13.0.0 library](https://www.nuget.org/packages/SoundScript/13.0.0)
is published. [V13 CLI release downloads](https://github.com/dharangutti/sound-script/releases/tag/v13.0.0)
and the [browser Playground](https://soundscript.net/playground/) also exist.
`SoundScript.Cli` is not published on nuget.org. The evidence below is a
historical record of the earlier packaging task, which did not itself publish.

This report is the release handoff for the V13 SoundScript package. It records
repository evidence only; local packing does not publish to nuget.org.

## Branch

- Branch: codex/nuget-docs-applications
- Validated implementation commit: `305b6f2` (packaging and final package
  validation).

## Packaging

- Package ID: SoundScript
- Version: 13.0.0
- Project: src/SoundScript/SoundScript.csproj
- Target framework: net10.0
- Package size: nupkg 5,350,829 bytes; companion snupkg 210,414 bytes.
- Contents: facade and bundled SoundScript libraries, transcription, XML docs,
  README, icon, licenses, repository metadata, and the packaged wordbank corpus.
- XML documentation: `SoundScript.Api.xml` and XML files for all 14 bundled
  public assemblies are present in `lib/net10.0`; matching portable PDB files
  are present in the companion symbols package (snupkg). The pre-fix package
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

The current validation evidence is:

- dotnet pack: passed for `SoundScript.13.0.0.nupkg` and its companion symbols
  package.
- Package size: nupkg 5,350,829 bytes; snupkg 210,414 bytes. The nupkg contains
  14 bundled DLLs and matching XML files under `lib/net10.0`; the snupkg contains
  the matching 14 PDB files.
- Package file list: includes `SoundScript.Api.dll`, all bundled SoundScript
  libraries, README, icon, licenses, repository metadata, and the packaged
  `Data/corpus/v2026.07` corpus; CLI and Playground binaries are excluded.
- Fresh local consumer install and execution: passed in an isolated net10.0
  consumer. WAV output was 97,064 bytes with SHA-256
  `4F8B4D4714BD2C3844035DCDE8FEBD9CC5CCA7BC61F0E19DBA0DA13925063999`; MIDI
  output was 75 bytes with SHA-256
  `C03D3166E8A993F192DCD3F70676031EC63CC56DD471126C1D990C8B6BD5565E`;
  transcription produced 3 notes and copied 73 corpus files. The validator log
  is `artifacts/nuget-validation/package-validator-final.log`.
- .NET tests: 1,168 passed, 0 failed, 0 skipped in the full Debug run with the
  fresh published Playground artifact; the focused facade set was 7/7. TRX:
  `src/SoundScript.Tests/TestResults/SoundScript.Debug.Published.trx`.
- Node tests: 20 passed, 0 failed, 0 skipped (`artifacts/nuget-validation/node-tests.log`).
- Fresh Playground startup: normal, transient 503, and corrupt-resource scenarios
  all passed with 2, 2, and 3 target requests respectively
  (`artifacts/nuget-validation/playground-startup-fresh.log`).
- Fresh transcription browser checks: Melody, Polyphonic, Mixed, and Percussion
  all passed parity, playback/editing/export, rejection or stale-output,
  cancellation, and mobile-layout coverage; logs are the four `*-browser-fresh.log`
  files under `artifacts/nuget-validation`.
- Sample builds and runs: DynamicAudio produced 6 WAV files; DevOpsSonification
  ran 100 tests (96 passed, 4 failed) and produced WAV, MIDI, source, and manifest
  outputs; its repeated WAV hash was
  `6DB8FBCB646C4D6D5E6274A16C5F4228B1E622A6DCB91E0F0E86C672277B2F56` and its
  repeated MIDI hash was
  `1D2EFA5FFEBD04251E540C1C214BC396EEA71ED083D78C2096C715F7A06B3E25`.
  TestFixtureGenerator seed-7 repeat and CompileFile checks passed.
- Release build: solution and all three samples passed with 0 errors; the run
  emitted three pre-existing xUnit 2031 warnings in `VisualTimelineTests`.
  Package validation passed the 14-assembly dependency, XML, symbols, consumer,
  and corpus checks.
- CLI smoke tests: `examples/blocks.ss` rendered 7 notes and
  `examples/wave-effects.ssw` rendered WAV successfully. The clean-sine
  transcription fixture produced 6 notes with 100% pitch recall and wrote
  SoundScript, JSON, and preview outputs. Hit WAV rendering passed; hit MIDI
  correctly exited 1 with `Unpitched hit playback currently requires wave backend`.
  Visual CLI export also passed: FFmpeg produced a decode-verified WebM with
  192 scenes, VP9 video at 320x180/24 fps, Opus audio, 8.008 seconds duration,
  and 168,052 bytes. Logs and outputs are under
  `artifacts/nuget-validation/cli`.
- Determinism checks: repeated CLI MIDI hash
  `5A79DF998BDA3447A8CB61FFB4D1E491E58FBFCBC1BCFAF8AFE8429B42338B55` and WAV
  hash `08E0D46D6E8F3A60C9C3B5118E37113354485A65D2EA02A3DE61492C0CF43059`.

## Publishing

The earlier packaging task did not publish; SoundScript 13.0.0 is now available
on NuGet as linked above. The manual workflow
.github/workflows/publish-nuget.yml is the controlled publication path; its
publish input defaults to false and publication requires the NUGET_API_KEY
repository secret.

## Validation scope

Validation ran locally on Windows against the branch artifacts. The GitHub
publishing workflow was reviewed but not executed, and package ownership and
public nuget.org availability were not verified. Packaged README bytes match
`packaging/README.md`; XML was verified through the isolated consumer cache.

## Known limitations

Transcription is bounded to normalized mono PCM and a maximum 120-second
analysis input. Monophonic is the supported baseline. Extract Melody,
Polyphonic / Piano, Mixed Audio / Roles, and Percussion / Rhythm are
experimental. Mixed roles are symbolic estimates, not isolated stems.
Percussion emits unpitched hit events and MIDI explicitly rejects those events.
Reconstruction consistency does not establish ground-truth accuracy.

## Git handoff

- Final documentation commit: the commit containing this report; resolve its
  hash with `git rev-parse HEAD` after final integration.
- Working tree: final integration status is checked by the release owner after
  concurrent implementation commits are combined.
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

The full Debug run passed 1,168/1,168 tests, the Node suite passed 20/20, and
fresh browser checks passed startup (normal, 503 recovery, and corrupt-resource
rejection) plus all four transcription modes. All three application samples
built and ran. CLI smoke and repeated-output hashes are recorded above. The
final package was validated as SoundScript 13.0.0 with 14 bundled DLL/XML pairs,
14 portable PDBs in the companion symbols package, a successful isolated
consumer, and 73 copied corpus files. That earlier task did not publish;
the library was subsequently published as SoundScript 13.0.0.

### Compatibility and determinism

The change is additive. Existing CLI commands, language semantics, Playground
behavior, and renderer paths remain in use. Identical source, options, assets,
and engine version produce repeatable output.

### Known limitations

Transcription modes beyond Monophonic are experimental. Mixed roles are
symbolic estimates rather than isolated stems, percussion is Wave-only because
MIDI rejects unpitched hits, and round-trip consistency is not ground-truth
accuracy.
