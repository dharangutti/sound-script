# VideoLab browser explorer

This is a static interface to the completed CLI MVP, not an in-browser editor or
FFmpeg port. Six demos expose composition, transforms/easing/audio gain, expressions,
reusable effects, conditional layers and data-driven sequences. Ten real parameter
snapshots support exact scene inspection, playback and twenty MP4/WebM downloads.
It never uploads user files.

From `experiments/VideoLab`, regenerate the site with:

```powershell
dotnet run -c Release -- webproof
node web/smoke.cjs
```

The first command runs all 143 core checks, including repeated renders and decoded
media assertions, then renders every additional gallery binding twice in both formats
and fully decodes each output (16 additional format/binding checks). It packages all
180 baseline scenes and all 30 frames of each focused example directly from the engine.
JSON object keys and source text line endings are normalized for stable packaging.
It builds into a fresh temporary artifact folder and replaces the distribution only
after successful rendering. FFmpeg/ffprobe must meet the parent README requirements. Browser
verification uses Playwright (the repository's pinned `scripts/package.json`);
set `PLAYWRIGHT_MODULE` to that installation when it is outside this checkout.

`site/` is a deliberately versioned, reproducible static distribution. Only synthetic
fixtures are published. No manual-download or real-media validation input is used.
The public artifact has no dependencies on production assemblies or client scripts.
`publication.json` is supplied by the production staging process with the exact
approved branch, commit, milestone and artifact digest. It is not a source file.

After regeneration and browser tests, update `publication-evidence.json`, commit
all Lab changes on `experiments/videolab`, preserve a new milestone tag, and promote
that exact commit and evidence digest in the production Labs manifest. Never retag
the original POC milestone. Production owns final deployment; this branch cannot
deploy the entire website.

The browser is a finite feature explorer: it offers predefined snapshots, not
arbitrary parameter values. The expression variants demonstrate batch-style shared
composition bindings, but the browser does not execute the CLI batch command.
Scene data comes from `Snapshot.SceneAt`; browser video
seeking is approximate. Native CLI authoring, source policy and determinism limits
remain as documented in the parent README. Missing data and unsupported playback
produce user-visible errors; the other codec and local downloads remain available.
