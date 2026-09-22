# SoundScript V14 acceptance report

## Summary

Version: **14.0.0**, .NET SDK 10.0.303, Windows x64. Integration branch: `codex/soundscript-v14-media-runtime`.
Baseline: `11f6e7c` (latest main at task start); public compatibility baseline: published SoundScript 13.0.2.
Tested implementation/package commit: **7950a487c1ffd3e0b1eb4416218ccf5784eddcc9**. The subsequent documentation/evidence commit records these results and bundles the hash inventory.
Review PR: [#133](https://github.com/dharangutti/sound-script/pull/133). No merge, tag, GitHub Release or NuGet publication was performed.

Final Release regression: **1,230 passed, 0 failed, 0 skipped**. Focused V14 coverage: **26 passed**.
JavaScript regression: **26 passed, 0 failed, 0 skipped**. Package and isolated consumer validation passed.
APICompat found no breaking changes. Two separate consumer processes produced **22 identical SHA-256 hashes**.

## Evidence index

- **R**: `src/SoundScript/SoundScriptMediaCompilation.cs`, `SoundScriptEngine.cs: CompileMedia`; `MediaRuntimeTests` (6 cases).
- **J**: `src/SoundScript.Media/TemporalVisualJson.cs`; `MediaJsonTests` (2 cases).
- **S**: `src/SoundScript.Media/TemporalSvgRenderer.cs`; `MediaSvgTests` (18 cases, including all 9 canonical shapes).
- **W**: `samples/ProgrammableMediaWeb`; `scripts/verify-programmable-media.cjs`, actual Edge audio playback.
- **C**: `samples/ProgrammableMedia`; `scripts/validate-media-package.ps1`, fresh external workspace/cache and two process runs.
- **P**: `scripts/validate-nuget.ps1`, package/NuSpec/XML/symbol/license/dependency inspection and another external WAV/MIDI/transcription consumer.
- **F**: complete Release `SoundScript.Tests` run and existing JavaScript regression; exact commands below.
- **D**: [runtime guide](programmatic-media-runtime.md), [tutorial](tutorials/programmable-media.md), [article](articles/programmable-media-runtime-dotnet.md).

## Requirements matrix

Every numbered master section is represented below; additional audit detail follows. PASS means implemented and exercised,
or an explicit architectural prohibition was inspected and respected. Evidence abbreviations resolve to exact files above.

| Requirement / master section | Implementation | Test/evidence | Status |
|---|---|---|---|
| REQ-RUNTIME-01 / 1: deterministic .NET media runtime | R composes both rails; typed scenes feed J/S | R, C, D | PASS |
| REQ-RUNTIME-02 / 2: reuse existing engine | Existing parser, VisualInterpreter, VisualTimeline, StateAt, scene builder, TemporalAudioRenderer | Production diff reviewed; no second parser/timeline/scene/clock | PASS |
| REQ-SCENE-01 / 3: Scene = F(time) | `SceneAt(TimeSpan)` calls existing StateAt | R arbitrary query order and exact boundaries; D | PASS |
| REQ-TIME-01 / 4: negative, zero, middle, boundaries, end, after, empty, overlap | Existing half-open timeline contract | `FacadePreservesIntervalsOverlapWaitAndInterpolation`, `EmptyProgramAndIndependentCompilationsAreDeterministic` | PASS |
| REQ-RUNTIME-03 / 5: minimal facade | `SoundScriptCompilation.CompileMedia`, concrete compiled object | C uses no AST, CLI or process; D | PASS |
| REQ-RUNTIME-04 / 6: no premature interface | Concrete immutable timeline + lazy cached audio | No IMediaRuntime introduced; one implementation | PASS |
| REQ-SYNC-01 / 7: shared time | Existing PCM tempo map and timeline; host time input | `MediaTimeMatchesPcmEventsAcrossTempoBoundary` constant/ramp; W | PASS |
| REQ-AUDIO-01 / 8: first-class deterministic audio | `RenderAudio`, `RenderCompleteWavBytes` use Wave; byte[] copies | R prefix/tail/padding tests; C WAV/MIDI/stereo; stream tradeoff documented | PASS |
| REQ-SYNC-02 / 9: host playback | Browser owns play/pause; core has no event loop | W actual audio.currentTime; D desktop pattern | PASS |
| REQ-SCENE-02 / 10: typed contract | Existing TemporalVisualScene/Primitive/ShapePath | J/S accept typed scenes; no internal JSON | PASS |
| REQ-JSON-01 / 11: scene JSON | `TemporalVisualJson.Serialize`, schemaVersion 1.0 | J deserializes fields/order/paths; D schema table | PASS |
| REQ-SVG-01 / 12: SVG adapter and geometry | S paints existing canonical paths, 1280×720 | `EveryCanonicalShapePaintsExistingGeometry` 9 cases | PASS |
| REQ-SECURITY-01 / 13: SVG injection | XML construction; paint allowlist | S label/attribute/paint attacks; inspected golden and C security.svg | PASS |
| REQ-SECURITY-02 / 14: JSON escaping/stability | System.Text.Json safe encoder | J quotes, slash, newline, Unicode, angles, ampersand, 10,000-character label | PASS |
| REQ-WEB-01 / 15: HTML/Canvas consumers | Plain HTML sample; Canvas/DOM mapping in D | W; no new web framework or Canvas timing engine | PASS |
| REQ-AUDIO-02 / 16: optional WebM | Existing sampling/raster/FFmpeg adapter unchanged | Real 288-frame WebM encoded and decoded; missing dependency tests in F | PASS |
| REQ-RUNTIME-05 / 17: typed runtime data | `MonitoringScenario.BuildSource` | C/W Healthy, Warning, Critical change tempo/pitch/color/size/text; no Template<T> | PASS |
| REQ-DOTNET-01 / 18: neutral sample | `samples/ProgrammableMedia` | C compile → audio → SceneAt → JSON → SVG, no browser/FFmpeg/CLI | PASS |
| REQ-WEB-02 / 19: web sample | ASP.NET Core endpoints, native audio controls, plain JS | W play/pause/resume/restart/seek/scenarios/scaling | PASS |
| REQ-DOCS-01 / 20: desktop/other .NET | D explains host player.Position and typed primitive rendering | WPF/WinUI/MAUI/Avalonia/Blazor/ASP.NET/console/worker/game applicability; no dedicated adapter claim | PASS |
| REQ-DOTNET-02 / 21: ergonomics | C uses only local package from outside repo | Fresh cache; names discoverable, typed scene/duration/audio; R sandbox/error tests | PASS |
| REQ-RUNTIME-06 / 22: errors | Existing .NET errors retained; invalid time explicit | `MediaPreservesSandboxAndReportsInvalidInputs`, W HTTP 400; F missing FFmpeg | PASS |
| REQ-DETERMINISM-01 / 23: repeat outputs | C two independent processes | 22 matching hashes; WebM decoded semantically; D scope | PASS |
| REQ-SYNC-03 / 24: representative sync points | Same PCM adapter/tempo map | R 0, 1, 1.5, 2, 3.999, 4 seconds and every audio note boundary, including tempo ramp end | PASS |
| REQ-SCENE-03 / 25: scene cases | Existing timeline/scene builder | R + `VisualTimelineTests`, `VisualPresetTests`, `AudioVisualUseCaseTests`; constants, waits, placement, overlap, animation | PASS |
| REQ-SVG-02 / 26: SVG tests | S includes bounds, paths, paint, opacity, rotation | S parses every canonical generated SVG; deterministic repeat/security/Unicode | PASS |
| REQ-JSON-02 / 27: JSON tests | J versioned serializer | J schema, fields, invariant decimals, ordering, empty golden, paths, Unicode | PASS |
| REQ-COMPAT-01 / 28: existing capability regression | Existing outputs and APIs preserved | F full suite; detailed capability map below | PASS |
| REQ-DOCS-02 / 29: central architecture guide | `docs/programmatic-media-runtime.md` | D includes required diagram and common timing statement | PASS |
| REQ-DOCS-03 / 30: update entry points | README, package README, dotnet-api, nuget, application-samples, documentation, visual-temporal | Link validator; historical published 13.0.2 links retained | PASS |
| REQ-DOCS-04 / 31: article draft | `docs/articles/programmable-media-runtime-dotnet.md` | Opened through Playground docs viewer in Edge; not externally published | PASS |
| REQ-WEB-03 / 32: Playground semantics | Same existing engine; learning navigation only | F + real browser normal/retry/corruption and learning links | PASS |
| REQ-PACKAGE-01 / 33: final version | Directory.Build.props 14.0.0/V14 | Version bumped after APIs/samples passed; CLI version test and P | PASS |
| REQ-COMPAT-02 / 34: slice branches | Six codex/v14-* slices fast-forwarded into integration | Runtime 34fbfc7, JSON fd0e0bf, SVG 27ae9e7, web 716f664, .NET f510167, final hardening commit above | PASS |
| REQ-RUNTIME-07 / 35: runtime slice | R | Initial 3 focused tests passed before integration | PASS |
| REQ-JSON-03 / 36: JSON slice | J | Runtime + JSON 5 passed before integration | PASS |
| REQ-SVG-03 / 37: SVG slice | S | Runtime + JSON + SVG 22 passed before integration | PASS |
| REQ-WEB-04 / 38: browser slice | W | Actual browser verification before integration | PASS |
| REQ-DOTNET-03 / 39: generic .NET slice | C | Package-backed sample ran all 3 scenarios before integration | PASS |
| REQ-DOCS-05 / 40: docs/security/hardening | D, R/J/S security, C/P | 26 focused cases; added tutorial/navigation at user's explicit later request | PASS |
| REQ-COMPAT-03 / 41: diff hardening | Production diff reviewed, Labs unchanged | No absolute local paths/Debug references/generated output/project refs in new consumers; XML docs included | PASS |
| REQ-COMPAT-04 / 42: full regression | F plus W/browser learning | 1,230 .NET + 26 JS; 0 failed/skipped | PASS |
| REQ-PACKAGE-02 / 43: local package | `SoundScript.14.0.0.nupkg` and snupkg | P inspected 14 assemblies/docs/dependencies/license/readme/symbols | PASS |
| REQ-PACKAGE-03 / 44: external consumer | C/P separate fresh temporary workspaces | No project refs, new cache, package-only source mapping, full media plus basic MIDI/WAV | PASS |
| REQ-COMPAT-05 / 45: compatibility | Additive APIs only | APICompat 10.0.401 against published 13.0.2; no removed APIs/signature changes | PASS |
| REQ-RUNTIME-08 / 46: simple developer flow | Compile → CompileMedia → RenderAudio/SceneAt → J/S | C compiled exact workflow | PASS |
| REQ-DOCS-06 / 47: truthful positioning | D and README | Limits explicitly documented; no unsupported desktop/streaming/codec claims | PASS |
| REQ-DOCS-07 / 48: final report/review | This report + integration PR | All evidence below; no automatic publication/merge | PASS |
| REQ-DOCS-08 / additional user request | Tutorial under docs/tutorials; article and guide; Playground navigation + bundled learn/ | Real browser opens all three; nested guide link followed successfully | PASS |

### Detailed acceptance checks

| Requirement | Implementation / evidence | Status |
|---|---|---|
| REQ-TIME-02 no silent clamp | Negative rejection; after-end scene retains requested time; R | PASS |
| REQ-AUDIO-03 audio tail and defensive copies | `AudioIsPreservedAndDefensivelyCopiedWithoutVisuals`; exact PCM prefix retained when padded | PASS |
| REQ-SECURITY-03 sandbox survives facade | `MediaPreservesSandboxAndReportsInvalidInputs` plus `ImportSandboxTests`; missing file differs from root escape | PASS |
| REQ-JSON-04 no AST leakage | Schema only includes typed primitive/path fields; consumer deserializes JSON | PASS |
| REQ-SVG-04 rotation once | Canonical paths already transformed; S asserts source point coordinates, no extra transform on path groups | PASS |
| REQ-SECURITY-04 inspected SVG | `Golden/v14-security.svg`, parsed and inspected; no script/img/event attributes; safe title/text/data-name | PASS |
| REQ-WEB-05 stale responses | `app.js` abort/generation guard on seek/scenario; audio.currentTime authoritative | PASS |
| REQ-WEB-06 bad endpoint inputs | W verifies negative/NaN/huge times, unknown scenario return 400; after-end empty | PASS |
| REQ-PACKAGE-04 package contents | 14 DLLs and matching XML, 14 portable symbol sets, 73 corpus files, existing 6 direct dependencies, no Labs/Debug outputs | PASS |
| REQ-COMPAT-06 warnings/ignored tests | Final build no compiler/analyzer warnings; 0 skipped tests; prior failures explained below | PASS |
| REQ-DOCS-09 changed local links | `node scripts/verify-v14-docs.cjs`; file and Markdown heading existence | PASS |
| REQ-COMPAT-07 unresolved scan | Changed files scanned for TODO/FIXME/HACK/TEMP/placeholder/not implemented/NotImplementedException; classifications below | PASS |

## Tests and commands

All commands run from the repository root unless a script creates an external workspace.

```powershell
# Prerequisites, build, Release Playground publish, full suite and JS checks
./scripts/validate-release.ps1
# Final full rerun after isolating global Wordbank mutation tests
dotnet test SoundScript.sln -c Release --no-restore --logger 'trx;LogFileName=Release.trx' --results-directory artifacts/validation/Release
node --test scripts/transcription-browser-input.test.cjs scripts/playground-integrity.test.cjs scripts/playground-startup.test.cjs scripts/corpus-provenance.test.cjs
# Focused additions
dotnet test src/SoundScript.Tests -c Release --no-restore --filter 'FullyQualifiedName~MediaRuntimeTests|FullyQualifiedName~MediaJsonTests|FullyQualifiedName~MediaSvgTests'
# Real browser, including bundled learning navigation (CHROMIUM_PATH points to installed Edge)
node scripts/verify-playground-startup.cjs artifacts/playground
node scripts/verify-programmable-media.cjs http://127.0.0.1:5198
# Real optional video adapter
dotnet src/SoundScript.Cli/bin/Release/net10.0/soundscript.dll video examples/visual-temporal.ssv --out artifacts/v14/regression.webm --width 320 --height 180 --fps 24 --jobs 2
# Local package and two independent external consumers
dotnet pack src/SoundScript -c Release -o artifacts/v14/package
./scripts/validate-nuget.ps1 artifacts/v14/package/SoundScript.14.0.0.nupkg -PackageVersion 14.0.0
./scripts/validate-media-package.ps1 artifacts/v14/package/SoundScript.14.0.0.nupkg
# Public compatibility against the published baseline downloaded from NuGet
dotnet tool install Microsoft.DotNet.ApiCompat.Tool --tool-path artifacts/v14/tools --version 10.0.401
./artifacts/v14/tools/apicompat package artifacts/v14/package/SoundScript.14.0.0.nupkg --baseline-package artifacts/v14/SoundScript.13.0.2.nupkg --run-api-compat --enable-rule-cannot-change-parameter-name
node scripts/verify-v14-docs.cjs
git diff --check
```

Final .NET result: **1,230/1,230**, 34 seconds; **26/26** focused cases; JavaScript **26/26**.
Playground browser: normal startup, transient-503 retry and corrupt-resource rejection passed; tutorial/guide/article
opened and rendered; tutorial → runtime guide navigation passed. Web sample: play, pause, resume, restart,
seek, actual-audio synchronization, scenario changes, scaling, invalid inputs and after-end state passed.
WebM: **288 scenes at 24 FPS**, 320×180, rendered and decode-verified. Existing SS2105 warning is expected for
this fixture: its audio extends beyond the visual duration and the existing video adapter truncates it.
The new complete media facade preserves audio tails; this does not alter the old video policy.

Detailed logs/TRX are retained under ignored `artifacts/v14` and `artifacts/validation/Release`.
`validate-media-package.ps1` retains its fresh temporary consumer/cache for inspection and reproduces all hashes.

### Existing capability coverage

`.ss/.ssw/.ssv`, MIDI and CLI: `ExampleCompilationTests`, `SoundScriptPackageApiTests`, `CliProductTests`.
Mono/stereo WAV and Wave: `WaveRenderingTests`, `WaveV2Tests`, `WaveV3Tests`, `WaveV4Tests`, `WaveV8Tests`, `WaveDeterminismTests`.
SoundCSS/OGG: `CliSoundCssTests`, `SoundCssDspMappingTests`, existing Timbre/OfflineRenderer tests.
Composition/prosody: `ComposeWaveCliTests`, `WordProsodyTests`, `ProsodyRenderTests`, `ProsodySpeechRendererTests`.
Expressive playback: `ExpressivePerformanceTests`, `PerformanceCliTests`, `TempoRampExportTests`.
Visual/WebM: `VisualTimelineTests`, `VisualPresetTests`, `VisualParityTests`, `AudioVisualUseCaseTests`, real export above.
Transcription: all existing transcription/melody/polyphonic/mixed/percussion suites, including installed FFmpeg integration.
Vocals/Wordbank: existing vocal/continuous-vocal/Wordbank suites, including installed eSpeak integration.
Playground: preset/publish suites, JavaScript tests, actual published browser startup and documentation navigation.
Missing FFmpeg/encoder behavior: `CliProductTests.PreflightRequiresFfmpegAndEncodersWithoutWritingOutputs`.

### Resolved validation findings

- The CLI release test had a hardcoded 13.0.2 expectation; updated to 14.0.0 alongside the version source.
- A prosody click test passed alone but intermittently failed in parallel. Existing Wordbank tests mutate the
  global locale registry while unrelated renderers read it. `WordbankCatalogCollection` now disables parallel
  execution of that mutating collection with other collections. The synthesis implementation and threshold
  are unchanged. The full suite then passed; tests were not disabled or skipped.
- Three pre-existing xUnit2031 warnings were removed by using predicate overloads of `Assert.Single`.
- Package `--no-build` was incompatible with the repository's custom pack target; validated normal `dotnet pack`.
- The Wordbank submodule was initialized before packaging its license. No upstream/submodule revision changed.
- Sample build file locks were resolved by stopping the sample server before rebuilding; final build has no warnings/errors.
- Historical comments in existing Playground source are not unfinished V14 code; the zero-item scan classification is below.

## Determinism

See [the committed SHA-256 inventory](v14-artifact-hashes.json): all **22 entries** matched in two independent
external-consumer processes. It covers source, MIDI, synchronized mono WAV, stereo WAV, schema JSON, SVG,
scene snapshot arrays and the security SVG. Healthy/Warning/Critical differ in every primary media format.
Determinism scope: identical source/assets/options/version/renderer inputs. WebM uses decode/semantic validation,
not a codec-byte identity claim. Baseline NuGet 13.0.2 SHA-256:
`76e414bce8a76403b3ffed6b7da123b13e3e405c7d548a831ed40ab6ead893a5`.

## Security

Labels containing `<script>alert(1)</script>`, an img/onerror payload, quote/onload payloads and `< > & " '` remain
XML text or a single escaped `data-name` value. Unicode survives parsing. Explicit malicious paint URLs are rejected.
`MediaSvgTests.SecurityGoldenContainsOnlyEscapedTextAndAttributes` byte-compares the reviewed fixture, and the
external consumer parses its generated SVG and rejects script/img/event nodes. JSON is deserialized and long labels
round-trip. These tests do not claim to sanitize arbitrary host-authored HTML; hosts must render labels as text.

## Package

Local **SoundScript 14.0.0** package, not published. Final nupkg SHA-256:
`d4b25b3a88737ede53c784caf1d46350d13f0964b7629640f14c174e6ce51b95`.
It is retained under `artifacts/v14/final-package`; the earlier candidate under `artifacts/v14/package` has the same API implementation.
P inspected NuSpec, metadata, README install/version,
MIT license, Wordbank CC0/font licenses, expected dependency versions, corpus content, XML and portable symbols.
Assemblies (each with matching XML): Api, Compose, Core, Media, Midi, Parser, Prosody, Timbre, Transcription,
Visual, Vocal, Voice, Wave, Wordbank. No Debug binaries, local project outputs, Labs files or unexpected assemblies.
Dependencies remain the existing DryWetMidi, OggVorbisEncoder, SkiaSharp, SkiaSharp Linux assets, SkiaSharp.HarfBuzz
and HarfBuzzSharp Linux assets. No new dependency or bundled browser/FFmpeg executable was added.

## Clean consumer and ergonomics

Two fresh workspaces outside the repository used only the local package and separate empty caches.
Package source mapping pins SoundScript to the candidate feed. C copied the console sample's application code,
compiled it without project references, and exercised every V14 API. The validator also extracted, compiled and
ran the C# examples from the runtime guide, article and tutorial against that package, including the tutorial's SoundScript source.
P independently tested basic WAV/MIDI,
Wave options, XML discovery, corpus copy and transcription (3 notes). Neither needed AST knowledge, CLI invocation,
console scraping or SoundScript process launching. Console output here is verification evidence, not an API dependency.
The ordinary consumer needs only `SoundScript` and `SoundScript.Media`; a sandbox additionally uses `SoundScript.Core`.

## Compatibility

APICompat 10.0.401 compared against the published 13.0.2 nupkg with parameter-name checks and reported:
**“APICompat ran successfully without finding any breaking changes.”** No APIs were removed or signatures changed.
Added APIs: `SoundScriptCompilation.CompileMedia`; `SoundScriptMediaCompilation` with Timeline, Duration,
RenderAudio and SceneAt; `TemporalAudioRenderer.RenderCompleteWavBytes`; `TemporalVisualJson.Serialize`/SchemaVersion;
`TemporalSvgRenderer.Render`. No obsolete APIs or migration steps. Existing fixed-duration video rendering remains unchanged.

## Known limitations

- Complete media audio is mono, cached in memory; Duration triggers first rendering and reads required assets.
  Existing stereo/stream APIs remain available separately. This is not a streaming player.
- Canonical text keeps the existing Latin bitmap font restrictions. Legacy Unicode labels use host fonts.
- SVG legacy cards preserve evaluated content/bounds/rotation/opacity but omit Playground decorative shadows/gradients.
- The browser sample can lag by request latency; audio.currentTime remains authoritative. It is a local integration demo.
- No dedicated WPF/WinUI/MAUI/Avalonia adapter or generic template framework is claimed.
- Host changes to the global Wordbank catalog during a render are outside identical-input determinism guarantees.
- Core/JSON/SVG need no FFmpeg; optional WebM still requires a compatible installed encoder.

## Unresolved items

None.

The TODO/FIXME/HACK/TEMP/placeholder/not-implemented scan excludes generated artifacts and classifies matches
in changed existing files: the Playground HTML `placeholder` attribute is an ordinary input hint;
this report's scan terminology is descriptive. No unfinished V14 implementation,
commented-out production code, unexplained skipped tests, broken changed Markdown links or Labs changes remain.
The integration checkpoint is committed and reviewable. The final documentation/evidence commit is intentionally outside
the implementation/package commit identified above; no runtime behavior is changed by recording evidence.
GitHub's separate Windows/Linux/macOS Debug/Release matrix was still running when this report was prepared;
the exact 1,230-test green acceptance run recorded here is local Windows Release, not a claim that remote CI has finished.
