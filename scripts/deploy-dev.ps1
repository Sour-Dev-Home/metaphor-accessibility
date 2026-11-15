<#
.SYNOPSIS
Dev inner loop: builds the mod, puts it in Reloaded-II's mods folder, enables it for the game, and launches the game
with it.

.DESCRIPTION
1. Hashes the game exe and refuses to run on a build missing from the known-builds list.
2. Builds the mod project (dotnet publish) into a temporary folder. Nothing in Reloaded-II is touched if this fails.
3. Replaces the mod's folder in Reloaded-II's mods folder (named by its mod ID) with the new build.
4. Adds the mod to the game's EnabledMods, keeping the mods already enabled.
5. Launches with the user's own Reloaded-II (--launch) and waits for the mod's mod-status.json. With -ScanSignatures,
   the mod also scans the running game for other mods' signatures (issue #21), and this prints its report.

The game is left running. Folder locations come from ReloadedII.json, never from fixed paths.

Exit codes: 0 the mod is ready with all its hooks; 1 it refused, timed out, or the game exited; 2 the deploy couldn't
start (nothing launched).

Loader facts: docs/raw-sources/reloaded-ii-config-location-2026-11-13.md, reloaded-ii-launch-and-logs-2026-11-13.md
and reloaded-ii-app-and-mod-config-2026-11-15.md.

