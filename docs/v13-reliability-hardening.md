# V13 Reliability & Release Hardening

This pass retains version 13.0.0, language behavior and transcription algorithms.
Labs is outside scope. Nothing in this validation publishes packages, tags or releases.

## Baseline recorded before implementation (2026-09-21)

- Checkout: `main`, clean; HEAD and remote main both
  `3e64ddc5d4be6f36317de51acaa22eb926c7e06e`.
- Work branch: `codex/v13-reliability-hardening`.
- `global.json`: 10.0.300, `rollForward: latestFeature`, no prerelease;
  installed selection: .NET SDK 10.0.303 on Windows.
- `SoundScript.sln`: 22 projects: SoundScript (facade), Core, Parser, Midi,
  Compose, Prosody, Timbre, Voice, Wave, Vocal, Wordbank, Visual, Media,
  Transcription, Cli, Cli.TestFfmpeg, Playground, Web, Tests; samples
  DynamicAudio, DevOpsSonification, TestFixtureGenerator. `tools/VisualParity`
  is an additional project outside the solution. All target net10.0.
- The pinned `wordbank` submodule (`10a5b2a8d49fbc38ba99a4f322016e7772b797d5`)
  was initially uninitialized. No Playground publish or prior Debug build existed.
- `dotnet restore SoundScript.sln`: passed.
- `dotnet test SoundScript.sln -c Release --no-restore`: 1,067 passed,
  97 failed, 0 skipped, 1,164 reported. Windows Application Control blocked
  assembly loading (`0x800711C7`) in 67 failures and affected discovery; this
  was not a complete successful discovery/run. Other failures included missing
  Debug CLI outputs, submodule fixtures/schema, and the Playground publish.
- User-requested retry using the same built assemblies (`--no-build`):
  1,138 passed, 30 failed, 0 skipped, 1,168 reported; Application Control errors
  cleared without changing policy. Submodule initialization was also started
  during this retry; missing-fixture failures still occurred. Use the prepared
  final run for complete validation rather than treating this retry as isolated.
- Baseline Node boundary/startup/integrity tests: 20 passed, 0 failed/skipped.
- Pre-existing compiler warnings: three xUnit2031 diagnostics at
  `VisualTimelineTests.cs` lines 91 and 338; tracked separately from failures.
- Local logs/TRX are under `artifacts/hardening/` (ignored build evidence).

## Validation evidence from this pass

| Check | Result |
|---|---|
| Solution restore | Passed |
| Debug and Release solution builds | Passed, zero errors; three pre-existing xUnit2031 warnings per compiling test build |
| Release validation before any Debug build | 1,201 passed, 0 failed/skipped, including the explicit fresh Playground publish |
| Final Debug / Release suites | 1,204 passed in each configuration, 0 failed/skipped; 36 added cases |
| Node boundary/integrity/startup/provenance suites | 26 passed, 0 failed/skipped |
| Real browser startup | Normal, 503 recovery and corrupt-resource rejection passed |
| Four transcription browser scripts | Extraction, polyphonic, mixed and percussion passed synthetic CLI/browser parity, playback/editing/export, rejection, stale-state/cancellation and mobile-layout checks |
| CLI smoke | 13.0.0 version, blocks validation/MIDI, Wave effects, clean-sine transcription source/report/preview/completion marker passed |
| VisualParity tool Release build | Passed, zero warnings/errors |
| Local package validation | nupkg/snupkg created; 14 DLL/XML/PDB sets checked; isolated net10.0 consumer rendered WAV/MIDI and transcribed; 73 corpus files copied |
| Dependency inventory | SDK JSON and workflow inventory generated successfully |

The newly added trailing-dot case first failed in Debug (1,203 passed, 1 failed).
Windows erased that alias during `GetFullPath`; validation now rejects ambiguous
components before canonicalization. Final reruns passed. No test was removed.
The real FFmpeg/eSpeak tests executed on this machine; zero final skips were reported.

Package validation used:

```powershell
dotnet pack src/SoundScript/SoundScript.csproj -c Release --no-restore --output artifacts/hardening/nuget
pwsh scripts/validate-nuget.ps1 -PackagePath artifacts/hardening/nuget/SoundScript.13.0.0.nupkg
```

An earlier `pack --no-build --no-restore` attempt failed with NETSDK1085: the existing
bundled-assembly package targets resolve/build project references. Use the documented
command above. Supporting no-build packing remains a small packaging follow-up.
The package validator emits an informational `dotnet add --no-restore` warning before
its explicit consumer restore; that restore/build/run succeeded. No package was pushed.

