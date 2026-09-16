$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $repo 'artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$stage = Join-Path $artifacts ('site-build-' + [guid]::NewGuid().ToString('N'))
$publish = Join-Path $stage 'playground'
$site = Join-Path $stage 'site'
New-Item -ItemType Directory -Path $site -Force | Out-Null

# A fresh directory is essential: Publish + Copy does not remove older files.
dotnet publish (Join-Path $repo 'src/SoundScript.Playground') -c Release "-p:PublishDir=$publish/"
if ($LASTEXITCODE -ne 0) { throw 'Playground publish failed; existing site artifact untouched.' }
node (Join-Path $PSScriptRoot 'verify-playground-integrity.cjs') $publish
if ($LASTEXITCODE -ne 0) { throw 'Playground integrity failed; existing site artifact untouched.' }
Get-ChildItem -LiteralPath (Join-Path $repo 'docs') -Force |
    Where-Object { $_.Name -ne 'playground' } |
    Copy-Item -Destination $site -Recurse -Force
Move-Item -LiteralPath $publish -Destination (Join-Path $site 'playground')
# GitHub Pages must serve _framework verbatim. Never apply Git text conversion.
[IO.File]::WriteAllText((Join-Path $site '.nojekyll'), '')
[IO.File]::WriteAllText((Join-Path $site '.gitattributes'), "* -text`n")
node (Join-Path $PSScriptRoot 'verify-playground-integrity.cjs') (Join-Path $site 'playground')
if ($LASTEXITCODE -ne 0) { throw 'Final site integrity failed.' }

$destination = [IO.Path]::GetFullPath((Join-Path $artifacts 'site'))
if ($destination -ne [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/site'))) { throw 'Unsafe site destination.' }
if ((Test-Path -LiteralPath $destination) -and ((Get-Item -LiteralPath $destination).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Site destination must not be a link.' }
# Only promote a fully verified tree. The workflow publishes this tree in one commit.
$previous = Join-Path $stage 'previous-site'
if (Test-Path -LiteralPath $destination) { Move-Item -LiteralPath $destination -Destination $previous }
try { Move-Item -LiteralPath $site -Destination $destination }
catch {
    if (Test-Path -LiteralPath $previous) { Move-Item -LiteralPath $previous -Destination $destination }
    throw
}
Write-Host "Verified deployment artifact: $destination"
