# NuGet end-to-end implementation and validation report

Validated on Windows with .NET SDK **10.0.303**, published SoundScript **13.0.2**
and FFmpeg **9.0.1-full_build-www.gyan.dev**. Base: main commit
`7b6470cb40fd76a05a5c4bc14c9bcac3e66631ea`.

## Delivered behavior

Two genuine NuGet consumers demonstrate application data → source → media and
audio → structured transcription → score edit → new media. They reference
only `SoundScript` 13.0.2, never production projects or the CLI. One monitoring
builder handles Healthy, Warning and Critical. The round trip changes flute
to piano and explicitly enables expressive Wave performance so the instrument
edit changes audio as well as MIDI.

The [central guide](nuget-end-to-end.md) contains the complete verified
[CLI ↔ NuGet parity matrix](nuget-end-to-end.md#verified-cli--nuget-parity),
integration call counts, dependency requirements, output handling and API
friction. Tutorials and an unpublished article are based on the running code.

## Files and sample structure

New files (18):

```text
docs/
  nuget-end-to-end.md
  nuget-end-to-end-validation.md
  tutorials/
    industrial-monitoring.md
    media-round-trip.md
  articles/
    programmable-media-dotnet.md
samples/
  IndustrialMonitoring/
    IndustrialMonitoring.csproj
    MonitoringScenario.cs
    Program.cs
    README.md
    Scenarios/
      healthy.json
      warning.json
      critical.json
  MediaRoundTrip/
    MediaRoundTrip.csproj
    Program.cs
    README.md
tools/
  Validate-NuGetExamples.ps1
  NuGetExamplesValidation/
    NuGetExamplesValidation.csproj
    Program.cs
```

Modified files (8): `README.md`, `packaging/README.md`, `docs/nuget.md`,
`docs/dotnet-api.md`, `docs/common-tasks.md`, `docs/documentation.md`,
`docs/application-samples.md`, `docs/cli.md`. Changes correct current package
versions, link the new material, and clarify programmatic WebM availability.
Historical V13 CLI release URLs remain unchanged.

No production C#, project references, solution entries, engine behavior or
`experiments/SoundScript.Labs/` files changed.

## Commands and outputs

From the repository root:

```sh
dotnet build samples/IndustrialMonitoring -c Release
dotnet build samples/MediaRoundTrip -c Release
dotnet run --project samples/IndustrialMonitoring -c Release
dotnet run --project samples/MediaRoundTrip -c Release
```

Monitoring writes `artifacts/samples/industrial-monitoring/<status>/`:
`<status>.ss`, `.ssw`, `.ssv`, `.mid`, `-music.wav`, `-stereo.wav`, `-alert.wav`,
`-synchronized.wav`, optional `.webm`, plus `timeline.json`, `scenes.json` and
`scenario.json`. WebM is 640×360 at 24 FPS with four seconds of synchronized
audio. Native timelines do not have a frame rate.

Round trip writes `artifacts/samples/media-round-trip/Input/melody.wav` and
`Output/{report.json,transcribed.ss,modified.ss,reconstructed.mid,reconstructed.wav}`.
An optional second argument supplies an existing PCM WAV instead of generating
the fixture. No copyrighted recording is bundled.

## Variation and semantic checks

| Scenario | Input temperature/vibration/load | Tempo | MIDI pitch / count / program | Visual cue |
|---|---|---:|---|---|
| Healthy | 68 / 0.14 / 52 | 90 | 72 (C5) / 2 / 73 | green, 100px, 3s fade |
| Warning | 86 / 0.48 / 78 | 116 | 79 (G5) / 4 / 0 | amber, 180px, 2s fade |
| Critical | 96 / 0.82 / 94 | 139 | 84 (C6) / 8 / 40 | red, 260px, 1s fade |

These are demonstration mappings for sonification, not safety thresholds. The
monitoring tutorial changes only temperature, vibration, load and status.

Automated validation checks actual MIDI headers, parsing, notes, instruments and
tempo events; RIFF/WAVE headers, decoding, non-silent PCM, stereo channels and
four-second synchronized duration; distinct hashes between scenarios; status
geometry and opacity progression at 0, 2 and 3.99 seconds; and an empty state at
the end-exclusive four-second boundary. A representative critical video frame
was also decoded and visually inspected for legible telemetry and layout.

Round-trip checks confirm four detected pitches `[60, 64, 67, 72]`, permitted
suitability, and different MIDI and WAV bytes after the instrument edit.
Silence is rejected with a diagnostic report and removal of an old reconstructed
WAV. Invalid equipment identifiers are rejected before generating source.

## Repeatability

```powershell
pwsh -NoProfile -File tools/Validate-NuGetExamples.ps1
```

**39 deterministic artifacts matched byte for byte across two independent runs**:
33 monitoring source/MIDI/WAV/JSON artifacts and six round-trip artifacts.
The complete SHA-256 inventory is generated at
`artifacts/nuget-examples-validation/hashes.json`. Representative full hashes:

| Artifact | SHA-256 |
|---|---|
| Healthy music WAV | `ACEF0849D74CB4ED7BE649664DDA070D699809BCAA1D61934C2F6C66FD913087` |
| Warning music WAV | `93C432D1974BA65FE6A143607D0EFA8CE4E9E40170499591D4476AA9466914CF` |
| Critical music WAV | `0591D017DC29F5D51D003DABF6C9A894DA4CC44E0B70176FF6E3F22BBB41A7FF` |
| Modified round-trip source | `E347CFC1355B076F5388C69D9EAD7324A8C1E5002B17A33AA9B92A6F4E880421` |
| Reconstructed WAV | `235CCF73209E90BA46D9FC1FDEA54EBD7829CECAC3FA3DEC6FB27CF9A5EE39F1` |

These results describe the validated environment, package, inputs and options;
they are not a cross-runtime or cross-codec byte-identity promise.

FFmpeg was available and all **six** WebM exports (three scenarios × two runs)
passed `FfmpegWebmExporter.EncodeAndVerify`, including decoding both audio and
video streams. WebM container bytes were deliberately excluded from deterministic
hash assertions. First-run file sizes were 84,516 bytes (Healthy), 85,572 (Warning)
and 83,135 (Critical).

The explicit missing-executable run succeeded for all three scenarios. It
reported WebM skipped, created no WebM, and preserved identical MIDI, music,
alert and synchronized WAV, timeline and scene outputs.

## Genuine package isolation and API evidence

**Published NuGet validation passed. Local-package validation was not needed
and was not claimed.** The script copies all three package-only projects into
a unique workspace under the user's local application data, outside the
repository's MSBuild/project-reference graph. It restores from only nuget.org
into a fresh per-run cache, then builds and runs Release. All three isolated
builds completed with zero warnings and errors.

The downloaded `soundscript.13.0.2.nupkg` was opened as a ZIP. Its SHA-256:
`76E414BCE8A76403B3FFED6B7DA123B13E3E405C7D548A831ED40AB6EAD893A5`.
Its NuSpec identifies the base commit above. `lib/net10.0/` contains each of
these 14 assemblies **and its matching XML documentation**:

```text
SoundScript.Api          SoundScript.Compose      SoundScript.Core
SoundScript.Media        SoundScript.Midi         SoundScript.Parser
SoundScript.Prosody      SoundScript.Timbre       SoundScript.Transcription
SoundScript.Visual       SoundScript.Vocal        SoundScript.Voice
SoundScript.Wave         SoundScript.Wordbank
```

The validation consumer loads all 14 and records exported types and method
signatures in `artifacts/nuget-examples-validation/public-api.json`.
`SoundScript.Cli.SourceAnalysis` is not present. Dependencies include
DryWetMidi 8.0.3, OggVorbisEncoder 1.2.2, SkiaSharp 3.119.4 and the declared
native/HarfBuzz dependencies; the successful clean consumer run resolves them.

Beyond the two applications, executable probes pass for compose/prosody AST
generation, source print/recompile, stereo Wave rendering, both append APIs,
SoundCSS parsing and WAV/OGG rendering (`OggS` signature), built-in vocal
generation, and vocal batch. Wordbank public APIs were compiled/invoked on
missing-source paths to verify typed results without mutating a corpus.
External eSpeak generation, normalization of a real recording and compressed
input transcription were audited in code/public signatures, not independently
executed by these sample probes. Existing regression tests remain separate
evidence; package inclusion alone is not claimed as execution of every mode.

## Regression result and prerequisites

**1,204 passed; zero failed; zero skipped**, Release:

```powershell
git submodule update --init --recursive -- wordbank
$publish = Join-Path $PWD 'artifacts/playground'
dotnet publish src/SoundScript.Playground -c Release "-p:PublishDir=$publish/"
dotnet test src/SoundScript.Tests/SoundScript.Tests.csproj -c Release --logger "trx;LogFileName=nuget-end-to-end.trx" --results-directory artifacts/nuget-examples-tests
```

For first-time setup use the repository's `scripts/validate-release.ps1`, which
sets an absolute Playground publish path and builds before testing. The local
run used that absolute path convention. The TRX is retained in the ignored
results directory. Initial tests failed because the submodule and published
Playground assets were absent; preparing them resolved all 14 failures without
changing tests. Existing xUnit2031 analyzer warnings were not changed.

An early isolated run in the OS temporary directory lost extracted dependencies
after restore. The retained local-application-data workspace completed the full
run successfully. This was not treated as evidence of missing package contents.

## API and version decisions

The media capabilities are available programmatically. The advanced-only set
includes composition/prosody, source printing, SoundCSS, temporal visuals/WebM,
transcription, vocal synthesis/batch and wordbank operations. The consolidated
CLI validation/inspection report, target policy, complete preflight report and
transcription completion envelope have no equivalent packaged API. Individual
engines expose structured data, warnings or exceptions; no console scraping is
needed. The parity matrix also records the existing append versus Wave/source
AST distinction instead of overstating parity.

A/V orchestration has the most boilerplate. Synchronous synthesis/preflight and
process-wide corpus configuration are additional documented service integration
constraints. Existing byte arrays, streams, typed score/options/results and
timeline objects are sufficient; no universal output abstraction is justified.

New typed constructs are **sample-local** `EquipmentStatus`, `MonitoringScenario`
and `MonitoringSources`, with a deterministic builder. No package facade APIs,
public library constructs, stream overloads or AST exposure were added. Public
API compatibility impact is **none**. The version remains **13.0.2**: this work
contains samples, validation and documentation only. Neither a patch nor minor
release is justified.

No NuGet package, Git tag, GitHub release or external article was published.
Playground publishing above produced only local ignored test prerequisites.
The work is prepared for pull-request review, not merged.
