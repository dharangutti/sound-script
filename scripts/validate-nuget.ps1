[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateScript({ if (-not (Test-Path -LiteralPath $_ -PathType Leaf)) { throw "PackagePath does not exist: $_" }; $true })]
    [string] $PackagePath,

    [Parameter(Mandatory = $false)]
    [string] $PackageVersion
)

$ErrorActionPreference = 'Stop'
$script:Failures = [System.Collections.Generic.List[string]]::new()
$script:Warnings = [System.Collections.Generic.List[string]]::new()
$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
& (Join-Path $PSScriptRoot 'update-docs.ps1') -Check
$script:PreviousNugetPackages = $env:NUGET_PACKAGES
$resolvedPackagePath = (Resolve-Path -LiteralPath $PackagePath).Path

function Pass([string] $Message) {
    Write-Host "[PASS] $Message" -ForegroundColor Green
}

function Fail([string] $Message) {
    [void]$script:Failures.Add($Message)
    Write-Host "[FAIL] $Message" -ForegroundColor Red
}

function Warn([string] $Message) {
    [void]$script:Warnings.Add($Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Invoke-DotNet([string[]] $DotNetArgs) {
    $output = (& dotnet @DotNetArgs 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($DotNetArgs -join ' ') failed (exit $LASTEXITCODE):`n$output"
    }
    if ($output) { Write-Host $output }
    return $output
}

function Xml-Text([System.Xml.XmlNode] $Parent, [string] $Name) {
    $node = $Parent.SelectSingleNode("*[local-name()='$Name']")
    if ($null -eq $node) { return $null }
    return [string]$node.InnerText
}

function Get-ProjectGraph([string] $RootProject) {
    $pending = [System.Collections.Generic.Queue[string]]::new()
    $visited = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $projects = [System.Collections.Generic.List[object]]::new()
    $pending.Enqueue((Resolve-Path -LiteralPath $RootProject).Path)

    while ($pending.Count -gt 0) {
        $projectPath = $pending.Dequeue()
        if (-not $visited.Add($projectPath)) { continue }

        [xml]$projectXml = Get-Content -LiteralPath $projectPath -Raw
        $assemblyNode = $projectXml.SelectSingleNode("/*[local-name()='Project']/*[local-name()='PropertyGroup']/*[local-name()='AssemblyName']")
        $assemblyName = if ($null -ne $assemblyNode -and $assemblyNode.InnerText.Trim()) {
            $assemblyNode.InnerText.Trim()
        } else {
            [IO.Path]::GetFileNameWithoutExtension($projectPath)
        }

        $projectRecord = [pscustomobject]@{
            Path = $projectPath
            AssemblyName = $assemblyName
            Xml = $projectXml
        }
        $projects.Add($projectRecord)

        $baseDirectory = Split-Path -Parent $projectPath
        foreach ($reference in @($projectXml.SelectNodes("//*[local-name()='ProjectReference']"))) {
            $include = [string]$reference.Include
            if ([string]::IsNullOrWhiteSpace($include)) { continue }
            $normalizedInclude = $include -replace '[\\/]', [string][IO.Path]::DirectorySeparatorChar
            $referencePath = [IO.Path]::GetFullPath((Join-Path $baseDirectory $normalizedInclude))
            if (Test-Path -LiteralPath $referencePath -PathType Leaf) {
                $pending.Enqueue($referencePath)
            } else {
                Fail "Project graph reference '$include' from '$projectPath' does not exist."
            }
        }
    }
    return @($projects)
}

function Dependency-VersionMatches([string] $Declared, [string] $ProjectVersion) {
    if ($Declared -eq $ProjectVersion) { return $true }
    # NuGet commonly emits an exact project PackageReference as [x,); accept
    # that equivalent lower-bound form while still rejecting a different floor.
    if ($Declared -match '^\[(?<minimum>[^,\]]+),') {
        return $Matches.minimum -eq $ProjectVersion
    }
    return $false
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("soundscript-nuget-validation-" + [guid]::NewGuid().ToString('N'))
$extractRoot = Join-Path $tempRoot 'package'
$consumerRoot = Join-Path $tempRoot 'consumer'
$nugetCache = Join-Path $tempRoot 'nuget-cache'
$localFeed = Join-Path $tempRoot 'feed'
$null = New-Item -ItemType Directory -Path $tempRoot, $extractRoot, $consumerRoot, $nugetCache, $localFeed -Force

Write-Host "Validating package: $resolvedPackagePath"
Write-Host "Diagnostic workspace retained at: $tempRoot"

try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($resolvedPackagePath)
    try {
        $entries = @($archive.Entries)
        $entryNames = @($entries | ForEach-Object FullName)
        [IO.Compression.ZipFile]::ExtractToDirectory($resolvedPackagePath, $extractRoot)
    } finally {
        $archive.Dispose()
    }

    if ([IO.Path]::GetExtension($resolvedPackagePath) -ne '.nupkg') {
        Fail 'PackagePath must point to a .nupkg file.'
    }

    $nuspecEntryName = @($entryNames | Where-Object { $_ -match '(?i)(^|/)[^/]+\.nuspec$' }) | Select-Object -First 1
    if (-not $nuspecEntryName) {
        Fail 'Package does not contain a nuspec.'
        throw 'Cannot continue without nuspec.'
    }

    [xml]$nuspec = Get-Content -LiteralPath (Join-Path $extractRoot $nuspecEntryName) -Raw
    $metadata = $nuspec.SelectSingleNode("//*[local-name()='metadata']")
    if ($null -eq $metadata) { Fail 'Nuspec metadata node is missing.'; throw 'Cannot continue without metadata.' }

    $packageId = Xml-Text $metadata 'id'
    $nuspecVersion = Xml-Text $metadata 'version'
    if ([string]::IsNullOrWhiteSpace($packageId) -or [string]::IsNullOrWhiteSpace($nuspecVersion)) {
        Fail 'Nuspec id/version metadata is missing.'
        throw 'Cannot continue without package identity.'
    }
    if ($PackageVersion -and $PackageVersion -ne $nuspecVersion) {
        Fail "Expected package version '$PackageVersion', nuspec reports '$nuspecVersion'."
    }
    Pass "Package identity is $packageId $nuspecVersion"

    $requiredMetadata = @('description', 'authors', 'tags', 'projectUrl', 'repository', 'license', 'readme', 'icon')
    foreach ($field in $requiredMetadata) {
        $node = $metadata.SelectSingleNode("*[local-name()='$field']")
        if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.InnerText) -and $field -notin @('repository', 'license')) {
            Fail "Nuspec metadata '$field' is missing."
        }
    }
    $repository = $metadata.SelectSingleNode("*[local-name()='repository']")
    if ($null -eq $repository -or [string]$repository.url -notmatch 'github\.com/dharangutti/sound-script') {
        Fail 'Nuspec repository metadata does not point at the SoundScript GitHub repository.'
    }
    $licenseNode = $metadata.SelectSingleNode("*[local-name()='license']")
    if ($null -eq $licenseNode -or [string]$licenseNode.type -ne 'expression') {
        Fail 'Nuspec license metadata must use an SPDX license expression.'
    }

    foreach ($requiredRootEntry in @('README.md', 'icon.png')) {
        if ($entryNames -notcontains $requiredRootEntry) { Fail "Package is missing root entry '$requiredRootEntry'." }
    }
    $readmeEntryName = Xml-Text $metadata 'readme'
    if (-not $readmeEntryName -or $entryNames -cnotcontains $readmeEntryName) {
        Fail "Nuspec README entry '$readmeEntryName' is missing from the package."
    } else {
        $readmeText = Get-Content -LiteralPath (Join-Path $extractRoot $readmeEntryName) -Raw
        # The package under test may be an unpublished development version. Its
        # README must retain independently validated public onboarding, not
        # advertise that development package as already available on NuGet.
        $canonicalReadme = Get-Content -LiteralPath (Join-Path $script:RepoRoot 'packaging/README.md') -Raw
        if ($readmeText.Replace("`r`n", "`n") -cne $canonicalReadme.Replace("`r`n", "`n")) {
            Fail 'Packaged README differs from the canonical, release-state-validated packaging/README.md. Regenerate documentation and repack.'
        } else {
            Pass 'Packaged README matches validated public documentation independently of development package version.'
        }
    }
    $iconEntry = $entries | Where-Object { $_.FullName -eq 'icon.png' } | Select-Object -First 1
    if ($null -ne $iconEntry -and $iconEntry.Length -gt 1MB) { Fail "Package icon is larger than 1 MiB ($($iconEntry.Length) bytes)." }
    if (-not (@($entryNames | Where-Object { $_ -match '(?i)^LICENSE(?:/|$)' }).Count -gt 0)) {
        Fail 'Package does not contain a bundled LICENSE file.'
    }
    $licenseEntries = @($entryNames | Where-Object { $_ -match '(?i)^licenses/.+' })
    if ($licenseEntries.Count -eq 0) { Fail 'Package has no bundled third-party/license notices.' }
    if ($entryNames -notcontains 'licenses/corpus/v2026.07/en/SOURCES.md') { Fail 'Corpus provenance notice missing from package licenses.' }
    $injectedEntries = @($entryNames | Where-Object { $_ -match '(?i)^(contentFiles|build|buildTransitive|analyzers|tools)/' })
    if ($injectedEntries.Count -gt 0) { Fail "Unexpected consumer content/imports: $($injectedEntries -join ', ')" }
    else { Pass 'No contentFiles, build, buildTransitive, analyzers, or tools injected into consumers' }

    $libEntries = @($entries | Where-Object { $_.FullName -match '(?i)^lib/[^/]+/SoundScript[^/]*\.dll$' })
    $tfms = @($libEntries | ForEach-Object { $_.FullName -split '/' | Select-Object -Index 1 } | Sort-Object -Unique)
    if ($libEntries.Count -eq 0) { Fail 'Package contains no SoundScript library assemblies.'; throw 'Cannot continue without library assemblies.' }
    if ($tfms.Count -ne 1) { Warn "Package contains multiple target frameworks: $($tfms -join ', ')." }
    $tfm = $tfms | Select-Object -First 1
    Pass "Discovered $($libEntries.Count) SoundScript assemblies for $tfm"

    $graphRoot = Join-Path $script:RepoRoot 'src/SoundScript/SoundScript.csproj'
    if (Test-Path -LiteralPath $graphRoot -PathType Leaf) {
        $graph = Get-ProjectGraph $graphRoot
        $expectedAssemblyNames = @($graph | ForEach-Object { $_.AssemblyName } | Sort-Object -Unique)
        $actualAssemblyNames = @($libEntries | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_.Name) } | Sort-Object -Unique)
        foreach ($expected in $expectedAssemblyNames) {
            if ($actualAssemblyNames -notcontains $expected) { Fail "Bundled project graph assembly '$expected' is missing from lib/$tfm." }
        }
        foreach ($actual in $actualAssemblyNames) {
            if ($expectedAssemblyNames -notcontains $actual) { Fail "Unexpected SoundScript assembly '$actual' is present; package graph does not explain it." }
        }

        $componentPackages = @{}
        foreach ($project in $graph) {
            foreach ($packageRef in @($project.Xml.SelectNodes("//*[local-name()='PackageReference']"))) {
                $id = [string]$packageRef.Include
                $version = [string]$packageRef.Version
                if ([string]::IsNullOrWhiteSpace($id) -or [string]::IsNullOrWhiteSpace($version)) { continue }
                if (-not $componentPackages.ContainsKey($id)) { $componentPackages[$id] = [System.Collections.Generic.HashSet[string]]::new() }
                [void]$componentPackages[$id].Add($version)
            }
        }

        $nuspecDependencies = @{}
        foreach ($dependency in @($metadata.SelectNodes("//*[local-name()='dependencies']//*[local-name()='dependency']"))) {
            $id = [string]$dependency.id
            $version = [string]$dependency.version
            if ($id) { $nuspecDependencies[$id] = $version }
        }
        foreach ($id in $componentPackages.Keys) {
            foreach ($version in $componentPackages[$id]) {
                if (-not $nuspecDependencies.ContainsKey($id)) {
                    Fail "Component graph references package '$id' $version, but it is absent from nuspec dependencies."
                } elseif (-not (Dependency-VersionMatches $nuspecDependencies[$id] $version)) {
                    Fail "Dependency '$id' is $($nuspecDependencies[$id]) in nuspec but $version in a component project."
                }
            }
        }
        Pass "Nuspec dependency versions align with $($graph.Count)-project component graph"
    } else {
        Warn "Source project graph not found at $graphRoot; dependency graph checks were skipped."
    }

    $badLibraryEntries = @($libEntries | Where-Object { $_.FullName -match '(?i)(cli|playground|test|\.exe$)' })
    foreach ($bad in $badLibraryEntries) { Fail "Package contains a CLI, Playground, or test binary: $($bad.FullName)." }
    $junkEntries = @($entryNames | Where-Object { $_ -match '(?i)(^|/)(bin|obj)/|\.runtimeconfig\.json$|\.deps\.json$|\.pdb$' })
    if ($junkEntries.Count -gt 0) { Warn "Package has $($junkEntries.Count) build/runtime entries outside the expected library payload; inspect the retained workspace." }

    foreach ($lib in $libEntries) {
        $base = $lib.FullName.Substring(0, $lib.FullName.Length - 4)
        if ($entryNames -notcontains "$base.xml") { Fail "XML documentation is missing for $($lib.FullName)." }
        $dllPath = Join-Path $extractRoot ($lib.FullName -replace '/', [string][IO.Path]::DirectorySeparatorChar)
        $dllBytes = [IO.File]::ReadAllBytes($dllPath)
        if ($dllBytes.Length -lt 2 -or $dllBytes[0] -ne 0x4d -or $dllBytes[1] -ne 0x5a) { Fail "Bundled assembly is not a valid PE image: $($lib.FullName)." }
        $xmlPath = Join-Path $extractRoot (($base + '.xml') -replace '/', [string][IO.Path]::DirectorySeparatorChar)
        try {
            [xml]$assemblyXml = Get-Content -LiteralPath $xmlPath -Raw
            $documentedNameNode = $assemblyXml.SelectSingleNode("//*[local-name()='assembly']/*[local-name()='name']")
            $documentedName = if ($null -eq $documentedNameNode) { '' } else { [string]$documentedNameNode.InnerText }
            $expectedName = [IO.Path]::GetFileNameWithoutExtension($lib.Name)
            if ($documentedName -ne $expectedName) { Fail "XML documentation '$($base).xml' identifies '$documentedName'; expected '$expectedName'." }
        } catch {
            Fail "XML documentation '$($base).xml' is not well-formed: $($_.Exception.Message)"
        }
    }
    Pass 'Checked XML documentation for every bundled assembly'

    $symbolPackagePath = [IO.Path]::ChangeExtension($resolvedPackagePath, '.snupkg')
    if (-not (Test-Path -LiteralPath $symbolPackagePath -PathType Leaf)) {
        Fail "Companion symbol package is missing: $symbolPackagePath"
    } else {
        $symbolArchive = [IO.Compression.ZipFile]::OpenRead($symbolPackagePath)
        try {
            $symbolEntryNames = @($symbolArchive.Entries | ForEach-Object FullName)
            $symbolExtractRoot = Join-Path $tempRoot 'symbols'
            [IO.Compression.ZipFile]::ExtractToDirectory($symbolPackagePath, $symbolExtractRoot)
        }
        finally { $symbolArchive.Dispose() }
        foreach ($lib in $libEntries) {
            $symbolName = $lib.FullName.Substring(0, $lib.FullName.Length - 4) + '.pdb'
            if ($symbolEntryNames -notcontains $symbolName) { Fail "Portable symbols are missing from .snupkg for $($lib.FullName)." }
            else {
                $pdbPath = Join-Path $symbolExtractRoot ($symbolName -replace '/', [string][IO.Path]::DirectorySeparatorChar)
                $pdbBytes = [IO.File]::ReadAllBytes($pdbPath)
                if ($pdbBytes.Length -lt 4 -or [Text.Encoding]::ASCII.GetString($pdbBytes, 0, 4) -ne 'BSJB') {
                    Fail "Portable symbols entry '$symbolName' does not have a Portable PDB BSJB header."
                }
            }
        }
        Pass 'Checked portable symbols in the companion .snupkg for every bundled assembly'
    }

    $env:NUGET_PACKAGES = $nugetCache
    Copy-Item -LiteralPath $resolvedPackagePath -Destination $localFeed -Force
    $escapedFeed = [Security.SecurityElement]::Escape($localFeed)
    $configPath = Join-Path $consumerRoot 'NuGet.Config'
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$escapedFeed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="local"><package pattern="SoundScript" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath $configPath -Encoding UTF8

    $consumerProject = Join-Path $consumerRoot 'Consumer.csproj'
    Invoke-DotNet @('new', 'console', '--name', 'Consumer', '--framework', $tfm, '--output', $consumerRoot, '--no-restore') | Out-Null
    $consumerProgram = @'
using System.Security.Cryptography;
using SoundScript;
using SoundScript.Transcription;
using SoundScript.Wave;
using SoundScript.Wordbank;
using SoundScript.Vocal;

const string source = "tempo 120 track cue { instrument piano mf C4 e E4 e G4 q }";
var compilation = SoundScriptEngine.Compile(source);
var wav = compilation.RenderWave();
var optionsWav = compilation.RenderWave(new WaveRenderOptions());
var midi = compilation.RenderMidi();
var audio = PcmWaveInput.Decode(wav);
var transcription = new TranscriptionEngine().Transcribe(audio, new TranscriptionOptions(Tempo: 120));
var wavAgain = compilation.RenderWave();
var midiAgain = compilation.RenderMidi();
if (!wav.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !wav.AsSpan(8, 4).SequenceEqual("WAVE"u8)) throw new Exception("WAV header check failed.");
if (!midi.AsSpan(0, 4).SequenceEqual("MThd"u8)) throw new Exception("MIDI header check failed.");
if (!wav.SequenceEqual(optionsWav)) throw new Exception("Options overload changed default WAV output.");
if (!wav.SequenceEqual(wavAgain) || !midi.SequenceEqual(midiAgain)) throw new Exception("Output is not deterministic.");
if (transcription.Score.Tracks.Sum(track => track.Notes.Count) <= 0) throw new Exception("Transcription produced no notes.");
File.WriteAllBytes(Path.Combine(Environment.CurrentDirectory, "consumer.wav"), wav);
File.WriteAllBytes(Path.Combine(Environment.CurrentDirectory, "consumer.mid"), midi);
Console.WriteLine($"WAV_BYTES={wav.Length}");
Console.WriteLine($"WAV_SHA256={Convert.ToHexString(SHA256.HashData(wav))}");
Console.WriteLine($"MIDI_BYTES={midi.Length}");
Console.WriteLine($"MIDI_SHA256={Convert.ToHexString(SHA256.HashData(midi))}");
Console.WriteLine($"TRANSCRIPTION_NOTES={transcription.Score.Tracks.Sum(track => track.Notes.Count)}");
var runtime = SoundScriptEngine.CompileRuntime("param intensity = 0.25 perform expressive track cue { gain intensity C4 q } visual \"cue\" for 4s { shape circle set opacity intensity }");
var first = runtime.Bind();
runtime.Set("intensity", 0.8m);
var changed = runtime.Bind();
if (first.RenderAudio().SequenceEqual(changed.RenderAudio())) throw new Exception("Runtime audio did not adapt.");
if (!changed.RenderAudio().SequenceEqual(runtime.RenderAudio())) throw new Exception("Runtime audio is not deterministic.");
if (runtime.Statistics.Parses != 1) throw new Exception("Runtime source reparsed.");
Console.WriteLine($"RUNTIME_SCENE={changed.SceneAt(TimeSpan.FromSeconds(2)).Primitives.Count}");
var independent = runtime.CreateInstance();
if (independent.Get("intensity") != 0.25m || independent.Revision != 0) throw new Exception("Independent runtime did not start from defaults.");
try { runtime.Set("intensity", 2m); throw new Exception("Invalid value was accepted."); }
catch (ArgumentOutOfRangeException ex)
{
    if (!ex.Message.Contains("intensity") || !ex.Message.Contains("0 through 1")) throw new Exception("Unhelpful constraint diagnostic.");
    Console.WriteLine($"EXPECTED_DIAGNOSTIC={ex.Message}");
}
if (runtime.Get("intensity") != 0.8m) throw new Exception("Rejected update changed state.");
runtime.Reset();
if (!first.RenderAudio().SequenceEqual(runtime.RenderAudio())) throw new Exception("Runtime reset changed defaults.");
WordbankCatalog.ResetToEmbedded();
CorpusCatalog.Reset();
if (!CorpusCatalog.TryLoadEmbedded() || !CorpusCatalog.TryGetLemma("en", "hello", out var lemma)) throw new Exception("Embedded corpus metadata missing.");
if (!CorpusCatalog.TryGetAudioBytes(lemma, out var corpusWav) || !corpusWav.AsSpan(0, 4).SequenceEqual("RIFF"u8)) throw new Exception("Embedded corpus audio missing.");
var vocal = new WordbankVocalEngine().SynthesizeToWavBytes("hello welcome", new VocalEngineOptions { Locale = "en" });
if (vocal.Length <= 44 || CorpusCatalog.LoadedRoot is not null) throw new Exception("Resource-only vocal playback failed.");
Console.WriteLine($"VOCAL_BYTES={vocal.Length}");
'@
    Set-Content -LiteralPath (Join-Path $consumerRoot 'Program.cs') -Value $consumerProgram -Encoding UTF8
    # Add only the reference, then perform a full explicit restore with the
    # isolated config/cache below. The CLI's no-restore advisory is retained.
    Invoke-DotNet @('add', $consumerProject, 'package', $packageId, '--version', $nuspecVersion, '--no-restore') | Out-Null
    Invoke-DotNet @('restore', $consumerProject, '--configfile', $configPath, '--packages', $nugetCache) | Out-Null
    Invoke-DotNet @('build', $consumerProject, '--no-restore', '-c', 'Release') | Out-Null
    Push-Location $consumerRoot
    try {
        $consumerOutput = Invoke-DotNet @('run', '--project', $consumerProject, '--no-build', '-c', 'Release')
    } finally {
        Pop-Location
    }
    if ($consumerOutput -notmatch 'WAV_BYTES=([0-9]+)') { Fail 'Fresh consumer did not report WAV output.' }
    if ($consumerOutput -notmatch 'MIDI_BYTES=([0-9]+)') { Fail 'Fresh consumer did not report MIDI output.' }
    if ($consumerOutput -notmatch 'TRANSCRIPTION_NOTES=([0-9]+)') { Fail 'Fresh consumer did not complete transcription.' }
    if ((Test-Path -LiteralPath (Join-Path $consumerRoot 'consumer.wav')) -and (Test-Path -LiteralPath (Join-Path $consumerRoot 'consumer.mid'))) {
        Pass 'Fresh consumer compiled, ran WAV/MIDI rendering, and completed transcription'
    } else {
        Fail 'Fresh consumer did not write both media outputs.'
    }

    $cachedPackageRoot = Join-Path (Join-Path (Join-Path (Join-Path $nugetCache $packageId.ToLowerInvariant()) $nuspecVersion) 'lib') $tfm
    foreach ($xmlEntry in @($libEntries | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_.Name) + '.xml' })) {
        if (-not (Test-Path -LiteralPath (Join-Path $cachedPackageRoot $xmlEntry))) { Fail "Consumer NuGet cache is missing XML docs '$xmlEntry'." }
    }
    if (@(Get-ChildItem -LiteralPath $cachedPackageRoot -Filter 'SoundScript*.xml' -File -ErrorAction SilentlyContinue).Count -gt 0) {
        Pass 'Fresh consumer package cache contains facade XML documentation'
    } else {
        Fail 'Fresh consumer package cache is missing facade XML documentation.'
    }

    $consumerBin = Join-Path $consumerRoot 'bin'
    $corpusFiles = @()
    if (Test-Path -LiteralPath $consumerBin) {
        $corpusFiles = @(Get-ChildItem -LiteralPath $consumerBin -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '(?i)[\\/]Data[\\/]corpus[\\/]' })
    }
    if ($corpusFiles.Count -eq 0) { Pass 'Consumer output has no copied corpus content; runtime binding and embedded vocal playback succeeded' }
    else { Fail "Consumer output unexpectedly copied $($corpusFiles.Count) corpus files." }
}
catch {
    Fail $_.Exception.Message
}
finally {
    if ($null -eq $script:PreviousNugetPackages) {
        Remove-Item Env:NUGET_PACKAGES -ErrorAction SilentlyContinue
    } else {
        $env:NUGET_PACKAGES = $script:PreviousNugetPackages
    }
    Write-Host "Validation workspace retained at: $tempRoot"
    if ($script:Warnings.Count -gt 0) { Write-Host "Warnings: $($script:Warnings.Count)" -ForegroundColor Yellow }
    if ($script:Failures.Count -gt 0) {
        Write-Host "Failures: $($script:Failures.Count)" -ForegroundColor Red
        foreach ($failure in $script:Failures) { Write-Host " - $failure" -ForegroundColor Red }
        exit 1
    }
    Write-Host 'NuGet validation completed successfully.' -ForegroundColor Green
}
