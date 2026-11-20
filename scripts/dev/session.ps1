<#
.SYNOPSIS
One command for every game session (#437): deploy, run, smoke, spike, inspect, status.

.DESCRIPTION
- deploy: builds the mod, installs it into Reloaded-II's mods folder, enables it for the game (keeping the mods already
  enabled), launches the game through Reloaded-II and waits for the mod's mod-status.json.
- run: deploy in Debug, with the -OptIn files in the mod's folder, then watches the game for -Seconds, prints the mod's
  skipped, failed and ready lines from Reloaded-II's log, says whether the game survived and counts the records in
  message-log.txt without printing game text. With -SaveLog, the game exiting ends the run (quit it when you are done)
  and message-log.txt and transcript.txt are copied into a new timestamped folder there, which must be outside any git
  repository.
- smoke: launches with exactly the -ModFolders mods enabled, in a throwaway Reloaded-II profile, and waits for each one's
  status file.
- spike: scans the exe on disk and the running game for the same signatures (issue #21) and writes a raw-source draft
  to -OutFile. Review it, then save it under docs/raw-sources/.
- inspect: sends each request to the live inspector of a running Debug build (run -OptIn inspector) and prints each
  response. Commands: docs/contributors/how-to/inspect-the-running-game.md.
- status: read-only. What is deployed, the game build, the active opt-ins, whether the inspector is listening, and the
  valid opt-in names.

The game and Reloaded-II are found by the game locator (scripts/player/MetaphorInstall.psm1); METAPHOR_GAME_EXE and
METAPHOR_RELOADED_EXE override it, and -GameExe and -LauncherExe override those.

Every verb leaves the machine as it found it, also after a failure or Ctrl+C: Reloaded-II's root config is restored,
the game is closed unless -KeepGame, and the opt-in files it added are removed. The install itself (the mod's folder
and its place in the game's enabled mods) stays: that is what deploy and run are for.

Each verb ends with one line, "ok: ..." or "failed: ... - what to do". Local paths in the output are shortened to
<game>, <mods>, <reloaded>, <project>, <repo>, <temp>, <save> or ~, so it can go into an issue as it is. Exit codes: 0
ok; 1 failed after the game was launched or the mod was installed, a cleanup failed, or an inspector request answered
err; 2 failed with nothing launched or changed. Only reads the game (ADR 0002).

The work is in scripts/dev/GameSession.psm1, whose header cites the loader facts.

.EXAMPLE
pwsh scripts/dev/session.ps1 run -Seconds 300 -OptIn ui-text-panes,inspector

.EXAMPLE
pwsh scripts/dev/session.ps1 inspect 'base' 'read base+0x10 16'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)] [ValidateSet('deploy', 'run', 'smoke', 'spike', 'inspect', 'status')] [string] $Verb,
    # inspect: the requests, one per argument, sent in order on one connection.
    [Parameter(Position = 1, ValueFromRemainingArguments)] [string[]] $Request,
    # run: how long to watch the game after the mod reports, in seconds (default 120).
    [int] $Seconds,
    # run: opt-in names, as in opt-in-<name>.txt; commas also separate them. status lists the valid ones.
    [string[]] $OptIn,
    # run: a folder outside any git repository for the message log and transcript.
    [string] $SaveLog,
    # deploy, run: leave the game running at the end. Without it the game is closed by killing it (closing its window
    # only asks "Exit game?"), so a save in progress is lost: use a test save.
    [switch] $KeepGame,
    # deploy: the build configuration (default Debug).
    [string] $Configuration,
    # smoke: the folders of the built mods to test, each holding a ModConfig.json.
    [string[]] $ModFolders,
    # spike: where to write the draft (default artifacts/signature-spike/), and the probes to scan for.
    [string] $OutFile,
    [string] $Probes,
    # inspect, status: the inspector's pipe (LiveInspector.PipeName in the mod), and how long inspect waits for it.
    [string] $PipeName,
    [ValidateRange(1, 60000)] [int] $TimeoutMilliseconds,
    # The game executable and Reloaded-II's launcher, when the locator and the environment variables don't find them.
    [string] $GameExe,
    [string] $LauncherExe,
    # The mod project (default src/Metaphor.Hooks), Reloaded-II's root config (default its fixed AppData path), the
    # known-builds list, and how long a launch waits for the mods to report (default 120 s).
    [string] $Project,
    [string] $ReloadedConfig,
    [string] $KnownBuilds,
    [int] $TimeoutSeconds
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'GameSession.psm1') -Force

$options = @{}
foreach ($name in $PSBoundParameters.Keys) {
    if ($name -ne 'Verb' -and $name -notin [Management.Automation.PSCmdlet]::CommonParameters) { $options[$name] = $PSBoundParameters[$name] }
}
exit (Invoke-GameSession $Verb $options)
