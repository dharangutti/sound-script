# V15 documentation reliability acceptance

Development identity: **15.0.0 / V15 — Documentation Reliability & Developer Experience**.
Public state: **14.0.0**, library NuGet published, CLI NuGet unpublished, CLI GitHub
Release published. V15 is **unreleased**. Baseline: 65976c6e4608ef91132b19a5fad334ba29cd2f89.

## Outcome and audit

One canonical structured hub is [documentation.md](documentation.md). The former
V11 [SoundScript.md](SoundScript.md) is a concise compatibility gateway. Viewer default,
navigation, footer, softwareHelp, canonical metadata and sitemap now use the canonical
hub. Goal-oriented entries link existing guides; historical reports have their own section.

The [persistent audit](v15-documentation-audit.md) records the pre-edit findings and
complete reviewed inventory. The [manifest](docs-manifest.json) classifies **200** scoped
files: **139 living**, **50 historical**, **11 explicitly excluded**, no unclassified or
conflicting files. Global scan exclusions cover artifacts, bin/obj, dependencies,
binary/temporary/build output and Labs. Explicit exclusions cover committed Playground
output, its vendored parser dependency and deliberately invalid test fixture sources.

Corrected: mixed V13/V13.0.2 installation guidance, V14 candidate wording in current
package/runtime/tutorial docs, V13 CLI downloads/release checklist, old software metadata,
development badge stamping, three broken heading references, and two package-validation
scripts that conflated development packages with public onboarding. Pinned sample versions
are labelled compatibility baselines. The old release reports, draft articles, historical
test counts, candidate wording and compatibility claims remain unchanged. Release notes
only gain an unreleased V15 section. 113 generic living fences now identify source, shell,
output or plain text; historical documents were not cosmetically normalized.

## Authoritative facts

| Fact | Sole authority |
| --- | --- |
| Development version, label, codename | Directory.Build.props |
| Public version and its publication channels | docs/release-state.json |
| Library package ID, framework, repository identity, site URL | src/SoundScript/SoundScript.csproj |
| CLI package ID, command name, framework | src/SoundScript.Cli/SoundScript.Cli.csproj |
| Public command registration | CliArguments.Commands in src/SoundScript.Cli/CliArguments.cs |
| Exception exit values | Diagnostics.ExitCode in src/SoundScript.Cli/Diagnostics.cs |
| Successful CLI exit | src/SoundScript.Cli/Program.cs and CommandHandlers.cs |
| Release archive platforms | .github/workflows/release-cli.yml |
| Release history | RELEASE_NOTES.md |
| Classification, exclusions and approved blocks | docs/docs-manifest.json |

## Release-state schema and invariants

~~~json
{
  "schemaVersion": 1,
  "publicVersion": "14.0.0",
  "library": { "nugetPublished": true },
  "cli": { "nugetPublished": false, "githubReleasePublished": true }
}
~~~

The executable schema validator rejects unsupported schema versions, missing/extra fields,
wrong types, invalid SemVer and public versions newer than development. Prerelease ordering
and build metadata semantics are handled. Flags apply to this publicVersion only. Disabled
channels cannot emit a public install command or release link; future CLI NuGet publication
automatically renders the version-pinned global-tool command. IDs and framework are read
from unambiguous unconditional project properties, never copied into release state.

Release notes remain authored history. The existing escaped homepage-history renderer
now defaults to public state and skips newer development sections; it is not used to infer
package IDs, install commands, publication flags or target frameworks.

## Generation and deterministic validation

[update-docs.ps1](../scripts/update-docs.ps1) delegates to the dependency-free Node helper
[docs-state.mjs](../scripts/docs-state.mjs), reusing the vendored Markdown tokenizer for links.
There are **26 generated blocks in 15 files**: LIBRARY_INSTALL, CLI_DISTRIBUTION,
CURRENT_PUBLIC_RELEASE, CURRENT_DEVELOPMENT_VERSION, DOTNET_REQUIREMENT,
LOCAL_LIBRARY_INSTALL, SITE_METADATA and PUBLIC_BADGE. Only mapped body slices are written.
Everything is validated and rendered in memory before mutation; a late failure writes nothing.

Check mode is read-only and fails on state/schema errors, unclassified/missing/conflicting
files, invalid exclusions/overrides, malformed/nested/mismatched/duplicate/reversed markers,
block drift, stale current install/link/identity claims, channel contradictions, command
coverage or exit-code mismatch, and broken living local links/fragments. Diagnostics include
file, category/check, actual condition, expected condition and corrective action. Success
returns zero; violations return nonzero. BOM, CRLF/LF and mixed-block conventions are tested;
unrelated bytes are preserved. No clock, network or machine-specific value drives output.

Historical documents never receive generated current-state blocks. Explicit historical
contexts permit genuine comparisons in living guides. Scope and patterns are deterministic;
explicit pattern conflicts require a justified override and explicit-list contradictions fail.

CLI validation reads actual registration: 14 public entries including visual, both vocal
subcommands and both wordbank subcommands. Help/version and option aliases are documented
separately, as are experimental transcription modes. Exit mappings are checked directly
against Diagnostics.ExitCode plus success zero; runtime values were not changed.

Links are checked offline: living Markdown targets, repository paths, heading fragments,
reference-style links and HTML docs destinations. Fenced code is not parsed as links.
Package/release links use canonical IDs and public versions. External availability is not
a required local/CI network dependency.

## Code presentation and accessibility

The original dark palette, typography, logo, major layout and navigation remain. Fenced
blocks gain a small language/Copy toolbar. Source, terminal and output containers differ
visually; output intentionally also receives Copy for logs. Inline code is unchanged.
All requested language labels/aliases are supported; unknown labels are assigned as text.

Code is created with DOM textContent, preserving source and intentional blank lines safely.
Copy uses only code text; there are no synthetic prompts or copied UI labels. Literal prompts
remain literal. Buttons work independently, expose descriptive names, announce Copied!
and reset. Clipboard denial tries a local text-area fallback, restores focus/selection and
reports failure without corrupting code. No UI permission request is issued. Markdown uses
LF internally; Windows may normalize the native clipboard to CRLF, verified separately from
the exact API argument. Copy buttons remain visible at 360px and long lines scroll inside
the code block. Native keyboard focus and reduced-motion behavior remain clear.

Syntax highlighting is intentionally deferred. Labels and code containers satisfy the
presentation goal without another dependency or redefining SoundScript grammar. Conditional
highlighting language/token requirements are explicitly N/A; escaping/security is tested.

## CI integration

