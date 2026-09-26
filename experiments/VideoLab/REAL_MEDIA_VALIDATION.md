# VideoLab v0.2 real-media validation

Tested locally on 2026-09-26 with Windows, .NET SDK 10.0.303 and FFmpeg/ffprobe 9.0.1 full shared build. The branch started at `387291b31c46d555d264c233fc2b315e4151d2e1`, preserved by `labs-videolab-poc-v0.1.0`.

## Actual media: 17 cases

**16 cases rendered successfully; one timing-ambiguous WebM was rejected as expected.** Each successful case rendered twice to MP4 and twice to WebM. Every repeat pair matched by SHA-256, every distinct output fully decoded, frame count/canvas/48 kHz stereo matched expectations, and source fingerprints were unchanged. There is no cross-machine determinism claim.

| Tested source | Actual characteristics | Policy/result |
| --- | --- | --- |
| Pixabay woman/model, four downloaded variants | 1080×1920, 540×960, 360×640 H.264; 30000/1001 CFR; original has stereo AAC, derivative downloads may have no audio | Pass; normalized to 30 fps, explicit audio when present |
| Pixabay photography, four variants | 1920×1280, 1620×1080, 810×540, 540×360 H.264, 25 fps, no audio | Pass; 3:2 aspect fit, including a nonzero-trim repeated-reference case |
| Pixabay dog, three variants | 1920×1080 H.264 at 50 fps; 960×540 and 640×360 at 25 fps; stereo AAC | Pass; explicit clip audio mixed/resampled to 48 kHz stereo |
| Pixabay beach/photographer, three variants | 1920×1080 H.264 at 50 fps; 640×360 and 480×270 at 25 fps; no audio | Pass |
| Locally available recorded music | Stereo MP3, 48 kHz | Pass; nonzero trim and gain |
| Locally available exported audio | WAV | Pass |
| Locally available application video export | VP9 WebM, nominal 30 fps, nonuniform PTS (including 28/32/33/34/39 ms steps) | Expected `UNSUPPORTED_TIMING`; no claim that all WebM is unsupported |

Public footage provenance: [woman/model 216973](https://pixabay.com/videos/woman-model-fashion-hairstyle-216973/), [photography 7826](https://pixabay.com/videos/woman-girl-shooting-photography-7826/), [dog 15305](https://pixabay.com/videos/dog-pet-animal-small-furry-cute-15305/), [photographer/beach 91](https://pixabay.com/videos/photographer-beach-photography-91/). Sources came from the user's suggested Pixabay collection and manual downloads. The duplicate filename copy of the beach clip was not counted as another test. No actual media, derived real-media outputs, private paths or private asset hashes are committed.

The local manifest is `real-media/manifest.json`. Its ignored `real-media/results/report.json` contains machine-readable stream metadata, private source fingerprints and output hashes. The template under `examples/` has only fictional filenames and is safe to copy. No manifest is required for normal selftest.

## Generated counterparts: not actual camera validation

The v0.2 synthetic suite separately exercises these conditions. They must not be confused with proof against real phone recordings or a broad codec certification.

| Generated condition | Result |
| --- | --- |
| 1920×1080 landscape into portrait canvas; 1080×1920 into landscape | Centered aspect fit and black bars agree with pixel checks |
| Square, smaller-than-canvas, larger-than-canvas, 6:1 aspect | Pass |
| 24/25/30/60 fps and 24000/1001, 30000/1001, 60000/1001 | Normalized source-frame markers match decoded pixels |
| CFR VP9 WebM | Pass |
| SDR HEVC in MOV | Pass on this build |
| MOV containing 90-degree display metadata | Metadata ignored explicitly; decoded output matches unrotated encoded plane |
| Nonuniform timing, non-square SAR, interlace, HDR/P3 tags | Explicit rejection |
| 44.1 kHz mono WAV | Equal 48 kHz stereo channels, conditional silence and gaps verified numerically |
| Exact source length, slightly longer source, nonzero trim, trim near end, repeated references | Pass |
| Short/missing/corrupt sources, absent video/audio stream, unknown duration | Distinct diagnostics; existing output preserved |

## Findings and decisions

- A real 25 fps MP4 uses time base 1/25. This is valid; preflight was corrected to permit a whole-frame tick while still checking actual packet spacing. Quantization tolerance now distinguishes integer ticks/frame from fractional tick spacing.
- Nominal FPS labels alone are insufficient. The exported WebM's observed timestamp variation is rejected conservatively. Prefix inspection covers the requested source end plus one second for packet reorder; unconsumed suffixes are not certified.
- Rational CFR input can be normalized deterministically under the existing integer output model. No rational output model was added.
- Source rotation is encoded-pixel orientation, not automatic display orientation. Actual phone MOV/HEVC recordings were not available; generated equivalents establish the policy but do not claim phone compatibility.
- Crop refers to the normalized, letterboxed source plane. It is not a raw-camera-pixel crop. Coordinate/anchor/rotation ordering is specified in [SEMANTICS.md](SEMANTICS.md).
- No assets were mutated; local fingerprinting is opt-in validation work, not an ordinary render requirement.

## Limitations and next experiment

Only short spans were rendered in this bounded lab. Diverse edit lists, timestamp discontinuities outside consumed prefixes, unusual channel layouts, long files and camera-specific metadata remain unvalidated. SDR-only output targets yuv420p; HDR, wide gamut, Dolby Vision and professional color preservation are not supported. FFmpeg codec availability remains environment-dependent. This is trusted local-media tooling, not an upload service.

The next suggested experiment is v0.3 optimized lowering compared against the retained frame-oriented reference backend. No v0.3 work, editor effects, text, publishing, merge or release was performed here.
