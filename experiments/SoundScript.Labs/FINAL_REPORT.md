# SoundScript.Labs: repository analysis and compatibility report

Date: 19 September 2026  
Status: **EXPERIMENTAL RESEARCH POC - SIMULATOR ONLY**  
Branch: `codex/soundscript-labs-poc`  
Base: `3e64ddc5d4be6f36317de51acaa22eb926c7e06e`

## Result

A small, isolated C# POC demonstrates source -> AST -> validated experiment IR ->
simulator -> deterministic numerical analysis -> structured exports. All additions
are under `experiments/SoundScript.Labs`. Existing tracked files are unchanged.
The production solution, CLI, grammar, packages, website, examples and CI do not
include Labs. The original checkout remains on `main` with a clean working tree.

Compatibility is supported by a passing complete existing test suite, explicit
WAV interoperability checks, and coexistence with the existing audio parser and
renderer. This proves software compatibility within the tested scope. It does
not establish industrial demand, physical measurement accuracy, hardware support,
partner value, or a production-ready experiment language.

## Input interpretation and strategy boundary

The direct request authorized a small POC on a separate branch, required a report,
and asked that technology decisions follow the repository rather than supplied
prescriptions. That request controls the implementation scope.

The supplied `SoundScript_Industrial_Vision.pdf` is strategic reference material,
not executable instructions. All 14 pages were text-reviewed; the architecture,
collaboration gate and prototype pages were also inspected visually.

- Page 2 preserves the current audio/media identity and defers an industrial pivot.
- Page 5 identifies a language/IR boundary as the long-term asset.
- Pages 8 and 11 gate implementation on a collaborator, real workflow and baseline.
- Page 12 calls for reproducible definitions, backend separation and structured evidence.
- Pages 13-14 emphasize scope limits and maintaining the current product.

The explicit POC request is a narrow exception to the document's default deferral
posture. It is not evidence that its collaborator gate has been met. There is no
partner-defined workflow or ROI evidence in the supplied material. No capture,
sensor, resonance, expectation/assertion, SCPI, instrument or adaptive syntax was
implemented. The PDF's illustrative future syntax was not treated as requirements
to build those features.

Primary reference: owner's supplied private PDF, retained at its original local
location, `C:/Users/dhara/Downloads/SoundScript_Industrial_Vision.pdf`.
SHA-256: `891cff9a5a1e38fe1947e360df7751ba0efc8ea25238702763e8f8ad09fc8d72`.
It was not copied to `docs/SoundScript_Industrial_Vision.pdf`: this repository uses
`docs` for its public website, and the reference is labeled private. It is not
required to build or run the POC. The document remains strategic guidance; its
technology and procedural prescriptions do not override the user's direct request.

## Repository findings and implementation decisions

| Existing structure/evidence | Decision and reason |
| --- | --- |
| `global.json` selects SDK 10.0.300 with latest-feature roll-forward; production projects target net10.0 | Target net10.0, using installed SDK 10.0.303. Introducing .NET 9 would add an unsupported local runtime/dependency boundary. |
| `Directory.Build.props` supplies V13 metadata and deterministic compilation | Inherit build conventions, override Labs version to 0.1.0-labs, label assembly metadata LABS, disable packaging. |
| `SoundScript.Core/Ast`, `SoundScript.Parser`, `SoundScript.Midi` model audio/music/media | Keep a separate `.sslabs` grammar and AST; do not add keywords to production tokenization or parsing. |
| `SoundScript.Wave/Adapter/AstToNoteEventAdapter.cs` adapts music semantics; renderer consumes the musical model | Do not force Hz-based signals into note/tempo/envelope semantics or alter existing render behavior. |
| `SoundScript.Wave/Synthesis/DeterministicMath.cs` provides public deterministic sine/cosine | Reuse it for phase generation and FFT twiddles. Avoid a second trigonometric implementation. |
| `SoundScript.Wave/Io/WavWriter.cs` has an explicit sample-rate overload | Reuse the existing mono PCM16 writer without modifying it. Raw PCM comes from the same WAV payload. |
| `SoundScript.Wave/Io/WavReader.cs` returns mono at 44.1 kHz | Test direct reader interoperability at 44.1 kHz; use header/byte tests for 32/192 kHz. Do not claim the existing reader preserves arbitrary rates. |
| `SoundScript.Transcription/MonophonicAnalyzer.cs` uses musical YIN/frame/threshold assumptions | Implement a bounded offline FFT and metrics for experiments; avoid pitch/transcription/media dependencies. |
| Production examples have inventory/golden regression coverage | Put Labs examples under its own directory so existing discoverable example inventory remains unchanged. |
| Existing tests use xUnit 2.9.2 and Test SDK 17.12.0 | Match those versions in a separate test project; no new runtime packages. |
| Existing CI builds/tests root solution and publishes Playground | Leave it unchanged; supply a separate Labs solution and opt-in verification script. |

