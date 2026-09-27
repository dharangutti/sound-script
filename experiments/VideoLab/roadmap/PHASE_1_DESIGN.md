# Phase 1 design decisions (implementation follows Phase 0 gate)

Add optional `texts` and `callouts` arrays without changing existing script members.
Reuse numeric transforms, conditions, effects, immutable programs and SceneAt.
Layer order stays videos, shapes, then text and callouts; old scripts retain their
original backend and outputs. Count every new element in existing resource bounds.

Text has a bounded plane (width/height), literal text, integer fontSize, hex color,
alignment, at/frames and the existing transform/when/effects fields. Position,
opacity, size/scale and rotation remain existing animated numeric properties.
Phase 1 does not introduce runtime string parameters. Single-line printable ASCII
is the initial explicit supported character set, with no wrapping or fallback.

Bundle unmodified DejaVu Sans 2.37 and its license; embed the font bytes in the Lab
assembly, recording their hash. Never resolve a system font. Render-time UTF-8 text
files and the font live in a private generated asset directory. FFmpeg receives
relative generated filenames with a dedicated working directory and expansion=none.
No authored text, path or expression is interpolated into a filter string. Plan
remains pure: it describes generated resources; Render materializes/cleans them.
Reference: https://ffmpeg.org/ffmpeg-filters.html#drawtext-1
Font: https://github.com/dejavu-fonts/dejavu-fonts/releases/tag/version_2_37

A callout uses the same caption renderer plus a rectangular label background and
a simple pointer line. Anchor is the label plane's transformed position; target
is an explicit bounded point in that plane. Transforming the callout moves the
label and pointer together. No diagram engine, screen-space tracking or CAD meaning.

The acceptance example combines two video spans, trim/place/crossfade, title,
callout, shape, audio and bounded transform/gain parameters. The browser adds
readable Text/Callout/Transition lanes and a small edit-summary panel showing
clip timing and selected-frame transforms. Source JSON remains authoritative.

Testing must cover actual decoded glyph/pointer pixels and alignment, literal
filter-like text, conditions, animated transforms/opacity, snapshot isolation,
invalid schema/resource values, font fingerprint, repeated MP4/WebM, input and
temporary-resource protection, and an end-to-end browser example. Keep all prior
assertions. No public publication pin changes in this phase.
