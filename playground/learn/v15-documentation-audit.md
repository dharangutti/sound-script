# V15 documentation audit

Baseline: `65976c6e4608ef91132b19a5fad334ba29cd2f89`; clean working tree.
Recorded before structural edits. This is historical engineering evidence.

## Scope and classification

Root Markdown, docs Markdown/HTML/state JSON, packaging Markdown, sample/tutorial
READMEs, site navigation/metadata (including Playground source HTML and sitemap),
project metadata, scripts/help and workflows are inventoried deterministically by
`scripts/docs-state.mjs`. The manifest records every file's classification.
Build output, artifacts, dependencies, binaries, temporary files and
`experiments/SoundScript.Labs/` are outside the scan. Committed Playground output
is explicitly excluded in the manifest; its source HTML is reviewed instead.
Historical release notes, version reports, measurements and draft articles keep
their original claims. No unclassified file is permitted at acceptance.

## Findings before edits

| Surface inspected | Finding / disposition |
| --- | --- |
| README.md | Public V14 correct, repeated install/release facts need ownership; development/public conflated |
| docs/documentation.md | Useful intent-oriented hub; expand navigation without duplicating guides |
| docs/SoundScript.md | Competing V11 index; retain URL as concise compatibility gateway |
| docs/doc.html | Legacy default/nav/SEO; raw escaped fences lack labels and copy; preserve dark theme |
| docs/index.html | V14 candidate badge, legacy softwareHelp; preserve layout/history |
| packaging/README.md | V14 local candidate onboarding contradicts public library |
| docs/quick-start.md | Recommends V13 install |
| docs/nuget.md | V13.0.2 public/V14 candidate wording and local install conflated |
| docs/dotnet-api.md | API explanation reviewed; unchanged contracts, valid V13 historical link |
| docs/cli.md | V13 downloads; registrations and exit-code source must drive checks |
| docs/common-tasks.md | Useful task recipes; no runtime changes needed |
| docs/programmatic-media-runtime.md | Incorrect current candidate wording; preserve compatibility baseline |
| docs/tutorials/programmable-media.md | Local candidate onboarding should use public install |
| docs/releasing.md | Stale V13 identity/distribution; document independent release-state workflow |
| .github/workflows/deploy-pages.yml | Development badge stamping could advertise unreleased V15 |
| scripts/update-homepage-release.ps1 | Keep safe renderer; add explicit public-state input at publishing call site |
| samples and other tutorials | Pinned V13.0.2 sample dependencies are real; identify as compatibility baselines |
| project package metadata | IDs, framework, command and URLs already canonical; no change required |
| scripts/validate-nuget.ps1 | Packaged README version incorrectly tied to nuspec; validate canonical public README independently |
| scripts/validate-media-package.ps1 | Fixed V14 package expectation; use development props for local package validation |

## Authoritative facts

| Fact | Sole authority |
| --- | --- |
| Development version, V label, codename | Directory.Build.props |
| Public version and version-specific publication channels | docs/release-state.json |
| Library ID, framework, repository URL, public site URL | src/SoundScript/SoundScript.csproj |
| CLI ID, framework, tool command | src/SoundScript.Cli/SoundScript.Cli.csproj |
| CLI public commands | CliArguments.Commands in src/SoundScript.Cli/CliArguments.cs |
| CLI exception exit values | Diagnostics.ExitCode in src/SoundScript.Cli/Diagnostics.cs |
| CLI success exit | src/SoundScript.Cli/Program.cs and CommandHandlers.cs |
| Release archive platforms | .github/workflows/release-cli.yml |
| Release history | RELEASE_NOTES.md (never install/distribution authority) |
| Classification and allowed blocks | docs/docs-manifest.json |

The command registration contains `visual` as well as the requested commands and
`vocal generate` / `vocal batch` and `wordbank ensure` / `wordbank normalize`.
All registrations are public; transcription
modes can be experimental. `help`, `--help`/`-h`, `--version`/`-v` are handled by
Program.cs; `--output`/`-o` alias `--out`. There are no hidden command registrations.

## Preservation and candidates

