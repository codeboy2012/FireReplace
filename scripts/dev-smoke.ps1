<#
.SYNOPSIS
    Developer convenience: build, launch, and report the result plus any crash log entries.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [int]$Seconds = 10
)

$ErrorActionPreference = 'Continue'

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repoRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

$crashLog = Join-Path $env:LOCALAPPDATA 'FireReplace\crash.log'
$report = Join-Path $artifacts 'dev-smoke.txt'

Remove-Item -LiteralPath $crashLog -ErrorAction SilentlyContinue
Set-Content -LiteralPath $report -Value '--- build ---' -Encoding utf8

& "$PSScriptRoot\dev-check.ps1" -Configuration $Configuration 2>&1 | Add-Content -LiteralPath $report -Encoding utf8

Add-Content -LiteralPath $report -Value '--- smoke ---' -Encoding utf8
& "$PSScriptRoot\smoke-run.ps1" -Configuration $Configuration -Seconds $Seconds 2>&1 | Add-Content -LiteralPath $report -Encoding utf8

Add-Content -LiteralPath $report -Value '--- crash log ---' -Encoding utf8
if (Test-Path -LiteralPath $crashLog) {
    Get-Content -LiteralPath $crashLog -TotalCount 25 | Add-Content -LiteralPath $report -Encoding utf8
}
else {
    Add-Content -LiteralPath $report -Value '(no crash log)' -Encoding utf8
}

Write-Host "report=$report"
