param(
    [Parameter(Mandatory)][string]$IndexPath,
    [string]$ReleaseNotesPath = (Join-Path $PSScriptRoot '../RELEASE_NOTES.md'),
    [string]$PropsPath = (Join-Path $PSScriptRoot '../Directory.Build.props')
)

$ErrorActionPreference = 'Stop'

# Only the small inline Markdown vocabulary used by release bullets is rendered.
# Everything else (including raw HTML) is escaped, never injected into the page.
function ConvertTo-ReleaseHtml([string]$Text) {
    $pattern = '`([^`]+)`|\[([^\]]+)\]\(([^\s)]+)\)|\*\*(.+?)\*\*|\*([^*]+)\*'
    $result = [System.Text.StringBuilder]::new()
    $offset = 0
    foreach ($token in [regex]::Matches($Text, $pattern)) {
        [void]$result.Append([System.Net.WebUtility]::HtmlEncode($Text.Substring($offset, $token.Index - $offset)))
        if ($token.Groups[1].Success) {
            [void]$result.Append('<code>' + [System.Net.WebUtility]::HtmlEncode($token.Groups[1].Value) + '</code>')
        } elseif ($token.Groups[2].Success) {
            $href = $token.Groups[3].Value
            if ($href -match '^docs/(.+\.md)(#.*)?$') {
                $href = 'doc.html?p=' + [uri]::EscapeDataString($Matches[1]) + $Matches[2]
            } elseif ($href -notmatch '^https?://') {
                if ($href -match '^[\w./-]+(?:#[\w-]+)?$' -and $href -notmatch '^[/]|(?:^|/)\.\.(?:/|$)') {
                    $href = 'https://github.com/dharangutti/sound-script/blob/main/' + $href
                } else {
                    throw "Unsupported release-note link: $href"
                }
            }
            [void]$result.Append('<a href="' + [System.Net.WebUtility]::HtmlEncode($href) + '">' + (ConvertTo-ReleaseHtml $token.Groups[2].Value) + '</a>')
        } else {
            $tag = if ($token.Groups[4].Success) { 'strong' } else { 'em' }
            $value = if ($token.Groups[4].Success) { $token.Groups[4].Value } else { $token.Groups[5].Value }
            [void]$result.Append("<$tag>" + (ConvertTo-ReleaseHtml $value) + "</$tag>")
        }
        $offset = $token.Index + $token.Length
    }
    [void]$result.Append([System.Net.WebUtility]::HtmlEncode($Text.Substring($offset)))
    $result.ToString()
}

[xml]$props = Get-Content -LiteralPath $PropsPath -Raw
$version = [string]$props.Project.PropertyGroup.Version
$label = [string]$props.Project.PropertyGroup.SoundScriptVersionLabel
if ($version -notmatch '^(\d+)\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
    throw 'Directory.Build.props must define a semantic Version.'
}
$currentMajor = [int]$Matches[1]
if ($label -cne "V$currentMajor") { throw "SoundScriptVersionLabel '$label' does not match Version '$version'." }

$notes = Get-Content -LiteralPath $ReleaseNotesPath -Raw
$sections = [regex]::Matches($notes, '(?ms)^## ([^\r\n]+)\r?\n(.*?)(?=^## |\z)')
$releases = @(
    foreach ($section in $sections) {
        $heading = $section.Groups[1].Value
        if ($heading -notmatch '^[Vv]?(\d+(?:\.\d+){0,2}(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?)\s+[—–-]\s+(.+)$') {
            throw "Unsupported release heading: $heading"
        }
        $releaseVersion = $Matches[1]
        $title = $Matches[2]
        $major = [int]($releaseVersion.Split('.')[0])
        if ($major -lt 8) { continue }
        $bullets = [System.Collections.Generic.List[string]]::new()
        $continuing = $false
        foreach ($line in ($section.Groups[2].Value -split '\r?\n')) {
            if ($line -match '^[-*] (.+)$') {
                $bullets.Add($Matches[1].Trim())
                $continuing = $true
            } elseif ($continuing -and $line -match '^ {2,}\S' -and $line -notmatch '^\s+([-*+] |\d+[.)] )') {
                $bullets[$bullets.Count - 1] += ' ' + $line.Trim()
            } else {
                $continuing = $false
            }
        }
        if ($bullets.Count -eq 0) { throw "No top-level bullets found for release $releaseVersion." }
        [pscustomobject]@{ Version = $releaseVersion; Major = $major; Title = $title; Bullets = $bullets }
    }
)
if ($releases.Count -eq 0) { throw 'No V8+ releases found in RELEASE_NOTES.md.' }
# Release notes are newest first. Legacy V-major headings can only validate the major;
# a full version heading must agree exactly, including patch/prerelease information.
$latest = $releases[0]
if ($latest.Major -ne $currentMajor -or ($latest.Version.Contains('.') -and $latest.Version -cne $version)) {
    throw "Latest release '$($latest.Version)' does not match Directory.Build.props Version '$version'."
}

$index = Get-Content -LiteralPath $IndexPath -Raw
$start = '<!--RELEASE_HISTORY_GENERATED_START-->'
$end = '<!--RELEASE_HISTORY_GENERATED_END-->'
if ([regex]::Matches($index, [regex]::Escape($start)).Count -ne 1 -or
    [regex]::Matches($index, [regex]::Escape($end)).Count -ne 1 -or
    $index.IndexOf($start) -ge $index.IndexOf($end)) {
    throw 'Homepage must contain exactly one ordered pair of release-history markers.'
}
$newline = if ($index.Contains("`r`n")) { "`r`n" } else { "`n" }
$entries = for ($i = 0; $i -lt $releases.Count; $i++) {
    $release = $releases[$i]
    $entryLabel = if ($i -eq 0) { $label } else { 'V' + $release.Version }
    $class = if ($i -eq 0) { 'release-current' } else { 'release-previous' }
    $badge = if ($i -eq 0) { '<span class="release-label">Current</span>' } else { '' }
    $style = 'width:auto;padding:0 0.6rem;'
    if ($i -eq 0) { $style += 'background:rgba(110,231,183,0.2);color:var(--accent);' }
    $bullets = if ($i -eq 0) { $release.Bullets } else { @($release.Bullets[0]) }
    $summary = ($bullets | ForEach-Object { ConvertTo-ReleaseHtml $_ }) -join '<br>'
    '                    <li class="' + $class + '"><span class="step-num" style="' + $style + '">' +
        [System.Net.WebUtility]::HtmlEncode($entryLabel) + '</span><div><strong>' +
        (ConvertTo-ReleaseHtml $release.Title) + '</strong>' + $badge + '<br>' + $summary + '</div></li>'
}
$contentStart = $index.IndexOf($start) + $start.Length
$updated = $index.Substring(0, $contentStart) + $newline + ($entries -join $newline) + $newline +
    '                    ' + $index.Substring($index.IndexOf($end))
[IO.File]::WriteAllText([IO.Path]::GetFullPath($IndexPath), $updated, [System.Text.UTF8Encoding]::new($false))
Write-Host "Generated homepage history: $label ($version), $($releases.Count) releases."
