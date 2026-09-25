# Programmable media runtime for .NET

SoundScript defines deterministic audio, visual state and media time. Applications choose playback and rendering.
HTML, SVG, Canvas and WebM are output strategies. They are not separate SoundScript timing models.

```text
                     SoundScript
                         ↓
                 compiled media
                         ↓
          ┌──────────────┴──────────────┐
          ↓                             ↓
       Audio                        Timeline
       WAV/PCM                      StateAt(t)
          ↓                             ↓
          └────────── shared t ─────────┘
                         ↓
                     Scene model
             ┌───────────┼───────────┐
             ↓           ↓           ↓
           JSON         SVG       custom UI
                                  / WebM
```

## The public API

See the [NuGet guide](nuget.md) for package availability and installation.

<!-- GENERATED:DOTNET_REQUIREMENT_START -->
Requires .NET 10.0 (`net10.0`). Use the SDK selected by `global.json` for repository development.
<!-- GENERATED:DOTNET_REQUIREMENT_END -->

```csharp
using SoundScript;
using SoundScript.Media;

var compilation = SoundScriptEngine.Compile("""
    tempo 120
    track cue { C4 q E4 q G4 h }
    sync audio
    visual "moving" for 4s {
        shape circle
        fill "#16a34a"
        animate x 200 -> 1080 over 4s
    }
    """);
var media = compilation.CompileMedia();
byte[] wav = media.RenderAudio();
TimeSpan duration = media.Duration;
var timeline = media.Timeline;
var scene = media.SceneAt(TimeSpan.FromSeconds(2));
string json = TemporalVisualJson.Serialize(scene);
string svg = TemporalSvgRenderer.Render(scene);
byte[] midi = compilation.RenderMidi();
```

`CompileMedia()` composes `VisualInterpreter`, `VisualTimeline.StateAt`, `TemporalVisualSceneBuilder`
and `TemporalAudioRenderer` over the existing parser/AST. The AST stays private in the facade.
There is no second parser, timeline, scene graph, interpolation engine or playback clock.
The concrete compiled object is sufficient; no `IMediaRuntime` or generic `Template<T>` is introduced.

## Runtime parameters (V16 candidate)

The current feature branch adds `SoundScriptEngine.CompileRuntime` for opt-in
runtime values while preserving the static `Compile` and `CompileFile`
contracts. A runtime program parses its source and builds the visual timeline
once. Its decimal `Parameters` metadata declares each value's default and
allowed range; `Get`, `Set`, atomic `SetMany`, and `Reset` manage typed host
state without converting values to source text. `Statistics` reports the
actual initial tokenization, parse, and timeline compilation counts.

`Bind()` captures a stable `SoundScriptRuntimeSnapshot`. Later changes leave
that snapshot intact. Its `RenderAudio`, `RenderWave`, `RenderMidi`, and
`SceneAt(time)` use the existing renderers and timing model. Runtime support is
limited to direct track gain and constant visual x/y/opacity/rotation/width/
height values. Structure, notes, imports, tempo, and media timing do not change
at runtime. Unsupported references and invalid values fail before updating
state. Static imports remain available through `CompileFile`; runtime source
does not resolve an import graph.

