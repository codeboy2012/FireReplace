<#
.SYNOPSIS
    Launches the built FireReplace executable, confirms it reaches a rendered window, then closes it.

.DESCRIPTION
    A smoke test for the UI: the application must start, create a window and stay alive without the
    global exception handler firing. Any startup failure is reported through the exit code and the
    report file. This does not touch a Fire TV.

.PARAMETER Configuration
    Build configuration to run. Defaults to Debug.

.PARAMETER Seconds
    How long to let the application run before closing it.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [int]$Seconds = 10
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repoRoot "src/FireReplace/bin/$Configuration/net8.0-windows/FireReplace.exe"
$reportDir = Join-Path $repoRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$report = Join-Path $reportDir 'smoke-run.txt'

if (-not (Test-Path -LiteralPath $exe)) {
    "MISSING $exe" | Set-Content $report
    Write-Host "FireReplace.exe not found. Build first."
    exit 1
}

. "$PSScriptRoot/common.ps1"

# A framework-dependent build needs to find the shared runtime. That is automatic for a normal
# machine-wide .NET install; when the SDK lives in a per-user folder, DOTNET_ROOT points the host at it.
if (-not $env:DOTNET_ROOT) {
    $dotnetDir = Split-Path -Parent (Get-DotnetPath)
    if ($dotnetDir -and (Test-Path -LiteralPath (Join-Path $dotnetDir 'host/fxr'))) {
        $env:DOTNET_ROOT = $dotnetDir
    }
}

$lines = @("exe=$exe", "started=$(Get-Date -Format o)", "dotnetRoot=$($env:DOTNET_ROOT)")
$process = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds $Seconds

if ($process.HasExited) {
    $lines += "result=EXITED_EARLY"
    $lines += "exitCode=$($process.ExitCode)"
    $lines | Set-Content $report
    Write-Host "FireReplace exited early with code $($process.ExitCode)."
    exit 1
}

$process.Refresh()
$lines += "result=ALIVE"
$lines += "mainWindowTitle=$($process.MainWindowTitle)"
$lines += "mainWindowHandle=$($process.MainWindowHandle)"
$lines += "workingSetMB=$([math]::Round($process.WorkingSet64 / 1MB, 1))"
$lines += "startupMs=$([math]::Round(((Get-Date) - $process.StartTime).TotalMilliseconds))"

Stop-Process -Id $process.Id -Force
$lines += "stopped=$(Get-Date -Format o)"
$lines | Set-Content $report

Write-Host ($lines -join [System.Environment]::NewLine)

if ($process.MainWindowHandle -eq 0) {
    Write-Host 'The process was alive but never created a window.'
    exit 1
}

exit 0
