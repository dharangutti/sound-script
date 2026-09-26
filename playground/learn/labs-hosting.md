# SoundScript Labs lifecycle

Labs are experimental work, not stable SoundScript APIs. An experiment can be listed
without being MVP-qualified or publicly runnable. `docs/labs-manifest.json` is the
single reviewed catalog and publication authority, separate from production release
state. Changing it does not publish NuGet, CLI or a new SoundScript version.

## Architecture

```text
experimental branch → tested static distribution → immutable milestone + commit
                                              ↓ explicit manifest promotion on main
main → existing production build → validated Labs staging → one Pages artifact
                                                        → soundscript.net/labs/
```

The existing Deploy SoundScript Site workflow is the only full-site publisher. It
runs for main pushes or manual dispatch on main; experiment pushes and tags do not
deploy. Staging fetches each approved branch and milestone explicitly into a fresh
temporary Git repository. The milestone must resolve to the approved full commit,
and the commit must be an ancestor of the branch. Nothing uses an unrecorded HEAD.

The static distribution is read from Git blobs at that commit, checked against its
SHA-256 tree digest and evidence, and written only to `labs/<id>/`. Branch code is
never executed with deployment credentials. Runtime compilation/rendering happens
on the experimental branch before promotion. A fresh staging tree removes withdrawn
routes naturally. Failed validation leaves the previous site artifact intact.

This first contract supports versioned static distributions, including generated
media, up to 64 MiB / 1,000 files per Lab. It intentionally does not support arbitrary
branch build commands, backend services, service workers or base URL rewriting.
Same-origin Lab JavaScript must be reviewed as trusted code before promotion;
separate paths are deployment isolation, not browser security isolation.

## Register and qualify

1. Create an experiment branch such as `experiments/example` and keep implementation
   under its own experimental directory. Do not add it to the production solution.
2. Add a unique lowercase slug, description, actual branch, exact commit, preserved
   milestone and status to the catalog. Start with `publish: false`, `mvp: false`,
   and `artifact: null`. Explain why it is not hosted.
3. Define one coherent MVP scenario, tests, limits and reproducibility expectations.
   Allowed maturity values are `exploration`, `poc`, `mvp`, `preserved`, `archived`.
4. Record MVP qualification separately from publication. CLI MVPs do not automatically
   become browser apps. A listed milestone may be a commit for a preserved experiment;
   publication requires an actual tag matching the approved commit.

## Promote deliberately

Build and test the Lab in a clean checkout. Verify real functionality, errors,
navigation, mobile layout and all assets beneath its intended subpath. Publish no
placeholder controls, private media or unfinished features. Preserve the generated
distribution on the experimental branch, with Git text conversion disabled for
files covered by hashes. Commit a JSON evidence record beside it:

```json
{
  "schemaVersion": 1,
  "mvp": "pass",
  "artifactSha256": "<tree digest>",
  "checks": {
    "build": { "result": "pass", "command": "<actual build command>" },
    "tests": { "result": "pass", "command": "<actual test command>" },
    "determinism": { "result": "pass", "command": "<actual repeatability check>" },
    "browser": { "result": "pass", "command": "<actual browser smoke command>" }
  }
}
```

Evidence is reviewed attestation, not a substitute for tests. Preserve test counts,
tool versions, limits and outcomes in that evidence or linked branch documentation.
The browser smoke is rerun by production CI against the staged bytes. Regenerating
the native renderer is a Lab responsibility and is not run under production tokens.

Use the exported `inventory`, `treeHash` and `hash` functions in `scripts/labs.cjs`
to calculate artifact/evidence hashes. The tree digest is SHA-256 of sorted records
`relativePath + NUL + SHA256(fileBytes) + LF`. The evidence file must be outside the
artifact to avoid a circular hash. Preserve a new milestone tag; never move old tags.

Open a production PR changing the manifest to `status: "mvp"`, `mvp: true`,
`publish: true`, with the exact tagged commit and artifact directory/digest plus
evidence path/digest. This reviewed manifest change is the promotion gate, following
the existing explicit release-state promotion convention without duplicating a
release workflow. A branch push or tag by itself cannot publish a Lab.

```powershell
node scripts/labs.cjs check
node --test scripts/labs.test.cjs
./scripts/publish-site.ps1
node scripts/verify-labs-browser.cjs artifacts/site
```

For an offline review only, `node scripts/labs.cjs stage <fresh-directory> --offline`
reads pinned blobs from the local repository. Production never passes that flag:
it must verify remote branch/tag provenance on every build. The output includes
`publication.json` with the deployed commit. Catalog-only Labs get no live route.

To withdraw a Lab, set `publish: false`, remove its artifact contract (set to null),
and give a reason. Keep its branch and milestone for historical discovery. Use
`preserved` or `archived` as appropriate. The next full Pages deployment drops the
route without altering production content.

## Initial discovery and assessment

Repository heads, Lab tags, experiments folders and history identify three Labs:

| Lab | Branch | Maturity and hosting |
| --- | --- | --- |
| VideoLab | `experiments/videolab` | MVP PASS; user accepted the CLI MVP. Browser explorer added on that branch, with real rendered snapshots and scene inspection. |
| Signal Lab / SoundScript.Labs | `codex/soundscript-labs-poc` | POC, explicitly not MVP. Preserved `labs-signal-poc-v0.1.0`; CLI simulation, no hosted app. |
| Runtime Binding Lab | `codex/runtime-binding-lab` | Preserved research prototype. Milestone commit `de848e2c61d992658a5ed3e4a15644d3cfd2a427`; no publication approval. |

Runtime Binding Lab already has historical experiment files on main; this hosting
work does not introduce or move them. Signal and VideoLab implementation remains
outside main. VideoLab's original `labs-videolab-poc-v0.1.0` remains preserved.
Its MVP evidence includes 143 automated checks, repeated MP4/WebM byte comparisons,
decoded transition/animation/audio assertions and 17 private real-media cases
(16 successful renders, one expected VFR rejection). The public explorer ships only
synthetic proof media and supports two fixed parameter snapshots; it does not upload
assets, compile scripts or run FFmpeg in the browser.

No production runtime/API/media behavior changes. The homepage gains a Labs link;
the existing Playground build and integrity/browser checks remain in place.
