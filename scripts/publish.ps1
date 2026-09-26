<#
.SYNOPSIS
    Publishes FireReplace and packages a release zip.

.DESCRIPTION
    Produces a self-contained Windows build so the end user does not have to install .NET, then
    assembles the release layout:

        FireReplace-win-x64/
            FireReplace.exe
            <runtime files>
            assets/icon.png
            README.md
            LICENSE
            platform-tools/README.txt   (setup instructions, not the tools themselves)
            apks/README.txt             (where to put your own APKs)

    Android SDK Platform Tools and the APKs are deliberately NOT bundled. Their licences and
    redistribution terms are not ours to assume, so the zip ships instructions instead.

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.PARAMETER Runtime
    Target runtime identifier. win-x64 (default) or win-arm64.

.PARAMETER Version
    Version stamped into the assembly and the archive name.

.PARAMETER OutputDirectory
    Where to place the publish folder and the zip. Defaults to ./artifacts.

.EXAMPLE
    .\scripts\publish.ps1
    .\scripts\publish.ps1 -Runtime win-arm64 -Version 1.1.0
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',

    [string]$Version = '1.0.0',

    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. "$PSScriptRoot/common.ps1"

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts' }

# AssemblyVersion/FileVersion must be strictly numeric (major.minor.build.revision), so strip any
# pre-release suffix such as "-beta.1" from the given version first.
$numericVersion = ($Version -replace '-.*$', '')
$versionParts = $numericVersion.Split('.')
while ($versionParts.Count -lt 3) { $versionParts += '0' }
$numericVersion = ($versionParts[0..2] -join '.')

$stageName = "FireReplace-$Runtime"
$stageDir = Join-Path $OutputDirectory $stageName
$zipPath = Join-Path $OutputDirectory "$stageName.zip"

Write-Step "Publishing FireReplace $Version for $Runtime"

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
Remove-Item -LiteralPath $stageDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue

# The .ico is derived from the supplied icon.png and is required for the executable icon.
& "$PSScriptRoot/generate-icon.ps1" | Out-Null

Invoke-Dotnet -Arguments @(
    'publish', (Join-Path $repoRoot 'src/FireReplace/FireReplace.csproj'),
    '--configuration', $Configuration,
    '--runtime', $Runtime,
    '--self-contained', 'true',
    '--output', $stageDir,
    '--nologo',
    "-p:Version=$Version",
    "-p:AssemblyVersion=$numericVersion.0",
    "-p:FileVersion=$numericVersion.0",
    '-p:DebugType=none',
    '-p:DebugSymbols=false'
)

$exe = Join-Path $stageDir 'FireReplace.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw 'Publish did not produce FireReplace.exe.'
}

Write-Ok "FireReplace.exe published ($([math]::Round((Get-Item -LiteralPath $exe).Length / 1KB)) KB)"

# ---------------------------------------------------------------- release layout
Write-Step 'Assembling the release layout'

New-Item -ItemType Directory -Force -Path (Join-Path $stageDir 'assets') | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'assets/icon.png') -Destination (Join-Path $stageDir 'assets/icon.png') -Force

foreach ($file in @('README.md', 'LICENSE', 'SECURITY.md')) {
    $source = Join-Path $repoRoot $file
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $stageDir $file) -Force
    }
}

New-Item -ItemType Directory -Force -Path (Join-Path $stageDir 'platform-tools') | Out-Null
@"
Android SDK Platform Tools are not bundled with FireReplace.
Google distributes them under its own terms, so you download them yourself.

1. Download "SDK Platform-Tools for Windows" from
   https://developer.android.com/tools/releases/platform-tools
2. Extract the archive.
3. Copy the CONTENTS of the extracted platform-tools folder into this folder, so that you end up with:

   FireReplace.exe
   platform-tools\adb.exe
   platform-tools\AdbWinApi.dll
   platform-tools\AdbWinUsbApi.dll

AdbWinApi.dll must be present: adb.exe will not start on Windows without it.

FireReplace looks for platform-tools\adb.exe next to FireReplace.exe. If you already have a copy
elsewhere, point Settings > ADB > Platform Tools location at it instead.
"@ | Set-Content -LiteralPath (Join-Path $stageDir 'platform-tools/README.txt') -Encoding utf8

New-Item -ItemType Directory -Force -Path (Join-Path $stageDir 'apks') | Out-Null
@"
Put the APK files you want to install into this folder.

FireReplace never downloads an APK. It only installs files that are already here, and it always
shows you the file name before installing anything.

Recognised file names:

  home-on-fire*.apk    Home on Fire  - redirects the Fire TV Home button
  Projectivy*.apk      Projectivy Launcher - the replacement launcher

Get them from their own projects:

  Home on Fire         https://github.com/toolicious/home-on-fire
  Projectivy Launcher  https://forum.xda-developers.com/ (search for Projectivy Launcher)

Download an APK for the CPU architecture your Fire TV reports on the FireReplace dashboard.
Many Fire TV devices are armeabi-v7a.
"@ | Set-Content -LiteralPath (Join-Path $stageDir 'apks/README.txt') -Encoding utf8

Write-Ok 'Release layout assembled'

# ---------------------------------------------------------------- archive
Write-Step 'Creating the archive'
Compress-Archive -Path (Join-Path $stageDir '*') -DestinationPath $zipPath -CompressionLevel Optimal

$zipSize = [math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 1)
Write-Ok "$([System.IO.Path]::GetFileName($zipPath)) created ($zipSize MB)"
Write-Host ''
Write-Host "  staging: $stageDir"
Write-Host "  archive: $zipPath"
