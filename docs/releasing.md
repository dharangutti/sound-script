# Release and package checklist

Development identity
comes from Directory.Build.props; public distribution comes from release-state.json.
See [documentation maintenance](documentation-maintenance.md) for schema, ownership
and the explicit publication-state update process.

## Publish and promote the library

1. On `main`, run **Publish SoundScript NuGet package** with `publish=true`.
2. The publication workflow validates and pushes the library package, then waits
   for the exact public version and verifies it with a fresh consumer before
   reporting success. With `publish=false`, green means validation only; the run
   summary explicitly says no upload occurred.
3. **Promote public release** checks the successful publication run and its actual
   publish step. A successful validation-only run cannot promote a release.
4. It reads the release identity from the publication commit, checks it against
   current `main`, and polls NuGet.org every 30 seconds for at most 60 minutes.
5. A fresh `net10.0` consumer with an empty package cache restores the exact public
   package, builds, and executes deterministic WAV, MIDI and visual media APIs.
6. The workflow generates a promotion PR updating public state, the exact release
   heading and generated documentation. Review the verification summary, approve
   generated PR workflow runs if requested, and merge after required checks pass.
7. The existing **Deploy SoundScript Site** workflow runs on the merge to `main`.
   Its staged homepage generator marks the public release as Current. A delayed
   site deployment does not invalidate the already-published NuGet package.

The promotion workflow never republishes a package, creates a CLI tag, or merges
its own PR. CLI channel flags reset when the public version changes; flags already
recorded for the same public version survive reruns.

Publishing uses the NuGet V3 service index. Its advertised upload resource still
uses `/api/v2/package`; seeing that URL in push logs is expected, not an obsolete
feed configuration. Upload acceptance precedes NuGet validation and indexing.
Public verification checks exact-version availability and restore; gallery/search
visibility can lag. Duplicate uploads remain safe to retry with `--skip-duplicate`,
but still require public consumer verification. See the
[NuGet publishing documentation](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package).

Release-note headings must match the version and codename in `Directory.Build.props`.
Promotion removes either `(unreleased)` or `(unpublished candidate)` from that
exact heading, preserving historical releases. The publication workflow tests
this contract before uploading.

### Manual recovery and verification

After the automation workflow is merged into `main`, open **Actions → Promote
public release → Run workflow**, select `main`, and enter the successful NuGet
publication run ID in `publication_run_id`. Equivalently:

```sh
gh workflow run promote-public-release.yml --ref main -f publication_run_id=PUBLICATION_RUN_ID
```

Use the run ID from the publication workflow URL, not a job ID. No additional NuGet
publication is needed. To verify a package locally, run
`pwsh -File scripts/verify-public-nuget.ps1 -Version VERSION`, replacing `VERSION`
with the exact published version. This performs network verification without
changing release state.

- **Indexing or network delay:** availability or consumer verification fails before
  any release-state edits. Rerun promotion with the same publication run ID later.
- **Already promoted:** the public consumer and documentation checks run again;
  clean state succeeds without a new branch or PR. Inconsistent existing state
  fails clearly and requires an explicit repair.
- **Existing PR:** the workflow reports the existing `automation/promote-VERSION`
  PR without overwriting it. Review that PR, or close it and delete its branch
  before regenerating. An orphan branch is reported without a force-push.
- **Main advanced:** a different development version, label or codename fails the
  publication identity check. A main-branch update during verification also stops
  PR creation; rerun against the reviewed current state. Do not override the
  publication version to force promotion.
- **Failed PR creation after push:** inspect the deterministic branch and open its
  PR manually, or delete the branch and rerun. No duplicate branch is force-pushed.

### GitHub permissions and review

In **Settings → Actions → General → Workflow permissions**, enable **Allow GitHub
Actions to create and approve pull requests** for automatic PR creation (subject
to organization policy). The workflow uses `GITHUB_TOKEN`; no PAT is required.
The repository's default token can stay read-only. Only the PR job receives
`contents: write` and `pull-requests: write`; only provenance resolution needs
`actions: read`. Consumer build/run executes in a separate read-only job.

