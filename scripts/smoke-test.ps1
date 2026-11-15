<#
.SYNOPSIS
In-game smoke test: launches the game through Reloaded-II with exactly the test mods enabled, waits for each mod's
status file, then closes the game and puts the loader config back.

.DESCRIPTION
1. Hashes the game exe and refuses to run on a build missing from the known-builds list.
2. Backs up ReloadedII.json, then points its folders at a throwaway profile holding only the test mods.
3. Launches with the user's own Reloaded-II (--launch).
4. Waits for each test mod's mod-status.json (ready or refused, the build, hooks loaded out of expected).
5. Closes the game and restores the config, including after a failure. A backup left by a killed run is restored first.

Exit codes: 0 every test mod is ready with all its hooks; 1 a mod refused, timed out, or the game exited; 2 the test
couldn't start. Paste the output into PRs that touch hooking, loading or injection.

Loader facts: docs/raw-sources/reloaded-ii-config-location-2026-11-13.md, reloaded-ii-launch-and-logs-2026-11-13.md,
reloaded-ii-app-and-mod-config-2026-11-15.md and reloaded-ii-clean-exit-2026-11-14.md.

.EXAMPLE
pwsh scripts/smoke-test.ps1 -ModFolders <built test mod folder>
#>
[CmdletBinding()]
param(
    # The game executable. Defaults to METAPHOR_GAME_EXE.
    [string] $GameExe = $env:METAPHOR_GAME_EXE,
    # Reloaded-II's launcher (Reloaded-II.exe). Defaults to METAPHOR_RELOADED_EXE.
    [string] $LauncherExe = $env:METAPHOR_RELOADED_EXE,
    # The folders of the mods to test, each holding a ModConfig.json.
    [Parameter(Mandatory)] [string[]] $ModFolders,
    # Reloaded-II's root config, at a fixed AppData path the loader doesn't let us move.
    [string] $ReloadedConfig = (Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Reloaded-Mod-Loader-II/ReloadedII.json'),
    [string] $KnownBuilds = (Join-Path $PSScriptRoot '../src/Metaphor.Core/known-game-builds.json'),
    [int] $TimeoutSeconds = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$StatusFileName = 'mod-status.json' # Metaphor.Core.ModStatusFile.FileName
$PollMilliseconds = 500
$ProfileFolders = [ordered]@{
    ApplicationConfigDirectory = 'Apps'
    ModConfigDirectory         = 'Mods'
    ModUserConfigDirectory     = 'User/Mods'
    MiscConfigDirectory        = 'User/Misc'
    PluginConfigDirectory      = 'Plugins'
}

function Stop-Start([string] $Message) {
    Write-Host "smoke test can't start: $Message"
    exit 2
}

function Read-Json([string] $Path) { Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json }

function Write-Json([string] $Path, $Value) {
    New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
    $Value | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}

# As Reloaded-II's ApplicationConfig.GetAbsoluteAppLocation: a relative AppLocation is relative to its AppConfig.json.
function Resolve-AppLocation([string] $ConfigFile, [string] $Location) {
    $folder = Split-Path $ConfigFile
    if (-not [IO.Path]::IsPathRooted($Location)) { $Location = Join-Path $folder $Location }
    elseif ([IO.Path]::GetPathRoot($Location) -eq '\') { $Location = Join-Path ([IO.Path]::GetPathRoot($folder)) $Location.TrimStart('\') }
    [IO.Path]::GetFullPath($Location)
}

function Get-Game([string] $Path) {
    Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $Path }
}

# Each undo runs on its own, so one failure doesn't skip the rest.
function Invoke-Undo([string] $What, [scriptblock] $Undo) {
    try { & $Undo; $true }
    catch { Write-Host "cleanup failed: $What`: $($_.Exception.Message)"; $false }
}

$backup = "$ReloadedConfig.smoke-backup"
if (Test-Path -LiteralPath $backup) {
    Move-Item -LiteralPath $backup $ReloadedConfig -Force
    Write-Host "restored $ReloadedConfig from an interrupted run (Reloaded-II settings changed since then are discarded)"
}

if (-not $GameExe -or -not (Test-Path -LiteralPath $GameExe)) { Stop-Start 'set -GameExe or METAPHOR_GAME_EXE to the game executable' }
if (-not $LauncherExe -or -not (Test-Path -LiteralPath $LauncherExe)) { Stop-Start 'set -LauncherExe or METAPHOR_RELOADED_EXE to Reloaded-II.exe' }
if (-not (Test-Path -LiteralPath $ReloadedConfig)) { Stop-Start "no Reloaded-II config at $ReloadedConfig" }
$gameFull = [IO.Path]::GetFullPath($GameExe)
$launcherFull = [IO.Path]::GetFullPath($LauncherExe)
# The test ends the game by killing it, and a running launcher can save over the swapped config.
if (Get-Game $gameFull) { Stop-Start 'the game is already running; close it first' }
if (Get-Game $launcherFull) { Stop-Start 'Reloaded-II is already running; close it first' }

# 1. The game build.
$build = (Get-FileHash -LiteralPath $gameFull -Algorithm SHA256).Hash.ToLowerInvariant()
$known = @(Read-Json $KnownBuilds) | Where-Object sha256 -EQ $build
if (-not $known) { Stop-Start "unknown game build $build (add it: docs/contributors/how-to/add-a-game-build.md)" }
Write-Host "game build $build (Steam build $($known.steamBuildId))"

$mods = foreach ($folder in $ModFolders) {
    $modConfig = Join-Path $folder 'ModConfig.json'
    if (-not (Test-Path -LiteralPath $modConfig)) { Stop-Start "no ModConfig.json in $folder" }
    [pscustomobject]@{ Id = (Read-Json $modConfig).ModId; Source = (Resolve-Path -LiteralPath $folder).Path }
}
$duplicate = $mods | Group-Object Id | Where-Object Count -GT 1 | Select-Object -First 1
if ($duplicate) { Stop-Start "two test mods have the mod ID $($duplicate.Name)" }

$loader = Read-Json $ReloadedConfig
$application = Get-ChildItem -LiteralPath $loader.ApplicationConfigDirectory -Filter AppConfig.json -Recurse -Depth 1 |
    Where-Object { (Resolve-AppLocation $_.FullName (Read-Json $_.FullName).AppLocation) -ceq $gameFull } |
    Select-Object -First 1
if (-not $application) { Stop-Start "the game isn't registered in Reloaded-II; add $gameFull as an application first" }

$smokeProfile = Join-Path ([IO.Path]::GetTempPath()) "metaphor-smoke-$([guid]::NewGuid())"
$failed = $false
try {
    # 2. A throwaway profile with only the test mods, and the config pointed at it.
    foreach ($name in $ProfileFolders.Keys) {
        $folder = Join-Path $smokeProfile $ProfileFolders[$name]
        New-Item -ItemType Directory -Force $folder | Out-Null
        $loader | Add-Member -NotePropertyName $name -NotePropertyValue $folder -Force
    }
    $app = Read-Json $application.FullName
    $app.AppLocation = $gameFull
    $app.EnabledMods = @($mods.Id)
    Write-Json (Join-Path $smokeProfile "Apps/$($application.Directory.Name)/AppConfig.json") $app
    foreach ($mod in $mods) {
        # Named by mod ID, which is unique, so no two mods share a folder or a status file.
        $mod | Add-Member -NotePropertyName Folder -NotePropertyValue (Join-Path $smokeProfile "Mods/$($mod.Id)")
        Copy-Item -LiteralPath $mod.Source $mod.Folder -Recurse
        # A status file from an earlier run would pass without the game loading the mod.
        Remove-Item -Path (Join-Path $mod.Folder "$StatusFileName*") -Force
    }
    Copy-Item -LiteralPath $ReloadedConfig $backup
    Write-Json $ReloadedConfig $loader

    # 3. Launch. The launcher exits by itself once the game has started. One deadline covers launch and wait.
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $launcher = Start-Process -FilePath $launcherFull -ArgumentList '--launch', "`"$gameFull`"" -PassThru
    if (-not $launcher.WaitForExit($TimeoutSeconds * 1000)) { Write-Host 'the launcher did not exit'; $failed = $true }

    # 4. Wait for every status file, or the game exiting, or the timeout.
    $missedPolls = 0
    do {
        $pending = @($mods | Where-Object { -not (Test-Path -LiteralPath (Join-Path $_.Folder $StatusFileName)) })
        if ($pending.Count -eq 0) { break }
        $missedPolls = if (Get-Game $gameFull) { 0 } else { $missedPolls + 1 }
        if ($missedPolls -ge 2) { Write-Host 'the game exited before every test mod reported'; $failed = $true; break }
        Start-Sleep -Milliseconds $PollMilliseconds
    } while ((Get-Date) -lt $deadline)

    foreach ($mod in $mods) {
        $file = Join-Path $mod.Folder $StatusFileName
        if (-not (Test-Path -LiteralPath $file)) {
            Write-Host "$($mod.Id): no status within $TimeoutSeconds s"
            $failed = $true
            continue
        }
        $status = Read-Json $file
        if ($status.modId -ne $mod.Id) {
            Write-Host "$($mod.Id): status file names another mod, $($status.modId)"
            $failed = $true
        }
        elseif ($status.state -eq 'ready') {
            Write-Host "$($mod.Id): ready on $($status.gameBuildSha256), hooks $($status.hooksLoaded)/$($status.hooksExpected)"
            if ($status.hooksLoaded -ne $status.hooksExpected -or $status.gameBuildSha256 -ne $build) { $failed = $true }
        }
        else {
            Write-Host "$($mod.Id): $($status.state) on $($status.gameBuildSha256) ($($status.reason))"
            $failed = $true
        }
    }
}
finally {
    # 5. Close the game (closing its window only asks "Exit game?"), then put everything back.
    # The launcher goes first: one still running could save over the restored config.
    $clean = Invoke-Undo 'close the launcher' { Get-Game $launcherFull | ForEach-Object { $_.Kill(); $_.WaitForExit(10000) | Out-Null } }
    $clean = (Invoke-Undo 'close the game' { Get-Game $gameFull | ForEach-Object { $_.Kill(); $_.WaitForExit(10000) | Out-Null } }) -and $clean
    if (Test-Path -LiteralPath $backup) {
        $clean = (Invoke-Undo "restore $ReloadedConfig" { Move-Item -LiteralPath $backup $ReloadedConfig -Force }) -and $clean
    }
    $clean = (Invoke-Undo "remove $smokeProfile" {
            if (Test-Path -LiteralPath $smokeProfile) { Remove-Item -LiteralPath $smokeProfile -Recurse -Force }
        }) -and $clean
    if (-not $clean) { $failed = $true }
}

if ($failed) { Write-Host 'smoke test FAILED'; exit 1 }
Write-Host 'smoke test passed'
exit 0
