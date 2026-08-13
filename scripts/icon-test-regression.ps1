$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'icon-test.ps1'
$validIcon = Join-Path $PSScriptRoot '..\src\PublicCloudDownloader.App\Assets\PublicCloudDownloader.ico'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("pcd-icon-test-" + [Guid]::NewGuid().ToString('N'))
$failures = [System.Collections.Generic.List[string]]::new()

function Write-U32([byte[]]$bytes, [int]$offset, [uint32]$value) {
    [Array]::Copy([BitConverter]::GetBytes($value), 0, $bytes, $offset, 4)
}

function Test-Rejected([string]$name, [scriptblock]$mutate) {
    $bytes = [System.IO.File]::ReadAllBytes($validIcon)
    & $mutate $bytes
    $fixture = Join-Path $tempRoot ($name + '.ico')
    [System.IO.File]::WriteAllBytes($fixture, $bytes)
    try {
        & $validator -IconPath $fixture | Out-Null
        $failures.Add("$name was accepted")
    }
    catch {
        Write-Output "PASS: $name rejected: $($_.Exception.Message)"
    }
}

try {
    [System.IO.Directory]::CreateDirectory($tempRoot) | Out-Null

    Test-Rejected 'zero-length-payload' {
        param([byte[]]$bytes)
        Write-U32 $bytes 14 0
    }

    Test-Rejected 'invalid-ihdr-length' {
        param([byte[]]$bytes)
        $payloadOffset = [BitConverter]::ToUInt32($bytes, 18)
        $bytes[$payloadOffset + 8] = 0
        $bytes[$payloadOffset + 9] = 0
        $bytes[$payloadOffset + 10] = 0
        $bytes[$payloadOffset + 11] = 12
    }

    Test-Rejected 'invalid-ihdr-type' {
        param([byte[]]$bytes)
        $payloadOffset = [BitConverter]::ToUInt32($bytes, 18)
        $bytes[$payloadOffset + 12] = [byte][char]'J'
    }

    if ($failures.Count -gt 0) { throw "Icon validator regression failures: $($failures -join '; ')" }
    Write-Output 'Icon validator regression tests passed: 3/3'
}
finally {
    if ([System.IO.Directory]::Exists($tempRoot)) {
        [System.IO.Directory]::Delete($tempRoot, $true)
    }
}
