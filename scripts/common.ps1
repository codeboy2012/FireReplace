<#
.SYNOPSIS
    Shared helpers for the FireReplace build scripts.
#>

Set-StrictMode -Version Latest

function Write-Step {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host "  OK  $Message" -ForegroundColor Green
}

function Write-Warn {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host "  !!  $Message" -ForegroundColor Yellow
}

function Write-Fail {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host "  XX  $Message" -ForegroundColor Red
}

<#
.SYNOPSIS
    Locates the dotnet CLI, falling back to the default per-user install location.
#>
function Get-DotnetPath {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $candidates = @(
        (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'),
        (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe')
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) { return $candidate }
    }

    throw 'The .NET SDK was not found. Install .NET 8 SDK from https://dotnet.microsoft.com/download and try again.'
}

<#
.SYNOPSIS
    Runs the dotnet CLI and throws when it reports failure.
#>
function Invoke-Dotnet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $dotnet = Get-DotnetPath
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'

    & $dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}
