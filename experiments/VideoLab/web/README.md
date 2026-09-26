# VideoLab browser explorer

This is a static interface to the completed CLI MVP, not an in-browser editor or
FFmpeg port. It exposes real parameter snapshots, exact scene inspection, crossfade
inspection, playback and MP4/WebM downloads. It never uploads user files.

From `experiments/VideoLab`, regenerate the site with:

```powershell
dotnet run -c Release -- webproof
node web/smoke.cjs
```

The first command runs all 143 core checks, including repeated renders and decoded
media assertions, then packages the existing engine's A/B proof and all 180 scenes
for each binding. FFmpeg/ffprobe must meet the parent README requirements. Browser
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

The browser is a finite MVP explorer: it offers two pre-rendered snapshots, not
arbitrary parameter values. Scene data comes from `Snapshot.SceneAt`; browser video
seeking is approximate. Native CLI authoring, source policy and determinism limits
remain as documented in the parent README. Missing data and unsupported playback
produce user-visible errors; the other codec and local downloads remain available.
