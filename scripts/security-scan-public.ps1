$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $repoRoot

$patterns = @(
    @{ Name = 'private-key'; Regex = '-----BEGIN (?:RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----' },
    @{ Name = 'github-token'; Regex = '(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})' },
    @{ Name = 'generic-secret'; Regex = '(?i)\b(?:api[_-]?key|password|client[_-]?secret|access[_-]?token|refresh[_-]?token)\b\s*[:=]\s*["'']?([A-Za-z0-9_./+=:-]{16,})' }
)
$placeholder = '(?i)^(?:example|sample|dummy|fake|placeholder|redacted|changeme|not-a-secret|test|x+|a+|0+)[A-Za-z0-9_./+=:-]*$'
$findings = [System.Collections.Generic.List[string]]::new()

function Add-Finding([string]$kind, [string]$context) {
    $entry = "$kind at $context"
    if (-not $findings.Contains($entry)) { $findings.Add($entry) }
}

function Test-ContentLine([string]$line, [string]$context) {
    foreach ($pattern in $patterns) {
        $match = [regex]::Match($line, $pattern.Regex)
        if (-not $match.Success) { continue }
        if ($pattern.Name -eq 'generic-secret' -and $match.Groups.Count -gt 1 -and $match.Groups[1].Value -match $placeholder) { continue }
        Add-Finding $pattern.Name $context
    }
}

function Test-SensitivePath([string]$path, [string]$context) {
    $normalized = $path.Replace('\','/')
    if ($normalized -match '(^|/)(?:\.env(?:\..*)?|credentials?(?:\..*)?|secrets?(?:\..*)?)$') { Add-Finding 'sensitive-file' $context }
    if ($normalized -match '(^|/)(?:data|logs)/.+' -and $normalized -notmatch '/\.gitkeep$') { Add-Finding 'runtime-data' $context }
}
$tracked = @(git ls-files)
if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed.' }
foreach ($relative in $tracked) {
    Test-SensitivePath $relative "working-tree:$relative"
    $path = Join-Path $repoRoot $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    try {
        $lineNumber = 0
        foreach ($line in [System.IO.File]::ReadLines($path)) {
            $lineNumber++
            Test-ContentLine $line "working-tree:${relative}:$lineNumber"
        }
    } catch {
        # Binary/unreadable tracked files are covered by path checks and skipped for text matching.
    }
}

$currentCommit = ''
$currentPath = ''
$history = git log -p --all --no-ext-diff --unified=0 --format='@@COMMIT %H'
if ($LASTEXITCODE -ne 0) { throw 'git history scan failed.' }
foreach ($line in $history) {
    if ($line -match '^@@COMMIT ([0-9a-f]{40})$') { $currentCommit = $Matches[1]; $currentPath = ''; continue }
    if ($line -match '^\+\+\+ b/(.+)$') {
        $currentPath = $Matches[1]
        Test-SensitivePath $currentPath "history:${currentCommit}:$currentPath"
        continue
    }
    if ($line.StartsWith('+') -and -not $line.StartsWith('+++')) {
        Test-ContentLine $line.Substring(1) "history:${currentCommit}:$currentPath"
    }
}

if ($findings.Count -gt 0) {
    Write-Error "Public repository safety scan FAILED with $($findings.Count) finding(s)."
    $findings | Sort-Object | ForEach-Object { Write-Host " - $_" }
    exit 1
}

Write-Output "Public repository safety scan PASS ($($tracked.Count) tracked files + reachable Git history)."
exit 0