Generated candidates: library installation, CLI distribution, public/development
identity, framework, local package validation and site metadata. Explanations,
examples and architecture remain authored. V14 acceptance, V13 hardening/readiness,
NuGet validation evidence, older version reports and draft articles keep old
versions, candidate terminology, test counts and compatibility claims.

Unresolved at baseline: classification completion, local links/fragments, generic
fences, safe release-history filtering and viewer browser verification. Final
disposition and exact results belong in the V15 acceptance report.

## Final audit disposition

The manifest now covers 200 files: 139 living, 50 historical, 11 excluded.
No unclassified files remain. Historical documents receive no current-state generation.
No old historical report changed; RELEASE_NOTES.md only gains the unreleased V15 section.
113 formerly generic living fences are labelled; all living Markdown links/fragments pass.
Three stale heading links were corrected (fixture-generator and two Wave references).
All minimum requested files were inspected. The acceptance report records tests and limitations.

### Reviewed living files needing no change

These files were reviewed for current guidance, links or canonical configuration and did
not need content changes for V15. Explicit classification is retained in the manifest.

- .github/workflows/publish-nuget.yml
- .github/workflows/release-cli.yml
- CONTRIBUTING.md
- docs/PLAYGROUND.md
- docs/audio-visual-compositions.md
- docs/authoring.md
- docs/dotnet-api.md
- docs/example-learning-guide.md
- docs/media-primitives.md
- docs/melody-extraction.md
- docs/mixed-audio-transcription.md
- docs/musical-completeness.md
- docs/percussion-transcription.md
- docs/performance-interpretation.md
- docs/polyphonic-transcription.md
- docs/transcription-architecture.md
- docs/transcription.md
- docs/use-cases.md
- docs/visual-temporal.md
- samples/DevOpsSonification/README.md
- samples/DynamicAudio/README.md
- samples/ProgrammableMedia/README.md
- samples/TestFixtureGenerator/README.md
- scripts/build-av-examples.cjs
- scripts/bump-wordbank-submodule.sh
- scripts/cli-build-path.cjs
- scripts/corpus-provenance.cjs
- scripts/corpus-provenance.test.cjs
- scripts/dependency-inventory.ps1
- scripts/measure-polyphonic-acceptance.ps1
- scripts/performance-report.cjs
- scripts/playground-integrity.test.cjs
- scripts/playground-startup.test.cjs
- scripts/polyphonic-listening-report.cjs
- scripts/probe-playground.cjs
- scripts/sync-wordbank.sh
- scripts/transcription-browser-input.test.cjs
- scripts/update-site-metrics.ps1
- scripts/validate-release.ps1
- scripts/verify-av-playground.cjs
- scripts/verify-av.cjs
- scripts/verify-performance-playground.cjs
- scripts/verify-performance.cjs
- scripts/verify-playground-integrity.cjs
- scripts/verify-playground-startup.cjs
- scripts/verify-polyphonic-playground.cjs
- scripts/verify-programmable-media.cjs
- scripts/verify-transcription-playground.cjs
- scripts/verify-v14-docs.cjs
- scripts/verify-visual-parity-playground.cjs
- scripts/verify-visual-parity.cjs
- src/SoundScript.Cli.TestFfmpeg/SoundScript.Cli.TestFfmpeg.csproj
- src/SoundScript.Cli/SoundScript.Cli.csproj
- src/SoundScript.Compose/SoundScript.Compose.csproj
- src/SoundScript.Core/SoundScript.Core.csproj
- src/SoundScript.Media/SoundScript.Media.csproj
- src/SoundScript.Midi/SoundScript.Midi.csproj
- src/SoundScript.Parser/SoundScript.Parser.csproj
- src/SoundScript.Playground/SoundScript.Playground.csproj
- src/SoundScript.Prosody/SoundScript.Prosody.csproj
- src/SoundScript.Tests/SoundScript.Tests.csproj
- src/SoundScript.Timbre/SoundScript.Timbre.csproj
- src/SoundScript.Transcription/SoundScript.Transcription.csproj
- src/SoundScript.Visual/SoundScript.Visual.csproj
- src/SoundScript.Vocal/SoundScript.Vocal.csproj
- src/SoundScript.Voice/SoundScript.Voice.csproj
- src/SoundScript.Wave/SoundScript.Wave.csproj
- src/SoundScript.Web/SoundScript.Web.csproj
- src/SoundScript.Wordbank/SoundScript.Wordbank.csproj
- src/SoundScript/SoundScript.csproj

