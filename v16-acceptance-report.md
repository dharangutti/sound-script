# V16 candidate acceptance report

Validated on Windows, .NET SDK 10.0.303 / runtime 10.0.11, and Microsoft Edge Chromium. Development version: `16.0.0-preview.1`; public release state: `15.0.0`. This report concerns an unpublished candidate. No merge, tag, package publication or website deployment was performed.

## Decision and requirements

1. **RuntimeLab: GO WITH CONDITIONS.** The isolated checkpoint remains independently reproducible: 51 tests pass. Its original conditions cover approved binding scope, offline rendering and revalidation before production integration.
2. **Traceability:** all 124 tags are individually mapped in [RuntimeLab traceability](../experiments/SoundScript.RuntimeLab/TRACEABILITY.md): 115 PASS, 4 PASS WITH CONDITION, 5 NOT APPLICABLE, zero failures. Those statuses describe the Lab checkpoint, not a claim that later production work remains Lab-only.
3. **Feasibility:** the compile-once model graduated through three contained core components: contextual runtime parsing/binding-slot discovery; typed state and stable snapshots; private-copy/projection adapters calling existing renderers. CLI, Playground and packaging are separate integration work, not renderer redesign. See [feasibility review](../experiments/SoundScript.RuntimeLab/V16-FEASIBILITY.md).
4. **Architecture:** the runtime owns a private parsed template and compiled visual timeline. `Set`/`SetMany` update only decimal state. `Bind` reuses a cached snapshot or copies the addressed gain containers and visual values. The original source is not retained. MIDI receives private mutable notation copies. Existing Wave, MIDI, Visual and Media renderer/timing sources are unchanged from `main`; so are the existing AST, golden files, example sources and `experiments/SoundScript.Labs`.
5. **Every production file changed:** [production-files.txt](../validation/v16/production-files.txt) inventories all changed/new `src/` files against the pre-work `main` baseline. It includes tests and project metadata explicitly. The broader branch diff also includes Lab code/evidence, CLI/browser validation scripts, sample hosts, documentation and candidate version metadata.

## Language and developer surfaces

6. **Grammar/API:** opt-in `CompileRuntime` / `CompileRuntimeFile`; contextual `param name = decimal`; metadata, `Get`, strict decimal `Set`, atomic `SetMany`, `Reset`, `Revision`, `Bind`, `CreateInstance`, `RenderAudio`, `SceneAt`. Snapshots also expose ordinary `RenderWave` and `RenderMidi`. Direct expressive track gain and constant visual x/y/opacity/rotation/width/height are allowed. Bounds intersect when a parameter serves multiple targets.
7. **Compatibility:** existing static entry points and `let`/`marker` semantics remain. `param` is not a new global lexer keyword. Runtime decimal parsing is invariant and restores caller culture; ordinary parser behavior is preserved. Package layout intentionally changes: consumers that directly addressed the old copied `Data/corpus` tree must use the catalog/resource APIs or request an editable corpus path.
8. **Migration:** none for ordinary SoundScript programs or documented static APIs. Parameterized source must use the new opt-in entry point. Raw package-content consumers need the resource-path adjustment above.
9. **Types:** decimal only, mandatory literal defaults, case-sensitive ASCII names beginning with a letter and followed by letters/digits. No string evaluation, implicit numeric coercion, runtime source injection or unresolved required-value state. Unsupported targets, expressions, imports, declarations/collisions, unknown names, wrong categories and bounds are rejected.
10. **CLI:** `inspect --params`, `run --runtime`, `wave --runtime`, repeatable `--param name=value`; assignments imply runtime mode. Inspection includes defaults, bounds and effective values. Invalid assignments return usage code 2 and preserve existing output. CLI `wave` keeps ordinary unpadded Wave semantics; use API `RenderAudio` for visual-duration padding.
11. **Playground:** actual production compilation, metadata-driven decimal controls, atomic apply/reset, audio and SVG output, diagnostics, explicit recompile after source edits. Source and output share a desktop workspace and stack on narrow screens. The existing music, Studio, media and transcription workflows remain available.
12. **Documentation:** README quick start; runtime contract guide; CLI and .NET API guides; programmatic-media and Playground guides; sample READMEs; package/resource guidance; candidate notes; this report and the [DX gate](v16-dx-release-gate.md). Generated documentation uses the repository's independent public/development version ownership.
13. **Examples:** the console monitoring host compiles once and produces normal → warning → critical WAV/MIDI/JSON/SVG; the package-backed ASP.NET sample applies external state through the same API. No streaming or hidden source rewriting is used.

## Package audit