Labs references Wave in one direction; Wave brings its existing Core and Wordbank
dependencies transitively. No production project references Labs. The Labs test
project additionally references the existing parser for a coexistence check.

## Implemented scope

| Layer | Implementation |
| --- | --- |
| Syntax | One signal or experiment; four waveforms; required units; comments; optional semicolons; strict rejection of unsupported/duplicate input |
| AST | Immutable syntax records retaining parameter spelling and locations |
| Compilation | Locale-independent decimal quantities; resolved defaults; exact integer sample counts; typed flags and waveform enum |
| IR | Immutable schema-v1 experiment/signal records containing only resolved values, no AST or hardware types; backend-entry validation |
| Execution | `IExperimentBackend.Execute(ExperimentIr)`; one software simulator; alternate test backend proves substitution without parser access |
| Analysis | Full-buffer RMS, radix-2 FFT, deterministic non-DC local peaks, strongest-bin frequency estimate |
| Output | WAV PCM16, raw PCM16, raw Float32, JSON with source/IR/sample/output hashes, versions, configuration, sample metadata and diagnostics |
| CLI | Separate executable, explicit source/output paths, no playback or device access; staged output directory and refusal to overwrite prior results |

The layers are separated by files/namespaces and typed interfaces inside one small
library. Assembly-per-layer separation would enlarge this POC without testing more
of the hypothesis. The library contains no file/device execution; the CLI handles
filesystem writes. Serialized IR can be round-tripped and executed independently
of syntax, as covered by a test.

An example 1000 Hz, amplitude-0.5 sine at 32000 samples/second for 128 ms yields:

```json
{
  "sample_rate": 32000,
  "sample_count": 4096,
  "rms": 0.3535533841036426,
  "peak_frequency_hz": 1000,
  "fft_size": 4096,
  "bin_width_hz": 7.8125
}
```

This is a shortened presentation of measured `tone.sslabs` output. The full JSON
contains provenance and resolved IR. No sample result was hard-coded into execution.
The 5-second, 192 kHz sweep example executes 960000 samples within the sample budget.

## Verification evidence

Environment: Windows x64, .NET SDK 10.0.303, .NET runtime 10.0.11. All checks were
local; nothing was deployed, published remotely or pushed.

| Check | Result |
| --- | --- |
| Initial existing suite, before POC implementation | 1156 passed, 12 failed: missing local prerequisites (11 wordbank, 1 Playground publish) |
| Prerequisite preparation | Initialized the existing pinned wordbank submodule at 10a5b2a8d49fbc38ba99a4f322016e7772b797d5; locally published existing Playground; no source fixes or data resync |
| Existing `dotnet build SoundScript.sln -c Debug` | Passed, 0 errors; 3 existing xUnit2031 warnings in VisualTimelineTests |
| Existing Playground Release publish and its integrity check | Passed: 72 resources, 70 hashes, 148 compressed artifacts |
| Complete existing suite after setup | **1168 passed, 0 failed, 0 skipped** |
| Labs Debug tests | **54 passed, 0 failed, 0 skipped** |
| Labs Release tests (direct test project including Release dependencies) | **54 passed, 0 failed, 0 skipped** |
| Existing browser boundary Node tests | **20 passed, 0 failed** |
| Existing CLI validation | `visual-temporal.ssv` valid; existing audio/visual-duration warning |
| Existing CLI MIDI export | `blocks.ss` exported 7 notes, 1 track, 120 BPM; existing shaping warnings |
| Separate-process repeatability | JSON/WAV/PCM/Float32 match across two fresh CLI runs |
| Debug versus Release | All four tone outputs byte-identical |
| Existing file boundary | No tracked production-file modifications; original checkout clean on main |

