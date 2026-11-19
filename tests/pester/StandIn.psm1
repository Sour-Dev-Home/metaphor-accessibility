# Helpers the session tests share for the stand-in game (tests/Metaphor.StandInGame).

function Write-Json([string] $Path, $Value) {
    New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
    $Value | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}

# A machine for one test under ROOT, as scripts/dev/session.ps1 finds a real one:
# - game/METAPHOR.exe, the stand-in as the game, beside Metaphor.StandInGame.exe as the launcher;
# - reloaded/ReloadedII.json, the user's own Reloaded-II setup: the game registered as metaphor.exe with user.mod
#   enabled, and Reloaded-II's own reloaded.sharedlib.hooks, which every install has and the loader needs to start;
# - src/Metaphor.DevMod, a stand-in for the mod's source, whose build output holds a ModConfig.json naming metaphor.dev
#   and whose code names the opt-ins ui-text-panes and dialogue-window;
# - known-game-builds.json listing the stand-in's build.
# Sets METAPHOR_STANDIN_RELOADED_CONFIG, which the stand-in reads in place of the fixed AppData path.
function New-SessionFixture([Parameter(Mandatory)] [string] $Root, [Parameter(Mandatory)] [string] $StandIn) {
    $game = Join-Path $Root 'game'
    Copy-Item $StandIn $game -Recurse
    $gameExe = Join-Path $game 'METAPHOR.exe'
    Copy-Item (Join-Path $game 'Metaphor.StandInGame.exe') $gameExe
    $build = (Get-FileHash $gameExe -Algorithm SHA256).Hash.ToLowerInvariant()

    $reloaded = Join-Path $Root 'reloaded'
    $config = Join-Path $reloaded 'ReloadedII.json'
    $launcher = Join-Path $game 'Metaphor.StandInGame.exe'
    $mods = Join-Path $reloaded 'Mods'
    Write-Json $config ([ordered]@{
            ApplicationConfigDirectory = Join-Path $reloaded 'Apps'
            ModConfigDirectory         = $mods
            ModUserConfigDirectory     = Join-Path $reloaded 'User/Mods'
            MiscConfigDirectory        = Join-Path $reloaded 'User/Misc'
            PluginConfigDirectory      = Join-Path $reloaded 'Plugins'
            LauncherPath               = $launcher
        })
    $appConfig = Join-Path $reloaded 'Apps/metaphor.exe/AppConfig.json'
    Write-Json $appConfig ([ordered]@{ AppId = 'metaphor.exe'; AppLocation = $gameExe; EnabledMods = @('user.mod') })
    Write-Json (Join-Path $mods 'user.mod/ModConfig.json') @{ ModId = 'user.mod' }
    Write-Json (Join-Path $mods 'reloaded.sharedlib.hooks/ModConfig.json') @{ ModId = 'reloaded.sharedlib.hooks' }

    $project = Join-Path $Root 'src/Metaphor.DevMod'
    New-Item -ItemType Directory -Force $project | Out-Null
    Set-Content -LiteralPath (Join-Path $project 'Metaphor.DevMod.csproj') -Value @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>
  <ItemGroup><None Include="ModConfig.json" CopyToOutputDirectory="PreserveNewest" /></ItemGroup>
</Project>
'@
    Set-Content -LiteralPath (Join-Path $project 'Mod.cs') -Value @'
namespace Metaphor.DevMod;
public static class Mod { public const string A = "opt-in-ui-text-panes.txt"; public const string B = "opt-in-dialogue-window.txt"; }
'@
    Write-Json (Join-Path $project 'ModConfig.json') @{ ModId = 'metaphor.dev' }

    $known = Join-Path $Root 'known-game-builds.json'
    Write-Json $known @(@{ sha256 = $build; steamBuildId = 1; evidence = 'test' })
    $env:METAPHOR_STANDIN_RELOADED_CONFIG = $config

    [pscustomobject]@{
        Root = $Root; GameExe = $gameExe; Launcher = $launcher; Build = $build; Config = $config; AppConfig = $appConfig
        Mods = $mods; Project = $project; Known = $known; ModFolder = (Join-Path $mods 'metaphor.dev')
    }
}

# The parameters that point session.ps1 at FIXTURE.
function Get-FixtureArgs($Fixture, [switch] $NoPaths) {
    $paths = if ($NoPaths) { @() } else { @('-GameExe', $Fixture.GameExe, '-LauncherExe', $Fixture.Launcher) }
    @($paths) + @('-ReloadedConfig', $Fixture.Config, '-KnownBuilds', $Fixture.Known)
}

# Stops every stand-in launcher or game running from under FOLDER, then empties FOLDER, for a Describe's AfterAll on
# TestDrive (#348). Pester's own TestDrive cleanup fails on a file Windows still holds, and the tests left two kinds:
# - a stand-in a test's own stop missed: a game still starting has no Path in Get-Process yet (CIM's ExecutablePath
#   has it), and a launcher being stopped may just have started a game;
# - an exited stand-in's exe and DLLs, which stay locked for a second or two after it exits (2.5 s at most in five
#   tries on 2026-11-19, longer on a loaded machine).
# So it stops and waits for them until none is left, then deletes FOLDER's contents, retrying for up to a minute.
# Processes elsewhere, as another clone's tests, are left alone.
function Stop-StandIn([Parameter(Mandatory)] [string] $Folder) {
    $prefix = $Folder.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    for ($round = 0; $round -lt 5; $round++) {
        $left = @(Get-CimInstance Win32_Process -Filter "Name = 'METAPHOR.exe' OR Name = 'Metaphor.StandInGame.exe' OR Name = 'Reloaded-II.exe'" |
                Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) })
        if ($left.Count -eq 0) { break }
        foreach ($process in $left) {
            Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
            Wait-Process -Id $process.ProcessId -Timeout 10 -ErrorAction SilentlyContinue
        }
    }
    $deadline = (Get-Date).AddSeconds(60)
    while ($true) {
        try {
            Get-ChildItem -LiteralPath $Folder -Force | Remove-Item -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            if ((Get-Date) -gt $deadline) { throw }
            Start-Sleep -Milliseconds 200
        }
    }
}

Export-ModuleMember -Function New-SessionFixture, Get-FixtureArgs, Stop-StandIn