14. **Changes:** corpus metadata is embedded in Wordbank and WAVs in the small, separate `SoundScript.Wordbank.Corpus` resource assembly. Desktop playback reads resources directly. Explicit editable-path APIs lazily materialize versioned, owned local data; source-checkout content and browser per-word HTTP delivery remain supported. Blazor marks the WAV assembly lazy, and browser validation observed no eager requests for it. The package retains corpus CC0 licensing and `SOURCES.md` provenance under `licenses/`.
15. **Exact reproduced warning:** the validator's deliberate `dotnet add package --no-restore` step prints `warn : --no-restore|-n flag was used. No compatibility check will be done and the added package reference will be unconditional.` Subsequent explicit restore, build and execution pass; build reports **0 warnings, 0 errors**. The original VS Code warning text was not supplied and was not reproduced; no claim is made that this SDK advisory is that warning.
16. **Classification:** the advisory describes the validator's staged installation command, not a SoundScript package defect. No warning suppression was added. An earlier implicit restore failed on an incomplete HarfBuzz cache extraction; disk pressure was also observed during a later sample build. Those were tooling/environment failures, not proven package dependency defects. Fresh isolated restores and builds succeeded after workspace cleanup. A final `pack --no-build` attempt also failed because the package's existing reference-bundling target invokes Build; ordinary `dotnet pack --no-restore` succeeded. The failed attempt's stale archive was rejected, then rebuilt and revalidated.
17. **Consumer pollution:** baseline 73 `contentFiles`; candidate **zero**. No `build`, `buildTransitive`, analyzers or tools are injected. The clean consumer output contains no copied corpus tree. Fifteen managed assemblies, their XML documentation and companion portable symbols are validated. Normal transitive native dependencies remain supported dependencies, not accidental corpus content.
18. **Measured package sizes:** baseline `15.0.0`: **5,365,203 bytes**, 110 ZIP entries; candidate `16.0.0-preview.1`: **5,352,524 bytes**, 40 entries (12,679 bytes smaller). Final candidate SHA-256: `E7C07E7473EB6975EECF0A9257ECF64B749CA15200648A8C50AD38C1FD9851FB`. Size/hash identify the tested local artifact, not a reproducible-build promise.

## Validation and evidence

19. **Commands/results:** run from the repository root with pinned submodules initialized. Browser verification uses the local published Playground and installed Edge. Retained text evidence and machine-readable totals are in [validation/v16](../validation/v16/README.md).

```powershell
dotnet test SoundScript.sln -c Release --no-restore --logger 'trx;LogFileName=v16-final.trx' --results-directory artifacts/v16-tests
# 1,294 passed; 0 failed; 0 skipped. Lab-only baseline was 1,232.
dotnet test experiments/SoundScript.RuntimeLab/Tests/SoundScript.RuntimeLab.Tests.csproj -c Release --logger 'trx;LogFileName=v16-final-lab.trx' --results-directory artifacts/v16-tests
# 51 passed; 0 failed; 0 skipped.
node --test scripts/transcription-browser-input.test.cjs scripts/playground-integrity.test.cjs scripts/playground-startup.test.cjs scripts/corpus-provenance.test.cjs
# 26 passed; 0 failed.
node --test scripts/homepage-release.test.cjs
# 9 passed; 0 failed.
pwsh -NoProfile -File scripts/update-docs.ps1 -Check
# Generated documentation and classifications agree with release state.
dotnet pack src/SoundScript -c Release --no-restore -o artifacts/v16-final-feed
pwsh -NoProfile -File scripts/validate-nuget.ps1 -PackagePath artifacts/v16-final-feed/SoundScript.16.0.0-preview.1.nupkg
# Package audit + fresh external restore/build/runtime/static/transcription/vocal consumer: PASS.
pwsh -NoProfile -File scripts/validate-media-package.ps1 -PackagePath artifacts/v16-final-feed/SoundScript.16.0.0-preview.1.nupkg
# Two independent media runs; 22 equal hashes; 3 distinct scenarios; SVG safety; 3 documentation examples: PASS.
dotnet run --project samples/RuntimeParameters/RuntimeParameters.csproj -c Release -- artifacts/v16-runtime-example --benchmark
# Both media change; structural counts 1/1/1; benchmark.json generated.
```

20. **Browser results:** actual Blazor/Chromium runtime compile/apply/reset/reject/recompile; static source without misleading controls; original MIDI run/download; on-demand vocal and Studio WAV download; no page errors. Desktop 1440×1000, laptop 1024×768, mobile 390×844 all pass editor/control reachability and horizontal-overflow checks. Startup normal/503 retry/corrupt-resource recovery pass. Transcription input/playback, CLI parity, invalidation/rejection/cancellation/mobile pass. Existing visual workflow discovers all 28 presets, compiles/scrubs 20, and passes mixed Wave/Voice pause/resume/restart/WebM export. Package-backed web sample passes all three legacy scenarios plus runtime adaptation/reset/invalid-input/UI checks.

