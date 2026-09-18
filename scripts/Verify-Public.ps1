[CmdletBinding()]
param(
    [switch]$TrackedOnly
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$findings = [System.Collections.Generic.List[string]]::new()
$excludedDirectories = @('.git', 'bin', 'obj', 'publish', 'artifacts', '.vs', '.idea', '.vscode')
$textExtensions = @('.cs', '.csproj', '.json', '.example', '.md', '.ps1', '.sh', '.yml', '.yaml', '.txt', '.xml', '.config', '.ini')
$blockedNamePatterns = @(
    'SshTunnelTray.json',
    '*.cmd', '*.log', '*.dmp', '*.dump', '*.bak', '*.tmp',
    '.env', '.env.*', 'secrets.*', 'credentials.*', 'passwords.*',
    '*.pem', '*.key', '*.ppk', '*.p12', '*.pfx', '*.jks', '*.kdbx',
    'id_rsa', 'id_rsa.*', 'id_ecdsa', 'id_ecdsa.*',
    'id_ed25519', 'id_ed25519.*', 'id_dsa', 'id_dsa.*',
    'authorized_keys', 'known_hosts'
)

function Get-RelativePath([string]$fullPath) {
    $relative = $fullPath.Substring($root.Length).TrimStart([char[]]'\/')
    return $relative.Replace('\', '/')
}

function Add-Finding([string]$message) {
    [void]$findings.Add($message)
}

function Get-Candidates {
    if ($TrackedOnly) {
        if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
            throw 'Для -TrackedOnly требуется git.'
        }

        $tracked = @(git -C $root ls-files)
        if ($LASTEXITCODE -ne 0) {
            throw 'git ls-files завершился с ошибкой.'
        }

        foreach ($relative in $tracked) {
            $fullPath = Join-Path $root $relative
            if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
                Get-Item -LiteralPath $fullPath
            }
        }
        return
    }

    Get-ChildItem -LiteralPath $root -File -Recurse -Force | Where-Object {
        $relative = Get-RelativePath $_.FullName
        -not ($excludedDirectories | Where-Object {
            $relative -eq $_ -or $relative.StartsWith("$_/", [StringComparison]::OrdinalIgnoreCase)
        })
    }
}

$items = @(Get-Candidates)
foreach ($item in $items) {
    $relative = Get-RelativePath $item.FullName

    foreach ($pattern in $blockedNamePatterns) {
        if ($item.Name -like $pattern) {
            Add-Finding "Запрещённый файл: $relative"
            break
        }
    }

    if ($relative -match '(^|/)(bin|obj|publish|dist|build|out|generated)(/|$)') {
        Add-Finding "Каталог сборки/генерации: $relative"
    }

    if ($item.Length -eq 0 -or $item.Length -ge 10MB) {
        continue
    }

    if ($item.Name -eq 'Verify-Public.ps1') {
        continue
    }

    $extension = $item.Extension.ToLowerInvariant()
    if ($textExtensions -notcontains $extension) {
        continue
    }

    $text = [System.IO.File]::ReadAllText($item.FullName)
    $secretPatterns = @(
        '(?i)(github_pat|ghp_|gho_|ghs_|ghr_|github_token)\s*[:=]\s*[A-Za-z0-9_\-]{20,}',
        '(?i)(api[_-]?key|access[_-]?token|client[_-]?secret)\s*[:=]\s*[''\"]?[A-Za-z0-9_./+=:-]{12,}',
        '(?im)-----BEGIN (?:OPENSSH|RSA|EC|DSA|PGP) PRIVATE KEY-----',
        '(?i)\bAKIA[0-9A-Z]{16}\b',
        '(?i)"(?:password|token|secret|privateKey)"\s*:\s*"(?!null|YOUR_|your-|example)[^"]+"'
    )

    foreach ($pattern in $secretPatterns) {
        if ($text -match $pattern) {
            Add-Finding "Возможный секрет в: $relative"
            break
        }
    }

    $userPathPattern = '(?i)(?:[A-Z]:\\Users\\|/home/|/Users/)(?!YOUR_NAME(?:\\|/)|USERNAME(?:\\|/)|<)[^\r\n''"]+'
    if ($text -match $userPathPattern) {
        Add-Finding "Абсолютный пользовательский путь в: $relative"
    }
}

if ($findings.Count -gt 0) {
    $findings | Sort-Object -Unique | ForEach-Object { Write-Error $_ }
    exit 1
}

$scope = if ($TrackedOnly) { 'tracked files' } else { 'working tree without generated output' }
Write-Output "PUBLIC CHECK PASSED ($scope)"
