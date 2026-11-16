<#
.SYNOPSIS
Listening run: deploys the Debug mod, lets the game run for -Seconds, says whether it survived, and summarises the
mod's message-log.txt by record type, without printing any game text.

.DESCRIPTION
1. Runs scripts/deploy-dev.ps1 with -Configuration Debug, which builds, deploys and launches the mod and waits for its
   mod-status.json. Only Debug builds write message-log.txt (Metaphor.Core.MessageLog).
2. Watches the game for -Seconds and says whether it is still running, or when it exited.
3. With -Close, closes the game.
4. Counts the records in message-log.txt by type (compile, lookup, register, request, and requests by function). The
   log holds game text, so its lines are never printed; it stays in the mod's folder.

Local paths in the output are shortened to <mods>, <reloaded>, <game>, <project>, <repo>, <temp> or ~, so the output can
go into an issue or a raw source as it is.

Exit codes: 0 the game survived and the mod wrote its log; 1 the game exited, the deploy failed after launching, or
there was no log; 2 the deploy couldn't start (nothing launched). Only reads the game (ADR 0002).

.EXAMPLE
pwsh scripts/listen.ps1 -Seconds 300 -Close
#>
[CmdletBinding()]
param(
    # The game executable. Defaults to METAPHOR_GAME_EXE, then to the game as registered in Reloaded-II.
    [string] $GameExe = $env:METAPHOR_GAME_EXE,
    # Reloaded-II's launcher (Reloaded-II.exe). Defaults to METAPHOR_RELOADED_EXE, then to LauncherPath in ReloadedII.json.
    [string] $LauncherExe = $env:METAPHOR_RELOADED_EXE,
    # The mod project, built in Debug. Its folder must hold the ModConfig.json naming its mod ID.
    [string] $Project = (Join-Path $PSScriptRoot '../src/Metaphor.Hooks'),
    # How long to let the game run after the mod reports, in seconds.
    [ValidateRange(1, 86400)] [int] $Seconds = 120,
    # Close the game at the end, by killing it as smoke-test.ps1 does (closing its window only asks "Exit game?"), so
    # a save in progress is lost: use it on a test save. Without it, the game is left running.
    [switch] $Close,
    # Reloaded-II's root config, at a fixed AppData path the loader doesn't let us move.
    [string] $ReloadedConfig = (Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Reloaded-Mod-Loader-II/ReloadedII.json'),
    [string] $KnownBuilds = (Join-Path $PSScriptRoot '../src/Metaphor.Core/known-game-builds.json'),
    # How long the deploy waits for the mod to report.
    [int] $TimeoutSeconds = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReloadedDev.psm1') -Force

$MessageLogFileName = 'message-log.txt' # Metaphor.Core.MessageLog.FileName

# Each folder that can show up in the output, with the name it is shown as. Longer paths go first, so the mods folder
# inside the Reloaded-II folder is named as itself.
$folders = [ordered]@{}
function Add-Folder([string] $Name, [string] $Path) {
    if ($Path) { $folders[$Name] = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/') }
}

function Hide-LocalPaths([string] $Text) {
    foreach ($entry in $folders.GetEnumerator() | Sort-Object { $_.Value.Length } -Descending) {
        foreach ($form in $entry.Value, $entry.Value.Replace('\', '/')) {
            $Text = [regex]::Replace($Text, [regex]::Escape($form), $entry.Key, 'IgnoreCase')
        }
    }
    $Text
}

function Write-Out([string] $Text) { Write-Host (Hide-LocalPaths $Text) }

function Stop-Start([string] $Message) {
    Write-Out "listening run can't start: $Message"
    exit 2
}

# The record counts in a message log, by the word after "<n> ms" (Metaphor.Core.MessageLog), and its requests by the
# function named in via=. Source lines ("  | ...") are counted, not shown.
function Get-MessageLogSummary([string] $Path) {
    # The game holds the log open for writing while it runs.
    $stream = [IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
    try { $lines = (New-Object IO.StreamReader($stream)).ReadToEnd() -split "\r?\n" }
    finally { $stream.Dispose() }

    $types = @{}
    $requests = @{}
    $sourceLines = 0
    $unrecognised = 0
    foreach ($line in $lines) {
        if ($line -eq '') { continue }
        if ($line.StartsWith('  | ')) { $sourceLines++; continue }
        if ($line -match '^\d+ ms ([a-z]+)\b') {
            $types[$Matches[1]] = 1 + $types[$Matches[1]]
            if ($Matches[1] -eq 'request' -and $line -match ' via=([A-Za-z0-9_]+)') { $requests[$Matches[1]] = 1 + $requests[$Matches[1]] }
            continue
        }
        $unrecognised++
    }

    $counts = foreach ($type in $types.Keys | Sort-Object) {
        if ($type -eq 'compile') { "$($types[$type]) compile ($sourceLines source lines)" } else { "$($types[$type]) $type" }
    }
    if ($unrecognised) { $counts = @($counts) + "$unrecognised unrecognised line$(if ($unrecognised -ne 1) { 's' })" }
    "message log: $(if ($counts) { $counts -join ', ' } else { 'empty' })"
    if ($requests.Count) {
        "requests by function: $(($requests.Keys | Sort-Object | ForEach-Object { "$_ $($requests[$_])" }) -join ', ')"
    }
}

$modConfig = Join-Path $Project 'ModConfig.json'
Add-Folder '<project>' $Project
Add-Folder '<repo>' (Join-Path $PSScriptRoot '..')
Add-Folder '<temp>' ([IO.Path]::GetTempPath())
Add-Folder '~' ([Environment]::GetFolderPath('UserProfile'))
Add-Folder '<reloaded>' (Split-Path $ReloadedConfig)
if (-not (Test-Path -LiteralPath $modConfig)) { Stop-Start "no ModConfig.json in $Project" }
$modId = [string] (Read-Json $modConfig).ModId

# The game and mods folder as the deploy finds them, so the output can name them and the watch can find the game.
$loader = if (Test-Path -LiteralPath $ReloadedConfig) { Read-Json $ReloadedConfig }
if ($loader) {
    $GameExe = (Resolve-LoaderPaths $loader $GameExe $LauncherExe).GameExe
    Add-Folder '<mods>' ([string] $loader.ModConfigDirectory)
}
if ($GameExe) { Add-Folder '<game>' (Split-Path $PSCmdlet.GetUnresolvedProviderPathFromPSPath($GameExe)) }

# 1. Deploy the Debug mod, in its own process so its exit code is real. Its output is passed on with paths shortened.
$deployArgs = @('-Project', $Project, '-Configuration', 'Debug', '-ReloadedConfig', $ReloadedConfig,
    '-KnownBuilds', $KnownBuilds, '-TimeoutSeconds', $TimeoutSeconds)
if ($GameExe) { $deployArgs += '-GameExe', $GameExe }
if ($LauncherExe) { $deployArgs += '-LauncherExe', $LauncherExe }
& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'deploy-dev.ps1') @deployArgs 2>&1 | ForEach-Object { Write-Out "$_" }
$deployExit = $LASTEXITCODE
if ($deployExit -ne 0) {
    Write-Out "listening run $(if ($deployExit -eq 2) { "can't start" } else { 'FAILED' }): the deploy failed"
    exit $(if ($deployExit -eq 2) { 2 } else { 1 })
}

# 2. Watch the game.
$gameFull = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($GameExe)
$started = Get-Date
$survived = $true
while (((Get-Date) - $started).TotalSeconds -lt $Seconds) {
    if (-not (Get-Game $gameFull)) { $survived = $false; break }
    Start-Sleep -Milliseconds 250
}
if ($survived) { Write-Out "the game is still running after $Seconds s" }
else { Write-Out "the game exited after $([int] ((Get-Date) - $started).TotalSeconds) s" }

# 3. Close it, when asked.
if ($Close -and $survived) {
    Get-Game $gameFull | ForEach-Object { $_.Kill(); $_.WaitForExit(10000) | Out-Null }
    Write-Out 'closed the game'
}

# 4. Summarise the log.
$log = Join-Path ([string] $loader.ModConfigDirectory) "$modId/$MessageLogFileName"
$hasLog = Test-Path -LiteralPath $log
if ($hasLog) { Get-MessageLogSummary $log | ForEach-Object { Write-Out $_ } }
else { Write-Out "no $MessageLogFileName`: is the mod a Debug build?" }

if (-not $survived -or -not $hasLog) { Write-Out 'listening run FAILED'; exit 1 }
Write-Out 'listening run passed'
exit 0
