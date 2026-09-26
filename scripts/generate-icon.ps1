<#
.SYNOPSIS
    Produces assets/icon.ico from assets/icon.png.

.DESCRIPTION
    Windows executables need a multi-resolution .ico for the taskbar, Explorer and Alt-Tab.
    This script derives that file from the supplied icon.png; it never invents new artwork.
    Each frame is stored as a PNG inside the ICO container, which Windows Vista and later support.

.PARAMETER Source
    Path to the source PNG. Defaults to assets/icon.png.

.PARAMETER Destination
    Path of the ICO to write. Defaults to assets/icon.ico.

.PARAMETER Force
    Regenerates the ICO even when it is newer than the PNG.
#>
[CmdletBinding()]
param(
    [string]$Source,
    [string]$Destination,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $Source) { $Source = Join-Path $repoRoot 'assets/icon.png' }
if (-not $Destination) { $Destination = Join-Path $repoRoot 'assets/icon.ico' }

if (-not (Test-Path -LiteralPath $Source)) {
    throw "Source icon not found: $Source"
}

if ((-not $Force) -and (Test-Path -LiteralPath $Destination)) {
    $sourceTime = (Get-Item -LiteralPath $Source).LastWriteTimeUtc
    $destTime = (Get-Item -LiteralPath $Destination).LastWriteTimeUtc
    if ($destTime -ge $sourceTime) {
        Write-Host "icon.ico is already up to date."
        return
    }
}

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = New-Object 'System.Collections.Generic.List[byte[]]'
$original = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $Source))

try {
    foreach ($size in $sizes) {
        $bitmap = New-Object System.Drawing.Bitmap $size, $size
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.DrawImage($original, 0, 0, $size, $size)
        }
        finally {
            $graphics.Dispose()
        }

        $stream = New-Object System.IO.MemoryStream
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames.Add($stream.ToArray())
        }
        finally {
            $stream.Dispose()
            $bitmap.Dispose()
        }
    }
}
finally {
    $original.Dispose()
}

$output = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $output

try {
    # ICONDIR
    $writer.Write([uint16]0)              # reserved
    $writer.Write([uint16]1)              # type: icon
    $writer.Write([uint16]$frames.Count)  # image count

    $offset = 6 + (16 * $frames.Count)
    for ($i = 0; $i -lt $frames.Count; $i++) {
        $size = $sizes[$i]
        $data = $frames[$i]

        # ICONDIRENTRY
        $writer.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))  # width, 0 means 256
        $writer.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))  # height
        $writer.Write([byte]0)            # palette colours
        $writer.Write([byte]0)            # reserved
        $writer.Write([uint16]1)          # colour planes
        $writer.Write([uint16]32)         # bits per pixel
        $writer.Write([uint32]$data.Length)
        $writer.Write([uint32]$offset)

        $offset += $data.Length
    }

    foreach ($data in $frames) {
        $writer.Write($data)
    }

    $writer.Flush()
    [System.IO.File]::WriteAllBytes($Destination, $output.ToArray())
}
finally {
    $writer.Dispose()
    $output.Dispose()
}

Write-Host "Wrote $Destination ($((Get-Item -LiteralPath $Destination).Length) bytes, $($frames.Count) frames)."
