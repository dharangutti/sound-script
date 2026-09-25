# RuntimeLab requirement traceability

Each tagged requirement in [`Requirements.md`](Requirements.md) appears once below. “Pass with condition” identifies a stated scope limit or a revalidation item; it does not silently widen the requirement. Requirement status is assessed for this Lab-only phase and its current evidence.

Evidence paths below are relative to the repository root. Test names refer to `Tests/RuntimeTests.cs`; commands run from the repository root. Fresh serial validation passed: Lab 51/51, production 1,232/1,232, browser 26/26. Exact commands and TRX summaries are in `Evidence/validation.json`. The production run history and benchmark caveat are recorded in `README.md`.

| Requirement | Status | Evidence |
|---|---|---|
| REQ-RUNTIME-001 | PASS | `RuntimeProgram.cs` / `TemplateCompiler.cs`: one initial parse and timeline; `CompileOnceAndTenThousandUpdatesPreserveTemplateAndOldSnapshot` checks counters stay 1. |
| REQ-RUNTIME-002 | PASS | Project and sources are confined to `experiments/SoundScript.RuntimeLab/`; no production project references it. |
| REQ-RUNTIME-003 | PASS | `README.md` ends with one recommendation, GO WITH CONDITIONS; evidence, measurement, risks, and production impact are recorded here and there. |
| REQ-ISO-001 | PASS | All Lab project, source, examples, tests, and docs are under `experiments/SoundScript.RuntimeLab/`. |
| REQ-ISO-002 | PASS | Change inventory for this Lab phase is confined to RuntimeLab; no `experiments/SoundScript.Labs/` files are changed. |
| REQ-ISO-003 | PASS WITH CONDITION | `SoundScript.RuntimeLab.csproj` and test project are independently buildable; production regression validation uses existing solution artifacts/local publish prerequisites. |
| REQ-ISO-004 | PASS | Lab implementation is in `RuntimeProgram.cs`, `RuntimeParameters.cs`, and `TemplateCompiler.cs`; production code is not modified. |
| REQ-ISO-005 | PASS | No package, release, tag, website, public docs, or release metadata operation is part of this branch. |
| REQ-ISO-006 | PASS | Branch is `codex/runtime-binding-lab`; no merge to main is part of the Lab phase. |
| REQ-SAFE-001 | PASS | No production parser source is changed; Lab delegates to `SoundScript.Parser.Parser`. `UnchangedProgramsAreByteAndBehaviorEquivalent` and the prior 1,232-test production pass support compatibility. |
| REQ-SAFE-002 | PASS | No production AST source is changed; `RuntimeProgram.Bind` copies only the addressed track container and gain nodes. Template preservation is asserted by `CompileOnceAndTenThousandUpdatesPreserveTemplateAndOldSnapshot`. |
| REQ-SAFE-003 | PASS | No production interpreter source is changed; Lab invokes the current `VisualInterpreter.Interpret` once in `TemplateCompiler`. |
| REQ-SAFE-004 | PASS | Production renderer sources are unchanged. `RuntimeFrame` calls `TemporalAudioRenderer`, `TemporalVisualSceneBuilder`, `TemporalVisualJson`, and `TemporalSvgRenderer`; literal-production equivalence is tested. |
| REQ-SAFE-005 | PASS | Lab stores and queries the production `VisualTimeline`; `BindingPreservesOutOfOrderIntervalsOverlapWaitAndTempoRamp` checks existing timeline behavior. |
| REQ-SAFE-006 | PASS | No production public API source is changed; the new public API is contained within the experimental namespace/project. |
| REQ-SAFE-007 | PASS | Golden files were not rewritten. `UnchangedProgramsAreByteAndBehaviorEquivalent` checks WAV bytes and scene JSON against production; fresh production suite passed 1,232/1,232. |
| REQ-SAFE-008 | PASS | Lab removes only leading `param` declarations; other `let` tokens are parsed by production. Test `UnchangedProgramsAreByteAndBehaviorEquivalent` covers a compile-time `let`. |
| REQ-SAFE-009 | PASS | Lab does not rewrite markers; `UnchangedProgramsAreByteAndBehaviorEquivalent` covers a production `marker` declaration and reference. |
| REQ-REVIEW-001 | PASS | Review recorded against production facade and compilation path referenced by `RuntimeTests.cs`: `SoundScriptEngine.Compile`, `.CompileFile`, and `SoundScriptCompilation`/`CompileMedia`; tests use the public compile path. |
| REQ-REVIEW-002 | PASS | `RuntimeProgram.cs` uses production `CompileMedia` equivalents through `VisualTimeline`, `TemporalAudioRenderer`, `SceneAt` behavior; `BindingPreservesOutOfOrderIntervalsOverlapWaitAndTempoRamp` exercises media timeline cases. |
| REQ-REVIEW-003 | PASS | `RuntimeTests.cs` calls production WAV, MIDI-adapter, scene JSON, and SVG paths; `Program.cs` emits WAV/JSON/SVG. Production rendering entry points are not modified. |
| REQ-REVIEW-004 | PASS | `RuntimeTests.cs` covers `let`, `marker`, constants, and numeric expressions in `UnchangedProgramsAreByteAndBehaviorEquivalent`; `TemplateCompiler` recognizes only direct parameter references. |
| REQ-REVIEW-005 | PASS | `README.md` documents `let` as compile-time and unchanged; test `UnchangedProgramsAreByteAndBehaviorEquivalent` runs a `let`-based source through Lab and production. |
| REQ-PARAM-001 | PASS | `RuntimeParameter` is a separate decimal schema; `let` remains in the production AST. `PropertyRangesAreTypedAndEnforced` asserts schema type and bounds. |
| REQ-PARAM-002 | PASS | `param` preprocessing is contained in `TemplateCompiler`; no official grammar/parser changes. |
| REQ-PARAM-003 | PASS | `Examples/monitor.ss` declares intensity/xpos and binds opacity, x, and direct track gain. |
| REQ-PARAM-004 | PASS | `RuntimeProgram.Compile`, `Set`, and `RenderAudio` implement the experimental host flow; `Program.cs` demonstrates multiple updates and renders. |
| REQ-SCOPE-001 | PASS | `RuntimeParameter` accepts decimals only; `WrongTypesAndSourceInjectionAreRejected` checks non-decimal values. |
| REQ-SCOPE-002 | PASS | `TemplateCompiler.Range` supports x, y, opacity, rotation, width, height; `EveryApprovedVisualBindingMatchesProductionGeometry` covers each. |
| REQ-SCOPE-003 | PASS | Direct track gain is supported; `ParametersChangeOnlyIntendedAudioDynamicsAndVisualFields` verifies louder cue dynamics and stable unrelated notes. |
| REQ-SCOPE-004 | PASS | No runtime tempo binding exists; fixed tempo and tempo ramps retain production behavior in `BindingPreservesOutOfOrderIntervalsOverlapWaitAndTempoRamp`. |
| REQ-SCOPE-005 | PASS | Runtime references outside direct allowed properties throw in `TemplateCompiler`; no track creation API exists. |
| REQ-SCOPE-006 | PASS | No runtime note mutation API exists; tests verify note timing/pitch structure stays fixed while gain changes. |
| REQ-SCOPE-007 | PASS | `TemplateCompiler` rejects imports after parse with `NotSupportedException`; `UnknownSourceVariableAndImportsAreRejected` covers this. |
| REQ-SCOPE-008 | PASS | `TemplateCompiler` rejects parameter references outside direct approved positions and operators after a reference; `InvalidOrUnsupportedTemplatesFailClosed` covers expressions and control flow cases. |
| REQ-SCOPE-009 | PASS | No effects graph API or replacement behavior is implemented; `RuntimeProgram` only binds direct gain and visual slots. |
| REQ-SCOPE-010 | PASS | `RuntimeFrame.RenderAudio` renders complete WAV bytes; `README.md` explicitly bounds the experiment to offline renders. |
| REQ-SCOPE-011 | PASS | No synthesizer or live playback engine is present; `Program.cs` writes complete WAV files for user-controlled playback. |
| REQ-SCOPE-012 | PASS | No event scheduler exists; `RuntimeFrame` renders on demand through existing complete-render APIs. |
| REQ-SCOPE-013 | PASS WITH CONDITION | `README.md` defines single-owner updates; no concurrent mutation/thread-safety guarantee is offered or tested. |
| REQ-ARCH-001 | PASS | `TemplateCompiler` extracts declarations/references, then builds stable AST/timeline; `RuntimeProgram` holds separate values and binds snapshots to existing render paths. |
| REQ-ARCH-002 | PASS | `RuntimeFrame.RenderAudio` calls production `TemporalAudioRenderer`; visual calls use the production timeline and scene builder. |
| REQ-ARCH-003 | PASS | `RuntimeProgram` stores values separately from `CompiledTemplate`; `CompileOnceAndTenThousandUpdatesPreserveTemplateAndOldSnapshot` checks stable template and old frame. |
| REQ-ARCH-004 | PASS | External values enter as `decimal`; `Set(string, object?)` rejects non-decimal data. `WrongTypesAndSourceInjectionAreRejected` tests source-like strings. |
| REQ-ARCH-005 | PASS | `README.md` classifies the implementation as runtime binding after one initial token preprocessing/parse; `CompilationCounters` and its test assert no repeated parse. |
| REQ-ARCH-006 | PASS | `README.md` explicitly states `Set` does not re-tokenize, parse, or recompile timeline; counters are asserted in `CompileOnceAndTenThousandUpdatesPreserveTemplateAndOldSnapshot`. |
| REQ-INPUT-001 | PASS | `Examples/monitor.ss` and `RuntimeProgram.Compile(string)` use SoundScript-like structural source. |
| REQ-INPUT-002 | PASS | `RuntimeProgram.Set` receives values separately after compile; the 10,000-update test preserves the original compiled template. |
| REQ-INPUT-003 | PASS | Public `Set(name, decimal)` accepts host-supplied numeric data without integration to any external system; README lists representative host sources. |
| REQ-OUTPUT-001 | PASS | `Program.cs` emits production WAV bytes, temporal visual JSON, and SVG. |
| REQ-OUTPUT-002 | PASS | `Program.cs` writes state-specific JSON/SVG; `ParametersChangeOnlyIntendedAudioDynamicsAndVisualFields` verifies visual changes. |
| REQ-OUTPUT-003 | PASS | `ParametersChangeOnlyIntendedAudioDynamicsAndVisualFields` verifies changed gain affects cue velocity and WAV RMS; unchanged pitch/timing and fixed track remain stable. |
| REQ-OUTPUT-004 | PASS | Playback is user initiated by `<audio controls>` in generated `index.html`; no host playback is implemented. |
| REQ-DEMO-001 | PASS | `Examples/monitor.ss` defaults are intensity 0.25 and xpos 200. |
| REQ-DEMO-002 | PASS | `Program.cs` updates one runtime instance through .55/550 and .90/900; tests also cover those states. |
| REQ-DEMO-003 | PASS | `CompileOnceAndTenThousandUpdatesPreserveTemplateAndOldSnapshot` asserts template identity, original gain, reused note node, and one parse/timeline compile across updates. |
| REQ-DEMO-004 | PASS | `ParametersChangeOnlyIntendedAudioDynamicsAndVisualFields` checks x and opacity changes; `EveryApprovedVisualBindingMatchesProductionGeometry` checks all supported visual properties. |
| REQ-DEMO-005 | PASS | `ParametersChangeOnlyIntendedAudioDynamicsAndVisualFields` verifies intended gain change and stable unrelated track; `RuntimeStatesMatchIndependentlyCompiledLiteralProductionPrograms` compares WAV bytes. |
| REQ-DET-001 | PASS | `StateCycleAndIndependentCompilesProduceIdenticalWavJsonSvgHashes` checks repeated state and independent compile digest equality. |
| REQ-DET-002 | PASS | The same test hashes WAV, JSON, and SVG with SHA-256; `Program.cs` records hashes in `evidence.json`. |
| REQ-DET-003 | PASS | `CompileOnceAndTenThousandUpdatesPreserveTemplateAndOldSnapshot` checks old-frame repeat output; `Program.cs` checks repeated render equality. |
| REQ-DET-004 | PASS | `ParametersChangeOnlyIntendedAudioDynamicsAndVisualFields` checks xpos does not affect WAV, gain leaves note pitch/timing and fixed track stable, and opacity leaves geometry stable. |
| REQ-VAL-001 | PASS | `InvalidUpdatesAreAtomic` checks unknown name rejected, revision unchanged, and current frame retained. |
| REQ-VAL-002 | PASS | `InvalidOrUnsupportedTemplatesFailClosed` includes a duplicate declaration and asserts compile rejection. |
| REQ-VAL-003 | PASS | `WrongTypesAndSourceInjectionAreRejected` rejects strings, integers, doubles, booleans, and null; decimal is the declared type. |
| REQ-VAL-004 | PASS | `PropertyRangesAreTypedAndEnforced` checks supported property minima/maxima and rejects out-of-range updates. |
| REQ-VAL-005 | PASS | `TemplateCompiler` rejects a declared parameter with no approved binding; accepted parameters initialize from their declared defaults in `RuntimeProgram`. |
| REQ-VAL-006 | PASS | Values are accepted only as decimal; `WrongTypesAndSourceInjectionAreRejected` verifies source-like string cannot add syntax. |
| REQ-VAL-007 | NOT APPLICABLE | No runtime string parameter type is experimented with; `RuntimeParameter.ValueType` is always `decimal`. |
| REQ-REG-001 | PASS WITH CONDITION | Before the Lab implementation, `dotnet test src/SoundScript.Tests/SoundScript.Tests.csproj -c Release --no-restore` first reported 1,227 pass / 5 environment failures; after initializing the wordbank submodule, 1,229 pass / 3 local-publish failures; after satisfying local production build/publish prerequisites, 1,232 passed. Failure causes and setup commands are recorded in README; the baseline was green once local prerequisites were present. |
| REQ-REG-002 | PASS | `dotnet test src/SoundScript.Tests/SoundScript.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=runtime-lab-final.trx' --results-directory artifacts/runtime-lab-production` passed 1,232/1,232. |
| REQ-REG-003 | PASS | No production source changed; fresh production suite passed 1,232/1,232 and browser checks passed 26/26. |
| REQ-REG-004 | PASS | No production golden or expected-output files were changed; runtime/production equivalence is asserted in tests. |
| REQ-MEASURE-001 | PASS | `Program.cs` reports cold compile 174.4846 ms and warm compile median 221.5124 μs in `artifacts/runtime-lab/evidence.json`. |
| REQ-MEASURE-002 | PASS | `Program.cs` reports `Set` median 0.083255 μs / 0 B and two updates plus bind median 5.94875 μs / 1,080 B. |
| REQ-MEASURE-003 | PASS | `Program.cs` reports complete audio render median 10,928.1875 μs and scene query median 105.31205 μs. |
| REQ-MEASURE-004 | PASS | Counters in current `evidence.json` show one tokenization, parse, and timeline compile over 71,059 binds; counter assertions also appear in `CompileOnceAndTenThousandUpdatesPreserveTemplateAndOldSnapshot`. |
| REQ-MEASURE-005 | PASS | `Program.cs` records median bytes/op; current report includes 0 B for `Set` and 1,080 B for two sets plus bind. |
| REQ-MEASURE-006 | PASS | `README.md` maps graduation to compiler binding-schema/frontend, parameter-state/snapshot binder, and additive facade integration. |
| REQ-STOP-001 | PASS | No renderer modifications were needed; audio and visual output go through production renderers. |
| REQ-STOP-002 | PASS | No `let` semantic change; production `let` test remains equivalent. |
| REQ-STOP-003 | PASS | Literal production equivalence tests and fresh 1,232/1,232 production tests passed; source tree diff confirms no production changes. |
| REQ-STOP-004 | PASS | No parser replacement; Lab calls the production parser after token preprocessing. |
| REQ-STOP-005 | PASS | Copy-on-write is limited to addressed gain slots; visual values are snapshot overrides. Template immutability and note identity are tested. |
| REQ-STOP-006 | PASS | Production timing is retained through the existing `VisualTimeline`; timeline feature cases are tested. |
| REQ-STOP-007 | PASS | No parallel renderer exists; production renderer calls are used by `RuntimeFrame`. |
| REQ-STOP-008 | PASS | No streaming or continuous scheduling infrastructure exists; only complete renders are produced. |
| REQ-STOP-009 | PASS | No existing public API is broken or edited; experimental facade is additive and isolated. |
| REQ-STRUCT-001 | PASS WITH CONDITION | Files use an equivalent isolated layout: project, runtime facade/state, compiler, example, tests, requirements, README, and traceability. Exact suggested filenames are not required. |
| REQ-DOC-001 | PASS | `README.md` opening states the compile-once/update/render hypothesis. |
| REQ-DOC-002 | PASS | `README.md` “Architecture tested” describes token preprocessing, one parse/timeline, private AST copy-on-write, and visual overrides. |
| REQ-DOC-003 | PASS | `README.md` “Input and runtime state” documents the structural example. |
| REQ-DOC-004 | PASS | `README.md` documents `Set`, decimal state, validation, snapshots, and invalidation. |
| REQ-DOC-005 | PASS | `README.md` lists WAV, JSON, SVG and demo artifacts. |
| REQ-DOC-006 | PASS | `README.md` documents deterministic evidence and test results. |
| REQ-DOC-007 | PASS | `README.md` reports current timing/allocation observations and their machine/build overlap limitation. |
| REQ-DOC-008 | PASS | `README.md` identifies the three production components requiring graduation work. |
| REQ-DOC-009 | PASS | `README.md` identifies single-owner operation, unsupported features, repeat audio lowering, and locale integration risk. |
| REQ-DOC-010 | PASS | `README.md` final section contains exactly one allowed recommendation: GO WITH CONDITIONS. |
| REQ-REPORT-001 | PASS | Phase 1 documentation files created: `experiments/SoundScript.RuntimeLab/README.md` and `TRACEABILITY.md`; RuntimeLab change inventory is confined to that directory. |
| REQ-REPORT-002 | PASS | `README.md` and REQ-ARCH-005 row report token-preprocess plus private AST copy-on-write and visual overrides. |
| REQ-REPORT-003 | PASS | `README.md` explicitly says no source reparsing after initial setup; REQ-ARCH-006 evidence records parse counters. |
| REQ-REPORT-004 | PASS | Lab command `dotnet test experiments/SoundScript.RuntimeLab/Tests/SoundScript.RuntimeLab.Tests.csproj -c Release --logger 'trx;LogFileName=runtime-lab.trx' --results-directory artifacts/runtime-lab-tests` passed 51/51; production passed 1,232/1,232; browser command in README passed 26/26. Full commands and TRXs are in `Evidence/validation.json`. |
| REQ-REPORT-005 | PASS | `Examples/monitor.ss` and generated `Program.cs` states provide before/after input/output; tests compare literal production values. |
| REQ-REPORT-006 | PASS | No production files were touched in the RuntimeLab implementation; `README.md` describes production impact as future work only. |
| REQ-REPORT-007 | PASS | Risks and limits appear in `README.md` and are individually tied to behaviors in this table. |
| REQ-REPORT-008 | PASS | `README.md` ends with GO WITH CONDITIONS. |
| REQ-REPORT-009 | PASS | `README.md` recommends future-version consideration subject to three contained areas and locale decision. |
| REQ-ACCEPT-001 | PASS | `RuntimeProgram.Compile` initializes defaults; `Examples/monitor.ss` is the initialized sample. |
| REQ-ACCEPT-002 | PASS | `Program.cs` changes both intensity and xpos after compile; `CompileOnceAndTenThousandUpdatesPreserveTemplateAndOldSnapshot` performs repeated updates. |
| REQ-ACCEPT-003 | PASS | `ParametersChangeOnlyIntendedAudioDynamicsAndVisualFields` asserts expected audio and visual effects. |
| REQ-ACCEPT-004 | PASS | `StateCycleAndIndependentCompilesProduceIdenticalWavJsonSvgHashes` verifies deterministic repeats. |
| REQ-ACCEPT-005 | PASS | `InvalidUpdatesAreAtomic`, `WrongTypesAndSourceInjectionAreRejected`, and `PropertyRangesAreTypedAndEnforced` verify predictable failure. |
| REQ-ACCEPT-006 | PASS | Fresh Lab suite passed 51/51; production suite passed 1,232/1,232. |
| REQ-ACCEPT-007 | PASS | `README.md` explicitly distinguishes true binding after setup from source recompilation; counters and identity tests substantiate it. |
| REQ-ACCEPT-008 | PASS | No production source changed; unchanged program parity is tested against the production compile/render path. |
| REQ-ACCEPT-009 | PASS | README recommendation is exactly GO WITH CONDITIONS. |
| REQ-NONGOAL-001 | NOT APPLICABLE | No production V16 implementation is included; the entire project is experimental. |
| REQ-NONGOAL-002 | NOT APPLICABLE | No live music engine is implemented. |
| REQ-NONGOAL-003 | NOT APPLICABLE | No uninterrupted audio stream is implemented. |
| REQ-NONGOAL-004 | NOT APPLICABLE | No DAW playback engine is implemented. |
| REQ-NONGOAL-005 | PASS | Runtime values accept decimals only; injection checks appear in `WrongTypesAndSourceInjectionAreRejected`. |
| REQ-NONGOAL-006 | PASS | The existing compile/render architecture remains in use; no replacement engine is present. |
| REQ-NONGOAL-007 | PASS | README makes only an experimental finding and conditional future-version recommendation, with no valuation or marketing claim. |