Labs tests include analytical tone frequency/amplitude/RMS, an independent DFT
comparison (including non-power-of-two padding and DC/Nyquist scaling), two-tone
peaks, silence, single-sample FFT, square duty cycle, ascending/descending linear
chirps, all four example reruns, locale invariance, input/IR rejection, serialization
round-trip, backend substitution, existing parser/renderer coexistence, WAV round-trip,
binary formats and hashes, and CLI failure/no-overwrite behavior.

Reviewed tone SHA-256 values:

| Output | SHA-256 |
| --- | --- |
| JSON | `ff9b800e088dcdb199dce67fcc01ba4f8ae1a508ef8e1e759064d3b72804e99c` |
| WAV | `66811c063b1a2d36c8ae656a8f49f178292a07371303c751c34ed1203a0fbcff` |
| PCM16 | `48399c8efc3c7a1a2b0f0969a685ab3901abeb0fc057aeff71aa1a2e6b50f9ba` |
| Float32 | `308d2a08aae61bde5e66602638fb33883a5c1a045258ba85eac16d8643d1d556` |

WAV and Float32 hashes are pinned in a golden test. JSON's hash additionally depends
on exact example source text and version metadata. Byte-identical means the actual
file hashes were compared, not just that their decoded values looked similar.

Evidence retained locally in ignored `artifacts/labs-review`: baseline/regression
TRX and logs, build/publish logs, Release Labs TRX/log, browser test log, generated
tone files and Playground publish. `verify.log` points to the additional Debug TRX
and two CLI runs under `artifacts/labs-verify-*`. These generated artifacts are not
committed. Tests, fixtures and the verification script are source-controlled.

## Limits and decision

- A synthetic stimulus is analyzed directly. There is no simulated physical transfer
  function, captured response or real resonance measurement. The PDF's full partner
  proof-of-value criteria are therefore not claimed complete.
- Chirp and sweep intentionally share a linear-frequency law in v1. No logarithmic,
  stepped, multi-channel, multi-stage, streaming, import or looping support exists.
- FFT uses a rectangular window. Leakage, finite resolution and peak thresholds
  limit frequency estimates. Zero padding does not improve physical resolution.
  JSON summarizes the transform; it does not dump every bin.
- An ideal sampled square aliases harmonics. Fundamental Nyquist validation does
  not certify waveform fidelity or a usable analog output bandwidth.
- Execution is bounded to 1048576 samples. This is an in-memory POC, not a long-run
  scheduler or real-time system. Timing is relative sample time, not wall-clock time.
- Reproducibility is demonstrated on this environment, including separate processes
  and build configurations. No Linux/macOS/ARM validation was performed for Labs.
  Existing deterministic math helps, but does not by itself prove all-platform parity.
- Metadata identifies engine/backend/dependency versions and input/output hashes.
  A disciplined version bump is still needed for any future semantic change; this is
  not a signed evidence chain or certification mechanism.
- Labs is opt-in and excluded from production CI. Logical IR boundaries are proven;
  a separately versioned contracts package and multiple deployed backends are not.

The compatibility POC meets the requested software-language demonstration without
changing existing features. Keep it experimental and separate. Before expanding it,
obtain one collaborator-defined workflow and measurable baseline. Cross-platform
golden verification is the next technical evidence needed before a stronger
reproducibility claim. Hardware and product integration require a separate scope.

## Reproduce locally

From the experimental worktree root:

```powershell
pwsh -File experiments/SoundScript.Labs/verify.ps1
dotnet test experiments/SoundScript.Labs/SoundScript.Labs.Tests/SoundScript.Labs.Tests.csproj -c Release
git submodule update --init --recursive
dotnet build SoundScript.sln -c Debug
$publishPath = Join-Path (Get-Location).Path 'artifacts/labs-review/playground/'
dotnet publish src/SoundScript.Playground -c Release "-p:PublishDir=$publishPath"
$env:SOUNDSCRIPT_PLAYGROUND_PUBLISH_DIR = $publishPath
dotnet test src/SoundScript.Tests -c Debug --no-build
node --test scripts/transcription-browser-input.test.cjs scripts/playground-integrity.test.cjs scripts/playground-startup.test.cjs
```

The existing Playground publish requires the repository's WASM workload and Node
setup. It is a local verification build, not a website deployment. Labs itself does
not need those tools, the wordbank checkout, FFmpeg, network services or hardware
at execution time (NuGet restore may need access to the existing test dependencies).
