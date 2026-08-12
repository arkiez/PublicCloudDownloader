$ErrorActionPreference = 'Stop'

function Find-Iscc {
    if ($env:ISCC_PATH -and (Test-Path -LiteralPath $env:ISCC_PATH -PathType Leaf)) { return [System.IO.Path]::GetFullPath($env:ISCC_PATH) }
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe')
    )
    foreach ($candidate in $candidates) { if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) { return [System.IO.Path]::GetFullPath($candidate) } }
    return $null
}

$iscc = Find-Iscc
if (-not $iscc) {
    $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
    if (-not $winget) { throw 'Inno Setup is required. Install it from https://jrsoftware.org/isdl.php' }
    & $winget.Source install --id JRSoftware.InnoSetup -e -s winget --scope user --silent --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) { throw "winget could not install Inno Setup (exit $LASTEXITCODE)." }
    $iscc = Find-Iscc
}
if (-not $iscc) { throw 'Inno Setup was not found after installation. Install it from https://jrsoftware.org/isdl.php' }
Write-Output $iscc
