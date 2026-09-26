# VideoLab v0.2 validation

## Starting baseline and tools

Started from `387291b31c46d555d264c233fc2b315e4151d2e1` on `experiments/videolab`. Frozen tag `labs-videolab-poc-v0.1.0` was verified at that commit and left unchanged. Before modifying code, the baseline passed **29 legacy + 66 programmable = 95 checks**.

Environment: Windows, .NET SDK **10.0.303**, FFmpeg and ffprobe **9.0.1-full_build-www.gyan.dev**. The complete version lines and generated evidence remain in ignored local artifacts. No cross-environment byte equality is claimed.

## Synthetic acceptance suite

**143 executable checks passed: 29 legacy, 66 existing programmable and 48 new v0.2 checks.**

The 66 programmable checks cover typed expressions/AST limits, easing/keyframe endpoints, immutable snapshots, atomic parameter validation, independent effects, conditions, generated sequences, bounds, plan generation, culture independence, both output formats, and decoded transform/audio agreement. These were preserved without removing or weakening any assertion.

The 48 new checks cover physical frame markers under integer and fractional CFR input rates; normalized fit across landscape/portrait/square/small/large/unusual geometry; clockwise rotation about a noncentral anchor; exact/one-frame-longer/trimmed/near-end/short source spans; stream/metadata diagnostics; conservative VFR/SAR/interlace/HDR/P3 policy; display-rotation ignoring; SDR HEVC MOV and CFR VP9 WebM; 44.1 kHz mono remixed to 48 kHz stereo; conditional silence/gaps; repeated source references; preservation of existing output; and optional realtest skip/failure status. The deliberate missing-asset realtest nested inside selftest prints an expected failure before the final PASS; it verifies exit 1.

Machine-readable reports: `artifacts/proof.json`, `artifacts/programmable-proof.json`, `artifacts/normalization/proof.json`. Synthetic assets and outputs are regenerated locally; no proprietary input is required.

## Repeatability and baseline preservation

All MP4/WebM repeated pairs remained byte-identical. All six named baseline output hashes below also remained unchanged by v0.2 normalization/probing changes.

| Output | SHA-256 |
| --- | --- |
| programmable.mp4 | `C7173EB27D95D69B0C151DABEA381FB30D45AB19D4E11EE699EEA3A164B7D843` |
| programmable.webm | `8D0386533398F7C20A08F4304F43023A2A12B113A4190693887E0DBD3B84457E` |

The A/B baseline hashes are preserved in the original acceptance record below. Input hashes in synthetic reports refer only to generated fixtures. Private real-media hashes are not recorded in this document.

## Local real-media validation

The optional local manifest validated **17 cases: 16 successful render cases and one expected timing rejection**. Successful cases produced byte-identical repeated MP4 and WebM, fully decoded with the expected frame counts, canvas and 48 kHz stereo. Sources were fingerprinted before/after and unchanged. See [REAL_MEDIA_VALIDATION.md](REAL_MEDIA_VALIDATION.md) for the actual recording matrix versus generated counterparts. Private media, manifests and detailed reports remain ignored under `real-media/`.

## Commands and manual checks

From this directory:

```powershell
dotnet build
dotnet run -- selftest
dotnet run -- realtest real-media/manifest.json
dotnet run -- inspect examples/transforms.json 15
dotnet run -- plan examples/transforms.json artifacts/test.mp4
dotnet run -- render examples/transforms.json artifacts/test.mp4
dotnet run -- render examples/transforms.json artifacts/test.webm
dotnet run -- batch examples/expressions.json examples/batch.json
```

Manual inspect/plan output was captured locally; both direct renders and batch outputs decoded with `ffmpeg -v error -xerror -i <output> -f null -`. A missing optional manifest returned 0, invalid realtest usage returned 2, and the synthetic failure case returned 1. Portrait/landscape real-media output frames were visually inspected. `git diff --check` passed.

## Isolation and changed files

Every v0.2 changed/new file is under `experiments/VideoLab/`:

- `.gitignore`
- `ARCHITECTURE.md`
- `AssetProbe.cs`
- `Ffmpeg.cs`
- `NormalizationProof.cs`
- `Program.cs`
- `ProgrammableBackend.cs`
- `README.md`
- `REAL_MEDIA_VALIDATION.md`
- `RealMediaTests.cs`
- `SEMANTICS.md`
- `VALIDATION.md`
- `examples/real-media-manifest.example.json`

No production compiler/parser/runtime, project/package/version metadata, release workflow, production documentation/sample or main solution membership changed. The frame-oriented reference backend remains intact. No merge, push, package or release was performed.

## Original 29-check acceptance record

Validated on 2026-09-26, Windows, .NET SDK 10.0.303, FFmpeg 9.0.1 full shared build (Gyan). Run `dotnet run -- selftest` from this directory to reproduce the acceptance suite. The generated `artifacts/proof.json` contains the full assertion list, source hashes, output hashes and tool version.

**29 checks passed.** Two snapshots share one compiled composition while retaining different immutable parameter bindings. Each snapshot rendered twice to both MP4 and WebM; each repeated pair was byte-identical, while A and B differed. All four distinct outputs fully decoded with exactly 180 frames at 640×360.

| Snapshot | MP4 SHA-256 | WebM SHA-256 |
| --- | --- | --- |
| A | `13D52CF487994145F9A8F1D90B898281A126BFC942FF683F311860FED6A415DC` | `06336138754FE2479DCF27BA0B9980F08DCDADE814FA72C55443D03E58B8A20D` |
| B | `F83AEAD71E6963B6F295C475CC094A9CDA335800E8468CBC6C3DB64F63320C2E` | `3F12BEF003A83D0FD962D029FF5D96FE2C8D856D4C8B3B56773FBCA7EC7B1A83` |

Decoded pixel assertions verified red → blended purple → blue, parameter-controlled overlay placement, the animation destination and an explicitly placed black gap. Decoded audio contained both the 220 Hz source-clip tone and 440 Hz music tone. Increasing music gain from 0.2 to 0.6 produced an RMS ratio of 2.99918 in a music-only interval. A four-frame contact sheet was also visually inspected.

Validation additionally covered atomic rejection of parameter batches, bounds, negative trim, invalid transitions, unsupported shape dimensions, sequence/source-frame arithmetic, half-open lifetimes and preservation of an existing output when preflight rejects a short asset. CLI inspection at frame 98 returned both transition layers with incoming opacity 8/15 and the expected animated X position.

These are synthetic-media acceptance results, not a broad media-compatibility certification. File hashes apply to this environment and these generated asset bytes. Production SoundScript was not modified or exercised; the experiment has no production references and is absent from the product solution.
