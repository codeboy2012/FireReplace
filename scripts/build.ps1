<#
.SYNOPSIS
    Restores and builds the FireReplace solution.

.DESCRIPTION
    Builds every project in FireReplace.sln. Use -Configuration Debug for a development build.
    The script does not require the .NET SDK to be on PATH if it is installed in the default
    per-user location.

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.PARAMETER NoRestore
    Skips the implicit restore, for faster repeat builds.

.EXAMPLE
    .\scripts\build.ps1
    .\scripts\build.ps1 -Configuration Debug
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. "$PSScriptRoot/common.ps1"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'FireReplace.sln'

Write-Step "Building FireReplace ($Configuration)"

$arguments = @('build', $solution, '--configuration', $Configuration, '--nologo')
if ($NoRestore) { $arguments += '--no-restore' }

Invoke-Dotnet -Arguments $arguments

Write-Ok "Build succeeded ($Configuration)"
