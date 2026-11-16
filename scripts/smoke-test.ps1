<#
.SYNOPSIS
In-game smoke test: launches the game through Reloaded-II with exactly the test mods enabled, waits for each mod's
status file, then closes the game and puts the loader config back.

.DESCRIPTION
1. Hashes the game exe and refuses to run on a build missing from the known-builds list.
2. Backs up ReloadedII.json, then points its folders at a throwaway profile holding only the test mods, plus a copy
   of Reloaded-II's own reloaded.sharedlib.hooks, which the loader needs to start.
3. Launches with the user's own Reloaded-II (--launch).
4. Waits for each test mod's mod-status.json (ready or refused, the build, hooks loaded out of expected), then
   collects any -CollectFiles.
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
    # The game executable. Defaults to METAPHOR_GAME_EXE, then to the game as registered in Reloaded-II.
    [string] $GameExe = $env:METAPHOR_GAME_EXE,
    # Reloaded-II's launcher (Reloaded-II.exe). Defaults to METAPHOR_RELOADED_EXE, then to LauncherPath in ReloadedII.json.
    [string] $LauncherExe = $env:METAPHOR_RELOADED_EXE,
    # The folders of the mods to test, each holding a ModConfig.json.
    [Parameter(Mandatory)] [string[]] $ModFolders,
    # Reloaded-II's root config, at a fixed AppData path the loader doesn't let us move.
    [string] $ReloadedConfig = (Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Reloaded-Mod-Loader-II/ReloadedII.json'),
    [string] $KnownBuilds = (Join-Path $PSScriptRoot '../src/Metaphor.Core/known-game-builds.json'),
    [int] $TimeoutSeconds = 120,
    # Files each test mod writes after its status file, such as signature-scan.json. The test waits for them (within
    # the same timeout) and copies them to -CollectTo/<mod ID>/ before cleaning up; a missing one fails it.
    [string[]] $CollectFiles = @(),
    [string] $CollectTo
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReloadedDev.psm1') -Force

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

if (-not (Test-Path -LiteralPath $ReloadedConfig)) { Stop-Start "no Reloaded-II config at $ReloadedConfig" }
$loader = Read-Json $ReloadedConfig
$paths = Resolve-LoaderPaths $loader $GameExe $LauncherExe
$GameExe, $LauncherExe = $paths.GameExe, $paths.LauncherExe
if (-not $GameExe -or -not (Test-Path -LiteralPath $GameExe)) { Stop-Start 'no game executable: register the game in Reloaded-II, or set -GameExe or METAPHOR_GAME_EXE' }
if (-not $LauncherExe -or -not (Test-Path -LiteralPath $LauncherExe)) { Stop-Start 'no Reloaded-II.exe: open Reloaded-II once, or set -LauncherExe or METAPHOR_RELOADED_EXE' }
# Relative to the PowerShell location, as Test-Path read them, not the process's working folder.
$gameFull = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($GameExe)
$launcherFull = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($LauncherExe)
# The test ends the game by killing it, and a running launcher can save over the swapped config.
if (Get-Game $gameFull) { Stop-Start 'the game is already running; close it first' }
if (Get-Game $launcherFull) { Stop-Start 'Reloaded-II is already running; close it first' }

# 1. The game build.
$gameBuild = Get-GameBuild $gameFull $KnownBuilds
$build = $gameBuild.Sha256
if (-not $gameBuild.Known) { Stop-Start "unknown game build $build (add it: docs/contributors/how-to/add-a-game-build.md)" }
Write-Host "game build $build (Steam build $($gameBuild.Known.steamBuildId))"

$mods = foreach ($folder in $ModFolders) {
    $modConfig = Join-Path $folder 'ModConfig.json'
    if (-not (Test-Path -LiteralPath $modConfig)) { Stop-Start "no ModConfig.json in $folder" }
    [pscustomobject]@{ Id = (Read-Json $modConfig).ModId; Source = (Resolve-Path -LiteralPath $folder).Path }
}
$duplicate = $mods | Group-Object Id | Where-Object Count -GT 1 | Select-Object -First 1
if ($duplicate) { Stop-Start "two test mods have the mod ID $($duplicate.Name)" }
# Its folder in the throwaway profile holds Reloaded-II's own copy (below).
if ($mods.Id -contains 'reloaded.sharedlib.hooks') { Stop-Start 'a test mod has the mod ID reloaded.sharedlib.hooks, which is Reloaded-II''s own' }

$application = Find-Application $loader $gameFull
if (-not $application) { Stop-Start "the game isn't registered in Reloaded-II; add $gameFull as an application first" }
if ($CollectFiles -and -not $CollectTo) { Stop-Start '-CollectFiles needs -CollectTo' }

# The loader loads its own reloaded.sharedlib.hooks mod first and fails to start without it, even with no other mods
# (docs/raw-sources/reloaded-ii-log-on-normal-run-2026-11-14.md), so the throwaway profile gets a copy, not enabled.
$sharedHooks = Get-ChildItem -LiteralPath $loader.ModConfigDirectory -Filter 'ModConfig.json' -Recurse -Depth 1 -ErrorAction SilentlyContinue |
    Where-Object { try { (Read-Json $_.FullName).ModId -eq 'reloaded.sharedlib.hooks' } catch { $false } } |
    Select-Object -First 1
if (-not $sharedHooks) { Stop-Start "Reloaded-II's own reloaded.sharedlib.hooks mod isn't in its mods folder; open Reloaded-II once so it downloads it" }

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
    Copy-Item -LiteralPath $sharedHooks.Directory.FullName -Destination (Join-Path $smokeProfile 'Mods/reloaded.sharedlib.hooks') -Recurse
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
    if (-not (Wait-ModStatus $mods $gameFull $build $deadline $TimeoutSeconds)) { $failed = $true }

    # Then for the files to collect, which the throwaway profile would lose.
    if (-not $failed) {
        foreach ($mod in $mods) {
            foreach ($name in $CollectFiles) {
                $file = Join-Path $mod.Folder $name
                while (-not (Test-Path -LiteralPath $file) -and (Get-Date) -lt $deadline -and (Get-Game $gameFull)) {
                    Start-Sleep -Milliseconds 200
                }
                if (-not (Test-Path -LiteralPath $file)) { Write-Host "$($mod.Id): no $name"; $failed = $true; continue }
                $target = Join-Path $CollectTo $mod.Id
                New-Item -ItemType Directory -Force $target | Out-Null
                Copy-Item -LiteralPath $file -Destination $target
                Write-Host "$($mod.Id): collected $name"
            }
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
