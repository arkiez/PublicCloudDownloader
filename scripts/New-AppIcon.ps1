param(
    [string]$SourcePath = (Join-Path $PSScriptRoot '..\assets\Github-Octicons-Cloud-16.ico'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\src\PublicCloudDownloader.App\Assets\PublicCloudDownloader.ico')
)

$ErrorActionPreference = 'Stop'
$expectedHash = '3BD295FCE4CD7F33A563F3B2D60CADAD53583491F29E52B571016E2B9E2B979E'
$fullSourcePath = [System.IO.Path]::GetFullPath($SourcePath)
$fullOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$sourceHash = (Get-FileHash -LiteralPath $fullSourcePath -Algorithm SHA256).Hash
if ($sourceHash -ne $expectedHash) { throw "Source icon SHA-256 is $sourceHash, expected $expectedHash." }

$directory = Split-Path -Parent $fullOutputPath
[System.IO.Directory]::CreateDirectory($directory) | Out-Null
[System.IO.File]::Copy($fullSourcePath, $fullOutputPath, $true)

$outputHash = (Get-FileHash -LiteralPath $fullOutputPath -Algorithm SHA256).Hash
if ($outputHash -ne $expectedHash) { throw "Output icon SHA-256 is $outputHash, expected $expectedHash." }
Write-Output "Icon copied byte-for-byte: $outputHash"
