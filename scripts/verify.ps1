<#
.SYNOPSIS
    Runs the full verification pass: restore, build, test, asset checks and a publish check.

.DESCRIPTION
    This is what CI runs, and it is the fastest way to confirm a clean checkout is healthy.
    Nothing here touches a Fire TV: every test uses recorded ADB output.

    Steps:
      1. Restore
      2. Build (warnings are surfaced, not hidden)
      3. Run the unit tests
      4. Verify the branding assets exist and that icon.ico can be derived from icon.png
      5. Verify the application publishes

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.PARAMETER SkipPublish
    Skips the publish check, which is the slowest step.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. "$PSScriptRoot/common.ps1"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'FireReplace.sln'
$artifacts = Join-Path $repoRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

$failures = New-Object 'System.Collections.Generic.List[string]'

# ---------------------------------------------------------------- restore
Write-Step 'Restoring packages'
Invoke-Dotnet -Arguments @('restore', $solution)
Write-Ok 'Restore complete'

# ---------------------------------------------------------------- build
Write-Step "Building ($Configuration)"
Invoke-Dotnet -Arguments @('build', $solution, '--configuration', $Configuration, '--no-restore', '--nologo')
Write-Ok 'Build complete'

# ---------------------------------------------------------------- tests
Write-Step 'Running tests'
Invoke-Dotnet -Arguments @(
    'test', $solution,
    '--configuration', $Configuration,
    '--no-build',
    '--nologo',
    '--logger', "trx;LogFileName=$(Join-Path $artifacts 'test-results.trx')"
)
Write-Ok 'All tests passed'

# ---------------------------------------------------------------- assets
Write-Step 'Verifying branding assets'

$iconPng = Join-Path $repoRoot 'assets/icon.png'
if (-not (Test-Path -LiteralPath $iconPng)) {
    $failures.Add('assets/icon.png is missing. It is the project''s only branding asset.')
}
else {
    Write-Ok "assets/icon.png present ($([math]::Round((Get-Item -LiteralPath $iconPng).Length / 1KB)) KB)"
}

$iconIco = Join-Path $repoRoot 'assets/icon.ico'
if (-not (Test-Path -LiteralPath $iconIco)) {
    Write-Warn 'assets/icon.ico missing; generating it from icon.png'
    & "$PSScriptRoot/generate-icon.ps1" | Out-Null
}

if (Test-Path -LiteralPath $iconIco) {
    Write-Ok "assets/icon.ico present ($([math]::Round((Get-Item -LiteralPath $iconIco).Length / 1KB)) KB)"
}
else {
    $failures.Add('assets/icon.ico could not be produced from assets/icon.png.')
}

# The window icon is served from the embedded resource, so it must be in the build output assembly.
$appAssembly = Join-Path $repoRoot "src/FireReplace/bin/$Configuration/net8.0-windows/FireReplace.dll"
if (Test-Path -LiteralPath $appAssembly) {
    Write-Ok 'FireReplace.dll built'
}
else {
    $failures.Add("Expected build output not found: $appAssembly")
}

# ---------------------------------------------------------------- publish
if (-not $SkipPublish) {
    Write-Step 'Verifying publish'
    $publishDir = Join-Path $artifacts 'verify-publish'
    Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue

    Invoke-Dotnet -Arguments @(
        'publish', (Join-Path $repoRoot 'src/FireReplace/FireReplace.csproj'),
        '--configuration', $Configuration,
        '--runtime', 'win-x64',
        '--self-contained', 'true',
        '--output', $publishDir,
        '--nologo'
    )

    $exe = Join-Path $publishDir 'FireReplace.exe'
    if (Test-Path -LiteralPath $exe) {
        Write-Ok "Published FireReplace.exe ($([math]::Round((Get-Item -LiteralPath $exe).Length / 1KB)) KB)"
    }
    else {
        $failures.Add('Publish did not produce FireReplace.exe.')
    }

    foreach ($required in @('FireReplace.dll', 'FireReplace.Core.dll', 'FireReplace.runtimeconfig.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $publishDir $required))) {
            $failures.Add("Publish output is missing $required.")
        }
    }

    Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
}
else {
    Write-Warn 'Publish check skipped'
}

# ---------------------------------------------------------------- result
Write-Step 'Result'

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) { Write-Fail $failure }
    throw "Verification failed with $($failures.Count) problem(s)."
}

Write-Ok 'Verification passed'
