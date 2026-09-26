<#
.SYNOPSIS
    Developer convenience: builds the solution and prints only errors and warnings.

.DESCRIPTION
    Writes the full MSBuild output to artifacts/build.log and prints a condensed summary.
    Handy while iterating; CI uses build.ps1 and verify.ps1 instead.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Continue'
Set-StrictMode -Version Latest

. "$PSScriptRoot/common.ps1"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repoRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$logPath = Join-Path $artifacts 'build.log'

$dotnet = Get-DotnetPath
& $dotnet build (Join-Path $repoRoot 'FireReplace.sln') --configuration $Configuration --nologo -v m *>&1 |
    Out-File -FilePath $logPath -Encoding utf8

$exit = $LASTEXITCODE

Get-Content $logPath |
    Select-String -Pattern 'error |warning |Build succeeded|Build FAILED' |
    Select-Object -Unique -First 60 |
    ForEach-Object { $_.Line }

Write-Host "exit=$exit  log=$logPath"
exit $exit