Validation here ran on Windows. The updated Linux/macOS CI matrix was configured,
not executed remotely. Dedicated visual/performance stress-export scripts and external
recording measurement/listening sessions were not run; their .NET regression coverage
did run in the full suite. No human acceptance result is claimed.

## Clean validation

Prerequisites: Git, the .NET SDK selected by `global.json`, Node, PowerShell 7,
and the pinned wordbank submodule. Install eSpeak/eSpeak NG and FFmpeg to execute
their real integration tests; absent binaries are reported as explicit skips.
The original wordbank tests no longer silently pass when a required checkout is missing.
Use the checked-in embedded corpus; syncing upstream is a separate reviewed change.

```powershell
pwsh scripts/validate-release.ps1
pwsh scripts/validate-release.ps1 -Configuration Debug
```

The Release script declares this sequence (no Debug prerequisite):

```powershell
git submodule update --init --recursive -- wordbank
node scripts/corpus-provenance.cjs
dotnet restore SoundScript.sln
dotnet build SoundScript.sln -c Release --no-restore
$publish = Join-Path (Get-Location) 'artifacts/playground'
dotnet publish src/SoundScript.Playground -c Release --no-restore "-p:PublishDir=$publish/"
$env:SOUNDSCRIPT_PLAYGROUND_PUBLISH_DIR = $publish
dotnet test SoundScript.sln -c Release --no-build --logger 'trx;LogFileName=Release.trx' --results-directory artifacts/validation/Release
node --test scripts/transcription-browser-input.test.cjs scripts/playground-integrity.test.cjs scripts/playground-startup.test.cjs scripts/corpus-provenance.test.cjs
pwsh scripts/dependency-inventory.ps1
```

For Debug substitute `Debug` in build/test, retaining the explicit Release
Playground publish. Test executable paths use assembly metadata emitted by MSBuild
for the active configuration/framework. CI runs independent Debug and Release jobs
on Windows, Linux and macOS; a Release job never builds Debug.

Additional browser validation against the actual artifact:

```powershell
npm ci --prefix scripts
npx --prefix scripts playwright install chromium
node scripts/verify-playground-startup.cjs artifacts/playground
node scripts/verify-transcription-playground.cjs artifacts/playground
node scripts/verify-polyphonic-playground.cjs artifacts/playground polyphonic
node scripts/verify-polyphonic-playground.cjs artifacts/playground mixed
node scripts/verify-polyphonic-playground.cjs artifacts/playground percussion
```

Linux CI installs browser system dependencies with `--with-deps`. Browser verification
covers normal startup, transient 503 recovery and persistent integrity failure.
The transcription scripts use synthetic inputs and compare CLI/browser behavior,
playback, rejection, stale results and cancellation. CLI browser-verification scripts
default to Release; set `SOUNDSCRIPT_BUILD_CONFIGURATION=Debug` explicitly to check
a Debug CLI. Offline recording measurement scripts take `-Configuration`.

## Import and resource root

Trusted local `ProgramLoader.Load(path)` and `SoundScriptEngine.CompileFile(path)`
keep relative parent imports. All imports reject absolute, drive-relative, UNC
and device paths. Hosted callers explicitly establish an existing root:

```csharp
var root = new SoundScript.Core.AllowedPathRoot(jobDirectory);
var compilation = SoundScript.SoundScriptEngine.CompileFile(entryPath, root);
var wave = compilation.RenderWave();
```

The entry and every nested import are checked against that root. Both separator
forms and dot segments are normalized; containment includes a separator boundary
so `job2` cannot match `job`. Trusted root ancestors are canonicalized. Links and
reparse points below the root are rejected even when their targets are inside it;
Windows alternate streams and trailing-dot/space aliases are rejected. Normal
parent traversal within the root remains valid.

The compilation carries the root into mono/stereo sample loading, including
additional file overlays. Caller-provided Wave options cannot remove it, and
`SkipMissingSamples` cannot suppress a boundary violation. Low-level Wave callers
can set `WaveRenderOptions.AllowedRoot` themselves. Relative sample paths retain
their existing entry-directory semantics, including samples in imported scripts.
In-memory `Compile` and the low-level AST APIs are not automatically sandboxed.

This is a path boundary, not OS isolation. Hosts must stage files into a job-owned
root and prevent concurrent mutation of that root/ancestors. Path checks cannot
remove filesystem check/open races against an attacker able to modify those paths;
hard links, process execution, CPU/memory and output permissions need host policy.
The browser continues to reject filesystem imports.

## External processes

