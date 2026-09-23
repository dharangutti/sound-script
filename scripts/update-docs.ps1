<#
.SYNOPSIS
Regenerate approved current-state documentation blocks, or check without writing.
.DESCRIPTION
Requires Node.js 22+ and PowerShell 7. Facts come from project configuration and
docs/release-state.json. Run from any directory; no network access is performed.
#>
param([switch]$Check)
$ErrorActionPreference = 'Stop'
$arguments = @((Join-Path $PSScriptRoot 'docs-state.mjs'))
if ($Check) { $arguments += '--check' }
& node @arguments
if ($LASTEXITCODE -ne 0) { throw 'Documentation validation failed; see file/category/check diagnostics above.' }
