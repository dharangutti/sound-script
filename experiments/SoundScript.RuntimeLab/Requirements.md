# SoundScript RuntimeLab — Tagged Requirements

**Status:** Experimental / Lab only  
**Target location:** `experiments/SoundScript.RuntimeLab/`  
**Suggested branch:** `codex/runtime-binding-lab`  
**Purpose:** Evaluate runtime-bound SoundScript parameters without changing production SoundScript behavior.

---

## 1. Objective

### REQ-RUNTIME-001 — Core hypothesis
The experiment shall determine whether SoundScript can support:

> **compile structure once → update approved runtime values → render new output**

without changing existing production parser semantics, renderer algorithms, timing semantics, deterministic behavior, or public APIs.

### REQ-RUNTIME-002 — Experimental only
The implementation shall remain an experiment and shall not be treated as a production SoundScript feature.

### REQ-RUNTIME-003 — Graduation decision
The experiment shall end with one of these recommendations:

- **GO**
- **GO WITH CONDITIONS**
- **NO-GO**

The recommendation shall be supported by implementation evidence, tests, measurements, and identified production impact.

---

## 2. Isolation

### REQ-ISO-001 — RuntimeLab location
Create the experiment under:

```text
experiments/SoundScript.RuntimeLab/
```

### REQ-ISO-002 — Existing Labs untouched
Do not modify:

```text
experiments/SoundScript.Labs/
```

### REQ-ISO-003 — Independent experiment
RuntimeLab should remain independently buildable where practical.

Do not add it to the production solution unless required purely for local validation.

### REQ-ISO-004 — No production feature implementation
Do not implement runtime binding directly inside production SoundScript projects during this experiment.

### REQ-ISO-005 — No publication
Do not publish or modify:

- NuGet packages
- GitHub Releases
- tags
- public website
- public documentation
- release metadata

### REQ-ISO-006 — Branch isolation
Use a dedicated experimental branch such as:

```text
codex/runtime-binding-lab
```

Do not merge the experiment into `main`.

---

## 3. Production Safety

### REQ-SAFE-001 — Existing parser unchanged
Do not modify production parser behavior.

### REQ-SAFE-002 — Existing AST semantics unchanged
Do not change production AST semantics for existing SoundScript source.

### REQ-SAFE-003 — Existing interpreter unchanged
Do not alter the production interpreter algorithm to make the POC work.

### REQ-SAFE-004 — Existing renderers unchanged
Do not alter production:

- Wave rendering algorithms
- MIDI rendering algorithms
- temporal visual rendering algorithms
- SVG rendering algorithms
- JSON serialization behavior

except if a minimal exploratory change becomes absolutely necessary, in which case stop and classify the result as requiring production changes rather than silently continuing.

### REQ-SAFE-005 — Existing timing unchanged
Do not introduce a second timing model or change current SoundScript timing semantics.

### REQ-SAFE-006 — Existing APIs unchanged
Do not change existing public V15 APIs.

### REQ-SAFE-007 — Existing output unchanged
Unchanged SoundScript programs must retain existing production output.

Do not update golden files to make the experiment pass.

### REQ-SAFE-008 — Existing `let` unchanged
Existing `let` declarations shall remain immutable compile-time values.

### REQ-SAFE-009 — Existing `marker` unchanged
Existing `marker` behavior shall remain unchanged.

---

## 4. Existing Architecture Review

### REQ-REVIEW-001 — Inspect compilation path
Before implementing the POC, inspect:

- `SoundScriptEngine.Compile`
- `SoundScriptEngine.CompileFile`
- `SoundScriptCompilation`

### REQ-REVIEW-002 — Inspect media path
Inspect:

- `CompileMedia`
- `RenderAudio`
- `SceneAt`
- media timeline behavior

### REQ-REVIEW-003 — Inspect render paths
Inspect:

- `RenderWave`
- `RenderMidi`
- JSON scene serialization
- SVG scene rendering

### REQ-REVIEW-004 — Inspect authoring constants
Inspect current parser handling for:

- `let`
- `marker`
- constant resolution
- numeric expressions

### REQ-REVIEW-005 — Record current constant behavior
Document that current `let` values are compile-time constants resolved by the parser before the resulting AST is used.

---

## 5. Experimental Runtime Parameter Concept

### REQ-PARAM-001 — Separate runtime concept
Model runtime values separately from compile-time constants.