GitHub may require a maintainer to approve workflow runs on a generated PR before
checks execute. Approving those checks is distinct from reviewing or merging the
PR. See [GitHub token behavior](https://docs.github.com/en/enterprise-cloud%40latest/actions/concepts/security/github_token).

The exact promotion diff allowlist is maintained in `scripts/release-promotion.mjs`.
It permits public state, release notes and current generated documentation,
including package README and Playground HTML metadata. Changes to runtime code,
workflows or the documentation manifest are rejected in generated promotion PRs.

<!-- GENERATED:CURRENT_DEVELOPMENT_VERSION_START -->
Development: **16.0.0 / V16 — Adaptive Runtime Parameters**. Development identity does not imply publication.
<!-- GENERATED:CURRENT_DEVELOPMENT_VERSION_END -->

<!-- GENERATED:CURRENT_PUBLIC_RELEASE_START -->
Current public version: **16.0.0**. Publication channels are recorded in `docs/release-state.json`.
<!-- GENERATED:CURRENT_PUBLIC_RELEASE_END -->

<!-- GENERATED:CLI_DISTRIBUTION_START -->
`SoundScript.Cli` 16.0.0 is not published on nuget.org.

Download a platform archive from [CLI 16.0.0](https://github.com/dharangutti/sound-script/releases/tag/v16.0.0), verify its SHA-256 checksum, extract it, and run `soundscript` from that directory.
<!-- GENERATED:CLI_DISTRIBUTION_END -->

## Verify from a clean checkout

Use the SDK selected by `global.json` and run the complete suite before a
release candidate:

```sh
pwsh scripts/validate-release.ps1
pwsh scripts/validate-release.ps1 -Configuration Debug
```

The script explicitly initializes the pinned wordbank submodule, restores,
builds the selected configuration, publishes the Playground into `artifacts/playground`,
then tests with that directory declared. Release needs no Debug output.
See [V13 Reliability & Release Hardening](v13-reliability-hardening.md) for
individual commands, dependency inventory, security boundaries and limitations.

The test workflow runs independent Debug and Release jobs on Windows, Ubuntu, and macOS. The CLI release
workflow produces self-contained archives for Windows x64, Linux x64, macOS
x64, and macOS arm64. Its archives are named with the ref/version and runtime
identifier and each has a SHA-256 checksum.

## Pack and smoke-test the SoundScript library locally

From a [source checkout with submodules](../README.md#try-it-in-60-seconds), pack the library to a local source and inspect it before any external release:

```sh
dotnet pack src/SoundScript/SoundScript.csproj -c Release --output artifacts/nuget
```

Keep the build step enabled: the bundled-assembly package targets currently do
not support `dotnet pack --no-build` (NETSDK1085).

The `.nupkg` should contain the `SoundScript` facade, bundled SoundScript
assemblies, XML documentation, package README, icon, license files, and
repository metadata. Install it only from the local source in a fresh
`net10.0` consumer as described in [NuGet](nuget.md). This does not publish to
nuget.org.

## Pack and smoke-test the .NET tool locally

Pack to a local source, then install it into a disposable tool path. This never
uses a global tool installation or NuGet.org:

```sh
dotnet pack src/SoundScript.Cli -c Release --output artifacts/nuget
dotnet tool install --tool-path artifacts/tool-smoke \
  --add-source artifacts/nuget SoundScript.Cli
```

Run the command through the generated shim (use the platform-appropriate shim
extension on Windows) and a representative export:

```sh
artifacts/tool-smoke/soundscript --version
artifacts/tool-smoke/soundscript run examples/blocks.ss --out artifacts/tool-smoke/blocks.mid
```

On PowerShell, invoke the shim with `&` (for example,
`& artifacts/tool-smoke/soundscript.exe --version`). The package metadata
includes its MIT license, repository URL, project URL, README, tags, package
identifier, and `soundscript` tool command.

## Before external publication

Do these only after explicit release approval:

1. Confirm ownership of the `SoundScript` and `SoundScript.Cli` package IDs and
   configure the `NUGET_API_KEY` repository secret in the release environment.
2. Review the version, release notes, local library and tool package smoke tests, four runtime
   archives, and their checksums.
3. Use the manually triggered NuGet workflow only when publication is intended;
   its `publish` input defaults to `false` and must be explicitly set to
   `true`.
4. Verify the resulting public package and the CLI global installation command
   in a clean environment.

The local build, pack, and tool-install commands above do not publish a
package, push a tag, create a GitHub Release, or deploy the website. No package
is considered published merely because a local `.nupkg` exists.
