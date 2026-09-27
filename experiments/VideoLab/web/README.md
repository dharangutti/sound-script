# VideoLab browser workbench

Experimental programmable composition in .NET, separate from the supported
SoundScript NuGet package and CLI. Eight development cards cover real video/audio,
multiple clips, crossfade, titles, callouts, graphics, motion, expressions, effects,
conditions and sequences. Preview, read-only clip/text/callout/transition/audio tracks,
parameters and collapsible source
update together. Exact frame values come from native Snapshot.SceneAt; browser
video seeking is approximate. Export is MP4/H.264/AAC or WebM/VP9/Opus.

## Development and public gallery

This branch includes Phase 1 development ahead of the public v0.3.0 pin. Development
has eight demos, 20 snapshots and 40 exports; the currently approved baseline has
seven demos, 18 snapshots and 36 exports. Building this directory does not update
the website. See roadmap/PROGRESS.md and public publication.json for exact status.

The first composition combines coast/dog footage, animated graphics and mixed
synthetic audio. Sample Library offers two videos and two audio tracks in this
showcase, each with default and alternate bindings. Focused examples retain their
validated media, and Composition defaults restores their distinct source choices.
Choose a binding to change current runtime values; source defaults are shown beside
the numeric controls. Arbitrary values and personal media require local mode.
The static site cannot execute native .NET/FFmpeg and never uploads personal files.

## Private local mode

Install .NET 10 and FFmpeg/ffprobe on PATH (9.0.1 validated; see parent README for
codec/filter requirements). Check out experiments/videolab, then:

```powershell
cd experiments/VideoLab
dotnet run -c Release -- serve
```

Open http://127.0.0.1:8745/labs/videolab/ or My Files → Open local workbench from the
public page. Optional port: `serve 18745`. Bundled samples and the versioned site
work without private downloads. This command belongs only to the Lab executable.

1. Load Video + audio showcase and choose Sample Library or My Files.
2. Choose video/audio. One chosen video replaces both timed video spans; audio
   replaces the main music track while preserving the secondary mix track.
3. Change accentX or a gain. Parameter bindings reuse the compiled composition;
   only changing source media/demo compiles again. Reset defaults restores values.
4. Inspect a frame, then Render current composition to update the video. Until
   rendering finishes, the old preview is clearly labeled and export is disabled.
5. Play and export in the chosen MP4/WebM format. Download before resetting or
   rendering another output. Sample Library removes personal selections/results;
   End local session stops the process and deletes its temporary directory.

Validated input selections: MP4/WebM video and WAV/MP3 audio, 1 byte–50 MiB per file.
The entire span needed by the current composition must exist (six seconds for the
showcase music). Malformed or too-short replacements preserve a valid selection.
The native source policy still rejects detected VFR, interlace, non-square SAR,
HDR/wide-gamut/high-bit-depth video. An allowed extension does not guarantee that
all streams are supported. Source-normalization limits remain unchanged.

Files are copied into a generated temporary session on your own computer, never
to SoundScript.net. Only filenames/metadata and relative generated paths are shown.
No accounts, cloud storage or saved projects. Reset, normal shutdown or 30 minutes
of API inactivity removes private session files. Tab closing attempts reset; use
End local session for explicit cleanup. A crash/forced termination may leave an
OS-temp videolab-session-* directory; after stopping that process, remove its
session directory if necessary.

The adapter listens only on 127.0.0.1, checks Host/Origin and requires a random
per-process mutation token (CSRF protection, not user authentication). Only approved
examples, bounded bindings and generated paths are accepted. One operation runs
at a time. Uploads time out after 30 seconds, renders after three minutes; Cancel
kills the FFmpeg process tree. Trusted local media only: FFmpeg is not a sandbox.
Commands use the existing argument-list API, never shell interpolation.

## Phase 1 familiar editing example

Load **Titles, callouts and familiar edits**. Its source has two trimmed clips,
an explicit placement and 12-frame crossfade, a title, moving callout, shape and
music. The six track types make their lifetimes visible. **Editing properties**
shows source trims, timeline placement, join type, selected-frame transforms/crop,
caption font/alignment and audio gain. Timing remains authored in the source JSON;
this is not a drag-and-drop or trimming UI.

In local mode adjust position, scale, rotation, opacity, crop or musicGain; inspect
the updated frame and render. showCallout is still a bounded numeric condition
parameter, not a typed boolean. The alternate verified binding hides the callout
and changes transforms. Text/callout content uses the bundled licensed DejaVu Sans
font and single-line printable ASCII; no system font lookup or user font path.
Transforming a callout moves its label, rectangle and pointer as one plane. See
../SEMANTICS.md for exact clipping, alignment and local-target semantics.

## Rebuild and verify

```powershell
dotnet run -c Release -- webproof
node web/smoke.cjs
node web/local.test.cjs
node web/encoder.test.cjs
```

webproof runs all 143 existing checks, 43 Phase 1 editing checks and 36 gallery
repeat/decode pairs. It stages
a fresh distribution, canonicalizes JSON/text and writes checksums. Browser tests
use pinned Playwright from scripts/package.json; set PLAYWRIGHT_MODULE to its
installation when outside this checkout. Local tests own port 18745 and verify
uploads, native bindings, exports, cancellation, reset and shutdown cleanup.
Encoder regression compares 12 data-sequence MP4 renders in independent processes.
`dotnet run -c Release -- editingtest` runs the focused 43-check caption suite.
For a separately built local executable, set VIDEOLAB_DLL before local.test.cjs.

The x264 cpu-independent=1 setting fixes an observed repeat mismatch despite
identical raw frames. Some MP4 bytes differ from earlier milestones; old tags stay
immutable. Repeatability remains limited to identical inputs/tools/environment.

site/ is versioned. Four small samples include an adapted composed work from
user-supplied Pixabay downloads; see [credits](samples/CREDITS.md). Original
sources/private validation files are not packaged. After validation, update
publication-evidence.json, commit on experiments/videolab, tag a new milestone and
promote its exact artifact/evidence digests in the main Labs manifest. Production
staging injects publication.json. This branch cannot deploy the entire site.
