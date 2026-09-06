# V11 — Temporal Media Export

V11 extends V10's temporal Audio/Visual Playground with WebM export using a
shared scene profile and deterministic SoundScript.Wave PCM audio. Existing
MIDI, Wave, vocals, SoundCSS, and composition workflows remain available.

- **V10 authoring:** sequential `visual` intervals, `wait`, absolute `at`
  overlays, linear `animate` curves, and `sync audio` markers. Playback offers
  pause, resume, restart, exact scrubbing, and tempo-aware beat inspection.
- **V11 rendering:** `StateAt(t)` feeds a canonical scene shared by the live
  stage, browser exporter, and CLI rasterizer, with the same fitted PCM audio.
- **Browser export:** choose 24, 30, or 60 FPS and **Export Clip** in a browser
  supporting canvas capture and MediaRecorder. No account is needed.
- **CLI export:** FFmpeg encodes VP9/Opus WebM; both streams are decode-verified.
- **Example Library:** showcase, moving orb, sequential story cards, and
  layered product cues are available in the Audio/Visual workspace.

```bash
dotnet run --project src/SoundScript.Cli -- video examples/visual-temporal.ssv --output demo.webm --fps 30
```

Timeline states, scene primitives, and PCM are deterministic. Encoded WebM bytes
may differ across browsers and encoder versions. FPS is an output sampling
choice and is not part of the source language.

See the [media reference](visual-temporal.md), [use cases](use-cases.md),
and [example catalog](examples.md).
