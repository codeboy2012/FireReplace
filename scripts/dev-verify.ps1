<#
.SYNOPSIS
    Developer convenience wrapper: runs verify.ps1 and writes a condensed report to artifacts.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Continue'

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repoRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

$full = Join-Path $artifacts 'verify-full.log'
$summary = Join-Path $artifacts 'verify-summary.txt'

$arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'verify.ps1'), '-Configuration', $Configuration)
if ($SkipPublish) { $arguments += '-SkipPublish' }

& powershell @arguments *>&1 | Out-File -FilePath $full -Encoding utf8
$exit = $LASTEXITCODE

Get-Content -LiteralPath $full |
    Select-String -Pattern 'error |warning |==> |OK  |!!  |XX  |Passed!|Failed!|total:|failed:|Verification' |
    Select-Object -First 80 |
    ForEach-Object { $_.Line } |
    Out-File -FilePath $summary -Encoding utf8

"exit=$exit" | Add-Content -LiteralPath $summary -Encoding utf8
Write-Host "summary=$summary"
