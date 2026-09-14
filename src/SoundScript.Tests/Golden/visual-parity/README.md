# Browser references

These images come from the unmodified Playground Canvas renderer at 1280×720,
device pixel ratio 1, captured in Chromium/Edge on Windows. No video encoding
or resampling is involved. The scene JSON is produced by the same
`TemporalVisualSceneBuilder.Build(timeline.StateAt(time))` used by the preview.

Sources/times: `visual-org-chart.ssv` at 4.5s, `visual-information-cards.ssv` at
4s, `tools/VisualParity/legacy.ssv` at 1s and `primitives.ssv` at 1s.
The diagram images contain shared bitmap glyph paths, not OS fonts. Their
tolerances are correspondingly tighter. Named legacy labels use system fonts;
the legacy and primitives references allow those platform differences.

Regenerate intentionally with `dotnet run --project tools/VisualParity` and
`node scripts/verify-visual-parity.cjs`. Review the comparison gallery before
copying new browser images here. Never replace these references with CLI
output or relax thresholds simply to make a test pass.
