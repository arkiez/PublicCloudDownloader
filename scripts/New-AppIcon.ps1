param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\src\PublicCloudDownloader.App\Assets\PublicCloudDownloader.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

$fullOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
[System.IO.Directory]::CreateDirectory((Split-Path -Parent $fullOutputPath)) | Out-Null
$tempPng = [System.IO.Path]::ChangeExtension($fullOutputPath, '.source.png')

$cloudPath = 'M3.103,10.107 C3.103,5.863 6.548,2.5 10.836,2.5 C14.026,2.5 16.748,4.358 17.935,7.063 L17.945,7.085 17.946,7.091 C21.348,7.345 24,10.095 24,13.536 C24,17.148 21.076,20 17.431,20 H5.017 C2.23,20 0,17.83 0,15.06 A4.899,4.899 0 0 1 3.112,10.479 A7.696,7.696 0 0 1 3.103,10.107 Z M10.836,4 C7.351,4 4.603,6.717 4.603,10.107 C4.603,10.391 4.625,10.709 4.655,10.863 A0.75,0.75 0 0 1 4.103,11.732 C2.583,12.117 1.5,13.444 1.5,15.06 C1.5,16.977 3.032,18.5 5.017,18.5 H17.431 C20.274,18.5 22.5,16.294 22.5,13.536 C22.5,10.777 20.274,8.571 17.431,8.571 A0.75,0.75 0 0 1 16.735,8.101 L16.556,7.655 C15.606,5.5 13.424,4 10.836,4 Z'
$visual = [System.Windows.Media.DrawingVisual]::new()
$context = $visual.RenderOpen()
$context.DrawRoundedRectangle([System.Windows.Media.Brushes]::White, [System.Windows.Media.Pen]::new([System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString('#D4D4D4')), 5.8), [System.Windows.Rect]::new(12,12,232,232), 46.4, 46.4)
$geometry = ([System.Windows.Media.Geometry]::Parse($cloudPath)).Clone()
$geometry.Transform = [System.Windows.Media.MatrixTransform]::new([System.Windows.Media.Matrix]::new(5.8,0,0,5.8,58.4,58.4))
$primary = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString('#171717'))
$context.DrawGeometry($primary, $null, $geometry)
$context.Close()

$bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(256,256,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($visual)
$encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$stream = [System.IO.File]::Create($tempPng)
try { $encoder.Save($stream) } finally { $stream.Dispose() }

$python = @'
from PIL import Image
import sys
source, output = sys.argv[1], sys.argv[2]
im = Image.open(source).convert('RGBA')
im.save(output, format='ICO', sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
'@
$python | python - $tempPng $fullOutputPath
if ($LASTEXITCODE -ne 0) { throw "ICO packaging failed with exit code $LASTEXITCODE." }
Remove-Item -LiteralPath $tempPng -Force
Write-Output "Generated application icon matching the header cloud: $fullOutputPath"