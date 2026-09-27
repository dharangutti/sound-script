# Next after the feature gallery

The gallery now exposes six existing capabilities without changing rendering
semantics: composition, transforms/easing/gain, expressions and binding variants,
reusable effects, conditional layers, and data-driven sequences. It is a public
explanation of the engine, with reproducible synthetic media and exact inspection.

The next useful implementation is a **private local authoring workbench**. That is
a separate milestone; it is not implemented or implied by this static gallery.

1. Start a loopback-only companion process from the experimental CLI. Reuse
   `Composition.Compile`, atomic parameter bindings, `SceneAt`, asset diagnostics
   and `Ffmpeg.Render`; do not implement a second compiler in JavaScript.
2. Let the local browser select trusted video/audio assets and edit a declarative
   script. Show validation errors, duration/stream policy, timeline state and actual
   parameter changes before rendering. Keep all files on the user's computer.
3. Add one bounded render job at a time, progress, cancellation, and MP4/WebM output
   selection. Preserve existing destinations on failure. Require a per-session
   token and validated origins for local API requests; do not expose FFmpeg as a
   public upload service.
4. Validate an end-to-end scenario: two user clips, music, trim/sequence/crossfade,
   overlay animation, gain and a changed parameter. Compare repeated renders and
   verify errors/cancellation do not damage inputs or prior output.

After that, benchmark the reference backend against real projects before deciding
whether to optimize it or extract a separate repository. Keep the current reference
renderer as the correctness oracle. Browser-native FFmpeg/WASM is an independent
architecture decision, not a prerequisite for a useful private workbench.

The public site stays a static gallery until a separate authoring/public-hosting
scope is explicitly approved. Private recordings never enter the public distribution.
