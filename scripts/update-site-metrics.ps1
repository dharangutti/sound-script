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
$testMatches = Get-ChildItem -Path (Join-Path $repositoryRoot "src") -Recurse -Filter "*.cs" -File |
    Select-String -Pattern "\[(Fact|Theory|Test|TestCase|TestMethod)\b" -AllMatches
$testCount = ($testMatches | ForEach-Object { $_.Matches.Count } | Measure-Object -Sum).Sum

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

    $script:content = [regex]::Replace($content, $pattern, "`${1}$Value`${2}")
}

Update-MetricMarker -Name "COMMITS" -Value "$commitCount+"
Update-MetricMarker -Name "PROJECTS" -Value "$projectCount"
Update-MetricMarker -Name "TESTS" -Value "$testCount+"

Set-Content -LiteralPath $resolvedIndexPath -Value $content -Encoding utf8 -NoNewline
Write-Host "Updated site metrics: $commitCount+ commits, $projectCount projects, $testCount+ tests."
