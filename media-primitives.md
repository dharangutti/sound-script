# Media Primitives — Shapes, Colors, and Text

SoundScript's explicit presentation primitives extend V11's timed visuals with
reusable 2D shapes and appearance settings. They work in the Audio/Visual
Playground, browser WebM export, and CLI `video` export.

## First shape

```ss
tempo 120
track music { instrument piano C4 h E4 h G4 h C5 h }
sync audio
visual "progress" for 4s {
    shape rectangle
    fill "#6ee7b7"
    animate x 0.5 -> 0.5 over 4s
    animate y 0.5 -> 0.5 over 4s
    animate width 20 -> 400 over 4s
    animate height 24 -> 24 over 4s
}
```

`shape` explicitly selects the presentation. Without it, `intro`, `circle`,
`product`, and `sparkle` keep their existing styled treatments; other names
remain generic cards. A visual named `"rectangle"` is still a generic card
unless it contains `shape rectangle`. Shape and setting names are case-insensitive.

## Supported shapes

| `shape` value | Presentation | Useful for |
|---|---|---|
| `rectangle` | Flat filled rectangle | Bars, panels, banners |
| `roundedRectangle` | Rectangle with a 14-pixel corner radius, reduced for small bounds | Cards, labels, panels |
| `ellipse` | Ellipse using independent width and height | Badges, nodes, motion studies |
| `circle` | Plain circle; uses the smaller dimension if bounds differ | Indicators, nodes, dots |
| `triangle` | Upward-pointing triangle before rotation | Direction cues, warnings |
| `line` | Horizontal centre line before rotation, round endpoints | Dividers, diagram segments |
| `arrow` | Right-pointing filled arrow before rotation | Process flow, movement explanations |
| `ring` | Circular outline with an empty centre | Status indicators, targets, highlights |
| `text` | Centred, single-line portable bitmap lettering | Titles, captions, instructions |

Shapes default to the centre of the logical 1280×720 viewport. Most start at
240×120; text starts at 480×80; circle/ring start with radius 72. New shapes
have no automatic labels: add a separate `shape text` visual for a label.
This lets labels and shapes have independent placement, timing, and color.

## Static appearance settings

These declarations go inside the visual block, in any order alongside animations.
Each setting may appear once. Appearance settings require an explicit `shape`.

| Setting | Accepted value | Default / scope |
|---|---|---|
| `shape rectangle` | A shape from the table above | Required to opt in |
| `fill "#6ee7b7"` | Quoted six-digit RGB hex or `"none"` | Mint fill; line/ring use stroke only |
| `stroke "#ffffff"` | Quoted six-digit RGB hex or `"none"` | None for filled shapes; mint for line/ring |
| `strokeWidth 4` | Number from 0 to 128 logical pixels | 4; 0 hides the outline |
| `text "SYSTEM OK"` | Up to 120 supported characters | Required for `shape text`; not allowed on other shapes |
| `fontSize 42` | Number from 8 to 256 logical pixels | 42; text only; shrinks to fit its bounds |

Colors and text are static in this extension. Named CSS colors, alpha hex,
gradients, font selection, text outlines, and color interpolation are not supported.
Use `animate opacity` for fades. Use consecutive text visuals to change captions.
Line/ring ignore interior fill by design and reject non-`none` fill declarations.
Text is colored by `fill` and rejects a non-`none` stroke.

## Position, dimensions, and animation

The existing linear `animate property from -> to over duration` syntax is reused.
For a constant numeric value use identical endpoints. Curves start at the visual's
local time zero, hold their target after finishing, and must fit within the interval.

| Property | Explicit primitive behavior |
|---|---|
| `x`, `y` | Centre position. 0–1 is normalized; other values are logical pixels. Default 0.5. |
| `width`, `height` | Bounds, clamped to 8–1280 and 8–720 respectively. |
| `size` | Fallback for each dimension without a width/height curve. |
| `radius` | Circle/ring only; clamped to 4–360 before bounds are applied. Explicit width/height or size take precedence. |
| `rotation` | Degrees clockwise around the centre, clamped to −360–360. Applied to every explicit shape, including text. |
| `opacity` | Clamped to 0–1. Default 1. |

Circle and ring use the smaller final dimension for both axes; use `ellipse`
when unequal dimensions are intended. A line uses width for its length,
`strokeWidth` for thickness, and rotation for direction. Its height does not
change thickness. Outlines extend beyond the shape boundary and are clipped
at the viewport edge. Larger stroke widths can fill the centre of a small ring.
Settings such as `fontSize` and `strokeWidth` are static, not animation properties.
Other numeric curves can still be inspected as state, but do not affect rendering.

## Portable text and rendering

The new text primitive uses the same built-in 5×7 bitmap glyph geometry in both
renderers, without downloading fonts. Latin letters are displayed in uppercase;
digits, spaces, and `. , : ; ! ? + - / % ( ) [ ] = < > _` are supported.
Unsupported characters are rejected, rather than silently replaced. Text is
single-line and scales down uniformly to fit its width and height.

```ss
visual "caption" for 3s {
    shape text
    text "SYSTEM OK"
    fontSize 48
    fill "#f8fafc"
    animate opacity 0 -> 1 over 500ms
}
```

The Media layer generates shared paths with resolved dimensions and rotation.
The browser canvas and CLI rasterizer draw those paths. Geometry, timing, and
PCM audio are shared; antialiasing and encoded WebM bytes can differ. Legacy
named treatments retain their existing renderer-specific typography and styling.

## Application demos

All four new examples include a piano score and are available in the Playground's
**Audio/Visual** selector and **Example Library**, alongside the four original demos.

| Example | Demonstrates |
|---|---|
| [Progress indicator](../examples/visual-progress.ssv) | A left-anchored growing bar, outlined panel, text; 6 seconds |
| [Process diagram](../examples/visual-process.ssv) | Two labelled panels, animated arrow, line divider; 8 seconds |
| [Instruction sequence](../examples/visual-instructions.ssv) | Rotating triangle and three timed captions; 6 seconds |
| [Status display](../examples/visual-status.ssv) | Ring, circle, ellipse, and rotating text; 8 seconds |

```bash
dotnet run --project src/SoundScript.Cli -- visual examples/visual-process.ssv --at 3
dotnet run --project src/SoundScript.Cli -- video examples/visual-process.ssv --output process.webm --fps 30
```

CLI encoding requires FFmpeg. Browser **Export Clip** supports 24, 30, and 60 FPS
with canvas capture and MediaRecorder. FPS remains an export setting.

These are authored timelines, not live telemetry or UI widgets. Data binding,
interaction, image/video imports, arbitrary paths, and rich multilingual text
are outside this extension. See [temporal semantics](visual-temporal.md) and
[application use cases](use-cases.md).
