param([string]$IconPath = (Join-Path $PSScriptRoot '..\src\PublicCloudDownloader.App\Assets\PublicCloudDownloader.ico'))
$ErrorActionPreference = 'Stop'
$expectedSizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$bytes = [System.IO.File]::ReadAllBytes([System.IO.Path]::GetFullPath($IconPath))

function Read-U16([int]$offset) { [BitConverter]::ToUInt16($bytes, $offset) }
function Read-U32([int]$offset) { [BitConverter]::ToUInt32($bytes, $offset) }
function Read-Be32([int]$offset) {
    ([uint32]$bytes[$offset] -shl 24) -bor ([uint32]$bytes[$offset + 1] -shl 16) -bor
    ([uint32]$bytes[$offset + 2] -shl 8) -bor [uint32]$bytes[$offset + 3]
}

if ($bytes.Length -lt 6 -or (Read-U16 0) -ne 0 -or (Read-U16 2) -ne 1) { throw 'Invalid ICO header.' }
$count = Read-U16 4
if ($count -ne $expectedSizes.Count) { throw "Expected $($expectedSizes.Count) icon frames but found $count." }

$actualSizes = @()
for ($index = 0; $index -lt $count; $index++) {
    $entry = 6 + (16 * $index)
    $width = if ($bytes[$entry] -eq 0) { 256 } else { [int]$bytes[$entry] }
    $height = if ($bytes[$entry + 1] -eq 0) { 256 } else { [int]$bytes[$entry + 1] }
    $planes = Read-U16 ($entry + 4)
    $bits = Read-U16 ($entry + 6)
    $length = Read-U32 ($entry + 8)
    $offset = Read-U32 ($entry + 12)
    if ($width -ne $height -or $planes -ne 1 -or $bits -ne 32) { throw "Invalid ${width}x${height} frame metadata." }
    if ($offset + $length -gt $bytes.Length) { throw "Frame $width is outside the ICO payload." }
    $pngSignature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
    for ($signatureIndex = 0; $signatureIndex -lt $pngSignature.Count; $signatureIndex++) {
        if ($bytes[$offset + $signatureIndex] -ne $pngSignature[$signatureIndex]) {
            throw "Frame $width is not PNG-compressed."
        }
    }
    if ((Read-Be32 ($offset + 16)) -ne $width -or (Read-Be32 ($offset + 20)) -ne $height) { throw "Frame $width PNG dimensions do not match its ICO entry." }
    if ($bytes[$offset + 24] -ne 8 -or $bytes[$offset + 25] -ne 6) { throw "Frame $width is not 32-bit RGBA PNG." }
    $actualSizes += $width
}

if (Compare-Object $expectedSizes ($actualSizes | Sort-Object)) { throw 'ICO frame dimensions do not match the approved size set.' }
if (@($actualSizes | Select-Object -Unique).Count -ne $actualSizes.Count) { throw 'ICO contains duplicate dimensions.' }
Write-Output "Icon test passed: $($actualSizes -join ', ')"