.EXAMPLE
pwsh scripts/deploy-dev.ps1
#>
[CmdletBinding()]
param(
    # The game executable. Defaults to METAPHOR_GAME_EXE, then to the game as registered in Reloaded-II.
    [string] $GameExe = $env:METAPHOR_GAME_EXE,
    # Reloaded-II's launcher (Reloaded-II.exe). Defaults to METAPHOR_RELOADED_EXE, then to LauncherPath in ReloadedII.json.
    [string] $LauncherExe = $env:METAPHOR_RELOADED_EXE,
    # The mod project. Its build output must hold a ModConfig.json.
    [string] $Project = (Join-Path $PSScriptRoot '../src/Metaphor.Hooks'),
    [string] $Configuration = 'Debug',
    # Reloaded-II's root config, at a fixed AppData path the loader doesn't let us move.
    [string] $ReloadedConfig = (Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Reloaded-Mod-Loader-II/ReloadedII.json'),
    [string] $KnownBuilds = (Join-Path $PSScriptRoot '../src/Metaphor.Core/known-game-builds.json'),
    [int] $TimeoutSeconds = 120,
    # Ask the mod to scan the running game's memory for signature-probes.json (issue #21) and print its report.
    [switch] $ScanSignatures
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReloadedDev.psm1') -Force

function Stop-Start([string] $Message) {
    Write-Host "deploy can't start: $Message"
    exit 2
}

# Any other error (a corrupt config, a locked file) also means the deploy didn't get as far as launching.
$launched = $false
trap {
    Write-Host "deploy $(if ($launched) { 'FAILED' } else { "can't start" }): $($_.Exception.Message)"
    exit $(if ($launched) { 1 } else { 2 })
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
# A running game holds the old build's files, and a running launcher can save over the enabled mods.
if (Get-Game $gameFull) { Stop-Start 'the game is already running; close it first' }
if (Get-Game $launcherFull) { Stop-Start 'Reloaded-II is already running; close it first' }

# 1. The game build.
$gameBuild = Get-GameBuild $gameFull $KnownBuilds
if (-not $gameBuild.Known) { Stop-Start "unknown game build $($gameBuild.Sha256) (add it: docs/contributors/how-to/add-a-game-build.md)" }
Write-Host "game build $($gameBuild.Sha256) (Steam build $($gameBuild.Known.steamBuildId))"

$application = Find-Application $loader $gameFull
if (-not $application) { Stop-Start "the game isn't registered in Reloaded-II; add $gameFull as an application first" }

$staging = Join-Path ([IO.Path]::GetTempPath()) "metaphor-deploy-$([guid]::NewGuid())"
try {
    # 2. Build.
    dotnet publish $Project -c $Configuration -o $staging -v q --nologo | Out-Host
    if ($LASTEXITCODE -ne 0) { Stop-Start "the build of $Project failed, see the errors above" }
    $modConfig = Join-Path $staging 'ModConfig.json'
    if (-not (Test-Path -LiteralPath $modConfig)) { Stop-Start "the build of $Project has no ModConfig.json" }
    $modId = [string] (Read-Json $modConfig).ModId
    # The mod ID names the folder that gets deleted, so it must not reach outside the mods folder.
    if (-not $modId -or $modId -in '.', '..' -or $modId.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) {
        Stop-Start "the mod ID '$modId' in the build of $Project is not a plain folder name"
    }

    # 3. Replace the deployed copy, but only one that is this mod.
    $modsRoot = [string] $loader.ModConfigDirectory
    if (-not [IO.Path]::IsPathFullyQualified($modsRoot) -or -not (Test-Path -LiteralPath $modsRoot -PathType Container)) {
        Stop-Start "ModConfigDirectory in $ReloadedConfig isn't an existing full path: '$modsRoot'"
    }
    $folder = Join-Path $modsRoot $modId
    if (Test-Path -LiteralPath $folder) {
        if ((Get-Item -LiteralPath $folder -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            Stop-Start "$folder is a link; replace it with a plain folder first"
        }
        $existing = Join-Path $folder 'ModConfig.json'
        if (-not (Test-Path -LiteralPath $existing) -or (Read-Json $existing).ModId -ne $modId) {
            Stop-Start "$folder holds something other than $modId; move it out of the way first"
        }
        Remove-Item -LiteralPath $folder -Recurse -Force
    }
    Copy-Item -LiteralPath $staging $folder -Recurse
    # Metaphor.Core.SignatureScan.RequestFileName: the mod scans once when it's there, then deletes it. The probes stay
    # dev tooling; they're never part of the mod's build.
    if ($ScanSignatures) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../tools/Metaphor.SignatureCheck/signature-probes.json') `
            (Join-Path $folder 'signature-probes.json')
    }
    Write-Host "deployed $modId to $folder"

    # 4. Enable it, keeping the user's other mods. The old file stays beside it as a backup, and the new one is swapped in
    # whole, so a failed write can't leave a half-written config.
    $appFile = $application.FullName
    $app = Read-Json $appFile
    if (-not $app.PSObject.Properties['EnabledMods']) { $app | Add-Member -NotePropertyName EnabledMods -NotePropertyValue @() }
    $enabled = @($app.EnabledMods | Where-Object { $null -ne $_ })
    if ($enabled -notcontains $modId) {
        $app.EnabledMods = $enabled + $modId
        Write-Json "$appFile.deploy-new" $app
        Copy-Item -LiteralPath $appFile "$appFile.deploy-backup" -Force
        Move-Item -LiteralPath "$appFile.deploy-new" $appFile -Force
        Write-Host "enabled $modId for $($application.Directory.Name) (previous config: $appFile.deploy-backup)"
    }
    $others = @($enabled | Where-Object { $_ -ne $modId })
    if ($ScanSignatures -and $others) {
        # A mod that hooks first, such as CriFs V2 Hook, rewrites the start of the functions the probes look for.
        Write-Warning "other enabled mods: $($others -join ', '). Their hooks can change the bytes scanned; disable them in Reloaded-II for a clean comparison."
    }
}
finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}

# 5. Launch. The launcher exits by itself once the game has started. One deadline covers launch and wait.
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$launched = $true
$launcher = Start-Process -FilePath $launcherFull -ArgumentList '--launch', "`"$gameFull`"" -PassThru
if (-not $launcher.WaitForExit($TimeoutSeconds * 1000)) { Write-Host 'the launcher did not exit'; exit 1 }
$mods = @([pscustomobject]@{ Id = $modId; Folder = $folder })
if (-not (Wait-ModStatus $mods $gameFull $gameBuild.Sha256 $deadline $TimeoutSeconds)) { Write-Host 'deploy FAILED'; exit 1 }
if ($ScanSignatures) {
    # The mod writes Metaphor.Core.SignatureScan.ReportFileName after its status file.
    $report = Join-Path $folder 'signature-scan.json'
    while (-not (Test-Path -LiteralPath $report) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200 }
    if (-not (Test-Path -LiteralPath $report)) { Write-Host "no signature scan report within $TimeoutSeconds s"; exit 1 }
    Write-Host "signature scan report ($report):"
    Get-Content -Raw -LiteralPath $report | Write-Host
}
Write-Host 'deployed and running'
exit 0
