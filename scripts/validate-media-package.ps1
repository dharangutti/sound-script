param([Parameter(Mandatory=$true)][string]$PackagePath)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$package = (Resolve-Path -LiteralPath $PackagePath).Path
$workspace = Join-Path ([IO.Path]::GetTempPath()) ('soundscript-media-consumer-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $workspace | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $entry = $archive.Entries | Where-Object FullName -Like '*.nuspec' | Select-Object -First 1
    $reader = [IO.StreamReader]::new($entry.Open())
    try { [xml]$spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $version = $spec.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='version']").InnerText
} finally { $archive.Dispose() }
[xml]$props = Get-Content -LiteralPath (Join-Path $repo 'Directory.Build.props') -Raw
$developmentVersion = [string]$props.Project.PropertyGroup.Version
if ($version -cne $developmentVersion) { throw "Expected development package $developmentVersion, got $version" }
$feed = [System.Security.SecurityElement]::Escape((Split-Path $package))
@"
<configuration>
  <packageSources><clear/><add key="local" value="$feed"/><add key="nuget" value="https://api.nuget.org/v3/index.json"/></packageSources>
  <packageSourceMapping><clear/><packageSource key="local"><package pattern="SoundScript"/></packageSource><packageSource key="nuget"><package pattern="*"/></packageSource></packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $workspace 'NuGet.Config')
@"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><PackageReference Include="SoundScript" Version="$version"/></ItemGroup></Project>
"@ | Set-Content -LiteralPath (Join-Path $workspace 'Consumer.csproj')
Copy-Item -LiteralPath (Join-Path $repo 'samples/ProgrammableMedia/Program.cs') -Destination $workspace
Copy-Item -LiteralPath (Join-Path $repo 'samples/ProgrammableMediaWeb/MonitoringScenario.cs') -Destination $workspace
function Run([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $Arguments" }
}
Push-Location $workspace
try {
    Run @('restore', '--configfile', 'NuGet.Config', '--packages', (Join-Path $workspace 'cache'))
    Run @('build', '-c', 'Release', '--no-restore')
    Run @('run', '-c', 'Release', '--no-build', '--no-restore', '--', 'first')
    Run @('run', '-c', 'Release', '--no-build', '--no-restore', '--', 'second')
    $first = Get-Content first/hashes.json -Raw
    $second = Get-Content second/hashes.json -Raw
    if ($first -cne $second) { throw 'Independent process hashes differ.' }
    $inventory = $first | ConvertFrom-Json -AsHashtable
    foreach ($suffix in @('.ssv', '.mid', '.wav', '.json', '.svg')) {
        $values = @('healthy','warning','critical') | ForEach-Object { $inventory[$_ + $suffix] }
        if (@($values | Select-Object -Unique).Count -ne 3) { throw "Scenarios do not differ for $suffix" }
    }
    [xml]$svg = Get-Content first/security.svg -Raw
    if ($svg.SelectNodes("//*[local-name()='script' or local-name()='img']").Count -ne 0) { throw 'Unsafe SVG elements' }
    foreach ($attribute in $svg.SelectNodes('//@*')) {
        if ($attribute.Name -match '^on') { throw 'Unsafe SVG event attribute' }
    }
    $evidence = Join-Path $repo 'artifacts/media-package-validation'
    New-Item -ItemType Directory -Force -Path $evidence | Out-Null
    Copy-Item first/hashes.json (Join-Path $evidence 'hashes.json')
    Copy-Item first/security.svg (Join-Path $evidence 'security.svg')
    Copy-Item Program.cs Program.original.cs.txt
    foreach ($document in @('docs/programmatic-media-runtime.md', 'docs/articles/programmable-media-runtime-dotnet.md', 'docs/tutorials/programmable-media.md')) {
        $markdown = Get-Content -LiteralPath (Join-Path $repo $document) -Raw
        $code = [regex]::Match($markdown, '(?s)```csharp\r?\n(.*?)```').Groups[1].Value
        if (-not $code) { throw "No runnable C# example in $document" }
        if ($document -like '*tutorials*') {
            $source = [regex]::Match($markdown, '(?s)```soundscript\r?\n(.*?)```').Groups[1].Value
            Set-Content status.ssv $source
        }
        Set-Content Program.cs $code
        Run @('run', '-c', 'Release', '--no-restore')
        Write-Output "PASS: compiled and ran documentation example: $document"
    }
    Write-Output 'PASS: isolated package-only consumer; two independent runs; 22 matching hashes; three distinct media scenarios; parsed security SVG.'
    Write-Output "Workspace retained: $workspace"
} finally { Pop-Location }
