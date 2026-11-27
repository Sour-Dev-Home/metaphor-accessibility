# Starts Gemini CLI on this repository under the project's rules (#1031): installs the pinned Gemini CLI if another
# version (or none) is there, puts this repository's deny rules (gemini-policy.toml) in the user's Gemini policies
# folder, then runs gemini from the repository root, where .gemini/settings.json loads AGENTS.md. Arguments pass
# through, for example:
#   .\scripts\setup\gemini.ps1
#   .\scripts\setup\gemini.ps1 --version
# Gemini CLI keeps its user folder in GEMINI_CLI_HOME when that is set, else the home folder; this script follows it.
$ErrorActionPreference = 'Stop'
$geminiVersion = '0.71.1'
$policyName = 'metaphor-accessibility.toml'

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$geminiHome = if ($env:GEMINI_CLI_HOME) { $env:GEMINI_CLI_HOME } else { $HOME }

$installed = $null
if (Get-Command gemini -ErrorAction SilentlyContinue) {
    $installed = (& gemini --version 2>$null | Select-Object -First 1)
    if ($installed) { $installed = $installed.Trim() }
}
if ($installed -ne $geminiVersion) {
    Write-Host "Installing Gemini CLI $geminiVersion (found: $(if ($installed) { $installed } else { 'none' }))."
    & npm install -g "@google/gemini-cli@$geminiVersion"
    if ($LASTEXITCODE -ne 0) { throw "npm install of Gemini CLI $geminiVersion failed (exit $LASTEXITCODE)." }
}

$policies = Join-Path $geminiHome '.gemini/policies'
New-Item -ItemType Directory -Force $policies | Out-Null
$source = Join-Path $PSScriptRoot 'gemini-policy.toml'
$target = Join-Path $policies $policyName
$current = if (Test-Path -LiteralPath $target) { Get-Content -Raw -LiteralPath $target } else { $null }
if ($current -ne (Get-Content -Raw -LiteralPath $source)) {
    Copy-Item -LiteralPath $source -Destination $target -Force
    Write-Host "Installed this repository's deny rules in $target."
}

Push-Location $repo
try { & gemini @args }
finally { Pop-Location }