### Historical classification

These files preserve recorded engineering evidence and version-specific claims. Older
candidate wording, versions, counts and package availability are intentionally retained.

- RELEASE_NOTES.md
- TRANSCRIPTION_STATUS.md
- docs/SoundScript_Transcription_Master_Prompt.md
- docs/articles/programmable-media-dotnet.md
- docs/articles/programmable-media-runtime-dotnet.md
- docs/assets/visual-parity/after.metrics.json
- docs/assets/visual-parity/after.timings.json
- docs/assets/visual-parity/before.metrics.json
- docs/assets/visual-parity/before.timings.json
- docs/assets/visual-parity/reference.metadata.json
- docs/av-stress-test.md
- docs/cli-visual-parity.md
- docs/melody-extraction-measurements.json
- docs/melody-extraction-plan.md
- docs/mixed-audio-measurements.json
- docs/net10-migration.md
- docs/nuget-end-to-end-validation.md
- docs/nuget-release-readiness.md
- docs/percussion-measurements.json
- docs/percussion-transcription-plan.md
- docs/playground-startup-investigation.md
- docs/polyphonic-real-measurements.json
- docs/polyphonic-synthetic-measurements.json
- docs/polyphonic-transcription-plan.md
- docs/transcription-acceptance-measurements.json
- docs/transcription-engineering-report.md
- docs/v13-release-readiness.md
- docs/v13-reliability-hardening.md
- docs/v14-acceptance-report.md
- docs/v14-artifact-hashes.json
- docs/v15-documentation-audit.md
- docs/v15-documentation-reliability-report.md
- docs/v15-requirements.json
- docs/v4-architecture.md
- docs/v4.1-cycle-synthesis.md
- docs/v4.1.1-timbre-tuning.md
- docs/v5-prosody-architecture.md
- docs/whats-new-v1.2.md
- docs/whats-new-v11.md
- docs/whats-new-v2.md
- docs/whats-new-v3.1.md
- docs/whats-new-v3.md
- docs/whats-new-v4.1.1.md
- docs/whats-new-v4.1.md
- docs/whats-new-v4.md
- docs/whats-new-v5.md
- docs/whats-new-v6.md
- docs/whats-new-v7.md
- docs/whats-new-v8.md
- docs/whats-new-v9.md

### Explicit exclusions

- docs/playground/SoundScript.Playground.staticwebassets.endpoints.json — Committed generated Playground output; validate source and a fresh publish instead.
- docs/playground/emcc-props.json — Committed generated Playground output; validate source and a fresh publish instead.
- docs/playground/index.html — Committed generated Playground output; validate source and a fresh publish instead.
- docs/playground/soundfont/ATTRIBUTION.md — Committed generated Playground output; validate source and a fresh publish instead.
- docs/playground/soundfont/manifest.json — Committed generated Playground output; validate source and a fresh publish instead.
- scripts/docs-state.test.mjs — Test fixture source intentionally contains invalid documentation claims to verify rejection.
- scripts/verify-docs-code.cjs — Test fixture source intentionally contains invalid documentation claims to verify rejection.
- docs/assets/marked.min.js — Vendored Markdown parser; existing dependency is not rewritten for documentation maintenance.
- docs/playground/css/app.css — Committed generated Playground output; validate source and a fresh publish instead.
- docs/playground/js/midi-player.js — Committed generated Playground output; validate source and a fresh publish instead.
- docs/playground/js/soundfont-loader.js — Committed generated Playground output; validate source and a fresh publish instead.

### Resolved findings and limits

The canonical hub, release state, generation, CI, link/CLI checks and code UX are complete.
Public facts stay at 14.0.0 while development is 15.0.0. Package validators now respect this
separation. No blocking audit issue remains. Syntax highlighting is deferred by design;
external availability and cross-browser/cross-OS verification are separately scoped.
Arbitrary prose still requires editorial review; deterministic validation handles defined
current install, link, identity, candidate and distribution forms.
