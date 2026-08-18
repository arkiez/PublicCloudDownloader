param(
    [string]$IconPath = (Join-Path $PSScriptRoot '..\src\PublicCloudDownloader.App\Assets\PublicCloudDownloader.ico'),
    [string]$CanonicalPath = (Join-Path $PSScriptRoot '..\assets\Github-Octicons-Cloud-16.ico')
)

$ErrorActionPreference = 'Stop'
$expectedHash = '3BD295FCE4CD7F33A563F3B2D60CADAD53583491F29E52B571016E2B9E2B979E'
$expectedSizes = @(256, 256, 128, 96, 72, 64, 48, 32, 24, 16)
$expectedKinds = @('PNG', 'PNG', 'DIB', 'DIB', 'DIB', 'DIB', 'DIB', 'DIB', 'DIB', 'DIB')
$expectedPngSizes = @(512, 256)
$fullIconPath = [System.IO.Path]::GetFullPath($IconPath)
$fullCanonicalPath = [System.IO.Path]::GetFullPath($CanonicalPath)
$bytes = [System.IO.File]::ReadAllBytes($fullIconPath)
$canonicalBytes = [System.IO.File]::ReadAllBytes($fullCanonicalPath)

function Read-U16([int]$offset) { [BitConverter]::ToUInt16($bytes, $offset) }
function Read-U32([int]$offset) { [BitConverter]::ToUInt32($bytes, $offset) }
function Read-Be32([int]$offset) {
    ([uint32]$bytes[$offset] -shl 24) -bor ([uint32]$bytes[$offset + 1] -shl 16) -bor
    ([uint32]$bytes[$offset + 2] -shl 8) -bor [uint32]$bytes[$offset + 3]
}

if ($bytes.Length -lt 6 -or (Read-U16 0) -ne 0 -or (Read-U16 2) -ne 1) { throw 'Invalid ICO header.' }
$count = Read-U16 4
if ($count -ne $expectedSizes.Count) { throw "Expected $($expectedSizes.Count) icon frames but found $count." }
$directoryLength = 6 + (16 * $count)
if ($bytes.Length -lt $directoryLength) { throw 'ICO directory is truncated.' }

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

    $payloadStart = [uint64]$offset
    $payloadLength = [uint64]$length
    $fileLength = [uint64]$bytes.Length
    if ($payloadStart -lt [uint64]$directoryLength -or
        $payloadStart -gt $fileLength -or
        $payloadLength -gt ($fileLength - $payloadStart)) {
        throw "Frame $index is outside the ICO payload."
    }

    if ($expectedKinds[$index] -eq 'PNG') {
        if ($payloadLength -lt 33) { throw "Frame $index payload is too short for a PNG IHDR chunk." }
        $pngSignature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
        for ($signatureIndex = 0; $signatureIndex -lt $pngSignature.Count; $signatureIndex++) {
            if ($bytes[$offset + $signatureIndex] -ne $pngSignature[$signatureIndex]) {
                throw "Frame $index is not PNG-compressed."
            }
        }
        if ((Read-Be32 ($offset + 8)) -ne 13 -or
            $bytes[$offset + 12] -ne 73 -or $bytes[$offset + 13] -ne 72 -or
            $bytes[$offset + 14] -ne 68 -or $bytes[$offset + 15] -ne 82) {
            throw "Frame $index has an invalid PNG IHDR chunk."
        }
        $expectedPngSize = $expectedPngSizes[$index]
        if ((Read-Be32 ($offset + 16)) -ne $expectedPngSize -or (Read-Be32 ($offset + 20)) -ne $expectedPngSize) {
            throw "Frame $index PNG dimensions do not match the canonical payload."
        }
        if ($bytes[$offset + 24] -ne 8 -or $bytes[$offset + 25] -ne 6) {
            throw "Frame $index is not 32-bit RGBA PNG."
        }
    }
    else {
        if ($payloadLength -lt 40) { throw "Frame $index payload is too short for a DIB header." }
        $headerSize = Read-U32 $offset
        $dibWidth = [BitConverter]::ToInt32($bytes, $offset + 4)
        $storedHeight = [BitConverter]::ToInt32($bytes, $offset + 8)
        $dibPlanes = Read-U16 ($offset + 12)
        $dibBits = Read-U16 ($offset + 14)
        $compression = Read-U32 ($offset + 16)
        if ($headerSize -ne 40 -or $dibWidth -ne $width -or $storedHeight -ne (2 * $height) -or
            $dibPlanes -ne 1 -or $dibBits -ne 32 -or $compression -ne 0) {
            throw "Frame $index has an invalid DIB header."
        }
    }

    $actualSizes += $width
}

if (($actualSizes -join ',') -ne ($expectedSizes -join ',')) { throw 'ICO frame sequence does not match the canonical order.' }
$canonicalHash = (Get-FileHash -LiteralPath $fullCanonicalPath -Algorithm SHA256).Hash
if ($canonicalHash -ne $expectedHash) { throw "Canonical icon SHA-256 is $canonicalHash, expected $expectedHash." }
if (-not [System.Collections.StructuralComparisons]::StructuralEqualityComparer.Equals($bytes, $canonicalBytes)) {
    throw 'Icon does not match the canonical icon binary.'
}
Write-Output "Icon test passed: $($actualSizes -join ', ') ($expectedHash)"
