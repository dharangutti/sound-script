# V16 candidate: runtime parameters

This branch prepares an unpublished candidate. Public release state remains 15.0.0; no tag, package release, website deployment or merge is authorized by this work.

## Changes

- Opt-in `SoundScriptEngine.CompileRuntime` and `CompileRuntimeFile` compile structure once. Typed decimal state, atomic batches and stable render snapshots drive the existing audio and visual engines.
- Contextual `param name = decimal` declarations bind direct expressive track gain and constant visual x/y/opacity/rotation/width/height. Metadata reports each parameter's default and intersected range.
- CLI `inspect --params`, `run --runtime` and `wave --runtime` accept repeated `--param name=value` assignments. `--param` implies runtime compilation.
- The Playground discovers parameters from the compiled program, applies values through the production API and renders a new WAV and scene.
- Monitoring console and web examples demonstrate normal, warning and critical application states.
- The library package owns corpus resources inside assemblies instead of injecting `contentFiles` into consumer projects. Source-checkout CLI data and browser per-word audio delivery remain supported.

## Compatibility and migration

Existing static APIs, `let`, `marker`, source files and renderer algorithms retain their meanings. No static source migration is required. `param` is contextual only in runtime compilation; ordinary identifiers such as `track param` and `let param` remain legal. Runtime compilation deliberately uses invariant decimal parsing and restores the caller's culture; the existing static parser's numeric culture behavior is unchanged.

Use explicit runtime entry points/CLI options for parameterized source. All parameters require literal defaults, so a host need not supply values before rendering. Parameter names are case-sensitive. The initial version excludes imports, runtime arithmetic, structural changes, timing changes, nested gain bindings and arbitrary source execution. Existing static import workflows remain available separately.

Rendering remains offline work. `Set` and `Bind` do not reparse or reconstruct the timeline, but WAV/MIDI rendering still lowers and renders the captured state. A stable snapshot is the unit for matching audio and visuals; sequential convenience calls can observe different revisions if another thread updates between them.

Installed consumers need no Git submodule or repository path. Corpus playback reads assembly resources; explicit editable-path operations may materialize a corpus under the user's local application data. Packaging from a source checkout still requires the pinned Wordbank license/submodule.

See [runtime contracts and examples](runtime-parameters.md). The final acceptance report records measured validation and release conditions; this document does not claim publication.
