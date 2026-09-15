param(
    [string]$IndexPath = "docs/index.html"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$resolvedIndexPath = if ([System.IO.Path]::IsPathRooted($IndexPath)) {
    $IndexPath
} else {
    Join-Path $repositoryRoot $IndexPath
}

$commitCount = (git -C $repositoryRoot rev-list --count HEAD).Trim()
$projectCount = (Get-ChildItem -Path $repositoryRoot -Recurse -Filter "*.csproj" -File).Count

# Count discovered cases, not attributes. A theory can expand into many cases,
# so counting [Fact]/[Theory] markers understated the public test metric.
$testProject = Join-Path $repositoryRoot "src/SoundScript.Tests/SoundScript.Tests.csproj"
$listedTests = & dotnet test $testProject -c Debug --list-tests --no-restore --nologo 2>&1
$discoveryExitCode = $LASTEXITCODE

Write-Host "========== BEGIN TEST DISCOVERY OUTPUT =========="
$listedTests | ForEach-Object {
    Write-Host $_
}
Write-Host "=========== END TEST DISCOVERY OUTPUT ==========="
Write-Host "Test discovery exit code: $discoveryExitCode"

if ($discoveryExitCode -ne 0) {
    throw "Unable to discover SoundScript test cases for the site metric."
}

$testCount = @(
    $listedTests |
        Where-Object { $_ -match '^\s+SoundScript\.Tests\.' }
).Count

if ($testCount -eq 0) {
    throw "No SoundScript test cases were discovered for the site metric."
}

$displayTestCount = $testCount.ToString(
    "N0",
    [System.Globalization.CultureInfo]::InvariantCulture
)

$content = Get-Content -LiteralPath $resolvedIndexPath -Raw

function Update-MetricMarker {
    param(
        [string]$Name,
        [string]$Value
    )

    $pattern = "(?s)(<!--METRIC_$Name-->).*?(<!--/METRIC_$Name-->)"

    if (-not [regex]::IsMatch($content, $pattern)) {
        throw "Metric marker not found: $Name"
    }

    $script:content = [regex]::Replace(
        $content,
        $pattern,
        "`${1}$Value`${2}"
    )
}

Update-MetricMarker -Name "COMMITS" -Value "$commitCount+"
Update-MetricMarker -Name "PROJECTS" -Value "$projectCount"
Update-MetricMarker -Name "TESTS" -Value $displayTestCount

Set-Content `
    -LiteralPath $resolvedIndexPath `
    -Value $content `
    -Encoding utf8 `
    -NoNewline

Write-Host "Updated site metrics: $commitCount+ commits, $projectCount projects, $displayTestCount tests."
