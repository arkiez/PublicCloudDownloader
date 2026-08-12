param(
    [string]$ReleaseDirectory,
    [string]$InstallerPath,
    [string]$ZipPath
)
$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Invoke-SelfTest([string]$root) {
    $exe = Join-Path $root 'PublicCloudDownloader.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Missing application executable: $exe" }
    $process = Start-Process -FilePath $exe -ArgumentList '--self-test' -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw "Self-test failed in $root." }
}
function Test-Payload([string]$root) {
    $resolved = [System.IO.Path]::GetFullPath($root)
    foreach ($name in @('PublicCloudDownloader.exe', 'README.txt', 'data', 'logs')) { if (-not (Test-Path -LiteralPath (Join-Path $resolved $name))) { throw "Release payload is missing $name." } }
    $rootExecutables = @(Get-ChildItem -LiteralPath $resolved -File -Filter *.exe)
    if ($rootExecutables.Count -ne 1 -or $rootExecutables[0].Name -ne 'PublicCloudDownloader.exe') { throw 'Release root must contain exactly one application EXE.' }
    $forbidden = Get-ChildItem -LiteralPath $resolved -Recurse -File | Where-Object { $_.Name -match '^rclone.*\.exe$|\.conf$|credential|account-cache|token-cache' }
    if ($forbidden) { throw "Forbidden release file: $($forbidden[0].FullName)" }
    Invoke-SelfTest $resolved
}

$sourceFiles = Get-ChildItem (Join-Path $repoRoot 'src') -Recurse -File -Include *.cs,*.xaml,*.csproj | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
$retired = 'RcloneConfigService|AccountsWindow|SyncPreview|TransferMode\.Sync|rclone\.exe|OAuthClient|client_secret|UploadToCloud|CloudDestination'
foreach ($file in $sourceFiles) { if ((Get-Content $file.FullName -Raw) -match $retired) { throw "Retired feature identifier found in $($file.FullName)" } }
if ($ReleaseDirectory) { Test-Payload $ReleaseDirectory }

$tempRoot = $null
try {
    if ($ZipPath -or $InstallerPath) {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("pcd-release-test-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $tempRoot | Out-Null
    }
    if ($ZipPath) {
        $portableOne = Join-Path $tempRoot 'portable-one'
        Expand-Archive -LiteralPath ([System.IO.Path]::GetFullPath($ZipPath)) -DestinationPath $portableOne
        Test-Payload $portableOne
        $portableTwo = Join-Path $tempRoot 'portable-two'
        Move-Item -LiteralPath $portableOne -Destination $portableTwo
        Test-Payload $portableTwo
    }
    if ($InstallerPath) {
        $installDir = Join-Path $tempRoot 'installed'
        $install = Start-Process -FilePath ([System.IO.Path]::GetFullPath($InstallerPath)) -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER',"/DIR=$installDir",'/GROUP=Public Cloud Downloader Test','/MERGETASKS=!desktopicon') -Wait -PassThru
        if ($install.ExitCode -ne 0) { throw "Silent installer failed with exit code $($install.ExitCode)." }
        Invoke-SelfTest $installDir
        $external = Join-Path $tempRoot 'external-downloads'; New-Item -ItemType Directory -Path $external | Out-Null
        $sentinel = Join-Path $external 'keep-me.txt'; Set-Content -LiteralPath $sentinel -Value 'keep'
        $uninstaller = Get-ChildItem -LiteralPath $installDir -File -Filter 'unins*.exe' | Select-Object -First 1
        if (-not $uninstaller) { throw 'Installed uninstaller was not found.' }
        $uninstall = Start-Process -FilePath $uninstaller.FullName -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru
        if ($uninstall.ExitCode -ne 0) { throw "Silent uninstaller failed with exit code $($uninstall.ExitCode)." }
        if (-not (Test-Path -LiteralPath $sentinel -PathType Leaf)) { throw 'Uninstaller removed a file outside the application directory.' }
        if (Test-Path -LiteralPath $installDir) { throw 'Uninstaller did not remove the temporary application directory.' }
    }
} finally {
    if ($tempRoot) {
        $resolvedTemp = [System.IO.Path]::GetFullPath($tempRoot)
        $systemTemp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        if ($resolvedTemp.StartsWith($systemTemp, [System.StringComparison]::OrdinalIgnoreCase) -and [System.IO.Path]::GetFileName($resolvedTemp).StartsWith('pcd-release-test-')) {
            if (Test-Path -LiteralPath $resolvedTemp) { Remove-Item -LiteralPath $resolvedTemp -Recurse -Force }
        } else { throw "Refused unsafe cleanup path: $resolvedTemp" }
    }
}
Write-Output 'Release tests passed.'
