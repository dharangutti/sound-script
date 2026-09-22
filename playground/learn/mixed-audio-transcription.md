# Milestone 3 — Mixed audio / musical roles

Branch: `codex/mixed-audio-roles`, based on Milestone 2 commit `39e9d33`.

This implementation produces **symbolic musical-role estimates**, not isolated
audio stems. It does not implement true audio stem separation. The shared
polyphonic analyzer supplies simultaneous pitch observations; a deterministic
register/rank heuristic assigns bass, upper melody, and remaining harmony.
Instrument identity and the intended lead are not established by these labels.
Percussion, lyrics, diffuse mixtures and rejected frames remain unsupported.

```sh
soundscript transcribe mix.mp3 --mode mixed --out arrangement.ss
soundscript transcribe mix.mp3 --mode mixed --roles melody,bass --out selected.ss
```

Roles default to melody, harmony and bass. The Playground offers the same
selection, per-role diagnostics, individual analyzed-role playback, combined
generated playback, editable source and exports. Changing roles invalidates stale
results. Filtering preserves each retained note's absolute timing. Simultaneous
notes use parallel existing SoundScript tracks, without new language constructs.

## Phases and verification

1. Shared role interpretation and evidence; focused authored melody+bass,
   melody+chords, melody+bass+drums, small ensemble, dense and noisy fixtures.
2. CLI and Playground integration; selected-role timing, deterministic sync/async
   output, cancellation and invalid argument checks.
3. External Latin-jazz measurements, then broad regression and Release/browser
   verification. Human listening is a separate, deferred acceptance gate.

All 12 focused tests and all 1,142 .NET tests passed. Release publication and
normal/missing/corrupt startup checks passed. The mixed browser check passed
CLI/browser score parity, role disabling, individual bass playback, combined
editing/playback, exports, rejection, cancellation and mobile layout. The browser
fixture uses independent pitches: its earlier octave-related pair was correctly
suppressed by the conservative harmonic gate and was unsuitable for asserting
four recovered simultaneous notes.

Three ten-second excerpts of the original Latin-jazz recording (0, 30 and 60
seconds) generated 74, 80 and 69 estimated notes. Rejected active-frame fractions
were approximately 26%, 17% and 18%. See [measurements](mixed-audio-measurements.json).
Round-trip metrics measure generated audio against the generated score; they do
not measure fidelity to the recording. Original media and renders stay outside
Git. Recognizability and human listening acceptance remain pending; these
measurements do not establish successful reconstruction of the recording.
