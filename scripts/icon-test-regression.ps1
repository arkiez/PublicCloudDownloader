$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'icon-test.ps1'
$canonicalIcon = Join-Path $PSScriptRoot '..\assets\Github-Octicons-Cloud-16.ico'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("pcd-icon-test-" + [Guid]::NewGuid().ToString('N'))
$failures = [System.Collections.Generic.List[string]]::new()

function Write-U32([byte[]]$bytes, [int]$offset, [uint32]$value) {
    [Array]::Copy([BitConverter]::GetBytes($value), 0, $bytes, $offset, 4)
}

function Test-Rejected([string]$name, [string]$expectedMessage, [scriptblock]$mutate) {
    $bytes = [System.IO.File]::ReadAllBytes($canonicalIcon)
    & $mutate $bytes
    $fixture = Join-Path $tempRoot ($name + '.ico')
    [System.IO.File]::WriteAllBytes($fixture, $bytes)
    try {
        & $validator -IconPath $fixture -CanonicalPath $canonicalIcon | Out-Null
        $failures.Add("$name was accepted")
    }
    catch {
        if ($_.Exception.Message -notlike "*$expectedMessage*") {
            $failures.Add("$name returned '$($_.Exception.Message)'")
        }
        else {
            Write-Output "PASS: $name rejected: $($_.Exception.Message)"
        }
    }
}

try {
    [System.IO.Directory]::CreateDirectory($tempRoot) | Out-Null

    & $validator -IconPath $canonicalIcon -CanonicalPath $canonicalIcon | Out-Null

    Test-Rejected 'missing-frame' 'Expected 10 icon frames but found 9.' {
        param([byte[]]$bytes)
        $bytes[4] = 9
        $bytes[5] = 0
    }

    Test-Rejected 'reordered-frames' 'ICO frame sequence does not match the canonical order.' {
        param([byte[]]$bytes)
        $third = [byte[]]::new(16)
        $fourth = [byte[]]::new(16)
        [Array]::Copy($bytes, 38, $third, 0, 16)
        [Array]::Copy($bytes, 54, $fourth, 0, 16)
        [Array]::Copy($fourth, 0, $bytes, 38, 16)
        [Array]::Copy($third, 0, $bytes, 54, 16)
    }

    Test-Rejected 'zero-length-payload' 'payload is too short' {
        param([byte[]]$bytes)
        Write-U32 $bytes 14 0
    }

    Test-Rejected 'invalid-png-ihdr' 'invalid PNG IHDR chunk' {
        param([byte[]]$bytes)
        $payloadOffset = [BitConverter]::ToUInt32($bytes, 18)
        $bytes[$payloadOffset + 12] = [byte][char]'J'
    }

    Test-Rejected 'invalid-dib-header' 'invalid DIB header' {
        param([byte[]]$bytes)
        $payloadOffset = [BitConverter]::ToUInt32($bytes, 50)
        Write-U32 $bytes $payloadOffset 39
    }

    Test-Rejected 'altered-payload-byte' 'does not match the canonical icon binary' {
        param([byte[]]$bytes)
        $bytes[$bytes.Length - 1] = $bytes[$bytes.Length - 1] -bxor 1
    }

    if ($failures.Count -gt 0) { throw "Icon validator regression failures: $($failures -join '; ')" }
    Write-Output 'Icon validator regression tests passed: 6/6'
}
finally {
    if ([System.IO.Directory]::Exists($tempRoot)) {
        [System.IO.Directory]::Delete($tempRoot, $true)
    }
}
