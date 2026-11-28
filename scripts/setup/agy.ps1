# Starts the Antigravity CLI (agy) on this repository under the project's rules (#1031). agy loads AGENTS.md from the
# repository root by itself; this script adds the deny rules of .claude/settings.json for the session only, from
# agy-settings.json (`--settings`, never saved), and runs agy from the repository root. Arguments pass through:
#   .\scripts\setup\agy.ps1
#   .\scripts\setup\agy.ps1 -p "summarise the open issues"
# agy can't be pinned: Google's installer fetches the latest build, and agy updates itself. Install it once with Google's
# command (docs/contributors/README.md), which puts agy.exe in %LOCALAPPDATA%\agy\bin, not always on PATH.
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$settings = Join-Path $PSScriptRoot 'agy-settings.json'

$agy = (Get-Command agy -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
if (-not $agy) {
    $candidate = Join-Path $env:LOCALAPPDATA 'agy\bin\agy.exe'
    if (Test-Path -LiteralPath $candidate) { $agy = $candidate }
}
if (-not $agy) {
    Write-Host "agy isn't installed. Install it with Google's command, then run this again:"
    Write-Host '  irm https://antigravity.google/cli/install.ps1 | iex'
    exit 1
}

# The agy version the deny rules were last checked on, live. agy updates itself, and an update can change how rules
# match, so a different version gets a warning.
$checkedVersion = '1.5.18'
$version = (& $agy --version 2>$null | Select-Object -First 1)
if ($version) { $version = "$version".Trim() }
if ($version -ne $checkedVersion) {
    Write-Warning "agy is $version; its deny rules were checked on $checkedVersion. Check them again before trusting them: bash scripts/dev/agy-check-rules.sh"
}

Push-Location $repo
try {
    & $agy --settings $settings @args
    exit $LASTEXITCODE
}
finally { Pop-Location }