This is a V16 candidate under validation, not a package or release validation
claim. See the [runtime parameter guide](runtime-parameters.md), the
[monitoring host sample](../samples/RuntimeParameters/README.md), and the
[Playground's adaptive panel](PLAYGROUND.md#adaptive-media-runtime-parameters-candidate).

## Time and audio

`SceneAt(t)` is a pure seekable projection: Scene = F(time). It does not depend on previous queries or FPS.
Negative time throws `ArgumentOutOfRangeException`; zero evaluates the initial scene. Intervals are
half-open `[start,end)`. At an interval's exact end it is absent; after the final visual interval the
scene is empty. The requested time remains in the returned scene and is never silently clamped.
Overlaps use timeline order (start time, then source order). Narrative waits and absolute placements
retain their existing behavior.

The media duration covers both rails: the complete mono Wave output is preserved, including its tail,
and extended with silence if the visual timeline is longer. PCM duration is sample-quantized at 44,100 Hz;
it may exceed the authored visual end by less than one sample. At media duration no visuals remain.
`Timeline.Duration` is the visual/narrative duration; `Duration` is the resulting WAV duration.
An empty/audio-only timeline has no visual elements regardless of any minimum silence produced by Wave.

Audio is rendered lazily once, on first `RenderAudio()` or `Duration` access. Assets must remain stable
until then. `RenderAudio()` returns a defensive copy of the cached WAV. This convenience trades memory
for simple repeatable application integration; large or streaming jobs should use the existing Wave
stream APIs. There are no redundant Stream/ReadOnlyMemory facade overloads. `compilation.RenderStereoWave()`
and `RenderMidi()` retain their original contracts; the media facade supplies a mono synchronized rail.
The older `TemporalAudioRenderer.RenderToWavBytes(program, duration)` still fits exactly to a video duration.

Tempo automation remains in the existing PCM adapter and `TempoAutomationMap`. Visual source times are
elapsed seconds; `Timeline.StateAtAudioBeat(beat, tempoMap)` bridges score beats to the same elapsed time.
No host should simulate a separate visual clock.

## Typed scene and schema 1.0

The renderer contract is `TemporalVisualScene` → ordered `TemporalVisualPrimitive` → `TemporalShapePath`.
JSON is an output adapter, never the internal model. `TemporalVisualJson.Serialize(scene)` emits:

```json
{"schemaVersion":"1.0","timeSeconds":2,"primitives":[]}
```

| Field | Meaning |
|---|---|
| `schemaVersion` | String `1.0`; version of this adapter's schema |
| `timeSeconds` | Requested elapsed time as a JSON number |
| `primitives` | Ordered array of evaluated primitives |
| `name`, `kind`, `label` | Strings; identity, presentation kind, display/accessibility label |
| `left`, `top`, `width`, `height` | Logical pixel bounds in the 1280 × 720 viewport |
| `opacity`, `rotationDegrees` | Evaluated decimal values |
| `paths` | Null for legacy presentation, otherwise an ordered array (possibly empty) |
| path `points` | Ordered `{ "x": number, "y": number }` absolute viewport coordinates |
| path `closed` | Boolean; close the polygon when true |
| path `fill`, `stroke`, `strokeWidth` | Paint strings and logical stroke width |

Coordinates in paths already include rotation: do not rotate those paths again. Numbers are invariant,
strings safely escaped, Unicode preserved on JSON decoding, and property/primitive order deterministic
for the same typed input and version. Non-finite floating-point values in externally constructed scenes
are rejected by the JSON serializer. Consumers should tolerate additive schema fields.

## SVG, HTML, Canvas and custom renderers

`TemporalSvgRenderer.Render(scene)` emits XML with `viewBox="0 0 1280 720"`. Canonical rectangle,
rounded rectangle, ellipse, circle, triangle, line, arrow, ring and text use the existing path geometry.
Position, size, opacity, fill, stroke and stroke width are preserved. Canonical rotation is already in
the points. Text shapes keep the existing bitmap-glyph geometry and source-language restrictions
(120 supported Latin characters, displayed uppercase). Unicode labels in manually supplied scenes
and legacy text are escaped/preserved; host fonts determine legacy text appearance.

Legacy named visuals use a simple card/ellipse/text presentation. SVG does not promise pixel identity
with Playground gradients, shadows or background decoration. It preserves evaluated bounds, labels,
rotation and opacity. Unsupported source presentation still fails in the existing engine.

SVG uses XML construction, never markup interpolation. Paints are restricted to `none` or `#RRGGBB`;
URLs, scripts, event attributes and arbitrary markup are never generated. Invalid XML characters are
rejected by the XML writer. Security tests include quotes, angle brackets, ampersands, apostrophes,
Unicode, script/img-like strings, event-like strings and malicious paint values. The golden SVG is in
[the security fixture](../src/SoundScript.Tests/Golden/v14-security.svg).

The [HTML/JavaScript sample](../samples/ProgrammableMediaWeb/README.md) fetches an SVG scene and displays
it in an image at `audio.currentTime`. Its audio controls support play, pause, resume, restart and seek.
It cancels/discards stale responses after seeking or changing scenario and scales to mobile widths.
`requestAnimationFrame` controls repaint cadence only. Network latency can delay display; this is an
integration example, not a zero-latency player framework.

A custom Canvas renderer can consume the same paths without interpreting source or timing. Conceptual
application code for the JSON adapter (not a new SoundScript API):

```javascript
for (const p of scene.primitives) {
  ctx.globalAlpha = p.opacity;
  for (const path of p.paths ?? []) {
    if (!path.points.length) continue;
    ctx.beginPath();
    path.points.forEach((pt, i) => i ? ctx.lineTo(pt.x, pt.y) : ctx.moveTo(pt.x, pt.y));
    if (path.closed) ctx.closePath();
    if (path.closed && path.fill !== 'none') { ctx.fillStyle = path.fill; ctx.fill(); }
    if (path.stroke !== 'none') { ctx.strokeStyle = path.stroke; ctx.lineWidth = path.strokeWidth; ctx.stroke(); }
  }
}
```

Scale your context from 1280 × 720, clear it before painting and supply application-specific legacy text/card
presentation when `paths` is null. HTML/CSS hosts can similarly map bounds to positioned elements and use
`textContent` for labels. Neither mapping adds a timing engine.

WPF, WinUI, MAUI, Avalonia, Blazor, ASP.NET, consoles, workers and game/tool engines can use the same typed
model. Given an application-owned player, the integration is `var scene = media.SceneAt(player.Position);`.
Map `scene.Primitives` to the host's renderer. Dedicated adapters for these desktop frameworks are not supplied.
The host owns play/pause/event loops and obtains the actual audio playback position.

WebM remains an optional downstream path: existing timeline → scene sampling → raster frames → FFmpeg.
FPS, codecs, containers and rasterization remain outside the media facade/JSON/SVG flow. No FFmpeg process
is required to compile, render WAV, query scenes or serialize them. The existing WebM exporter reports
missing FFmpeg through `DependencyException`; codec output uses decode/semantic verification, not byte hashes.

## Application data and errors

The [console sample](../samples/ProgrammableMedia/README.md) and browser sample share an application-owned
`MonitoringScenario` builder. Healthy/Warning/Critical affect tempo, pitch, visual size, color and text.
Typed application data → validated application source builder → compilation → audio + scene.
The samples constrain data to an enum; arbitrary source fragments are not interpolated from HTTP input.

In-memory compilation rejects imports with `NotSupportedException`. For trusted files use
`SoundScriptEngine.CompileFile(path)`; for application-controlled resources use
`SoundScriptEngine.CompileFile(path, new AllowedPathRoot(root)).CompileMedia()` with `SoundScript.Core`.
The existing import and sample sandbox is retained. Keep the root stable; it is a path boundary, not
an OS sandbox. Missing required audio assets throw `FileNotFoundException` instead of being silently skipped.
Parser/semantic failures retain their existing meaningful .NET exceptions and source locations. Invalid
time throws `ArgumentOutOfRangeException`. HTTP example endpoints translate invalid time/scenario to 400.
There are no CLI exit codes or console-scraping requirements in the public API.

The bundled Wordbank corpus is embedded in the package assembly; the package
does not rely on corpus `contentFiles` beside the application. Playback can
read embedded audio bytes directly. APIs that explicitly request filesystem
paths materialize the corpus into a versioned, assembly-scoped directory under
the user's local application data area on supported desktop systems; browser
hosts continue to use in-memory resources. Avoid treating that cache path as a
general-purpose application data directory.

## Determinism, compatibility and validation

Determinism assumes the same source, assets, options, SoundScript version and deterministic renderer inputs.
Source, MIDI, WAV, scene snapshots, JSON and SVG are repeatable; different codecs/fonts/versions/platforms
are outside a claim of universal byte identity. The console sample writes SHA-256 inventories and the
external consumer validator runs it in two separate processes with a fresh package cache.

V14 is additive over 13.0.2. Existing compilation, MIDI, mono/stereo WAV, exact-duration video audio and
Playground behavior retain their contracts. No migration or obsolete API is required. The new complete
media audio contract is explicit and does not change the previous fixed-duration video API.

See [the acceptance report](v14-acceptance-report.md) for commands, exact results, compatibility evidence,
package inspection, requirement traceability and remaining limitations.