| Workflow | Job | Step | Command |
| --- | --- | --- | --- |
| .github/workflows/tests.yml | test | Check documentation state and drift | ./scripts/update-docs.ps1 -Check |
| .github/workflows/tests.yml | test | Test documentation validation and homepage history | node --test scripts/docs-state.test.mjs scripts/homepage-release.test.cjs; node scripts/verify-v15-acceptance.mjs |
| .github/workflows/tests.yml | test (Linux) | Verify documentation code UX | node scripts/verify-docs-code.cjs |
| .github/workflows/deploy-pages.yml | deploy | Check public documentation state | ./scripts/update-docs.ps1 -Check |

Site staging also checks docs before building and supplies public state to homepage history.
No new parallel workflow was created and no remote deployment/publishing workflow was run.

## Validation results

Local environment: Windows, .NET SDK selected by global.json (10.0.303), Node 26.4.0,
PowerShell 7; browser verification used installed headless Edge. CI uses Node 22 and Chromium.

| Validation | Exact result |
| --- | --- |
| update-docs.ps1; repeated update; update-docs.ps1 -Check | PASS; 200 scoped files; second update changes zero files; public 14.0.0 / development 15.0.0 |
| node --test scripts/docs-state.test.mjs scripts/homepage-release.test.cjs scripts/playground-integrity.test.cjs scripts/playground-startup.test.cjs scripts/transcription-browser-input.test.cjs scripts/corpus-provenance.test.cjs | 65 passed, 0 failed, 0 skipped: 30 docs + 9 homepage + 26 existing browser/provenance tests |
| node scripts/verify-docs-code.cjs | PASS: eight independent blocks, all label aliases, exact clipboard input, literal prompts/blank lines, safe text, keyboard/focus, success/reset, fallback/failure, 360px layout, canonical/legacy navigation and public metadata |
| dotnet publish src/SoundScript.Playground -c Release --no-restore -p:PublishDir=.../artifacts/playground/ | Local artifact only; integrity PASS: 76 resources, 74 hashes, 156 compressed files |
| dotnet test src/SoundScript.Tests -c Release --no-restore --logger trx;LogFileName=v15-regression.trx | 1230 passed, 0 failed, 0 skipped (43 seconds test duration) |
| dotnet pack src/SoundScript -c Release --no-restore --output artifacts/v15-packages | Local SoundScript.15.0.0.nupkg and symbols created; nothing published |
| scripts/validate-nuget.ps1 and scripts/validate-media-package.ps1 on the local V15 package | PASS: package metadata, 14 bundled assembly XML/symbol pairs, fresh WAV/MIDI/transcription consumer and 73 corpus files; media consumer repeated 22 matching hashes and compiled/ran all three documentation examples |
| node scripts/verify-v15-acceptance.mjs | 157 unique requirements: 155 PASS, 2 justified N/A, no missing/duplicate/failing rows |
| git diff --check | PASS |

The first .NET attempt exposed missing local publish/submodule prerequisites and an old
literal V14 version assertion (1215 pass / 15 fail). Initializing the pinned wordbank,
creating a fresh local Playground publish and replacing the redundant literal with a
SemVer format check resolved these; the final complete run above passes. Browser verification
also exposed Markdown trailing-blank-line trimming, now corrected and independently tested.

Screenshots were visually reviewed at desktop and mobile sizes. Local evidence is in
artifacts/v15-docs/code-desktop.png and code-mobile.png. The regression TRX is in
src/SoundScript.Tests/TestResults/v15-regression.trx. Package logs are artifacts/v15-docs/package-validation.log and media-package-validation.log. Generated artifacts are not committed.

## Protection and remaining limits

Runtime behavior, public APIs, DSL grammar, transcription algorithms, and audio/MIDI/Wave/media
output are unchanged. Product identity intentionally reports the development version. No
production C# implementation or runtime dependency changed; only one version regression
test was adjusted. Playground source HTML changes are public metadata/navigation only;
its boot code is unchanged. Labs is untouched. No package, tag, GitHub Release, website
deployment or external article was published. Local build, pack and publish-to-artifacts
commands are validation, not external publication.

Validation cannot prove arbitrary prose correct. New phrasing and historical exceptions
still need review. External link availability is intentionally separate. Syntax highlighting
is deferred. Browser testing here covers Chromium/Edge, not a full Firefox/Safari matrix;
the existing CI .NET multi-OS matrix is configured but was not run remotely in this task.
Clipboard newline normalization belongs to the operating system. Existing corpus provenance
limitations remain documented and were not reclassified as verified by this release.

## Complete requirement matrix

The supplied specification was extracted into [v15-requirements.json](v15-requirements.json),
including its SHA-256 and all requirement text. The verifier compares the exact inventory,
including the A11Y IDs, with every row below and rejects omissions, duplicates and failures.
Evidence/status mappings were reviewed against the implementation and final results.