Conceptually:

```text
let   = immutable compile-time value
param = runtime-bindable value
```

### REQ-PARAM-002 — Experimental syntax only
`param` does not need to become official SoundScript grammar.

Prefer implementing experimental parsing/preprocessing inside RuntimeLab.

### REQ-PARAM-003 — Example experimental source
Support an experiment conceptually similar to:

```text
param intensity = 0.5
param xpos = 200

tempo 120

track cue {
    C4 q E4 q G4 h
}

visual "indicator" for 4s {
    shape circle
    set x xpos
    set opacity intensity
}
```

### REQ-PARAM-004 — Runtime API concept
Provide an experimental host API conceptually similar to:

```csharp
var runtime = RuntimeProgram.Compile(source);

runtime.Set("intensity", 0.9m);
runtime.Set("xpos", 800m);

var output = runtime.Render();
```

Exact type and method names may differ if the implementation identifies a cleaner design.

---

## 6. Runtime Parameter Scope

### REQ-SCOPE-001 — Numeric-first scope
The first POC shall support only a minimal set of runtime-bindable numeric values.

### REQ-SCOPE-002 — Visual parameters
Target support for some or all of:

- X
- Y
- opacity
- rotation
- width
- height

The POC does not need to support every visual property.

### REQ-SCOPE-003 — Audio parameter
Attempt one safe audio-related numeric parameter if feasible without invasive changes.

### REQ-SCOPE-004 — Tempo optional
Tempo may be tested only if it can be supported without distorting the experiment or requiring invasive production changes.

### REQ-SCOPE-005 — No dynamic tracks
Do not support runtime creation or removal of tracks.

### REQ-SCOPE-006 — No dynamic notes
Do not support arbitrary runtime insertion/removal of note structures.

### REQ-SCOPE-007 — No dynamic imports
Do not support runtime modification of imports.

### REQ-SCOPE-008 — No runtime control-flow language
Do not implement runtime loops, branches, arbitrary expressions, or a second programming language.

### REQ-SCOPE-009 — No effects-graph replacement
Do not support runtime replacement of the effects graph.

### REQ-SCOPE-010 — No streaming engine
Do not implement continuous streaming audio.

### REQ-SCOPE-011 — No live synthesizer
Do not implement a real-time synthesizer or low-latency live performance engine.

### REQ-SCOPE-012 — No scheduler
Do not build a continuous event scheduler in this experiment.

### REQ-SCOPE-013 — No multithreaded mutation
Do not expand scope into concurrent/multithreaded runtime mutation.

---

## 7. Preferred Architecture

### REQ-ARCH-001 — Binding above production runtime
Prefer this conceptual architecture:

```text
SoundScript template
        ↓
extract experimental runtime parameter declarations/references
        ↓
stable structural representation
        ↓
runtime parameter table
        ↓
bind current values
        ↓
existing SoundScript rendering path
        ↓
WAV / scene / JSON / SVG as applicable
```

### REQ-ARCH-002 — Existing renderers remain authoritative
RuntimeLab shall reuse existing production rendering behavior wherever possible.

### REQ-ARCH-003 — Parameter state separate from structure
Runtime parameter state shall be represented separately from stable program structure.

### REQ-ARCH-004 — No hidden source injection
Runtime values shall not be treated as arbitrary SoundScript source fragments.

### REQ-ARCH-005 — Explicit implementation classification
The experiment shall identify which technique it actually uses:

1. true runtime binding without reparsing;
2. template/source substitution followed by normal recompilation;
3. AST cloning/resolution;
4. direct mutable AST state;
5. another clearly documented mechanism.

Do not describe source regeneration/recompilation as true runtime binding.

### REQ-ARCH-006 — Reparse visibility
The final report shall explicitly state whether parameter updates cause the SoundScript source to be reparsed.

---

## 8. Input

### REQ-INPUT-001 — Structural source
The primary input shall remain SoundScript-like source defining media structure.

### REQ-INPUT-002 — Runtime state
Runtime parameter values shall be provided separately from the structural source after initial setup.

### REQ-INPUT-003 — Host-controlled state
Parameter updates shall conceptually be usable from application state such as:

- game state
- monitoring data
- sensor values
- UI controls
- simulation state
- API responses

No integration with those external systems is required for the POC.

---

## 9. Output

