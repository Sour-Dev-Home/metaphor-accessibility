<#
.SYNOPSIS
Capture session: deploys the Debug mod, adds the named opt-in files, relaunches the game through Reloaded-II, reports
the hooks and any skipped or failed listener, and with -SaveLog copies the message log and transcript out once the game
exits.

.DESCRIPTION
1. Runs scripts/deploy-dev.ps1 with -Configuration Debug, then closes the game it launched. A deploy replaces the mod's
   folder, so opt-in files can only be added after it.
2. Creates an empty opt-in-<name>.txt in the deployed mod's folder for each name in -OptIns (HookLoader's OptInFile),
   and removes the deploy run's mod-status.json, message-log.txt and transcript.txt.
3. Launches with the user's own Reloaded-II (--launch), waits for the mod's mod-status.json and prints hooks n/m.
4. Prints the mod's skipped, failed and ready lines from Reloaded-II's log of this run
   (docs/raw-sources/reloaded-ii-launch-and-logs-2026-11-13.md, reloaded-ii-log-on-normal-run-2026-11-14.md).
5. With -SaveLog, waits for the game to exit (quit it when the capture is done), then copies message-log.txt and
   transcript.txt into a new timestamped folder in -SaveLog and summarises the message log without printing game text.
   The logs hold game text, so -SaveLog refuses a folder inside a git repository.

Without -SaveLog the game is left running. Local paths in the output are shortened as listen.ps1 shortens them.

Exit codes: 0 the mod is ready with all its hooks (and the logs were saved, with -SaveLog); 1 the deploy or the capture
run failed after launching; 2 the capture couldn't start (nothing launched). Only reads the game (ADR 0002).

