param([string]$OutputPath = (Join-Path $PSScriptRoot '..\src\PublicCloudDownloader.App\Assets\PublicCloudDownloader.ico'))

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)

function Scale([double]$value, [int]$size) { [float]($value * $size / 256.0) }

function New-RoundedSquare([int]$size) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $x = Scale 12 $size; $y = Scale 12 $size; $side = Scale 232 $size; $diameter = Scale 108 $size
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $side - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $side - $diameter, $y + $side - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $side - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    $path
}

function New-Cloud([int]$size) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.StartFigure()
    $path.AddBezier((Scale 69 $size), (Scale 154 $size), (Scale 47 $size), (Scale 154 $size), (Scale 30 $size), (Scale 137 $size), (Scale 30 $size), (Scale 115 $size))
    $path.AddBezier((Scale 30 $size), (Scale 115 $size), (Scale 30 $size), (Scale 95 $size), (Scale 45 $size), (Scale 79 $size), (Scale 64 $size), (Scale 76 $size))
    $path.AddBezier((Scale 64 $size), (Scale 76 $size), (Scale 72 $size), (Scale 48 $size), (Scale 97 $size), (Scale 29 $size), (Scale 127 $size), (Scale 29 $size))
    $path.AddBezier((Scale 127 $size), (Scale 29 $size), (Scale 158 $size), (Scale 29 $size), (Scale 184 $size), (Scale 50 $size), (Scale 191 $size), (Scale 79 $size))
    $path.AddBezier((Scale 191 $size), (Scale 79 $size), (Scale 211 $size), (Scale 82 $size), (Scale 226 $size), (Scale 99 $size), (Scale 226 $size), (Scale 119 $size))
    $path.AddBezier((Scale 226 $size), (Scale 119 $size), (Scale 226 $size), (Scale 141 $size), (Scale 208 $size), (Scale 159 $size), (Scale 186 $size), (Scale 159 $size))
    $path.AddLine((Scale 186 $size), (Scale 159 $size), (Scale 69 $size), (Scale 159 $size))
    $path.CloseFigure()
    $path
}

function Get-Stroke([int]$size, [float]$large, [float]$medium, [float]$small) {
    $viewBoxWidth = if ($size -le 16) { $small } elseif ($size -le 48) { $medium } else { $large }
    [float][Math]::Max(1.0, (Scale $viewBoxWidth $size))
}

$directory = Split-Path -Parent ([System.IO.Path]::GetFullPath($OutputPath))
[System.IO.Directory]::CreateDirectory($directory) | Out-Null
$payloads = [System.Collections.Generic.List[byte[]]]::new()

foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = $null
    $roundedSquare = $null
    $cloud = $null
    $tileBrush = $null
    $borderPen = $null
    $cloudPen = $null
    $arrowPen = $null
    $pngStream = $null
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $roundedSquare = New-RoundedSquare $size
        $cloud = New-Cloud $size
        $tileBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 255, 255))
        $ink = [System.Drawing.Color]::FromArgb(17, 17, 17)
        $borderWidth = Get-Stroke $size 12 14 20
        $cloudWidth = Get-Stroke $size 13 18 22
        $arrowWidth = Get-Stroke $size 15 20 24
        $borderPen = [System.Drawing.Pen]::new($ink, $borderWidth)
        $cloudPen = [System.Drawing.Pen]::new($ink, $cloudWidth)
        $arrowPen = [System.Drawing.Pen]::new($ink, $arrowWidth)
        foreach ($pen in @($borderPen, $cloudPen, $arrowPen)) {
            $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
            $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
            $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        }

        $graphics.FillPath($tileBrush, $roundedSquare)
        $graphics.DrawPath($borderPen, $roundedSquare)
        $graphics.DrawPath($cloudPen, $cloud)
        $graphics.DrawLine($arrowPen, (Scale 128 $size), (Scale 94 $size), (Scale 128 $size), (Scale 176 $size))
        $graphics.DrawLine($arrowPen, (Scale 101 $size), (Scale 149 $size), (Scale 128 $size), (Scale 176 $size))
        $graphics.DrawLine($arrowPen, (Scale 155 $size), (Scale 149 $size), (Scale 128 $size), (Scale 176 $size))

        $pngStream = [System.IO.MemoryStream]::new()
        $bitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
        $payloads.Add($pngStream.ToArray())
    }
    finally {
        if ($pngStream) { $pngStream.Dispose() }
        if ($arrowPen) { $arrowPen.Dispose() }
        if ($cloudPen) { $cloudPen.Dispose() }
        if ($borderPen) { $borderPen.Dispose() }
        if ($tileBrush) { $tileBrush.Dispose() }
        if ($cloud) { $cloud.Dispose() }
        if ($roundedSquare) { $roundedSquare.Dispose() }
        if ($graphics) { $graphics.Dispose() }
        $bitmap.Dispose()
    }
}

$fileStream = $null
$writer = $null
try {
    $fileStream = [System.IO.File]::Create([System.IO.Path]::GetFullPath($OutputPath))
    $writer = [System.IO.BinaryWriter]::new($fileStream)
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $payloadOffset = 6 + (16 * $sizes.Count)
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $size = $sizes[$index]
        $payload = $payloads[$index]
        $dimensionByte = if ($size -eq 256) { 0 } else { $size }
        $writer.Write([byte]$dimensionByte)
        $writer.Write([byte]$dimensionByte)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$payload.Length)
        $writer.Write([uint32]$payloadOffset)
        $payloadOffset += $payload.Length
    }
    foreach ($payload in $payloads) { $writer.Write($payload) }
}
finally {
    if ($writer) { $writer.Dispose() }
    elseif ($fileStream) { $fileStream.Dispose() }
}