### REQ-OUTPUT-001 — Reuse current outputs
The experiment should reuse existing SoundScript outputs rather than inventing a new output model.

### REQ-OUTPUT-002 — Visual output
The POC shall demonstrate a runtime parameter affecting a typed visual scene and/or JSON/SVG representation.

### REQ-OUTPUT-003 — Audio output
If a safe audio parameter is supported, demonstrate that changing that parameter changes generated audio output.

### REQ-OUTPUT-004 — No automatic playback requirement
The POC does not need to implement automatic host audio playback.

---

## 10. Adaptive Media Demonstration

### REQ-DEMO-001 — Initial state
Create one small adaptive-media example with initial values similar to:

```text
intensity = 0.25
xpos = 200
```

### REQ-DEMO-002 — Updated state
Update the same runtime instance/state to values similar to:

```text
intensity = 0.90
xpos = 900
```

### REQ-DEMO-003 — Structure stability
Demonstrate whether program structure remains unchanged between the two runtime states.

### REQ-DEMO-004 — Visual difference
Demonstrate that approved visual output changes according to the updated values.

### REQ-DEMO-005 — Audio difference
If audio parameter binding is included, demonstrate that the intended audio output changes while unrelated output remains stable.

---

## 11. Determinism

### REQ-DET-001 — Same input, same output
Verify:

```text
same template
+ same runtime parameter values
= identical resulting output
```

### REQ-DET-002 — Hash validation
Where byte determinism applies, compare hashes.

### REQ-DET-003 — Repeat render
Repeated rendering with identical state shall produce identical results where existing SoundScript guarantees determinism.

### REQ-DET-004 — Parameter isolation
Changing one runtime parameter shall not unexpectedly change unrelated output.

---

## 12. Validation and Error Handling

### REQ-VAL-001 — Unknown parameter
Reject updates to unknown parameters.

### REQ-VAL-002 — Duplicate declaration
Reject duplicate runtime parameter declarations.

### REQ-VAL-003 — Wrong type
Reject values incompatible with the declared parameter type.

### REQ-VAL-004 — Range validation
Reject values outside valid ranges for the target property.

### REQ-VAL-005 — Missing parameter behavior
Define predictable behavior for missing values.

### REQ-VAL-006 — Injection resistance
Runtime values shall not permit injection of arbitrary SoundScript syntax.

### REQ-VAL-007 — Strings
If strings are experimented with, they must remain data and must not become executable/source syntax.

---

## 13. Production Regression Protection

### REQ-REG-001 — Baseline test run
Run existing production tests before implementing the experiment.

Record exact test counts and results.

### REQ-REG-002 — Final production test run
Run existing production tests after the experiment.

Record exact test counts and results.

### REQ-REG-003 — No regression
The experiment shall not cause production test regressions.

### REQ-REG-004 — No golden rewrite
Do not modify existing production expected outputs solely to accommodate RuntimeLab.

---

## 14. Measurements

### REQ-MEASURE-001 — Initial compile/setup
Measure initial compile/setup time.

### REQ-MEASURE-002 — Parameter update
Measure runtime parameter update/bind time.

### REQ-MEASURE-003 — Subsequent render
Measure subsequent render time.

### REQ-MEASURE-004 — Reparse measurement
Record whether reparsing happens after `Set`.

### REQ-MEASURE-005 — Allocation observation
Record allocations if reasonably easy to measure.

Do not optimize prematurely.

### REQ-MEASURE-006 — Production impact map
List every production component that would require changes if the feature graduated.

---

## 15. Stop Conditions

Stop expanding the POC and document the finding if any of the following becomes necessary.

### REQ-STOP-001 — Broad renderer modification
Stop if runtime binding requires broad changes to production renderers.

### REQ-STOP-002 — `let` semantic change
Stop if the feature requires changing existing `let` semantics.

### REQ-STOP-003 — Existing output changes
Stop if unchanged existing programs would produce different output.

### REQ-STOP-004 — Parser replacement
Stop if the experiment requires replacing or substantially redesigning the current parser.

### REQ-STOP-005 — Pervasive mutable AST
Stop if the approach requires pervasive mutable AST state.

### REQ-STOP-006 — Second timing model
Stop if the feature requires a second independent timing model.

### REQ-STOP-007 — Second rendering engine
Stop if the feature requires a parallel rendering engine.

