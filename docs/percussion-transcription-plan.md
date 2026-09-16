# Milestone 4 — Percussion / rhythm

Branch `codex/percussion-rhythm-transcription`, based on mixed-role commit `2d1ba42`.

Percussion mode must not route detected transients through the pitched-note
transcriber or emit fake pitched notes from drum resonances.

1. Add an explicit unpitched canonical hit and SoundScript `hit kick|snare|hat|click
   :beats [vN]` AST/printer/parser path. The existing language has only pitched
   notes/chords and external WAV samples; none represents portable unpitched hits.
   Implement deterministic synthetic percussion playback in the wave backend.
   MIDI export will explicitly reject hits until a dedicated mapping is provided.
2. Add independent transient/band-energy observations, coarse classification,
   independent tempo/grid inference with alternative hypotheses, and evidence.
   Ambiguous transients use a generic click or are rejected; no pitch inference.
   Keep measured onset seconds separate from optional reconstruction quantization.
3. Integrate shared CLI and Playground mode, hit diagnostics, rhythm code,
   playback and exports. Reanalysis of generated audio must use percussion only.
4. Focused phase checks: syntax/rendering, authored kick/snare/hat and combined
   patterns, silence, pitched percussion, sustained/noisy material, deterministic
   sync/async behavior, cancellation and graceful failures. Then original drum
   excerpts, full regression, Release startup/integrity and browser workflow.

Classification scores describe spectral dominance and transient evidence, not
calibrated instrument probabilities. Detection in mixed rhythm sections remains
experimental. Real-audio recognizability requires deferred human listening.
