param([Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'
# Availability polling is shared with offline-tested promotion logic.
& node (Join-Path $PSScriptRoot 'release-promotion.mjs') availability $Version
if ($LASTEXITCODE -ne 0) { throw 'Public NuGet availability verification failed. Retry promotion after indexing completes.' }
$workspace = Join-Path ([IO.Path]::GetTempPath()) ('soundscript-public-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $workspace | Out-Null
function Run([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Public consumer dotnet failed: $Arguments" }
}
try {
    @'
<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>
'@ | Set-Content -LiteralPath (Join-Path $workspace 'NuGet.Config')
    @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><RestoreFallbackFolders></RestoreFallbackFolders></PropertyGroup><ItemGroup><PackageReference Include="SoundScript" Version="[$Version]"/></ItemGroup></Project>
"@ | Set-Content -LiteralPath (Join-Path $workspace 'Consumer.csproj')
    @'
using SoundScript;
using SoundScript.Media;
using System.Text.Json;
using System.Xml.Linq;
var source = """
tempo 120
track alarm { C4 q E4 q G4 q C5 q }
sync audio
visual "status" for 2s {
    shape circle
    fill "#16a34a"
    set width 100
    set height 100
    animate x 200 -> 600 over 2s
}
""";
var compilation = SoundScriptEngine.Compile(source);
var media = compilation.CompileMedia();
var first = media.RenderAudio();
var second = SoundScriptEngine.Compile(source).CompileMedia().RenderAudio();
if (first.Length <= 44 || !first.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !first.SequenceEqual(second))
    throw new Exception("WAV output is empty, invalid, or nondeterministic.");
var midi = compilation.RenderMidi();
if (midi.Length <= 14 || !midi.AsSpan(0, 4).SequenceEqual("MThd"u8)) throw new Exception("Invalid MIDI.");
var scene = media.SceneAt(TimeSpan.FromSeconds(1));
if (scene.Primitives.Count == 0) throw new Exception("Missing visual scene.");
using var json = JsonDocument.Parse(TemporalVisualJson.Serialize(scene));
_ = XDocument.Parse(TemporalSvgRenderer.Render(scene));
Console.WriteLine("PASS: public package compiled and executed deterministic WAV, MIDI, scene, JSON and SVG APIs.");
'@ | Set-Content -LiteralPath (Join-Path $workspace 'Program.cs')
    Push-Location $workspace
    try {
        Run @('restore', 'Consumer.csproj', '--configfile', 'NuGet.Config', '--packages', (Join-Path $workspace 'packages'), '--no-http-cache', '-p:NuGetAudit=false')
        $assets = Get-Content obj/project.assets.json -Raw | ConvertFrom-Json -AsHashtable
        if (-not $assets.libraries.ContainsKey("SoundScript/$Version")) { throw "Restore did not resolve exact SoundScript/$Version." }
        Run @('build', 'Consumer.csproj', '-c', 'Release', '--no-restore')
        Run @('run', '--project', 'Consumer.csproj', '-c', 'Release', '--no-build', '--no-restore')
    } finally { Pop-Location }
} finally {
    # Only delete the explicitly created temporary workspace, never a supplied path.
    if ((Split-Path $workspace) -eq ([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)) {
        Remove-Item -LiteralPath $workspace -Recurse -Force
    }
}
