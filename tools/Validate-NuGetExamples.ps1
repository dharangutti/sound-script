param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/nuget-examples-validation'),
    [string]$Ffmpeg = 'ffmpeg'
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath($OutputDirectory)
# Isolate the consumers from Directory.Build.props, project references and prior NuGet caches.
$consumer = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('SoundScript/consumers/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $consumer, $output | Out-Null
@'
<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources></configuration>
'@ | Set-Content -LiteralPath (Join-Path $consumer 'NuGet.Config')
function Invoke-DotNet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed ($LASTEXITCODE): $Arguments" }
}
foreach ($project in @('IndustrialMonitoring', 'MediaRoundTrip', 'NuGetExamplesValidation')) {
    $parent = if ($project -eq 'NuGetExamplesValidation') { 'tools' } else { 'samples' }
    $source = Join-Path $repository "$parent/$project"
    $destination = Join-Path $consumer $project
    New-Item -ItemType Directory -Force $destination | Out-Null
    Get-ChildItem -LiteralPath $source | Where-Object Name -NotIn @('bin', 'obj') |
        Copy-Item -Destination $destination -Recurse
    $csproj = Join-Path $destination "$project.csproj"
    Invoke-DotNet @('restore', $csproj, '--configfile', (Join-Path $consumer 'NuGet.Config'), '--packages', (Join-Path $consumer 'packages'))
    Invoke-DotNet @('build', $csproj, '-c', 'Release', '--no-restore', '--nologo')
}
foreach ($run in @('a', 'b')) {
    Invoke-DotNet @('run', '--project', (Join-Path $consumer 'IndustrialMonitoring'), '-c', 'Release', '--no-build', '--', 'all', (Join-Path $output "monitoring-$run"), $Ffmpeg)
    Invoke-DotNet @('run', '--project', (Join-Path $consumer 'MediaRoundTrip'), '-c', 'Release', '--no-build', '--', (Join-Path $output "roundtrip-$run"))
}
# Explicitly exercise graceful degradation even on hosts that have FFmpeg installed.
Invoke-DotNet @('run', '--project', (Join-Path $consumer 'IndustrialMonitoring'), '-c', 'Release', '--no-build', '--', 'all', (Join-Path $output 'no-ffmpeg'), (Join-Path $consumer 'missing-ffmpeg'))
foreach ($name in @('healthy', 'warning', 'critical')) {
    if (Test-Path -LiteralPath (Join-Path $output "no-ffmpeg/$name/$name.webm")) { throw 'Unexpected video without FFmpeg' }
    foreach ($file in @("$name.mid", "$name-music.wav", "$name-alert.wav", "$name-synchronized.wav", 'timeline.json', 'scenes.json')) {
        $actual = Get-FileHash -LiteralPath (Join-Path $output "no-ffmpeg/$name/$file")
        $expected = Get-FileHash -LiteralPath (Join-Path $output "monitoring-a/$name/$file")
        if ($actual.Hash -ne $expected.Hash) { throw "FFmpeg absence changed $file" }
    }
}
Invoke-DotNet @('run', '--project', (Join-Path $consumer 'NuGetExamplesValidation'), '-c', 'Release', '--no-build', '--', $output)
# Rejected-input checks must fail clearly and invalidate old reconstruction outputs.
$silent = Join-Path $output 'silence.wav'
$bytes = [IO.File]::ReadAllBytes((Join-Path $output 'roundtrip-a/Input/melody.wav'))
[Array]::Clear($bytes, 44, $bytes.Length - 44)
[IO.File]::WriteAllBytes($silent, $bytes)
$rejected = Join-Path $output 'roundtrip-rejected'
New-Item -ItemType Directory -Force (Join-Path $rejected 'Output') | Out-Null
Copy-Item -LiteralPath (Join-Path $output 'roundtrip-a/Output/reconstructed.wav') -Destination (Join-Path $rejected 'Output/reconstructed.wav')
& dotnet run --project (Join-Path $consumer 'MediaRoundTrip') -c Release --no-build -- $rejected $silent
if ($LASTEXITCODE -ne 1) { throw 'Silent input must be rejected' }
if (!(Test-Path -LiteralPath (Join-Path $rejected 'Output/report.json'))) { throw 'Rejected input must retain diagnostics' }
if (Test-Path -LiteralPath (Join-Path $rejected 'Output/reconstructed.wav')) { throw 'Stale reconstruction survived rejected input' }
$invalid = Join-Path $output 'invalid-scenario.json'
'{"EquipmentId":"bad\"id", "Temperature":68, "Vibration":0.14, "Load":52, "Status":"Healthy"}' | Set-Content -LiteralPath $invalid
& dotnet run --project (Join-Path $consumer 'IndustrialMonitoring') -c Release --no-build -- $invalid (Join-Path $output 'invalid-output') $Ffmpeg
if ($LASTEXITCODE -ne 1) { throw 'Invalid scenario must be rejected before source generation' }
Write-Host "Published NuGet consumer workspace retained at $consumer"
Write-Host "Validation outputs: $output"
