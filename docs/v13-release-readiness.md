# V13 release identity audit

Current distribution (2026-09-21): [SoundScript 13.0.0 is on NuGet](https://www.nuget.org/packages/SoundScript/13.0.0),
[V13 CLI artifacts are on GitHub](https://github.com/dharangutti/sound-script/releases/tag/v13.0.0),
and the [Playground is deployed](https://soundscript.net/playground/).
The audit and validation counts below describe the earlier identity update.
See [V13 Reliability & Release Hardening](v13-reliability-hardening.md) for current engineering validation.

The transcription implementation initially retained the V12 product identity.
The release identity is now **13.0.0 / V13 — Media-to-SoundScript Transcription**.

| Location checked | Before | Action / current value |
|---|---|---|
| `Directory.Build.props` | 12.0.0 / V12 / Musical Completeness & .NET 10 | All three release properties updated |
| `src/SoundScript.Core/VersionInfo.cs` | Assembly-derived V12 | Unchanged consumer; rebuilt as V13 |
| All project/package metadata, including `SoundScript.Cli.csproj` | Inherited 12.0.0 | Inherit 13.0.0; assembly/file version 13.0.0.0 |
| CLI `Program.cs`, `Diagnostics.cs` | VersionInfo-derived version output/JSON | Rebuilt as 13.0.0 |
| `CliProductTests.cs` | Literal 12.0.0 expectation | 13.0.0 |
| Playground `Pages/Playground.razor` | VersionInfo.Label badge | V13 from rebuilt assembly |
| Web demo `Pages/Index.razor` | VersionInfo.Label badge/kicker | V13 from rebuilt assembly |
| `README.md` | Current release V12 / 12.0.0 | V13 / 13.0.0 and current capability introduction |
| `RELEASE_NOTES.md` | Latest entry V12 | Added V13 entry; older entries untouched |
| `docs/architecture.md`, `docs/examples.md` | Current overview headers V12 | V13 with links to existing transcription docs |
| `docs/PLAYGROUND.md` | Current verification checklist header V11 | V13 with transcription verification link; historical sections preserved |
| `docs/cli.md` | Current installation text V12; sample product JSON 12.0.0 | V13 / 13.0.0; JSON schema remains 1 |
| `docs/index.html` | V12 current badge/card/link, 12.0.0 metadata | V13 current; V12 retained as previous release |
| `docs/doc.html` | Static 9.0.0 and dynamic 12.0.0 metadata | Both 13.0.0 |
| `docs/404.html`, `docs/contact/index.html`, `docs/industrial/index.html` | 9.0.0 software metadata | 13.0.0 |
| Playground source `wwwroot/index.html` | 11.0.0 metadata | 13.0.0 |
| `docs/playground/index.html` | 9.0.0 metadata | 13.0.0 |
| `.github/workflows/deploy-pages.yml` | Reads shared properties; publishes Playground on main | Already correct; unchanged |
| `.github/workflows/release-cli.yml`, `docs/releasing.md` | Inherited package version / tag-driven releases | Already correct; unchanged |
| `global.json`, NuGet references, Wordbank manifest, vendored marked.js | SDK/dependency/corpus versions | Not product versions; unchanged |
| Versioned docs/examples/comments, protocol `SoundScript.performance.v1:` | Historical / protocol identities | Preserved |

## Generated artifacts and deployment boundary

The checked-in `docs/playground/_framework` files are an old generated snapshot,
not version authorities. They were not manually patched or replaced with a large
binary build diff. The existing Pages workflow publishes a fresh Playground from
source on main before deploying to gh-pages. A fresh local V13 Release publish is
validated under `artifacts/transcription/v13-playground`. This PR does not itself
deploy the website or publish a NuGet package/GitHub Release.

## Validation

- Full solution build/test: 1,069 passed, zero failed/skipped.
- Browser bridge: 4 passed.
- Fresh Release Playground publish: successful.
- CLI `--version`: 13.0.0; package and assembly metadata checked against shared props.
- Historical V12 release notes/card retained; remaining V10/V11 references describe
  feature history and examples. No transcription feature changes in this version bump.

The subsequent real-user MP3 acceptance/stabilization task is on a separate branch.
The feature PR remains draft until those acceptance findings are evaluated.
