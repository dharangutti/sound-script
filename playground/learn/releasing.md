# Release and package checklist

This checklist prepares a SoundScript release without publishing anything.
`Directory.Build.props` is the single source of truth for the package and CLI
version, label, and release name. A future versioned release updates its three
release properties together and describes changes in `RELEASE_NOTES.md`.
The V13 Reliability & Release Hardening pass retains version 13.0.0.

The public library package is the single `SoundScript` project at
`src/SoundScript/SoundScript.csproj`. It targets `net10.0`, bundles the reusable
component assemblies, and includes transcription. The CLI package and CLI
release archives remain separate products.
The [13.0.0 library is already published](https://www.nuget.org/packages/SoundScript/13.0.0);
[V13 CLI downloads](https://github.com/dharangutti/sound-script/releases/tag/v13.0.0)
and the [browser deployment](https://soundscript.net/playground/) exist.

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

Pack the library to a local source and inspect it before any external release:

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
