# Release and package checklist

This checklist prepares a SoundScript release without publishing anything.
`Directory.Build.props` is the single source of truth for the package and CLI
version, label, and release name. Update its three release properties together,
then describe the user-visible change in `RELEASE_NOTES.md`.

## Verify from a clean checkout

Use the SDK selected by `global.json` and run the complete suite before a
release candidate:

```sh
dotnet build SoundScript.sln -c Debug
dotnet test src/SoundScript.Tests -c Debug
dotnet publish src/SoundScript.Playground -c Release
```

The test workflow runs this suite on Windows, Ubuntu, and macOS. The CLI release
workflow produces self-contained archives for Windows x64, Linux x64, macOS
x64, and macOS arm64. Its archives are named with the ref/version and runtime
identifier and each has a SHA-256 checksum.

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

1. Confirm ownership of the `SoundScript.Cli` package ID and configure the
   NuGet.org API key in the release environment.
2. Review the version, release notes, local package smoke test, four runtime
   archives, and their checksums.
3. Push an approved `v<version>` tag to run the release workflow, then verify
   the resulting GitHub Release and artifacts.
4. Publish the reviewed `.nupkg` to NuGet.org and verify the public global
   installation command in a clean environment.

The local build, pack, and tool-install commands above do not publish a
package, push a tag, create a GitHub Release, or deploy the website.