| Requirement | Evidence | Validation/inspection | Status | Notes |
| --- | --- | --- | --- | --- |
| REQ-BASE-001 | [docs/v15-documentation-audit.md](v15-documentation-audit.md) | Baseline inspection against 65976c6; canonical/viewer browser checks | PASS | Original modern hub, stale V11 hub/default and theme independently inspected. |
| REQ-BASE-002 | [docs/v15-documentation-audit.md](v15-documentation-audit.md) | Baseline inspection against 65976c6; canonical/viewer browser checks | PASS | Original modern hub, stale V11 hub/default and theme independently inspected. |
| REQ-BASE-003 | [docs/v15-documentation-audit.md](v15-documentation-audit.md) | Baseline inspection against 65976c6; canonical/viewer browser checks | PASS | Original modern hub, stale V11 hub/default and theme independently inspected. |
| REQ-BASE-004 | [docs/v15-documentation-audit.md](v15-documentation-audit.md) | Baseline inspection against 65976c6; canonical/viewer browser checks | PASS | Original modern hub, stale V11 hub/default and theme independently inspected. |
| REQ-VERSION-001 | [Directory.Build.props](../Directory.Build.props) | State tests; homepage public-state regression; release-note inspection | PASS | Development 15.0.0 / V15; public 14.0.0; V15 notes explicitly unreleased. |
| REQ-VERSION-002 | [Directory.Build.props](../Directory.Build.props) | State tests; homepage public-state regression; release-note inspection | PASS | Development 15.0.0 / V15; public 14.0.0; V15 notes explicitly unreleased. |
| REQ-VERSION-003 | [Directory.Build.props](../Directory.Build.props) | State tests; homepage public-state regression; release-note inspection | PASS | Development 15.0.0 / V15; public 14.0.0; V15 notes explicitly unreleased. |
| REQ-VERSION-004 | [Directory.Build.props](../Directory.Build.props) | State tests; homepage public-state regression; release-note inspection | PASS | Development 15.0.0 / V15; public 14.0.0; V15 notes explicitly unreleased. |
| REQ-SOURCE-001 | [docs/v15-documentation-audit.md](v15-documentation-audit.md) | Authoritative facts table and fail-closed literal-property reader | PASS | No IDs, framework, command name or repository identity duplicated in release state. |
| REQ-SOURCE-002 | [docs/v15-documentation-audit.md](v15-documentation-audit.md) | Authoritative facts table and fail-closed literal-property reader | PASS | No IDs, framework, command name or repository identity duplicated in release state. |
| REQ-SOURCE-003 | [docs/v15-documentation-audit.md](v15-documentation-audit.md) | Authoritative facts table and fail-closed literal-property reader | PASS | No IDs, framework, command name or repository identity duplicated in release state. |
| REQ-SOURCE-004 | [docs/v15-documentation-audit.md](v15-documentation-audit.md) | Authoritative facts table and fail-closed literal-property reader | PASS | No IDs, framework, command name or repository identity duplicated in release state. |
| REQ-STATE-001 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | validateState tests: fields/types/schema/SemVer/order; channel rendering tests | PASS | Flags describe the specific public version; prerelease precedence supported. |
| REQ-STATE-002 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | validateState tests: fields/types/schema/SemVer/order; channel rendering tests | PASS | Flags describe the specific public version; prerelease precedence supported. |
| REQ-STATE-003 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | validateState tests: fields/types/schema/SemVer/order; channel rendering tests | PASS | Flags describe the specific public version; prerelease precedence supported. |
| REQ-NOTES-001 | [scripts/update-homepage-release.ps1](../scripts/update-homepage-release.ps1) | Nine homepage tests and manual release-note diff review | PASS | Authored history retained; only public history selected; prose never supplies install state. |
| REQ-NOTES-002 | [scripts/update-homepage-release.ps1](../scripts/update-homepage-release.ps1) | Nine homepage tests and manual release-note diff review | PASS | Authored history retained; only public history selected; prose never supplies install state. |
| REQ-NOTES-003 | [scripts/update-homepage-release.ps1](../scripts/update-homepage-release.ps1) | Nine homepage tests and manual release-note diff review | PASS | Authored history retained; only public history selected; prose never supplies install state. |
| REQ-NOTES-004 | [scripts/update-homepage-release.ps1](../scripts/update-homepage-release.ps1) | Nine homepage tests and manual release-note diff review | PASS | Authored history retained; only public history selected; prose never supplies install state. |
| REQ-SCOPE-001 | [docs/docs-manifest.json](docs-manifest.json) | inventory plus classification check; scope exclusion tests | PASS | 200 files classified; build output, dependencies, temporary files and Labs excluded. |
| REQ-SCOPE-002 | [docs/docs-manifest.json](docs-manifest.json) | inventory plus classification check; scope exclusion tests | PASS | 200 files classified; build output, dependencies, temporary files and Labs excluded. |
| REQ-AUDIT-001 | [docs/v15-documentation-audit.md](v15-documentation-audit.md) | Persistent findings table, source map and reviewed-file inventory | PASS | Recorded before hub rewrite; final disposition and unchanged-file inventory appended. |
| REQ-AUDIT-002 | [docs/v15-documentation-audit.md](v15-documentation-audit.md) | Persistent findings table, source map and reviewed-file inventory | PASS | Recorded before hub rewrite; final disposition and unchanged-file inventory appended. |
| REQ-AUDIT-003 | [docs/v15-documentation-audit.md](v15-documentation-audit.md) | Persistent findings table, source map and reviewed-file inventory | PASS | Recorded before hub rewrite; final disposition and unchanged-file inventory appended. |
| REQ-DOC-001 | [docs/docs-manifest.json](docs-manifest.json) | Git diff reviewed against historical classification | PASS | Historical reports unchanged; only a new unreleased section prepended to release notes. |
| REQ-DOC-002 | [docs/docs-manifest.json](docs-manifest.json) | Git diff reviewed against historical classification | PASS | Historical reports unchanged; only a new unreleased section prepended to release notes. |
| REQ-DOC-003 | [docs/docs-manifest.json](docs-manifest.json) | Git diff reviewed against historical classification | PASS | Historical reports unchanged; only a new unreleased section prepended to release notes. |
| REQ-DOC-004 | [docs/docs-manifest.json](docs-manifest.json) | Git diff reviewed against historical classification | PASS | Historical reports unchanged; only a new unreleased section prepended to release notes. |
| REQ-MANIFEST-001 | [docs/docs-manifest.json](docs-manifest.json) | Manifest/classification tests and full repository check | PASS | Missing/unclassified/conflicting files and invalid exclusions fail; justified overrides explicit. |
| REQ-MANIFEST-002 | [docs/docs-manifest.json](docs-manifest.json) | Manifest/classification tests and full repository check | PASS | Missing/unclassified/conflicting files and invalid exclusions fail; justified overrides explicit. |
| REQ-MANIFEST-003 | [docs/docs-manifest.json](docs-manifest.json) | Manifest/classification tests and full repository check | PASS | Missing/unclassified/conflicting files and invalid exclusions fail; justified overrides explicit. |
| REQ-MANIFEST-004 | [docs/docs-manifest.json](docs-manifest.json) | Manifest/classification tests and full repository check | PASS | Missing/unclassified/conflicting files and invalid exclusions fail; justified overrides explicit. |
| REQ-MANIFEST-005 | [docs/docs-manifest.json](docs-manifest.json) | Manifest/classification tests and full repository check | PASS | Missing/unclassified/conflicting files and invalid exclusions fail; justified overrides explicit. |
| REQ-HUB-001 | [docs/documentation.md](documentation.md) | Hub validation, offline links and browser canonical/legacy navigation | PASS | Goal-first concise index; old URL remains a compatibility gateway; SEO/footer/sitemap updated. |
| REQ-HUB-002 | [docs/documentation.md](documentation.md) | Hub validation, offline links and browser canonical/legacy navigation | PASS | Goal-first concise index; old URL remains a compatibility gateway; SEO/footer/sitemap updated. |
| REQ-HUB-003 | [docs/documentation.md](documentation.md) | Hub validation, offline links and browser canonical/legacy navigation | PASS | Goal-first concise index; old URL remains a compatibility gateway; SEO/footer/sitemap updated. |
| REQ-HUB-004 | [docs/documentation.md](documentation.md) | Hub validation, offline links and browser canonical/legacy navigation | PASS | Goal-first concise index; old URL remains a compatibility gateway; SEO/footer/sitemap updated. |
| REQ-HUB-005 | [docs/documentation.md](documentation.md) | Hub validation, offline links and browser canonical/legacy navigation | PASS | Goal-first concise index; old URL remains a compatibility gateway; SEO/footer/sitemap updated. |
| REQ-HUB-006 | [docs/documentation.md](documentation.md) | Hub validation, offline links and browser canonical/legacy navigation | PASS | Goal-first concise index; old URL remains a compatibility gateway; SEO/footer/sitemap updated. |
| REQ-HUB-007 | [docs/documentation.md](documentation.md) | Hub validation, offline links and browser canonical/legacy navigation | PASS | Goal-first concise index; old URL remains a compatibility gateway; SEO/footer/sitemap updated. |
| REQ-HUB-008 | [docs/documentation.md](documentation.md) | Hub validation, offline links and browser canonical/legacy navigation | PASS | Goal-first concise index; old URL remains a compatibility gateway; SEO/footer/sitemap updated. |
| REQ-HUB-009 | [docs/documentation.md](documentation.md) | Hub validation, offline links and browser canonical/legacy navigation | PASS | Goal-first concise index; old URL remains a compatibility gateway; SEO/footer/sitemap updated. |
| REQ-HUB-010 | [docs/documentation.md](documentation.md) | Hub validation, offline links and browser canonical/legacy navigation | PASS | Goal-first concise index; old URL remains a compatibility gateway; SEO/footer/sitemap updated. |
| REQ-GEN-001 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | markerBlocks/render/generate tests; update twice and read-only check | PASS | 26 approved blocks in 15 files; no whole-guide generation or global version replacement. |
| REQ-GEN-002 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | markerBlocks/render/generate tests; update twice and read-only check | PASS | 26 approved blocks in 15 files; no whole-guide generation or global version replacement. |
| REQ-GEN-003 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | markerBlocks/render/generate tests; update twice and read-only check | PASS | 26 approved blocks in 15 files; no whole-guide generation or global version replacement. |
| REQ-GEN-004 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | markerBlocks/render/generate tests; update twice and read-only check | PASS | 26 approved blocks in 15 files; no whole-guide generation or global version replacement. |
| REQ-GEN-005 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | markerBlocks/render/generate tests; update twice and read-only check | PASS | 26 approved blocks in 15 files; no whole-guide generation or global version replacement. |
| REQ-TOOL-001 | [scripts/update-docs.ps1](../scripts/update-docs.ps1) | 30 documentation tests and successful PowerShell update/check | PASS | Node helper exposes independent validators; all validation precedes writes. |
| REQ-TOOL-002 | [scripts/update-docs.ps1](../scripts/update-docs.ps1) | 30 documentation tests and successful PowerShell update/check | PASS | Node helper exposes independent validators; all validation precedes writes. |
| REQ-TOOL-003 | [scripts/update-docs.ps1](../scripts/update-docs.ps1) | 30 documentation tests and successful PowerShell update/check | PASS | Node helper exposes independent validators; all validation precedes writes. |
| REQ-TOOL-004 | [scripts/update-docs.ps1](../scripts/update-docs.ps1) | 30 documentation tests and successful PowerShell update/check | PASS | Node helper exposes independent validators; all validation precedes writes. |
| REQ-TOOL-005 | [scripts/update-docs.ps1](../scripts/update-docs.ps1) | 30 documentation tests and successful PowerShell update/check | PASS | Node helper exposes independent validators; all validation precedes writes. |
| REQ-STALE-001 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | Current install/link/identity/candidate/channel tests | PASS | Historical contexts and legitimate old compatibility values remain valid. |
| REQ-STALE-002 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | Current install/link/identity/candidate/channel tests | PASS | Historical contexts and legitimate old compatibility values remain valid. |
| REQ-STALE-003 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | Current install/link/identity/candidate/channel tests | PASS | Historical contexts and legitimate old compatibility values remain valid. |
| REQ-DIST-001 | [docs/release-state.json](release-state.json) | All-channel rendering tests; future CLI NuGet command test | PASS | Existing tool packaging retained; public CLI uses GitHub archives while NuGet is false. |
| REQ-DIST-002 | [docs/release-state.json](release-state.json) | All-channel rendering tests; future CLI NuGet command test | PASS | Existing tool packaging retained; public CLI uses GitHub archives while NuGet is false. |
| REQ-DIST-003 | [docs/release-state.json](release-state.json) | All-channel rendering tests; future CLI NuGet command test | PASS | Existing tool packaging retained; public CLI uses GitHub archives while NuGet is false. |
| REQ-DIST-004 | [docs/release-state.json](release-state.json) | All-channel rendering tests; future CLI NuGet command test | PASS | Existing tool packaging retained; public CLI uses GitHub archives while NuGet is false. |
| REQ-STYLE-001 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Theme retained; terminal accent, labelled output with Copy, no decorative prompts. |
| REQ-STYLE-002 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Theme retained; terminal accent, labelled output with Copy, no decorative prompts. |
| REQ-STYLE-003 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Theme retained; terminal accent, labelled output with Copy, no decorative prompts. |
| REQ-STYLE-004 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Theme retained; terminal accent, labelled output with Copy, no decorative prompts. |
| REQ-STYLE-005 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Theme retained; terminal accent, labelled output with Copy, no decorative prompts. |
| REQ-STYLE-006 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | No synthetic prompt decoration is added. Browser test confirms literal PS> is copied and no prompt is invented. |
| REQ-STYLE-007 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Theme retained; terminal accent, labelled output with Copy, no decorative prompts. |
| REQ-STYLE-008 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Output blocks intentionally retain Copy for logs; documented as Output, never a runnable-command label. |
| REQ-STYLE-009 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Existing inline-code CSS retained; browser test confirms inline identifiers get no Copy button. |
| REQ-HIGHLIGHT-001 | [docs/documentation-maintenance.md](documentation-maintenance.md) | Dependency diff and browser malicious code-text checks | PASS | Highlighting deferred; DOM textContent escaping preserves safe code presentation. |
| REQ-HIGHLIGHT-002 | [docs/documentation-maintenance.md](documentation-maintenance.md) | Dependency diff and browser malicious code-text checks | N/A | Syntax highlighting was intentionally deferred; labels and safe code containers are implemented without a new dependency or grammar. |
| REQ-HIGHLIGHT-003 | [docs/documentation-maintenance.md](documentation-maintenance.md) | Dependency diff and browser malicious code-text checks | N/A | Syntax highlighting was intentionally deferred; labels and safe code containers are implemented without a new dependency or grammar. |
| REQ-HIGHLIGHT-004 | [docs/documentation-maintenance.md](documentation-maintenance.md) | Dependency diff and browser malicious code-text checks | PASS | Highlighting deferred; DOM textContent escaping preserves safe code presentation. |
| REQ-A11Y-001 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser keyboard Enter/focus test; live success/failure state; reduced-motion check | PASS | Native buttons, descriptive accessible names and visible non-color feedback. |
| REQ-A11Y-002 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser keyboard Enter/focus test; live success/failure state; reduced-motion check | PASS | Native buttons, descriptive accessible names and visible non-color feedback. |
| REQ-A11Y-003 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser keyboard Enter/focus test; live success/failure state; reduced-motion check | PASS | Native buttons, descriptive accessible names and visible non-color feedback. |
| REQ-A11Y-004 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser keyboard Enter/focus test; live success/failure state; reduced-motion check | PASS | Native buttons, descriptive accessible names and visible non-color feedback. |
| REQ-A11Y-005 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser keyboard Enter/focus test; live success/failure state; reduced-motion check | PASS | Native buttons, descriptive accessible names and visible non-color feedback. |
| REQ-MOBILE-001 | [docs/assets/docs-code.css](assets/docs-code.css) | Browser 360px viewport: no document overflow; pre scrolls; buttons within viewport | PASS | Toolbar is outside code scrolling; long lines never forced to wrap. |
| REQ-MOBILE-002 | [docs/assets/docs-code.css](assets/docs-code.css) | Browser 360px viewport: no document overflow; pre scrolls; buttons within viewport | PASS | Toolbar is outside code scrolling; long lines never forced to wrap. |
| REQ-MOBILE-003 | [docs/assets/docs-code.css](assets/docs-code.css) | Browser 360px viewport: no document overflow; pre scrolls; buttons within viewport | PASS | Toolbar is outside code scrolling; long lines never forced to wrap. |
| REQ-COPY-001 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser native clipboard, exact API argument, fallback and denial checks | PASS | Copies only code; preserves blank lines and literal prompts; no permission request in UI. |
| REQ-COPY-002 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser native clipboard, exact API argument, fallback and denial checks | PASS | Copies only code; preserves blank lines and literal prompts; no permission request in UI. |
| REQ-COPY-003 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser native clipboard, exact API argument, fallback and denial checks | PASS | Copies only code; preserves blank lines and literal prompts; no permission request in UI. |
| REQ-COPY-004 | [docs/assets/docs-code.js](assets/docs-code.js) | Browser native clipboard, exact API argument, fallback and denial checks | PASS | Copies only code; preserves blank lines and literal prompts; no permission request in UI. |
| REQ-FENCE-001 | [docs/docs-manifest.json](docs-manifest.json) | Living Markdown tokenizer audit and historical diff review | PASS | 113 generic living fences labelled by content; historical evidence not cosmetically rewritten. |
| REQ-FENCE-002 | [docs/docs-manifest.json](docs-manifest.json) | Living Markdown tokenizer audit and historical diff review | PASS | 113 generic living fences labelled by content; historical evidence not cosmetically rewritten. |
| REQ-CLI-001 | [src/SoundScript.Cli/CliArguments.cs](../src/SoundScript.Cli/CliArguments.cs) | validateCli compares all 14 registrations with public coverage table | PASS | Includes visual, both vocal and both wordbank commands; aliases/experimental modes documented. |
| REQ-CLI-002 | [src/SoundScript.Cli/CliArguments.cs](../src/SoundScript.Cli/CliArguments.cs) | validateCli compares all 14 registrations with public coverage table | PASS | Includes visual, both vocal and both wordbank commands; aliases/experimental modes documented. |
| REQ-CLI-003 | [src/SoundScript.Cli/CliArguments.cs](../src/SoundScript.Cli/CliArguments.cs) | validateCli compares all 14 registrations with public coverage table | PASS | Includes visual, both vocal and both wordbank commands; aliases/experimental modes documented. |
| REQ-EXIT-001 | [src/SoundScript.Cli/Diagnostics.cs](../src/SoundScript.Cli/Diagnostics.cs) | Every exception/value mapping and success value checked in docs | PASS | Runtime values unchanged; docs clarify success includes commands without media output. |
| REQ-EXIT-002 | [src/SoundScript.Cli/Diagnostics.cs](../src/SoundScript.Cli/Diagnostics.cs) | Every exception/value mapping and success value checked in docs | PASS | Runtime values unchanged; docs clarify success includes commands without media output. |
| REQ-EXIT-003 | [src/SoundScript.Cli/Diagnostics.cs](../src/SoundScript.Cli/Diagnostics.cs) | Every exception/value mapping and success value checked in docs | PASS | Runtime values unchanged; docs clarify success includes commands without media output. |
| REQ-LINK-001 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | Offline Markdown target/fragment tests and docs navigation checks | PASS | No required network availability checks; generated package/release IDs and versions derive from facts. |
| REQ-LINK-002 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | Offline Markdown target/fragment tests and docs navigation checks | PASS | No required network availability checks; generated package/release IDs and versions derive from facts. |
| REQ-META-001 | [docs/doc.html](doc.html) | Public metadata browser assertion and generated SITE_METADATA drift check | PASS | All current static software metadata generated; dynamic viewer reuses its static public value. |
| REQ-META-002 | [docs/doc.html](doc.html) | Public metadata browser assertion and generated SITE_METADATA drift check | PASS | All current static software metadata generated; dynamic viewer reuses its static public value. |
| REQ-META-003 | [docs/doc.html](doc.html) | Public metadata browser assertion and generated SITE_METADATA drift check | PASS | All current static software metadata generated; dynamic viewer reuses its static public value. |
| REQ-IA-001 | [docs/documentation.md](documentation.md) | Desktop/mobile screenshots and page diff review | PASS | Existing palette, logo, layout, cards, navigation and developer journey preserved. |
| REQ-IA-002 | [docs/documentation.md](documentation.md) | Desktop/mobile screenshots and page diff review | PASS | Existing palette, logo, layout, cards, navigation and developer journey preserved. |
| REQ-IA-003 | [docs/documentation.md](documentation.md) | Desktop/mobile screenshots and page diff review | PASS | Existing palette, logo, layout, cards, navigation and developer journey preserved. |
| REQ-CI-001 | [.github/workflows/tests.yml](../.github/workflows/tests.yml) | Workflow inspection: test job checks drift, tests docs and browser UX | PASS | Existing workflow extended; deployment preparation also checks state; no workflow executed remotely. |
| REQ-CI-002 | [.github/workflows/tests.yml](../.github/workflows/tests.yml) | Workflow inspection: test job checks drift, tests docs and browser UX | PASS | Existing workflow extended; deployment preparation also checks state; no workflow executed remotely. |
| REQ-CI-003 | [.github/workflows/tests.yml](../.github/workflows/tests.yml) | Workflow inspection: test job checks drift, tests docs and browser UX | PASS | Workflow .github/workflows/tests.yml; job test; step Check documentation state and drift; command ./scripts/update-docs.ps1 -Check. |
| REQ-DIAG-001 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | Failure fixtures assert categories; malformed-JSON diagnostic test; wrapper exit handling | PASS | Messages include file/category/check, actual, expected and corrective action; errors exit nonzero. |
| REQ-DIAG-002 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | Failure fixtures assert categories; malformed-JSON diagnostic test; wrapper exit handling | PASS | Messages include file/category/check, actual, expected and corrective action; errors exit nonzero. |
| REQ-ENCODING-001 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | BOM, CRLF, mixed endings, idempotency and byte-immutability tests | PASS | Only body slices written; deterministic offline rendering; surrounding bytes retained. |
| REQ-ENCODING-002 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | BOM, CRLF, mixed endings, idempotency and byte-immutability tests | PASS | Only body slices written; deterministic offline rendering; surrounding bytes retained. |
| REQ-ENCODING-003 | [scripts/docs-state.mjs](../scripts/docs-state.mjs) | BOM, CRLF, mixed endings, idempotency and byte-immutability tests | PASS | Only body slices written; deterministic offline rendering; surrounding bytes retained. |
| REQ-SAFE-001 | [Directory.Build.props](../Directory.Build.props) | Production/runtime diff review; 1230 .NET regression tests pass | PASS | Only development metadata, documentation HTML and one version test changed under product configuration/source. |
| REQ-SAFE-002 | [Directory.Build.props](../Directory.Build.props) | Production/runtime diff review; 1230 .NET regression tests pass | PASS | Only development metadata, documentation HTML and one version test changed under product configuration/source. |
| REQ-SAFE-003 | [Directory.Build.props](../Directory.Build.props) | Production/runtime diff review; 1230 .NET regression tests pass | PASS | Only development metadata, documentation HTML and one version test changed under product configuration/source. |
| REQ-SAFE-004 | [Directory.Build.props](../Directory.Build.props) | Production/runtime diff review; 1230 .NET regression tests pass | PASS | Only development metadata, documentation HTML and one version test changed under product configuration/source. |
| REQ-SAFE-005 | [Directory.Build.props](../Directory.Build.props) | Production/runtime diff review; 1230 .NET regression tests pass | PASS | No files in experiments/SoundScript.Labs/ changed; excluded from deterministic scan. |
| REQ-SAFE-006 | [Directory.Build.props](../Directory.Build.props) | Production/runtime diff review; 1230 .NET regression tests pass | PASS | Only local build/pack/publish-to-artifacts and validation ran. No package, tag, GitHub Release, site or article was published. |
| REQ-SAFE-007 | [Directory.Build.props](../Directory.Build.props) | Production/runtime diff review; 1230 .NET regression tests pass | PASS | Only development metadata, documentation HTML and one version test changed under product configuration/source. |
| REQ-TEST-STATE-001 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | validateState tests: fields/types/schema/SemVer/order; channel rendering tests | PASS | Flags describe the specific public version; prerelease precedence supported. |
| REQ-TEST-STATE-002 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | validateState tests: fields/types/schema/SemVer/order; channel rendering tests | PASS | Flags describe the specific public version; prerelease precedence supported. |
| REQ-TEST-STATE-003 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | validateState tests: fields/types/schema/SemVer/order; channel rendering tests | PASS | Flags describe the specific public version; prerelease precedence supported. |
| REQ-TEST-STATE-004 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | validateState tests: fields/types/schema/SemVer/order; channel rendering tests | PASS | Flags describe the specific public version; prerelease precedence supported. |
| REQ-TEST-STATE-005 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | validateState tests: fields/types/schema/SemVer/order; channel rendering tests | PASS | Flags describe the specific public version; prerelease precedence supported. |
| REQ-TEST-MANIFEST-001 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | Manifest/classification tests and full repository check | PASS | Missing/unclassified/conflicting files and invalid exclusions fail; justified overrides explicit. |
| REQ-TEST-MANIFEST-002 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | Manifest/classification tests and full repository check | PASS | Missing/unclassified/conflicting files and invalid exclusions fail; justified overrides explicit. |
| REQ-TEST-MANIFEST-003 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | Manifest/classification tests and full repository check | PASS | Missing/unclassified/conflicting files and invalid exclusions fail; justified overrides explicit. |
| REQ-TEST-MANIFEST-004 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | Manifest/classification tests and full repository check | PASS | Missing/unclassified/conflicting files and invalid exclusions fail; justified overrides explicit. |
| REQ-TEST-GEN-001 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | markerBlocks/render/generate tests; update twice and read-only check | PASS | 26 approved blocks in 15 files; no whole-guide generation or global version replacement. |
| REQ-TEST-GEN-002 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | markerBlocks/render/generate tests; update twice and read-only check | PASS | 26 approved blocks in 15 files; no whole-guide generation or global version replacement. |
| REQ-TEST-GEN-003 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | markerBlocks/render/generate tests; update twice and read-only check | PASS | 26 approved blocks in 15 files; no whole-guide generation or global version replacement. |
| REQ-TEST-GEN-004 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | markerBlocks/render/generate tests; update twice and read-only check | PASS | 26 approved blocks in 15 files; no whole-guide generation or global version replacement. |
| REQ-TEST-GEN-005 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | markerBlocks/render/generate tests; update twice and read-only check | PASS | 26 approved blocks in 15 files; no whole-guide generation or global version replacement. |
| REQ-TEST-GEN-006 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | markerBlocks/render/generate tests; update twice and read-only check | PASS | 26 approved blocks in 15 files; no whole-guide generation or global version replacement. |
| REQ-TEST-HIST-001 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | Git diff reviewed against historical classification | PASS | Historical reports unchanged; only a new unreleased section prepended to release notes. |
| REQ-TEST-HIST-002 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | Git diff reviewed against historical classification | PASS | Historical reports unchanged; only a new unreleased section prepended to release notes. |
| REQ-TEST-HIST-003 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | Git diff reviewed against historical classification | PASS | Historical reports unchanged; only a new unreleased section prepended to release notes. |
| REQ-TEST-HUB-001 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | Hub validation, offline links and browser canonical/legacy navigation | PASS | Goal-first concise index; old URL remains a compatibility gateway; SEO/footer/sitemap updated. |
| REQ-TEST-HUB-002 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | Hub validation, offline links and browser canonical/legacy navigation | PASS | Goal-first concise index; old URL remains a compatibility gateway; SEO/footer/sitemap updated. |
| REQ-TEST-HUB-003 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | Hub validation, offline links and browser canonical/legacy navigation | PASS | Goal-first concise index; old URL remains a compatibility gateway; SEO/footer/sitemap updated. |
| REQ-TEST-HUB-004 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | Hub validation, offline links and browser canonical/legacy navigation | PASS | Goal-first concise index; old URL remains a compatibility gateway; SEO/footer/sitemap updated. |
| REQ-TEST-HUB-005 | [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | Hub validation, offline links and browser canonical/legacy navigation | PASS | Goal-first concise index; old URL remains a compatibility gateway; SEO/footer/sitemap updated. |
| REQ-TEST-STYLE-001 | [scripts/verify-docs-code.cjs](../scripts/verify-docs-code.cjs) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Theme retained; terminal accent, labelled output with Copy, no decorative prompts. |
| REQ-TEST-STYLE-002 | [scripts/verify-docs-code.cjs](../scripts/verify-docs-code.cjs) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Theme retained; terminal accent, labelled output with Copy, no decorative prompts. |
| REQ-TEST-STYLE-003 | [scripts/verify-docs-code.cjs](../scripts/verify-docs-code.cjs) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Theme retained; terminal accent, labelled output with Copy, no decorative prompts. |
| REQ-TEST-STYLE-004 | [scripts/verify-docs-code.cjs](../scripts/verify-docs-code.cjs) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Theme retained; terminal accent, labelled output with Copy, no decorative prompts. |
| REQ-TEST-STYLE-005 | [scripts/verify-docs-code.cjs](../scripts/verify-docs-code.cjs) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Theme retained; terminal accent, labelled output with Copy, no decorative prompts. |
| REQ-TEST-STYLE-006 | [scripts/verify-docs-code.cjs](../scripts/verify-docs-code.cjs) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Theme retained; terminal accent, labelled output with Copy, no decorative prompts. |
| REQ-TEST-STYLE-007 | [scripts/verify-docs-code.cjs](../scripts/verify-docs-code.cjs) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Theme retained; terminal accent, labelled output with Copy, no decorative prompts. |
| REQ-TEST-STYLE-008 | [scripts/verify-docs-code.cjs](../scripts/verify-docs-code.cjs) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Theme retained; terminal accent, labelled output with Copy, no decorative prompts. |
| REQ-TEST-STYLE-009 | [scripts/verify-docs-code.cjs](../scripts/verify-docs-code.cjs) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Theme retained; terminal accent, labelled output with Copy, no decorative prompts. |
| REQ-TEST-STYLE-010 | [scripts/verify-docs-code.cjs](../scripts/verify-docs-code.cjs) | Browser verification: eight blocks, all label aliases, exact text and inline code | PASS | Theme retained; terminal accent, labelled output with Copy, no decorative prompts. |
| REQ-TEST-REG-001 | [src/SoundScript.Tests/CliProductTests.cs](../src/SoundScript.Tests/CliProductTests.cs) | 65 Node tests, 1230 .NET tests, browser checks and git diff --check | PASS | All nine homepage history tests pass, including V15 development plus V14 public state. |
| REQ-TEST-REG-002 | [src/SoundScript.Tests/CliProductTests.cs](../src/SoundScript.Tests/CliProductTests.cs) | 65 Node tests, 1230 .NET tests, browser checks and git diff --check | PASS | 65 total Node tests pass: 30 docs, 9 homepage, 26 existing Playground/transcription/provenance tests. |
| REQ-TEST-REG-003 | [src/SoundScript.Tests/CliProductTests.cs](../src/SoundScript.Tests/CliProductTests.cs) | 65 Node tests, 1230 .NET tests, browser checks and git diff --check | PASS | Windows Release suite: 1230 passed, 0 failed, 0 skipped; clean local Playground prerequisites prepared. |
| REQ-TEST-REG-004 | [src/SoundScript.Tests/CliProductTests.cs](../src/SoundScript.Tests/CliProductTests.cs) | 65 Node tests, 1230 .NET tests, browser checks and git diff --check | PASS | Final successful runs; initial missing prerequisites and version assertion were corrected. |
| REQ-TEST-REG-005 | [src/SoundScript.Tests/CliProductTests.cs](../src/SoundScript.Tests/CliProductTests.cs) | 65 Node tests, 1230 .NET tests, browser checks and git diff --check | PASS | Final successful runs; initial missing prerequisites and version assertion were corrected. |
| REQ-ACCEPT-001 | [scripts/verify-v15-acceptance.mjs](../scripts/verify-v15-acceptance.mjs) | 157-ID extraction and exact unique matrix inventory check | PASS | Full matrix, changed-file reasons, validation evidence and explicit limitations included. |
| REQ-ACCEPT-002 | [scripts/verify-v15-acceptance.mjs](../scripts/verify-v15-acceptance.mjs) | 157-ID extraction and exact unique matrix inventory check | PASS | Full matrix, changed-file reasons, validation evidence and explicit limitations included. |
| REQ-ACCEPT-003 | [scripts/verify-v15-acceptance.mjs](../scripts/verify-v15-acceptance.mjs) | 157-ID extraction and exact unique matrix inventory check | PASS | Full matrix, changed-file reasons, validation evidence and explicit limitations included. |

## Complete changed-file list

Compared with the clean baseline, every changed or newly added repository file is listed.
Ignored validation outputs and unchanged submodule checkout contents are not changes.

| File | Reason |
| --- | --- |
| [.github/workflows/deploy-pages.yml](../.github/workflows/deploy-pages.yml) | Replace development-version badge stamping with public documentation state check. |
| [.github/workflows/tests.yml](../.github/workflows/tests.yml) | Add drift check, documentation/homepage tests, acceptance inventory and browser code UX to existing test job. |
| [Directory.Build.props](../Directory.Build.props) | Set development identity to 15.0.0 / V15 and clarify independent public state. |
| [README.md](../README.md) | Generate public installation/distribution and public/development identity; remove duplicate hardcoded claims. |
| [RELEASE_NOTES.md](../RELEASE_NOTES.md) | Prepend actual V15 work as unreleased; preserve prior release history. |
| [docs/404.html](404.html) | Generate current public structured metadata and correct legacy documentation entry links. |
| [docs/SoundScript.md](SoundScript.md) | Replace stale V11 full index with inbound-compatible gateway. |
| [docs/advanced-chords.md](advanced-chords.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/application-samples.md](application-samples.md) | Clarify existing 13.0.2 package pins are compatibility baselines, not current onboarding. |
| [docs/architecture.md](architecture.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/assets/docs-code.css](assets/docs-code.css) | Use existing theme tokens for source/terminal/output, focus and mobile scrolling. |
| [docs/assets/docs-code.js](assets/docs-code.js) | Safe code containers, exact text, labels, native/fallback copy and consistent heading IDs. |
| [docs/blocks.md](blocks.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/cli.md](cli.md) | Current distribution block, correct success wording, command/exit authority tables and typed fences. |
| [docs/common-tasks.md](common-tasks.md) | Repair TestFixtureGenerator heading fragment. |
| [docs/contact/index.html](contact/index.html) | Generate current public structured metadata and correct legacy documentation entry links. |
| [docs/doc.html](doc.html) | Canonical default/nav/SEO, public metadata, safe code rendering and accessible enhancement. |
| [docs/docs-manifest.json](docs-manifest.json) | Declare complete classifications, exclusions and allowed generated blocks. |
| [docs/documentation-maintenance.md](documentation-maintenance.md) | Document schema, ownership, tooling, history exceptions, fence/copy rules and highlighting decision. |
| [docs/documentation.md](documentation.md) | Establish canonical goal-first index with separated historical evidence. |
| [docs/examples.md](examples.md) | Correct wave-reference fragment to existing V8 heading and label known code fences. |
| [docs/expressive-notation.md](expressive-notation.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/humanization.md](humanization.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/imports.md](imports.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/index.html](index.html) | Generate public version badge/structured metadata and canonical help URL without changing layout or history. |
| [docs/industrial/index.html](industrial/index.html) | Generate current public structured metadata and correct legacy documentation entry links. |
| [docs/language-reference.md](language-reference.md) | Correct wave-reference fragment to existing V8 heading and label known code fences. |
| [docs/layers.md](layers.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/musical-intelligence.md](musical-intelligence.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/notation.md](notation.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/nuget-end-to-end.md](nuget-end-to-end.md) | Generate current install and explain pinned sample compatibility baseline. |
| [docs/nuget.md](nuget.md) | Replace old published/candidate advice with generated public install, framework and local development commands. |
| [docs/orchestration.md](orchestration.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/patterns.md](patterns.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/phase8-wordbank-vocal.md](phase8-wordbank-vocal.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/phoneme-composer.md](phoneme-composer.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/phrases-v3.md](phrases-v3.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/phrases.md](phrases.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/pipeline.md](pipeline.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/playback-quality.md](playback-quality.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/programmatic-media-runtime.md](programmatic-media-runtime.md) | Remove stale V14 candidate claim; link public install and generate framework requirement. |
| [docs/quick-start.md](quick-start.md) | Replace stale V13 install with generated public library/CLI/framework facts. |
| [docs/release-state.json](release-state.json) | Explicit version-specific public distribution state independent of development. |
| [docs/releasing.md](releasing.md) | Separate development from public state and link documentation maintenance workflow. |
| [docs/sitemap.xml](sitemap.xml) | Point documentation entry at canonical hub. |
| [docs/soundcss.md](soundcss.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/stabilization.md](stabilization.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/tempo-automation.md](tempo-automation.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/text-to-melody.md](text-to-melody.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/timbre-engine.md](timbre-engine.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/track-metadata.md](track-metadata.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/tutorials/industrial-monitoring.md](tutorials/industrial-monitoring.md) | Clarify existing 13.0.2 package pins are compatibility baselines, not current onboarding. |
| [docs/tutorials/media-round-trip.md](tutorials/media-round-trip.md) | Clarify existing 13.0.2 package pins are compatibility baselines, not current onboarding. |
| [docs/tutorials/programmable-media.md](tutorials/programmable-media.md) | Replace local V14 candidate onboarding with public package guidance. |
| [docs/user-guide.md](user-guide.md) | Link canonical documentation hub and label source/output fences. |
| [docs/v15-documentation-audit.md](v15-documentation-audit.md) | Persistent baseline, source ownership, findings, classifications and unchanged-file review. |
| [docs/v15-documentation-reliability-report.md](v15-documentation-reliability-report.md) | Full acceptance evidence, every requirement and every changed-file reason. |
| [docs/v15-requirements.json](v15-requirements.json) | Preserve all 157 extracted requirement IDs/text and specification SHA-256. |
| [docs/vocal.md](vocal.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/wave-grammar.md](wave-grammar.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [docs/word-prosody.md](word-prosody.md) | Label previously generic living code/diagram/output fences; content semantics unchanged. |
| [packaging/README.md](../packaging/README.md) | Replace local candidate onboarding with generated public install and framework requirement. |
| [samples/IndustrialMonitoring/README.md](../samples/IndustrialMonitoring/README.md) | Clarify existing 13.0.2 package pins are compatibility baselines, not current onboarding. |
| [samples/MediaRoundTrip/README.md](../samples/MediaRoundTrip/README.md) | Clarify existing 13.0.2 package pins are compatibility baselines, not current onboarding. |
| [samples/ProgrammableMediaWeb/README.md](../samples/ProgrammableMediaWeb/README.md) | Explain local packing follows inherited development version. |
| [scripts/docs-state.mjs](../scripts/docs-state.mjs) | Deterministic fact/state/classification/marker/current/link/CLI validators and body-only generator. |
| [scripts/docs-state.test.mjs](../scripts/docs-state.test.mjs) | 30 positive/negative tests for state, markers, rendering, drift, history and source boundaries. |
| [scripts/homepage-release.test.cjs](../scripts/homepage-release.test.cjs) | Add explicit public-state fixtures and prevent unreleased V15 promotion. |
| [scripts/publish-site.ps1](../scripts/publish-site.ps1) | Check docs before staging and pass explicit public release state to homepage history. |
| [scripts/update-docs.ps1](../scripts/update-docs.ps1) | Portable PowerShell update/check entry point with propagated failure status. |
| [scripts/update-homepage-release.ps1](../scripts/update-homepage-release.ps1) | Default existing safe history renderer to public state and skip newer development notes. |
| [scripts/validate-media-package.ps1](../scripts/validate-media-package.ps1) | Read expected development version from props instead of requiring V14; use neutral artifact paths. |
| [scripts/validate-nuget.ps1](../scripts/validate-nuget.ps1) | Validate packaged README against canonical public documentation independently of development package version. |
| [scripts/verify-docs-code.cjs](../scripts/verify-docs-code.cjs) | Real-browser code UX, clipboard, safety, accessibility and navigation verification. |
| [scripts/verify-v15-acceptance.mjs](../scripts/verify-v15-acceptance.mjs) | Reject missing/duplicate requirement rows, failures and unjustified N/A entries. |
| [src/SoundScript.Playground/wwwroot/index.html](../src/SoundScript.Playground/wwwroot/index.html) | Generate public software metadata and canonical Docs navigation; preserve runtime boot code. |
| [src/SoundScript.Tests/CliProductTests.cs](../src/SoundScript.Tests/CliProductTests.cs) | Replace hardcoded V14 expectation with semantic format check while retaining VersionInfo equality. |
