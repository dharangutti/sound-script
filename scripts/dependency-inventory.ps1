param([string]$OutputDirectory = 'artifacts/dependency-inventory')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $repoRoot
try {
    $destination = [IO.Path]::GetFullPath($OutputDirectory)
    New-Item -ItemType Directory -Force $destination | Out-Null
    # The caller restores/builds Release first. JSON schema version is explicit.
    $inventory = & dotnet package list --project SoundScript.sln --include-transitive --format json --output-version 1 --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Dependency inventory failed; restore/build the solution first.' }
    $json = $inventory -join "`n"
    $parsed = $json | ConvertFrom-Json
    if (!$parsed.projects) { throw 'No project dependency inventory was produced.' }
    Set-Content -LiteralPath (Join-Path $destination 'nuget-dependencies.json') -Value $json -Encoding utf8
    Copy-Item -LiteralPath 'scripts/package-lock.json' -Destination (Join-Path $destination 'browser-tools.package-lock.json')
    $actions = foreach ($workflow in Get-ChildItem -LiteralPath '.github/workflows' -File) {
        foreach ($line in Get-Content -LiteralPath $workflow.FullName) {
            if ($line -match '^\s*(?:-\s*)?uses:\s*([^\s#]+)') {
                $reference = $Matches[1]
                [ordered]@{ workflow = $workflow.Name; reference = $reference; immutableSha = $reference -match '@[0-9a-fA-F]{40}$' }
            }
        }
    }
    ConvertTo-Json -InputObject @($actions) -Depth 4 | Set-Content -LiteralPath (Join-Path $destination 'workflow-actions.json') -Encoding utf8
    $commit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot record repository commit.' }
    $metadata = [ordered]@{
        kind = 'dependency-inventory'; schemaVersion = 1; configuration = 'Release'; commit = $commit
        workingTreeStatus = @(& git status --porcelain); sdk = (& dotnet --version).Trim()
        submodules = @(& git submodule status); node = (& node --version).Trim()
        scope = 'Solution NuGet direct/transitive packages and browser verification tooling lockfile; not a complete SPDX/CycloneDX SBOM.'
        exclusions = @('OS and .NET shared runtime', 'user-installed FFmpeg/eSpeak', 'corpus audio rights', 'vendored JS/fonts/soundfonts', 'Labs')
    }
    $metadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $destination 'build-context.json') -Encoding utf8
    Write-Host "Dependency inventory written to $destination"
}
finally { Pop-Location }