FFmpeg and eSpeak share `SafeProcess`: argument lists, no shell, concurrent
stdout/stderr draining, cancellation, and process-tree termination where the OS
supports it. The deadline covers both process exit and stream draining. Cleanup
has a separate five-second bound. FFmpeg retains its ten-minute timeout and
dependency/export diagnostics. eSpeak defaults to one minute per synthesis and
five seconds for version probing. `VocalEngineOptions.ProcessTimeout` and
`CancellationToken` configure synthesis; CLI cancellation flows through vocal
batch/composite paths and wordbank generation. Canceled probes propagate cancellation;
unavailable/failed/timed-out version probes yield `unknown`, never a fabricated version.

Output capture uses memory; callers must trust the selected executable and apply
host resource limits when executing untrusted workloads. Process-tree cleanup is
best effort if the OS has already detached/reaped descendants.

## Corpus provenance

```powershell
node scripts/corpus-provenance.cjs
node --test scripts/corpus-provenance.test.cjs
node scripts/corpus-provenance.cjs --write # regenerate metadata/source register after a reviewed sync
```

The validator accepts recognized audio file titles or explicit source URLs, rejects
malformed Commons URLs, requires notes for unresolved records and repository receipt
paths for any verified declaration. It validates metadata structure, not the truth
of a license or receipt. Missing status means `declared`, never `verified`.
The sync script runs the same annotation/rendering mechanism after copying upstream.

`bobtail`, `dashing`, `sleighing`, `test` and `world` retain their historical CC0 and
attribution declarations but are explicitly `unresolved`; no original source or
generation receipt was found in the embedded corpus, its Git history or the pinned
wordbank source. The upstream harvester contains the same explanatory placeholders.
Those notes are never converted into Commons URLs. Assets remain unchanged.
The pinned wordbank `CHANGELOG.md` entry for 0.6.2 says the three added pilot clips
were synthesized offline and relates them to test/world. This is a historical
assertion, not a generator/version/input receipt or a verified license chain;
the source register links to that precise revision without treating it as verification.
See the [record-level source register](../src/SoundScript.Wordbank/Data/corpus/v2026.07/en/SOURCES.md).

## Dependency inventory and workflow policy

After a Release restore/build, run `pwsh scripts/dependency-inventory.ps1`.
It uses the SDK's [package inventory command](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list)
with transitive packages and JSON schema version 1. CI uploads the NuGet inventory,
browser-tool lockfile, action references, SDK/commit/submodule and working-tree context.
This is a dependency inventory baseline, not a complete SPDX/CycloneDX SBOM or a
license/security attestation. It excludes OS/shared runtime, user-installed FFmpeg
and eSpeak, corpus rights and vendored JS/fonts/soundfonts. Release owners needing a
complete artifact SBOM must select a format/tool and include those components.

Action review: no existing action reference is pinned to an immutable SHA.
`actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/setup-node@v4`,
`actions/upload-artifact@v4`, `actions/download-artifact@v4`,
`peaceiris/actions-gh-pages@v4` and `softprops/action-gh-release@v2` use version tags.
This pass limits test workflow permissions to `contents: read` and disables persisted
checkout credentials. Existing action versions were not bulk-rewritten.
Choose an update policy/owner before adopting SHA pins across workflows, especially
the two third-party actions with publishing permissions; GitHub recommends
[full commit SHAs for immutability](https://docs.github.com/en/actions/reference/security/secure-use).
Binary signing/notarization remains a release-owner policy and credential decision.

## Completion and remaining limitations

Successful CLI transcription writes `<source>.completion.json` last, with mode,
version, completion state and file sizes/SHA-256 hashes. Previous markers are
invalidated after path preflight and before analysis/writing. A failed/rejected/canceled
attempt has no new marker; a preflight failure leaves an earlier valid set untouched.
Individual outputs still use atomic replacement; there is no multi-file rollback.
Use unique paths per concurrent job and verify marker hashes before consuming files.
Browser downloads and in-memory library results do not use filesystem manifests.

Monophonic remains the baseline; extraction, polyphonic, mixed-role and percussion
modes remain experimental. A rights-cleared representative recording corpus,
independent note/onset annotation, rejection and missed/extra/octave-error measurements,
human listening review and listening acceptance remain external work. No measurements
or rights conclusions are inferred from synthetic fixture or browser test success.

Chain-of-title/legal assignments, missing source receipts, and domain/brand ownership
documentation also require external evidence. Upstream wordbank harvester/schema
adoption of this metadata is a separate repository change; this repository's sync guard
prevents the malformed source register from being reintroduced here.
