# VideoLab semantic specification

## Timeline, frames and placement

All spans are integer frames with `[at, at + frames)` lifetimes. Frames start at zero. The timeline has an explicit total length; uncovered video is black and uncovered audio is silence. Output rates are 24, 25, 30, 50 or 60, so every video frame spans exactly 48,000/FPS audio samples.

`frame` is element-local. `progress = frame / (frames - 1)`, except a one-frame element where it is 0. Expressions and keyframes do not use elapsed wall time or evaluation history. `SourceFrame = trim + local frame` identifies the normalized source timeline, not a physical compressed packet or raw decoded frame.

Videos are ordered. Missing `at` sequences at the previous end minus incoming `fade`. A crossfade must overlap exactly the previous clip's end and cannot overlap another transition. Incoming opacity is multiplied by `min(1, localFrame/fade)`; outgoing video remains underneath until its lifetime ends. Cuts and explicitly placed black gaps are valid. Z-order is video declaration order, then shapes, text, and callouts in declaration order within each array. Conditional exclusion does not renumber layers. Scripts without captions retain their original order.

## Text and callouts (Phase 1)

Optional `texts` entries require literal `text`, `at`, `frames`, even `width`/`height`
plane dimensions. `fontSize` defaults to 24; `color` to FFFFFF; `align` to left.
Optional `callouts` require `label`, lifetime, plane dimensions and `targetX/targetY`;
fontSize defaults to 20, color FFFFFF, background 182438, align left. Both support
the existing `transform`, `when` and `effects`. Thus x/y, scale, size, rotation,
opacity, crop and anchor can be animated or bound without introducing new evaluation
rules. Label content, fontSize, color and local pointer target are compile-time data.
Runtime strings, font selection, wrapping and rich text are not Phase 1 features.

Text is 1–160 printable ASCII characters (U+0020–U+007E), nonblank and single-line.
Unsupported glyphs/control characters fail instead of relying on fallback. The
unmodified DejaVu Sans 2.37 font is embedded, licensed and fingerprinted under
fonts/. No system font discovery. Font size is an integer 8–96. Planes have even
dimensions 24–4096; height must accommodate fontSize + 16. Captions count toward all
existing element, element-frame, canvas and transform-work limits.

Text is drawn onto a transparent plane with top offset 8. Horizontal x is 8 (left),
(planeWidth - measuredTextWidth)/2 (center), or planeWidth - measuredTextWidth - 8
(right), using the pinned font/FFmpeg glyph metrics. Overflow clips at plane edges;
there is no implicit resize/wrap. Hinting, bitmap glyphs and text shaping are disabled.
Existing crop/scale/rotation/opacity/placement then apply to the complete plane.

A callout uses this same text plane, a full-width opaque label rectangle of height
fontSize + 16, and a 3-pixel-wide pointer segment from (width/2, fontSize+16) to the
local target. Pixel centers at distance <=1.5 from the clamped segment are included.
The target must be inside the plane and at y >= fontSize + 20. It is not a tracking
coordinate: moving or transforming the plane moves label and pointer together.
Pointer color equals text color; background has its own six-digit RGB value.

`SceneAt` exposes Kind=text/callout, literal Source and immutable Caption metadata
(text, font identity/hash, fontSize, color, alignment and optional background/target).
The existing layer Transform defines the plane in canvas space. Excluded captions
remain inspectable and validate normally. Legacy Clips/Shapes projections exclude
caption layers; consumers should use Layers for the complete composition.

Plans declare GeneratedText resources with safe generated filenames. Literal text
never enters filter syntax: Render writes UTF-8 files, supplies the embedded font,
and uses expansion=none in a dedicated temporary working directory. Static caption
planes are constructed once and split into the reference backend's frame transforms.
Normal failure/cancellation removes those resources; output replacement remains
atomic. Exact pixels/bytes remain conditional on the same FFmpeg/FreeType and machine
environment, not a cross-version typography guarantee.

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


## Typed audience bindings and groups (Phase 2)

Existing parameters remain decimal declarations. Optional typedParameters contains
{type:"enum", default:"qa", values:["shopfloor","qa","engineering"]} or
{type:"boolean", default:true}. Numeric and typed declarations share one 64-name
namespace. Enums have 1–32 unique, case-sensitive ASCII identifier values of at
most 64 characters. Arbitrary strings and implicit coercion are unsupported.

Runtime.SetMany remains the decimal API. Runtime.SetBindings accepts JSON scalars
for either kind in one atomic transaction. It validates every frame before
publishing; rejected changes alter neither kind. Snapshot.Values is the existing
decimal projection, TypedValues stores cloned enum/boolean JSON, and Bindings is
the combined view. Old snapshots retain their values and compiled structure.

Conditions accept audience == 'qa', showSafety == true, direct boolean names,
&&, || and parentheses. Equality requires matching types and enum domains;
literals must belong to the declared domain. Arithmetic and transforms remain
numeric. Logical operators short-circuit, with && above || and comparisons above
both. Existing numeric parameters named true/false retain precedence over boolean
literals; use a direct boolean name in such compositions. No reflection or host
script evaluation is available. Expression size/depth bounds still apply.

Optional groups maps names to {when?:expression}; each element/sequence can name
one group. At most 32 groups, no nesting, no implicit ordering or timing changes.
The group's predicate ANDs with the member's predicate; frame/progress remain
local to each member. SceneAt records group identity even on excluded layers and
audio. Group membership does not change stable z-order. Excluded members still
validate resource/transform bounds. A batch mixes numeric, enum and boolean JSON
bindings over one compiled composition. CLI enum assignments use audience=qa;
boolean assignments use showSafety=false.
