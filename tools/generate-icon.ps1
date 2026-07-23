<#
.SYNOPSIS
    Generates the Scachalka application icon (assets/scachalka.ico) and PNG (assets/logo.png).

.DESCRIPTION
    If assets/logo.png already exists it is used as the source image. Otherwise a branded
    placeholder (dark rounded square + gradient "S" + download arrow + wordmark) is drawn
    with GDI+ so the repository always builds with a real icon and no external dependencies.

    To use your own artwork: put your square PNG at assets/logo.png and re-run this script.

.NOTES
    Windows-only (uses System.Drawing / GDI+). Run from anywhere:
        pwsh tools/generate-icon.ps1
#>
[CmdletBinding()]
param()

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'assets'
$logoPng = Join-Path $assets 'logo.png'
$icoPath = Join-Path $assets 'scachalka.ico'
New-Item -ItemType Directory -Force -Path $assets | Out-Null

function New-PlaceholderLogo([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # Rounded dark background.
    $pad = [int]($size * 0.06)
    $r = [int]($size * 0.22)
    $rect = New-Object System.Drawing.Rectangle($pad, $pad, ($size - 2 * $pad), ($size - 2 * $pad))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rect.X, $rect.Y, $r, $r, 180, 90)
    $path.AddArc($rect.Right - $r, $rect.Y, $r, $r, 270, 90)
    $path.AddArc($rect.Right - $r, $rect.Bottom - $r, $r, $r, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $r, $r, $r, 90, 90)
    $path.CloseFigure()
    $bg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 24, 24, 30))
    $g.FillPath($bg, $path)

    # Gradient "S" wordmark glyph (purple -> blue), the brand mark.
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point($pad, $pad)),
        (New-Object System.Drawing.Point(($size - $pad), ($size - $pad))),
        [System.Drawing.Color]::FromArgb(255, 138, 92, 246),
        [System.Drawing.Color]::FromArgb(255, 47, 128, 255))
    $fontSize = [single]($size * 0.5)
    $font = New-Object System.Drawing.Font('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $glyphRect = New-Object System.Drawing.RectangleF($pad, ($pad - $size * 0.06), ($size - 2 * $pad), ($size - 2 * $pad))
    $g.DrawString('S', $font, $grad, $glyphRect, $fmt)

    # Download arrow under the S.
    $cx = $size / 2.0
    $ay = $size * 0.64
    $aw = $size * 0.16
    $ah = $size * 0.14
    $arrow = New-Object System.Drawing.Drawing2D.GraphicsPath
    $arrow.AddPolygon(@(
        (New-Object System.Drawing.PointF(($cx - $aw * 0.45), $ay)),
        (New-Object System.Drawing.PointF(($cx + $aw * 0.45), $ay)),
        (New-Object System.Drawing.PointF(($cx + $aw * 0.45), ($ay + $ah * 0.5))),
        (New-Object System.Drawing.PointF(($cx + $aw), ($ay + $ah * 0.5))),
        (New-Object System.Drawing.PointF($cx, ($ay + $ah * 1.3))),
        (New-Object System.Drawing.PointF(($cx - $aw), ($ay + $ah * 0.5))),
        (New-Object System.Drawing.PointF(($cx - $aw * 0.45), ($ay + $ah * 0.5)))
    ))
    $g.FillPath($grad, $arrow)

    # Tray line.
    $trayPen = New-Object System.Drawing.Pen(([System.Drawing.Color]::FromArgb(255, 200, 205, 215)), [single]($size * 0.03))
    $trayPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $trayPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $ty = $size * 0.82
    $g.DrawLine($trayPen, [single]($size * 0.34), [single]$ty, [single]($size * 0.66), [single]$ty)

    $g.Dispose()
    return $bmp
}

function Get-SourceBitmap([int]$size) {
    if (Test-Path $logoPng) {
        $src = [System.Drawing.Image]::FromFile($logoPng)
        try {
            $out = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $gg = [System.Drawing.Graphics]::FromImage($out)
            $gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $gg.DrawImage($src, 0, 0, $size, $size)
            $gg.Dispose()
            return $out
        }
        finally { $src.Dispose() }
    }
    return New-PlaceholderLogo $size
}

# Save a 512px logo.png if the user has not supplied one.
if (-not (Test-Path $logoPng)) {
    $master = New-PlaceholderLogo 512
    $master.Save($logoPng, [System.Drawing.Imaging.ImageFormat]::Png)
    $master.Dispose()
    Write-Host "Created placeholder assets/logo.png (replace it with your own artwork)."
}

# Build a PNG-compressed .ico (Vista+) with the standard icon sizes.
$sizes = @(16, 32, 48, 64, 128, 256)
$pngs = @()
foreach ($s in $sizes) {
    $bmp = Get-SourceBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngs += , ($ms.ToArray())
    $ms.Dispose()
}

$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
try {
    $bw.Write([UInt16]0)              # reserved
    $bw.Write([UInt16]1)              # type: icon
    $bw.Write([UInt16]$sizes.Count)   # image count

    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dim = $sizes[$i]
        $bw.Write([Byte]($(if ($dim -ge 256) { 0 } else { $dim })))  # width
        $bw.Write([Byte]($(if ($dim -ge 256) { 0 } else { $dim })))  # height
        $bw.Write([Byte]0)   # palette
        $bw.Write([Byte]0)   # reserved
        $bw.Write([UInt16]1) # color planes
        $bw.Write([UInt16]32) # bpp
        $bw.Write([UInt32]$pngs[$i].Length)
        $bw.Write([UInt32]$offset)
        $offset += $pngs[$i].Length
    }
    foreach ($png in $pngs) { $bw.Write($png) }
}
finally {
    $bw.Dispose()
    $fs.Dispose()
}

Write-Host "Wrote $icoPath ($($sizes -join ', ') px)."
