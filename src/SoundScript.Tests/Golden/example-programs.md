# Example compatibility baselines

`example-programs.json` contains SHA-256 fingerprints captured from the original
examples before the readability review on `codex/review-all-examples`.

For SoundScript sources, each fingerprint covers the parsed program with imports
resolved and authoring conveniences lowered. Serialization includes concrete AST
node properties recursively. Only `BarNode.Line` is normalized to zero: it is a
diagnostic source location that changes when comments are inserted, not musical
timing. SoundCSS fingerprints cover resolved phoneme profiles and word transform
plans. These snapshots do not depend on source whitespace or newline conventions.

`ExampleCompilationTests` discovers all `.ss`, `.ssw`, `.ssv`, and `.ssc` files in
`examples/` and `docs/assets/demos/`, plus the Playground's inline presets, default
script, Studio pairs, syntax example, and embedded stylesheet. It compiles them
through MIDI emission, Wave rendering, temporal scene/export planning plus PCM,
or SoundCSS profile/transform resolution as appropriate. The import-only library
is compiled too, without requiring it to emit notes. Visual presets load the same
embedded example files and retain their existing catalog and timeline tests.

The inventory assertion detects removal of a reviewed example or lost preset
discovery. Newly added examples are discovered automatically and require a
reviewed baseline. When intentionally changing an example, compare its parsed
program and rendered output before updating its fingerprint; never refresh these
values merely to make a failing test pass. Existing MIDI, note, timeline, and PCM
regressions remain independent checks on runtime behavior.

The suite renders local media without FFmpeg, network access, or external TTS.

The five `examples/performance-*.ss` fingerprints were added with expressive
performance after checking their parsed scores, deterministic MIDI/Wave renders,
and actual Playground playback. They do not replace any earlier fingerprints.
The Playground embeds those same five source files; dedicated performance tests
also compare their legacy and expressive output. Listening acceptance is recorded
separately in `docs/performance-interpretation.md`.
It does not compare encoded WebM container bytes or rebuild published website
binaries. Bundled sample paths resolve relative to their source scripts.
