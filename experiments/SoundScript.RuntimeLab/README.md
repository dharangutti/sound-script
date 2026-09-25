# SoundScript RuntimeLab

RuntimeLab tests the hypothesis that SoundScript can compile a fixed media structure once, update approved numeric values, and render new audio and visual output without changing production parser or renderer behavior.

This is an experimental, lab-only project. It does not make runtime parameters part of official SoundScript grammar or a production API. `param` declarations are accepted only at the beginning of in-memory source. The Lab compiler identifies declarations and direct references in the production tokenizer's token stream, converts those reference tokens to their initial decimal values, and invokes the existing parser and visual interpreter once. `let` and `marker` retain their existing compile-time meaning.

## Architecture tested

`TemplateCompiler` preprocesses the initial token stream, records parameter declarations and structural target addresses, then parses one stable production AST and builds one production `VisualTimeline`. The runtime parameter table is held separately from that structure. `Set` validates a decimal value against the intersection of all bound-property ranges and invalidates the current snapshot only when the value changes.

`Bind` captures an immutable value snapshot. For audio gain, it makes a private copy-on-write `ProgramNode` and the affected `TrackNode` containers, replacing only addressed `GainNode` values; unaffected nodes are shared inside the private template and remain unchanged. For visual properties, the snapshot stores typed property overrides applied to the state returned by the existing timeline. `RuntimeFrame` sends the resulting AST and timeline through the production audio and visual renderers. Rendering audio still performs the production AST-to-note lowering on each render.

The selected mechanism is **true runtime binding without reparsing after initialization**, implemented by Lab-only token preprocessing followed by private AST copy-on-write gain binding and sampled visual-property overrides. Parameter updates do not tokenize, parse, or compile the timeline again. They also do not cache a pre-rendered WAV. This classification is scoped to the existing compiled structure and approved direct bindings; it does not claim that every part of rendering avoids work.

The production path reviewed starts at `SoundScriptEngine.Compile` / `CompileFile`, produces a `SoundScriptCompilation`, and enters media work through `CompileMedia`. Audio rendering uses the production WAV/MIDI paths (`RenderWave` / `RenderMidi`); temporal visuals use `SceneAt`, then the existing JSON serializer or SVG renderer. Parser resolution turns `let` declarations, numeric expressions, and `marker` references into compile-time values in the AST. RuntimeLab places preprocessing above the production parser. The known CurrentCulture-sensitive gain conversion is isolated to the parameterized parse path only.

## Input and runtime state

See [`Examples/monitor.ss`](Examples/monitor.ss). It declares `intensity = 0.25` and `xpos = 200`, uses `intensity` for a track's direct `gain` and a visual's `opacity`, and uses `xpos` for the visual's `x`. A host supplies later values through `RuntimeProgram.Set(name, decimal)`; values are data and are never converted into source text. `Bind()` returns a stable snapshot. A later update creates a new snapshot and cannot change a previously bound frame.

```csharp
var runtime = RuntimeProgram.Compile(File.ReadAllText("monitor.ss"));
runtime.Set("intensity", 0.90m);
runtime.Set("xpos", 900m);
RuntimeFrame frame = runtime.Bind();
byte[] wav = frame.RenderAudio();
var scene = frame.SceneAt(TimeSpan.FromSeconds(2));
```

Supported numeric targets are direct track gain and visual `x`, `y`, `opacity`, `rotation`, `width`, and `height`. Values are `decimal`; bounds are checked, and shared parameters use the intersection of their targets' bounds. Gain requires the existing `perform expressive` mode. A parameter must have at least one supported binding. Visual runtime bindings require a unique visual name and a constant `set` value. Unknown names, duplicate declarations, invalid references, and unsupported syntax fail closed.

These ranges are RuntimeLab validation policy, not claims that production renderers impose the same clamps.

| Target | RuntimeLab range |
|---|---:|
| Gain / opacity | 0 to 1 |
| X | -12,800 to 12,800 |
| Y | -7,200 to 7,200 |
| Width | 8 to 1,280 |
| Height | 8 to 720 |
| Rotation | -360 to 360 |

## Output and demonstration

`Program.cs` renders three states from one compiled instance: normal (`intensity=.25`, `xpos=200`), warning (`.55`, `550`), and critical (`.90`, `900`). For each state it writes a complete WAV, temporal scene JSON, and SVG, plus `evidence.json` with SHA-256 hashes and measurements. Its `index.html` lets a reviewer compare the generated states. The audio output is complete offline rendering; there is no automatic playback, stream, live synthesizer, or scheduler.

The tests compare runtime-bound output against independently compiled literal production programs, check targeted audio and visual changes, verify unaffected tracks and fields, and compare repeated state-cycle and independent-compile hashes. See [`TRACEABILITY.md`](TRACEABILITY.md) for an individual disposition and evidence pointer for every tagged requirement.

## Reproduce

From the repository root:

```powershell
dotnet test experiments/SoundScript.RuntimeLab/Tests/SoundScript.RuntimeLab.Tests.csproj -c Release --logger 'trx;LogFileName=runtime-lab.trx' --results-directory artifacts/runtime-lab-tests
dotnet test src/SoundScript.Tests/SoundScript.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=runtime-lab-final.trx' --results-directory artifacts/runtime-lab-production
node --test scripts/transcription-browser-input.test.cjs scripts/playground-integrity.test.cjs scripts/playground-startup.test.cjs scripts/corpus-provenance.test.cjs
dotnet run --project experiments/SoundScript.RuntimeLab/SoundScript.RuntimeLab.csproj -c Release --no-build -- artifacts/runtime-lab
```

The production rerun used `git submodule update --init wordbank` and `dotnet publish src/SoundScript.Playground -c Release --no-restore -p:PublishDir=C:/Workspace/SoundScript/Lab.Runtime/sound-script/artifacts/playground/` for local prerequisites. Exact commands and fresh TRX summaries are in [`Evidence/validation.json`](Evidence/validation.json).

## Determinism and validation

The tests establish repeatability for the tested WAV, JSON, and SVG outputs, including a return to an earlier state and a separate compile of the same defaults. They also compare output at boundary and representative visual times against the production path. Updates reject unknown names, non-decimal values, injected source strings, and values outside property ranges. Duplicate declarations, unbound declarations, runtime expressions, unsupported targets, duplicate visual names, and imports are rejected. Missing parameters are an initialization error: every declaration must bind to an approved target, and each accepted declaration starts at its declared default.

## Performance observations

`Program.cs` warms the paths and reports seven batches of compile, update, bind, render, and scene-query timings, plus median per-operation allocations. The recorded run reports cold compile 174.4846 ms; warm compile 221.5124 μs; `Set` 0.083255 μs / 0 B; two `Set` calls plus `Bind` 5.94875 μs / 1,080 B; complete audio render 10,928.1875 μs; and `SceneAt` 105.31205 μs. The warm compile to bind ratio is 37.2368×. Tokenizations/parses/timeline compilations were 1/1/1 across 71,059 binds and 281,060 revisions. The results are observational and machine-specific; the benchmark overlapped regression builds. The initial production baseline was 1,227 passed / 5 failed; after initializing the missing wordbank submodule it was 1,229 passed / 3 failed because local publish output was absent. After satisfying local publish prerequisites, production passed 1,232 tests. Fresh serial revalidation passed Lab 51/51, production 1,232/1,232, and browser checks 26/26. All nine generated WAV/JSON/SVG hashes were independently recomputed and match the saved evidence. See [`Evidence/lab-evidence.json`](Evidence/lab-evidence.json) and [`Evidence/validation.json`](Evidence/validation.json).

## Production impact if graduated

The experiment changes no production files. A production version would need contained changes in three areas: (1) compiler binding-schema/frontend support to represent and validate approved parameter declarations and structural references; (2) parameter-state and snapshot binding support to preserve stable structure and deterministic snapshots; and (3) additive facade integration to expose compile, set, bind, and render functionality. The existing production parser's gain-number conversion uses current culture, while visual decimals use invariant culture. RuntimeLab contains this known gain parsing issue by parsing only parameterized source under invariant culture and restoring the caller's culture; plain programs retain the production behavior. Graduation should decide explicitly whether and how to address that production defect. Existing timing and renderer algorithms need no changes for this Lab design: audio still follows the current AST-to-note path and visuals still use the current timeline and scene builders.

At this Lab-phase checkpoint, `main:src` and `HEAD:src` have the same tree (`30b102aff65dd2e3e1ee7887411800b3279c4e22`) and production files touched are **none**. Lab files added on the branch: `Examples/monitor.ss`, `Program.cs`, `Requirements.md`, `RuntimeParameters.cs`, `RuntimeProgram.cs`, `SoundScript.RuntimeLab.csproj`, `TemplateCompiler.cs`, `Tests/RuntimeTests.cs`, `Tests/SoundScript.RuntimeLab.Tests.csproj`, `README.md`, and `TRACEABILITY.md`. Generated evidence files are `Evidence/lab-evidence.json` and `Evidence/validation.json`. Requirements file SHA-256 remains `B746C8A19C6678FA7EC3837049036F3CD9A8C6FF20E5F420474437B396984C39`.

## Risks and limits

The API is single-owner and does not promise thread-safe concurrent updates. It does not support imports, runtime-created tracks or notes, runtime expressions, control flow, nested gain binding, tempo binding, dynamic effects graphs, streaming, live synthesis, or continuous scheduling. Visual values are sampled when a frame's scene is requested. Audio render still repeats production AST-to-note lowering. The production gain parser's locale behavior is a known integration consideration, scoped around by the Lab as described above. Performance measurements are observational, machine-specific evidence, not a latency guarantee.

The concept merits consideration for a future SoundScript version if implementation stays within the three contained components above and the gain parsing culture policy is resolved.

See [`Requirements.md`](Requirements.md) for the unchanged acceptance contract and [`TRACEABILITY.md`](TRACEABILITY.md) for requirement-by-requirement status and evidence.

## Recommendation: GO WITH CONDITIONS
