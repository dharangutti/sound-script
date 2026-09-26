# Acceptance result

Validated on 2026-09-26, Windows, .NET SDK 10.0.303, FFmpeg 9.0.1 full shared build (Gyan). Run `dotnet run -- selftest` from this directory to reproduce the acceptance suite. The generated `artifacts/proof.json` contains the full assertion list, source hashes, output hashes and tool version.

**29 checks passed.** Two snapshots share one compiled composition while retaining different immutable parameter bindings. Each snapshot rendered twice to both MP4 and WebM; each repeated pair was byte-identical, while A and B differed. All four distinct outputs fully decoded with exactly 180 frames at 640×360.

| Snapshot | MP4 SHA-256 | WebM SHA-256 |
| --- | --- | --- |
| A | `13D52CF487994145F9A8F1D90B898281A126BFC942FF683F311860FED6A415DC` | `06336138754FE2479DCF27BA0B9980F08DCDADE814FA72C55443D03E58B8A20D` |
| B | `F83AEAD71E6963B6F295C475CC094A9CDA335800E8468CBC6C3DB64F63320C2E` | `3F12BEF003A83D0FD962D029FF5D96FE2C8D856D4C8B3B56773FBCA7EC7B1A83` |

Decoded pixel assertions verified red → blended purple → blue, parameter-controlled overlay placement, the animation destination and an explicitly placed black gap. Decoded audio contained both the 220 Hz source-clip tone and 440 Hz music tone. Increasing music gain from 0.2 to 0.6 produced an RMS ratio of 2.99918 in a music-only interval. A four-frame contact sheet was also visually inspected.

Validation additionally covered atomic rejection of parameter batches, bounds, negative trim, invalid transitions, unsupported shape dimensions, sequence/source-frame arithmetic, half-open lifetimes and preservation of an existing output when preflight rejects a short asset. CLI inspection at frame 98 returned both transition layers with incoming opacity 8/15 and the expected animated X position.

These are synthetic-media acceptance results, not a broad media-compatibility certification. File hashes apply to this environment and these generated asset bytes. Production SoundScript was not modified or exercised; the experiment has no production references and is absent from the product solution.
