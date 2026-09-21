param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $repoRoot
$previousPublish = $env:SOUNDSCRIPT_PLAYGROUND_PUBLISH_DIR
function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    Write-Host "$Command $($Arguments -join ' ')"
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE" }
}
try {
    Invoke-Checked git @('submodule', 'update', '--init', '--recursive', '--', 'wordbank')
    # Validate the checked-in data; syncing upstream is a separate reviewed change.
    Invoke-Checked node @('scripts/corpus-provenance.cjs')
    Invoke-Checked dotnet @('restore', 'SoundScript.sln')
    Invoke-Checked dotnet @('build', 'SoundScript.sln', '-c', $Configuration, '--no-restore')
    $publish = Join-Path $repoRoot 'artifacts/playground'
    Invoke-Checked dotnet @('publish', 'src/SoundScript.Playground', '-c', 'Release', '--no-restore', "-p:PublishDir=$publish/")
    $env:SOUNDSCRIPT_PLAYGROUND_PUBLISH_DIR = $publish
    Invoke-Checked dotnet @('test', 'SoundScript.sln', '-c', $Configuration, '--no-build', '--logger', "trx;LogFileName=$Configuration.trx", '--results-directory', "artifacts/validation/$Configuration")
    Invoke-Checked node @('--test', 'scripts/transcription-browser-input.test.cjs', 'scripts/playground-integrity.test.cjs', 'scripts/playground-startup.test.cjs', 'scripts/corpus-provenance.test.cjs')
    if ($Configuration -eq 'Release') { & (Join-Path $PSScriptRoot 'dependency-inventory.ps1') }
}
finally {
    $env:SOUNDSCRIPT_PLAYGROUND_PUBLISH_DIR = $previousPublish
    Pop-Location
}
