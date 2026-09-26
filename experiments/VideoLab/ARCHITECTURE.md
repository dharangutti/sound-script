# VideoLab architecture

## Isolation and pipeline

This experiment owns semantic evaluation and is independent of production SoundScript. The local build props stop parent version inheritance; no product project, parser, compiler, runtime, solution or NuGet package is referenced or modified. No optimized backend or production integration is part of v0.2.

Compilation proceeds through bounded strict JSON parsing (including duplicate/unknown rejection), script model, ordered data expansion, timeline validation, effect-template compilation, typed expression/animation compilation, immutable visual/audio programs and `Composition`. Defaults are evaluated at every programmable frame before compilation succeeds. All structural collections are immutable.

## State, expressions and animations

`Composition` is shared structure. A `Runtime` owns a value map under a lock. `SetMany` validates names/bounds and all resulting programmable states before atomically publishing a new immutable map. `Snapshot` captures the current map; existing snapshots never observe later changes. `SceneAt` has no playback cursor and supports out-of-order calls.

The expression AST contains typed literal, variable, unary, binary and closed-function nodes. Decimal arithmetic and explicit comparison booleans prevent backend-specific expression semantics. There is no reflection, host invocation or dynamic code. Depth/node limits apply after effect substitution as well as parsing. Scalar values are expressions or keyframe animations; easing is evaluated entirely in C#. Endpoint expressions use endpoint-local time. See [SEMANTICS.md](SEMANTICS.md) for exact formulas.

Effects are immutable templates with numeric argument defaults and scalar property maps. Invocation substitutes typed ASTs, validates arguments and key lifetimes, then merges properties deterministically. Effects cannot invoke effects. Structured datasets expand only into bounded video sequences. Parameterized batch generation creates independent snapshots sharing one composition.

## Scene authority and rendering

`SceneAt` exposes active layer identities, normalized source frames, stable z-order, inclusion, transforms/crop and evaluated numeric outputs, and active audio identities/positions/gains/inclusion. Excluded layers remain inspectable. Legacy scene views are projections. Coordinate meaning and transform order are specified independently of FFmpeg.

`ProgrammableBackend` remains a bounded, correctness-first **reference backend**. It evaluates all scenes, selects normalized source frames, applies constant numeric crop/scale/rotation/opacity/placement, and concatenates frame canvases with explicit timestamps. Audio gains/conditions become exact frame-sized sample blocks. Authored expression strings never enter the filter graph. Original legacy scripts retain their established efficient lowering route. A future optimized backend must compare decoded semantic results against this reference; it is deliberately not implemented here.

`Ffmpeg.Plan` performs no media I/O or subprocess work and returns inputs, complete graph, argument vector and expected settings. `AssetProbe` is render-time ingestion policy: metadata, stream presence/duration, SDR/square-pixel/progressive restrictions and consumed-prefix CFR validation. Multiple references probe once per path/stream through the furthest requested end. Fractional CFR inputs normalize to integer output ticks; noautorotate explicitly defines orientation policy. See the semantic spec for detection limits.

Render launches processes via `ProcessStartInfo.ArgumentList`, `UseShellExecute=false`, with independent stdout/stderr draining and `-nostdin`. Error output is capped at 1600 characters; concise coded probe diagnostics preserve an underlying cause when useful. A temporary filter file avoids command-line length limits. Successful output is atomically moved into place; `finally` cleans temporary graph/output on ordinary failure. No timeout, cancellation, crash-recovery or hostile-media sandbox is claimed.

The optional `realtest` harness never participates in normal selftest. It validates a strict local manifest, protects all input destinations, records source fingerprints, renders both containers twice, fully decodes/counts output and checks source bytes remain unchanged. Its local report can contain private paths/hashes and belongs under ignored `real-media/` only. Missing manifest is a successful skip. Synthetic counterparts remain reproducible without copyrighted media.

## Determinism

The boundary includes script, parameter bindings, asset bytes, VideoLab code, FFmpeg/encoder versions and machine environment. Array order and ordinal property/effect ordering define traversal; numbers use invariant culture. Fixed output formats, timestamps, single-threaded software processing, bitexact flags and metadata stripping stabilize exports. Trigonometric raster geometry uses floating point within this environment boundary. Temporary random filenames do not enter media bytes. No cross-machine or cross-version hash equality is promised. Snapshot immutability does not freeze external file content.

## Central resource limits

| Resource | Limit |
| --- | --- |
| Script size / JSON depth | 1,000,000 characters / 48 |
| Canvas | Positive even dimensions, each ≤4096 |
| General timeline / output FPS | 216,000 frames / 24,25,30,50,60 |
| Parameters / combined visual+audio elements / distinct asset references | 64 / 64 / 64 |
| Effect definitions / arguments / invocations per layer | 32 / 16 / 8 |
| Nested effect depth | 0 (forbidden) |
| Expression characters / expanded AST nodes / depth | 1024 / 128 / 32 |
| Keyframes per property | 64 |
| Datasets / total records / sequence blocks / generated elements | 32 / 128 / 32 / 128, also subject to total element cap |
| Programmable frames / summed element-frames | 600 / 4096 |
| Programmable canvas pixel work | width × height × frames ≤100,000,000 |
| Programmable transform pixel work | Sum of rotated bounding-plane pixels across all active frames ≤100,000,000, including excluded layers |
| Evaluated dimensions | Positive, ≤4096 before and after positive scale |
| Absolute x/y / rotation degrees | 16384 / 360000 |
| Opacity / anchor | [0,1] |
| Crop | Within normalized source plane, positive and at least one source pixel |
| Audio gain | [0,4] |
| Batch file / records | 100,000 characters / 1–32 |
| Real-media manifest / cases | 100,000 characters / 1–32 |
| Validated input timing | Positive nominal/average CFR ≤240 fps, usable time base ≤one frame |
| Validated source audio | 1–2 channels, 8–192 kHz |

These limits bound composition work, not arbitrary decoder resource use. Local media is trusted; corrupt/unusual input may still fail in FFmpeg. Private media and reports are never automatically committed.
