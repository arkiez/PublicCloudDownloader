param([string]$Executable = (Join-Path $PSScriptRoot '..\dist\PublicCloudDownloader\PublicCloudDownloader.exe'))
$ErrorActionPreference = 'Stop'
$links = @()
if ($env:PCD_GOOGLE_PUBLIC_URL) { $links += @{ Name = 'GoogleDrive'; Url = $env:PCD_GOOGLE_PUBLIC_URL } }
if ($env:PCD_ONEDRIVE_PUBLIC_URL) { $links += @{ Name = 'OneDrivePersonal'; Url = $env:PCD_ONEDRIVE_PUBLIC_URL } }
if ($links.Count -eq 0) { Write-Output 'SKIP: no live public-link environment variables were supplied.'; exit 0 }
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) { throw "Executable not found: $Executable" }
$root = Join-Path ([System.IO.Path]::GetTempPath()) ("pcd-live-smoke-" + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $root | Out-Null
    foreach ($link in $links) {
        $destination = Join-Path $root $link.Name; New-Item -ItemType Directory -Path $destination | Out-Null
        $process = Start-Process -FilePath $Executable -ArgumentList @('--headless-download', $link.Url, $destination) -Wait -PassThru -WindowStyle Hidden
        if ($process.ExitCode -ne 0) { throw "$($link.Name) live smoke test failed." }
        $files = @(Get-ChildItem -LiteralPath $destination -Recurse -File | Where-Object { $_.Name -notmatch '\.partial\.' })
        if ($files.Count -eq 0) { throw "$($link.Name) live smoke test produced no files." }
        if (Get-ChildItem -LiteralPath $destination -Recurse -File | Where-Object { $_.Name -match '\.partial\.' }) { throw "$($link.Name) left a partial file." }
        Write-Output "PASS: $($link.Name) public download"
    }
} finally {
    $resolved = [System.IO.Path]::GetFullPath($root); $temp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    if ($resolved.StartsWith($temp, [System.StringComparison]::OrdinalIgnoreCase) -and [System.IO.Path]::GetFileName($resolved).StartsWith('pcd-live-smoke-')) { if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force } }
}
