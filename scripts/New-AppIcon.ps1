param([string]$OutputPath = (Join-Path $PSScriptRoot '..\src\PublicCloudDownloader.App\Assets\PublicCloudDownloader.ico'))

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class IconNative { [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr handle); }
'@

$directory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$bitmap = [System.Drawing.Bitmap]::new(256, 256)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(15, 23, 42))
$cloud = [System.Drawing.Drawing2D.GraphicsPath]::new()
$cloud.AddEllipse(36, 86, 82, 82)
$cloud.AddEllipse(73, 43, 123, 123)
$cloud.AddEllipse(145, 91, 76, 76)
$cloud.AddRectangle([System.Drawing.Rectangle]::new(72, 112, 116, 61))
$graphics.FillPath([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(224, 242, 254)), $cloud)
$pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(2, 132, 199), 17)
$pen.StartCap = $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawLine($pen, 128, 111, 128, 218)
$graphics.DrawLine($pen, 91, 180, 128, 218)
$graphics.DrawLine($pen, 165, 180, 128, 218)
$handle = $bitmap.GetHicon()
try {
    $icon = [System.Drawing.Icon]::FromHandle($handle)
    $stream = [System.IO.File]::Create($OutputPath)
    try { $icon.Save($stream) } finally { $stream.Dispose(); $icon.Dispose() }
} finally {
    [IconNative]::DestroyIcon($handle) | Out-Null
    $pen.Dispose(); $cloud.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
