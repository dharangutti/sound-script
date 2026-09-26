# VideoLab semantic specification

## Timeline, frames and placement

All spans are integer frames with `[at, at + frames)` lifetimes. Frames start at zero. The timeline has an explicit total length; uncovered video is black and uncovered audio is silence. Output rates are 24, 25, 30, 50 or 60, so every video frame spans exactly 48,000/FPS audio samples.

`frame` is element-local. `progress = frame / (frames - 1)`, except a one-frame element where it is 0. Expressions and keyframes do not use elapsed wall time or evaluation history. `SourceFrame = trim + local frame` identifies the normalized source timeline, not a physical compressed packet or raw decoded frame.

Videos are ordered. Missing `at` sequences at the previous end minus incoming `fade`. A crossfade must overlap exactly the previous clip's end and cannot overlap another transition. Incoming opacity is multiplied by `min(1, localFrame/fade)`; outgoing video remains underneath until its lifetime ends. Cuts and explicitly placed black gaps are valid. Z-order is video declaration order, then shape declaration order. Conditional exclusion does not renumber layers.

## Source normalization

Each video stream's first presentation timestamp is rebased to zero. Presentation timestamps map to integer output-rate ticks by nearest rounding, halfway away from zero. At a normalized tick, the last available source frame mapped at or before that tick is selected. Multiple mapped frames can collapse to one tick; missing ticks repeat a frame. End-of-stream timing uses the same rounding. For CFR source rate R and output rate F, raw frame i maps to `roundAway(i * F/R)` before selection. Encoded timestamp quantization is relevant for coarse container time bases. Source-frame markers test the policy at 24/25/30/60 and 24000/1001, 30000/1001, 60000/1001.

Output rates are never fractional. Fractional **CFR input** is normalized; raw frames are not preserved one-for-one. `SceneAt` knows normalized positions without reading assets, so it cannot name the original physical decoded frame without source metadata. No API field claims otherwise.

The encoded pixel plane is used: auto-rotation is explicitly disabled. Display matrices/rotation tags are reported during probing but ignored. An upright phone display is therefore not promised for metadata-rotated storage. Non-square sample-aspect-ratio inputs are rejected. The source is aspect-fit and centered in the canvas, with black letterboxing; it is neither cropped to fill nor stretched. A 1920×1080 source in a portrait canvas occupies a centered horizontal band. This normalized canvas-sized image is the clip's layer plane before programmable operations.

VFR is outside the validated set. Preflight rejects mismatched average/nominal frame rate, invalid time bases and nonuniform packet presentation spacing in the consumed prefix (with one second of lookahead for packet reordering). Packet PTS are sorted for B-frame decode order. A time base equal to one whole frame is valid. Quantization tolerance is two microseconds for integer ticks/frame, otherwise 1.1 ticks. This is conservative prefix validation, **not proof that an unconsumed suffix or unusual edit list is CFR**. Missing/ambiguous metadata is rejected; no attempt is made to repair hostile or malformed media.

## Coordinate spaces and transforms

| Space | Meaning |
| --- | --- |
| Canvas | Pixels, x grows right, y grows down; origin at upper-left |
| Clip source plane | Canvas-sized image after FPS normalization, aspect fit and letterboxing |
| Shape source plane | Declared base width/height solid rectangle |
| Crop | Normalized fractions of that source plane, including a clip's letterboxing |
| Anchor | Normalized fractions of the cropped/resized plane; (0,0) upper-left, (0.5,0.5) center |

Operations are ordered: source normalization → crop → resize to width/height and multiply scaleX/Y → clockwise rotation → opacity → anchor-based placement → compositing in z-order. Position x/y gives the canvas location of the chosen anchor. Rotation is in degrees around that anchor. The rotated plane expands into a transparent bounding rectangle; placement offsets the bounding rectangle so the chosen anchor stays fixed. Off-canvas portions are clipped.