.EXAMPLE
pwsh scripts/capture.ps1 -OptIns ui-text-panes,dialogue-window -SaveLog $env:USERPROFILE/Videos/MetaphorCapture
#>
[CmdletBinding()]
param(
    # Opt-in names, as in opt-in-<name>.txt, or the file names themselves. Commas also separate names.
    [string[]] $OptIns = @(),
    # A folder outside any git repository to copy the message log and transcript into, after the game exits.
    [string] $SaveLog,
    # The game executable. Defaults to METAPHOR_GAME_EXE, then to the game as registered in Reloaded-II.
    [string] $GameExe = $env:METAPHOR_GAME_EXE,
    # Reloaded-II's launcher (Reloaded-II.exe). Defaults to METAPHOR_RELOADED_EXE, then to LauncherPath in ReloadedII.json.
    [string] $LauncherExe = $env:METAPHOR_RELOADED_EXE,
    # The mod project, built in Debug. Its folder must hold the ModConfig.json naming its mod ID.
    [string] $Project = (Join-Path $PSScriptRoot '../src/Metaphor.Hooks'),
    # Reloaded-II's root config, at a fixed AppData path the loader doesn't let us move. Its Logs folder is beside it.
    [string] $ReloadedConfig = (Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Reloaded-Mod-Loader-II/ReloadedII.json'),
    [string] $KnownBuilds = (Join-Path $PSScriptRoot '../src/Metaphor.Core/known-game-builds.json'),
    # How long each launch waits for the mod to report.
    [int] $TimeoutSeconds = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReloadedDev.psm1') -Force

$MessageLogFileName = 'message-log.txt' # Metaphor.Core.MessageLog.FileName
$TranscriptFileName = 'transcript.txt' # Metaphor.Core.Announcements.TranscriptAnnouncer.FileName

$folders = [ordered]@{}
function Write-Out([string] $Text) { Write-Host (Hide-LocalPaths $folders $Text) }

$launched = $false
function Stop-Capture([string] $Message) {
    Write-Out "capture $(if ($launched) { 'FAILED' } else { "can't start" }): $Message"
    exit $(if ($launched) { 1 } else { 2 })
}
trap { Stop-Capture $_.Exception.Message }

Add-PathName $folders '<project>' $Project
Add-PathName $folders '<repo>' (Join-Path $PSScriptRoot '..')
Add-PathName $folders '<temp>' ([IO.Path]::GetTempPath())
Add-PathName $folders '~' ([Environment]::GetFolderPath('UserProfile'))
Add-PathName $folders '<reloaded>' (Split-Path $ReloadedConfig)

# The opt-in files, each a bare file name that a hook in the project names, so a typo can't pass unnoticed and no name
# reaches outside the mod's folder.
$sources = @(Get-ChildItem -LiteralPath $Project -Filter *.cs -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object FullName -NotMatch '[\\/](bin|obj)[\\/]')
$optInFiles = foreach ($name in @($OptIns | ForEach-Object { $_ -split ',' } | ForEach-Object Trim | Where-Object { $_ })) {
    $file = if ($name -match '^opt-in-[a-z0-9][a-z0-9-]*\.txt$') { $name }
    elseif ($name -match '^[a-z0-9][a-z0-9-]*$') { "opt-in-$name.txt" }
    else { Stop-Capture "'$name' isn't an opt-in name (lower-case letters, digits and dashes, as in opt-in-<name>.txt)" }
    if (-not ($sources | Select-String -SimpleMatch "`"$file`"" -List)) { Stop-Capture "no hook in $Project opts in with $file" }
    $file
}

# The logs hold game text, so they never land where they could be committed.
if ($SaveLog) {
    $saveFull = [IO.Path]::GetFullPath($PSCmdlet.GetUnresolvedProviderPathFromPSPath($SaveLog))
    for ($dir = $saveFull; $dir; $dir = Split-Path $dir) {
        if (Test-Path -LiteralPath (Join-Path $dir '.git')) {
            Stop-Capture "-SaveLog $saveFull is inside the git repository at $dir; the logs hold game text, so save them outside it"
        }
    }
    Add-PathName $folders '<save>' $saveFull
}

$modConfig = Join-Path $Project 'ModConfig.json'
if (-not (Test-Path -LiteralPath $modConfig)) { Stop-Capture "no ModConfig.json in $Project" }
$modId = [string] (Read-Json $modConfig).ModId
if (-not (Test-Path -LiteralPath $ReloadedConfig)) { Stop-Capture "no Reloaded-II config at $ReloadedConfig" }
$loader = Read-Json $ReloadedConfig
$paths = Resolve-LoaderPaths $loader $GameExe $LauncherExe
$GameExe, $LauncherExe = $paths.GameExe, $paths.LauncherExe
Add-PathName $folders '<mods>' ([string] $loader.ModConfigDirectory)
if ($GameExe) { Add-PathName $folders '<game>' (Split-Path $PSCmdlet.GetUnresolvedProviderPathFromPSPath($GameExe)) }

# 1. Deploy the Debug mod, in its own process so its exit code is real, then close the game it launched.
$deployArgs = @('-Project', $Project, '-Configuration', 'Debug', '-ReloadedConfig', $ReloadedConfig,
    '-KnownBuilds', $KnownBuilds, '-TimeoutSeconds', $TimeoutSeconds)
if ($GameExe) { $deployArgs += '-GameExe', $GameExe }
if ($LauncherExe) { $deployArgs += '-LauncherExe', $LauncherExe }
& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'deploy-dev.ps1') @deployArgs 2>&1 | ForEach-Object { Write-Out "$_" }
$deployExit = $LASTEXITCODE
if ($deployExit -eq 2) { Stop-Capture 'the deploy failed' }
$launched = $true
$gameFull = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($GameExe)
$launcherFull = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($LauncherExe)
Get-Game $gameFull | ForEach-Object { $_.Kill(); $_.WaitForExit(10000) | Out-Null }
if ($deployExit -ne 0) { Stop-Capture 'the deploy failed; closed the game' }
Write-Out 'closed the game to add the opt-in files'

# 2. The opt-in files, and no leftovers from the deploy run to mistake for this one's.
$modFolder = Join-Path ([string] $loader.ModConfigDirectory) $modId
foreach ($file in $StatusFileName, $MessageLogFileName, $TranscriptFileName) {
    Remove-Item -LiteralPath (Join-Path $modFolder $file) -Force -ErrorAction SilentlyContinue
}
foreach ($file in $optInFiles) {
    New-Item -ItemType File -Force (Join-Path $modFolder $file) | Out-Null
    Write-Out "added $file to $modFolder"
}

# 3. Relaunch and wait for the mod. One deadline covers launch, status and the loader's log.
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$launchedAt = [DateTime]::UtcNow
if (-not (Start-WithLoader $launcherFull $gameFull $TimeoutSeconds)) { Stop-Capture 'the launcher did not exit' }
$mods = @([pscustomobject]@{ Id = $modId; Folder = $modFolder })
$ready = Wait-ModStatus $mods $gameFull (Get-GameBuild $gameFull $KnownBuilds).Sha256 $deadline $TimeoutSeconds

# 4. The mod's lines in Reloaded-II's log of this run, read from the open file (the folder listing lags). The mod logs
# its load line just after writing its status, so the file is read until that line is in it.
$logs = Join-Path (Split-Path $ReloadedConfig) 'Logs'
$logPattern = "* ~ $([IO.Path]::GetFileNameWithoutExtension($gameFull)).txt"
$prefix = "[$modId] "
$lines = @()
do {
    $log = Get-ChildItem -LiteralPath $logs -Filter $logPattern -File -ErrorAction SilentlyContinue |
        Where-Object CreationTimeUtc -GE $launchedAt | Sort-Object CreationTimeUtc | Select-Object -Last 1
    if ($log) {
        $stream = [IO.File]::Open($log.FullName, 'Open', 'Read', 'ReadWrite')
        try { $lines = @((New-Object IO.StreamReader($stream)).ReadToEnd() -split "\r?\n" | Where-Object { $_.Contains($prefix) }) }
        finally { $stream.Dispose() }
        if ($lines | Where-Object { $_ -match ' on game build ' }) { break }
    }
    Start-Sleep -Milliseconds 250
} while ((Get-Date) -lt $deadline)
if (-not $log) { Write-Out "no Reloaded-II log of this run in $logs" }
else {
    $shown = @($lines | Where-Object { $_ -match '\bskipped\b|\bfailed\b|\bReady on\b|\bRefused on\b' })
    if ($shown) { $shown | ForEach-Object { Write-Out $_ } }
    else { Write-Out "Reloaded-II's log has no load line from $modId" }
}
if (-not $ready) { Stop-Capture "the mod isn't ready with all its hooks; the game is left running" }

# 5. Save the logs once the game exits.
if (-not $SaveLog) { Write-Out 'capture running: the game is left running'; exit 0 }
Write-Out 'capturing: quit the game when done, and the logs are saved'
while (Get-Game $gameFull) { Start-Sleep -Milliseconds 500 }
$target = Join-Path $saveFull (Get-Date -Format 'yyyy-MM-dd HH.mm.ss')
New-Item -ItemType Directory -Force $target | Out-Null
$saved = foreach ($file in $MessageLogFileName, $TranscriptFileName) {
    $source = Join-Path $modFolder $file
    if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source $target; $file }
    else { Write-Out "no $file in $modFolder" }
}
if (-not $saved) { Stop-Capture "no logs to save: is the mod a Debug build?" }
Write-Out "saved $($saved -join ' and ') to $target"
if ($saved -contains $MessageLogFileName) { Get-MessageLogSummary (Join-Path $target $MessageLogFileName) | ForEach-Object { Write-Out $_ } }
Write-Out 'capture saved'
exit 0
