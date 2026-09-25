# V16 candidate feasibility review

The Lab checkpoint is commit `622f27c`: 124 requirements mapped, 51 Lab tests, 1,232 production tests and 26 browser checks passed. The Lab requirements and production source remained unchanged at that checkpoint. The user's separate V16 continuation authorizes subsequent production work conditionally; it does not retroactively turn the Lab into a production implementation.

## Contained integration design

Use three components: an opt-in parser binding schema, an owned state/snapshot binder, and additive `SoundScriptEngine` entry points. Do not promote the Lab token-context scanner: the existing parser already knows exactly when it is reading a gain or visual constant. An opt-in `ParseRuntime` mode can record these references while producing ordinary default-valued AST nodes. Existing `Parse`, `Compile`, and `CompileFile` continue their current behavior.

| Area | Decision and implication |
|---|---|
| Grammar | `param name = decimal` is contextual at the top level of runtime compilation. No lexer keyword is added. Existing names such as `track param` and `let param` remain legal. No existing syntax needs migration. |
| Constants | `let` and `marker` remain parser-resolved compile-time values. Reject parameter/constant collisions and runtime references in expressions, timing or structural positions. |
| AST | Keep the existing AST types and semantics. Record typed binding targets in a separate parse result; the public runtime API owns the parsed AST and never exposes it. Replace approved gain nodes in privately copied containers. |
| Values | Decimal metadata with defaults and target-range intersections. Approve direct track gain and constant visual x/y/opacity/rotation/width/height only. Gain requires `perform expressive`. Coordinate bounds are explicit host policy. |
| Audio | Existing WAV synthesis, effects, normalization and temporal padding remain authoritative. Rendering still lowers the bound AST to notes. Gain may change the mixed amplitude, including existing master processing. |
| MIDI | Existing interpretation writes derived StartTime on note/rest notation. Clone these objects and ancestor containers before MIDI interpretation; never let MIDI mutate a shared template or snapshot. No MIDI algorithm change is needed. |
| Visual timing | Compile the existing timeline once. Apply constant property overrides to its sampled state before the existing scene builder. Keep the Lab's unique-name restriction for bound visuals to avoid ambiguous targets. |
| Timing parameters | Exclude tempo, durations and placement from runtime bindings. They would invalidate scheduling and playback assumptions and are not needed for this feature. |
| Determinism | Test literal equivalence, repeats, state cycles and independent compilation for WAV/MIDI/JSON/SVG. Stable external assets and renderer version remain prerequisites. |
| Cache | Cache only the current bound snapshot by state revision. Any changed value invalidates it; rejected or no-op updates do not. Rendering remains explicit. |
| Concurrency | Serialize state operations with a private lock. Offer atomic multi-value validation/update. Snapshots contain private state and may be rendered independently; MIDI uses private notation copies. |
| Locale | New runtime compilation uses invariant numeric parsing and restores caller culture in finally. The ordinary parser's existing culture behavior remains unchanged. This is an explicit contract of the new entry point, not a silent global fix. |
| API | Add CompileRuntime / CompileRuntimeFile, parameter metadata, Get/Set/SetMany/Reset/Bind and typed render snapshots. Keep all V15 methods intact. File compilation retains sample-directory/root policy; imports are excluded from the initial runtime API. |
| CLI | Use existing render/inspect option conventions for parameter discovery and repeated --param assignments. Preserve legacy invocations, help and exit-code handling. |
| Playground | Add an adaptive example and controls calling the actual production runtime API. Compile explicitly; changing values renders a new snapshot. Do not imply streaming or live synthesis. |
| Package | New API stays in the existing distribution. Audit current corpus contentFiles separately before changing it, using a fresh external consumer and captured warnings. |
| Docs/examples | Add runtime documentation, candidate release/migration notes, C# and web/application examples. Validate every existing example without rewriting golden output. Keep public release-state metadata truthful. |

## Scope and conditions

No runtime imports, new tracks/notes, graph replacement, arbitrary expressions, independent scheduler, second clock or second renderer. A source file can still use the ordinary static API for its established import behavior. Runtime source is trusted author input; typed parameter updates are not executable source.

Production integration is technically clean and justified within these constraints. Proceed as a candidate on this feature branch, subject to complete regression, browser, example and external-package validation. This is permission to implement the candidate under the user's continuation request, not a release-readiness claim. No publication, tag, merge or external deployment is part of this work.