```powershell
$env:CHROMIUM_PATH='C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'
node scripts/runtime-media-browser.cjs artifacts/playground
node scripts/verify-playground-startup.cjs artifacts/playground
node scripts/verify-transcription-playground.cjs artifacts/playground
# Serve artifacts/playground locally on port 5191 for the existing AV check:
node scripts/verify-av-playground.cjs http://127.0.0.1:5191/
# Run the restored package-backed ProgrammableMediaWeb sample on port 5198:
node scripts/verify-programmable-media.cjs http://127.0.0.1:5198
```

21. **Examples:** full suite includes 89 repository example and 39 Playground example compilation cases, all passing. A permanent canonical-example test compares console, web and Playground source through all three states. Manual CLI invocations of that same committed source produce MIDI identical to the console/API in all three states. CLI Wave bytes also match literal ordinary Wave output in permanent regression tests; padded/unpadded WAVs are not incorrectly compared as equal files.
22. **Wordbank/Vocal:** all 65 test names containing Wordbank and all 54 containing Vocal pass (overlapping categories, not additive totals). Four new resource tests cover fallback/extraction/audio ownership. Fresh external consumer produces a 160,358-byte vocal WAV with embedded resources and no repository corpus. Corpus provenance checks pass with the five pre-existing unresolved records (`bobtail`, `dashing`, `sleighing`, `test`, `world`) still explicitly listed; this work does not settle their provenance.
23. **Determinism:** runtime tests compare WAV, MIDI, JSON and SVG with literal production rendering; repeated renders, reset, retained snapshots and cultures pass. The original static external-consumer WAV/MIDI hashes match the V15 baseline exactly. Canonical monitoring WAVs match console/API, real Playground and package-backed HTTP output:

| State | Parameters | WAV SHA-256 |
|---|---|---|
| Normal | intensity=0.25, xpos=200 | `faa98bc77125ccf385e04472a77d5002408144f8305e0b35b4929347783b4ae0` |
| Critical | intensity=0.9, xpos=900 | `70ea91ad0f8355b7ade1072601282944aebce775b15c104a7d56f43e88bfaa1a` |

Static baseline WAV: `4F8B4D4714BD2C3844035DCDE8FEBD9CC5CCA7BC61F0E19DBA0DA13925063999`; MIDI: `C03D3166E8A993F192DCD3F70676031EC63CC56DD471126C1D990C8B6BD5565E`. These demonstrate the tested programs/configuration, not byte identity across untested platforms, external assets or arbitrary future renderer versions.

24. **Compile once:** permanent production `TenThousandUpdatesDoNotRecompileAndOldSnapshotsRemainStable` asserts actual instrumented tokenization/parse/timeline calls remain 1/1/1. Independent instances reuse structure and preserve those counts. The sample benchmark finishes at revision 140,042 with all three counts still 1. Rendering legitimately lowers/renders a snapshot; that work is separate from parsing source or rebuilding the visual timeline.
25. **Performance:** seven-batch medians on this machine; [raw benchmark](../validation/v16/benchmark.json) includes ranges/iterations. Cold compilation was 692.53 ms including first-use/JIT costs; warm compilation 80.593 µs / 15,666.12 B. `Set` including `Get` was 0.27731 µs / 0 B; changed-state `Set` + `Bind` 3.45131 µs / 1,040 B; cached `Bind` 0.05756 µs / 0 B; `SceneAt` 22.419 µs; complete audio 5,503.5 µs. Binding is about **23.35× faster than warm compilation** in this workload. These are observations, not real-time or throughput guarantees; cold/warm timings varied with host load in earlier runs.

## Remaining conditions and handoff

26. **Limits/risks:** approved decimal targets only; no runtime timing/structure/imports/expressions or incremental streaming. A future session must own clock, buffers, render boundaries, smoothing and output adapters. Stable snapshots preserve V16 semantics, but incremental DSP is separate work. Keep external audio assets stable and do not concurrently reconfigure the process-global corpus catalog while rendering. Caller batch dictionaries must remain stable while captured. Snapshot convenience calls made separately may observe different revisions; bind once for synchronized media. Existing package-content consumers must review the resource-layout change. Browser validation is Windows Chromium, not Safari/Firefox or a complete accessibility certification. The original unspecified VS Code warning remains unreproduced; pre-existing corpus provenance gaps remain explicit. Package licensing and platform/release matrix need human review.
27. **Recommendation:** ready for final human review as an unpublished V16 candidate, with the documented scope and resource-layout conditions. Review the production file inventory, runtime/Wordbank changes, reports and browser screenshots; run the normal release/platform/licensing checks; then separately authorize merge and publication through the repository release process. No parser/AST/renderer/timing redesign is required to expose the implemented capability.

See the [DX gate](v16-dx-release-gate.md) for lifecycle, concurrency, mutation safety and all 23 developer-experience conclusions. The branch's final commits and clean status are reported with the delivery; `git diff --stat main...HEAD` gives the complete review scope.