### REQ-STOP-008 — Continuous scheduling
Stop if success depends on building continuous audio scheduling infrastructure.

### REQ-STOP-009 — Breaking API
Stop if existing public APIs must be broken.

---

## 16. Suggested RuntimeLab Structure

### REQ-STRUCT-001 — Project structure
A reasonable experimental layout is:

```text
experiments/
  SoundScript.Labs/
  SoundScript.RuntimeLab/
    SoundScript.RuntimeLab.csproj
    RuntimeProgram.cs
    RuntimeParameters.cs
    Program.cs
    README.md
    Tests/
```

Exact filenames may differ if the implementation identifies a cleaner structure.

---

## 17. RuntimeLab README

### REQ-DOC-001 — Hypothesis
README shall include the experiment hypothesis.

### REQ-DOC-002 — Architecture
README shall describe the architecture actually tested.

### REQ-DOC-003 — Input
README shall document structural input.

### REQ-DOC-004 — Runtime state
README shall document runtime parameter state.

### REQ-DOC-005 — Output
README shall document produced outputs.

### REQ-DOC-006 — Determinism
README shall document determinism results.

### REQ-DOC-007 — Performance
README shall document performance observations.

### REQ-DOC-008 — Production changes
README shall list production changes that would be required for graduation.

### REQ-DOC-009 — Risks
README shall document risks and limitations discovered.

### REQ-DOC-010 — Recommendation
README shall finish with exactly one of:

#### GO
Runtime parameter binding can be added as a thin backward-compatible layer without materially changing existing algorithms.

#### GO WITH CONDITIONS
The idea is sound but requires clearly identified contained production changes.

#### NO-GO
Supporting runtime binding would require invasive AST/parser/renderer/timing changes disproportionate to the benefit.

---

## 18. Final Codex Report

### REQ-REPORT-001 — Files
Report all files created and changed.

### REQ-REPORT-002 — Architecture selected
Report the architecture actually selected.

### REQ-REPORT-003 — Reparse status
Explicitly state whether source is reparsed after parameter updates.

### REQ-REPORT-004 — Tests
Report exact test commands and results.

### REQ-REPORT-005 — Before/after example
Show example input and before/after runtime outputs.

### REQ-REPORT-006 — Production files
State which production files were touched.

Preferred result:

```text
none
```

### REQ-REPORT-007 — Risks
Report risks discovered.

### REQ-REPORT-008 — Recommendation
State:

- GO
- GO WITH CONDITIONS
- NO-GO

### REQ-REPORT-009 — Future version suitability
State whether the concept deserves consideration for a future SoundScript version.

---

## 19. Acceptance Criteria

The RuntimeLab experiment is complete when:

### REQ-ACCEPT-001
A parameterized experimental program can be created and initialized.

### REQ-ACCEPT-002
At least two approved runtime values can be changed after initial setup.

### REQ-ACCEPT-003
Updated runtime values cause the expected output difference.

### REQ-ACCEPT-004
Repeated identical runtime states are deterministic where applicable.

### REQ-ACCEPT-005
Invalid updates are rejected predictably.

### REQ-ACCEPT-006
Existing production tests remain green.

### REQ-ACCEPT-007
The implementation honestly identifies whether it performs real runtime binding or recompilation.

### REQ-ACCEPT-008
No existing SoundScript public behavior is intentionally changed.

### REQ-ACCEPT-009
A GO / GO WITH CONDITIONS / NO-GO recommendation is documented.

---

## 20. Non-Goals

The following are explicitly outside this experiment:

### REQ-NONGOAL-001
No production V16 implementation.

### REQ-NONGOAL-002
No true live music engine.

### REQ-NONGOAL-003
No uninterrupted real-time audio stream.

### REQ-NONGOAL-004
No DAW-style playback engine.

### REQ-NONGOAL-005
No dynamic arbitrary SoundScript code injection.

### REQ-NONGOAL-006
No replacement of current compile/render architecture.

### REQ-NONGOAL-007
No valuation or marketing claims based only on the existence of the POC.

---

## Decision Principle

The experiment should answer one question:

> **Can SoundScript gain runtime-adaptive values as a thin, deterministic, backward-compatible layer while leaving its trusted compilation, timing, and rendering algorithms essentially intact?**

If yes, preserve the evidence for possible graduation.

If no, keep SoundScript simple and retain the current architecture.