Crop coordinates satisfy x/y ≥0, width/height >0, x+width ≤1, y+height ≤1, covering at least one source-plane pixel. Crop fractions are applied before resize. Thus cropWidth=0.5 means half the normalized plane, not half the visible source content. Width/height remain independent of crop dimensions. Scales must be positive; mirrored/negative-scale transforms are unsupported. Opacity and anchors lie in [0,1].

For reference rasterization, crop x/y/width/height floor to pixels; positive scaled dimensions round away from zero to at least one pixel. Rotation bounding dimensions are the ceiling of absolute rotated extents. Anchor offset rotates with the plane; canvas placement rounds away from zero. Resize and programmable rotation use nearest-neighbor sampling. `SceneAt` exposes decimal transforms before these raster rules; `Transform.Raster()` exposes the geometry calculation. Final yuv420p encoding can introduce chroma/codec differences. Ordinary SDR is tested; no managed HDR, wide-gamut, Dolby Vision or professional color pipeline is promised.

## Expressions, animations and conditions

Numeric expressions use invariant decimal literals, declared parameter names (optional `$`), `frame`, `progress`, `fps`, `canvasWidth`, `canvasHeight`, arithmetic +/−/×/÷, unary minus and parentheses. Functions: `min(a,b)`, `max(a,b)`, `clamp(v,min,max)`, `abs(v)`. Comparisons >, >=, <, <=, == and != produce booleans; operands are numeric. No implicit numeric/boolean conversion, general boolean algebra or arbitrary function calls exist.

Keyframes contain local `frame`, numeric/expression `value`, and optional outgoing `easing`. Keys increase strictly within the lifetime. Values hold outside the first/last keys; exact keys win. Step holds its left value until the next key. Quadratic ease-in is t²; ease-out is 1−(1−t)²; ease-in-out uses 2t² before midpoint and 1−2(1−t)² after it. Endpoint expressions are evaluated at their keyframe's local time. One key is a constant curve, including one-frame animations.

An optional `when` comparison controls visual or audio inclusion. Active excluded elements still appear with `Included=false` and still validate. Numeric outputs and gain are evaluated regardless of inclusion. Unknown identifiers, divide-by-zero, overflow and invalid evaluated bounds are errors. Defaults validate every programmable frame during compilation; candidate runtime bindings do so again before publication. A failed `SetMany` changes nothing and old snapshots remain unaffected.

## Audio

Only explicit `audio[]` spans contribute sound, including audio referenced from video files. Inputs are independently rebased to zero; original container audio/video start offsets are not automatically preserved. Resampling uses FFmpeg's software resampler to 48 kHz stereo without asynchronous clock compensation. Mono is remixed to equal stereo channels; stereo is retained. Only mono/stereo sources at 8–192 kHz are in the validation boundary.

Trim/sample positions are normalized frame indices times 48,000/FPS. Gain lies in [0,4], held constant for each exact frame-sized audio block. Conditional exclusion sets block gain to zero. Tracks sum without automatic loudness normalization; a fixed 0.95 limiter with latency compensation controls overload. Silence fills uncovered timeline samples. This is frame-rate gain control, not modulation inside a sample block. Source-video audio is not automatically faded by visual crossfades: authors must specify its gains/conditions.

## Effects, data and batches

Effects compile to immutable transform templates. Local argument names cannot shadow globals/built-ins; defaults are numeric, arguments numeric literals or expressions. AST substitution does not expose raw strings to FFmpeg. Invocations apply in array order; later properties override earlier ones; explicit transforms override effects. Within a block `scale` assigns both axes before explicit `scaleX/Y`. Nested/recursive effects are forbidden. Integer keyframes must fit each invocation lifetime.

Data records are `{asset, frames, trim?}`. Sequence blocks select a named dataset, at, fade and optional transform/effects. They append generated videos after explicit declarations, preserving order. No automatic sort, file discovery or general template language occurs. Counts are bounded.

Batch records bind separate immutable snapshots over the same composition. Destinations are unique and cannot overwrite inputs; CLI hosts protect source script/batch files. Outputs replace atomically per file, but a batch has no global rollback. Snapshots freeze semantics/parameters, not external asset bytes. Fingerprinting is optional; validation records it privately.
