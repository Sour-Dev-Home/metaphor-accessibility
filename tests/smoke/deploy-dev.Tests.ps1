# Tests for scripts/deploy-dev.ps1, run against the stand-in launcher and game (tests/Metaphor.StandInGame) and a
# stand-in mod project built in the test folder. Run: Invoke-Pester tests/smoke (Pester 5 or later).

BeforeAll {
    $repo = (Resolve-Path "$PSScriptRoot/../..").Path
    $script = Join-Path $repo 'scripts/deploy-dev.ps1'
    $standIn = Join-Path $repo 'tests/Metaphor.StandInGame/bin/Release/net9.0'
    dotnet build (Join-Path $repo 'tests/Metaphor.StandInGame') -c Release -v q --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'the stand-in game failed to build' }

    function Write-Json([string] $Path, $Value) {
        New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
        $Value | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
    }

    # Runs the deploy in its own process, so its exit code is real.
    function Invoke-Deploy([string] $Behaviour = 'ready', [string[]] $KnownBuild = @($script:build), [string] $Project = $script:modProject) {
        Write-Json $script:known @($KnownBuild | ForEach-Object { @{ sha256 = $_; steamBuildId = 1; evidence = 'test' } })
        $env:METAPHOR_STANDIN_BEHAVIOUR = $Behaviour
        $output = & pwsh -NoProfile -File $script -GameExe $script:gameExe -LauncherExe $script:launcher `
            -Project $Project -ReloadedConfig $script:config -KnownBuilds $script:known -TimeoutSeconds 5 2>&1
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
    }

    function Get-Game { Get-Process -ErrorAction SilentlyContinue | Where-Object Path -EQ $script:gameExe }
    function Get-EnabledMods { @((Get-Content -Raw $script:appConfig | ConvertFrom-Json).EnabledMods) }
}

Describe 'deploy-dev.ps1' {
    BeforeEach {
        # TestDrive is shared by every test in this Describe, so each test gets its own folder.
        $root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $game = Join-Path $root 'game'
        Copy-Item $standIn $game -Recurse
        $script:gameExe = Join-Path $game 'METAPHOR.exe'
        Copy-Item (Join-Path $game 'Metaphor.StandInGame.exe') $script:gameExe
        $script:launcher = Join-Path $game 'Metaphor.StandInGame.exe'
        $script:build = (Get-FileHash $script:gameExe -Algorithm SHA256).Hash.ToLowerInvariant()

        # The user's own Reloaded-II setup, which the deploy adds the mod to.
        $reloaded = Join-Path $root 'reloaded'
        $script:config = Join-Path $reloaded 'ReloadedII.json'
        $script:mods = Join-Path $reloaded 'Mods'
        Write-Json $script:config ([ordered]@{
                ApplicationConfigDirectory = Join-Path $reloaded 'Apps'
                ModConfigDirectory         = $script:mods
                ModUserConfigDirectory     = Join-Path $reloaded 'User/Mods'
                MiscConfigDirectory        = Join-Path $reloaded 'User/Misc'
                PluginConfigDirectory      = Join-Path $reloaded 'Plugins'
            })
        $script:appConfig = Join-Path $reloaded 'Apps/metaphor.exe/AppConfig.json'
        Write-Json $script:appConfig ([ordered]@{
                AppId = 'metaphor.exe'; AppLocation = $script:gameExe; EnabledMods = @('user.mod')
            })
        Write-Json (Join-Path $script:mods 'user.mod/ModConfig.json') @{ ModId = 'user.mod' }
        $script:configBefore = Get-FileHash $script:config

        # A stand-in for the mod's source: a project whose build output holds a ModConfig.json.
        $script:modProject = Join-Path $root 'src/Metaphor.DevMod'
        New-Item -ItemType Directory -Force $script:modProject | Out-Null
        Set-Content -LiteralPath (Join-Path $script:modProject 'Metaphor.DevMod.csproj') -Value @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>
  <ItemGroup><None Include="ModConfig.json" CopyToOutputDirectory="PreserveNewest" /></ItemGroup>
</Project>
'@
        Set-Content -LiteralPath (Join-Path $script:modProject 'Mod.cs') -Value 'namespace Metaphor.DevMod; public static class Mod { }'
        Write-Json (Join-Path $script:modProject 'ModConfig.json') @{ ModId = 'metaphor.dev' }
        $script:deployed = Join-Path $script:mods 'metaphor.dev'
        $script:known = Join-Path $root 'known-game-builds.json'
        $env:METAPHOR_STANDIN_RELOADED_CONFIG = $script:config
    }

    AfterEach {
        Get-Game | Stop-Process -Force
        Remove-Item Env:METAPHOR_STANDIN_BEHAVIOUR, Env:METAPHOR_STANDIN_RELOADED_CONFIG -ErrorAction SilentlyContinue
    }

    It 'builds the mod, deploys and enables it, and leaves the game running with it loaded' {
        $result = Invoke-Deploy

        $result.Output | Should -Match ([regex]::Escape("metaphor.dev: ready on $script:build, hooks 1/1"))
        $result.ExitCode | Should -Be 0
        Join-Path $script:deployed 'Metaphor.DevMod.dll' | Should -Exist
        Get-EnabledMods | Should -Contain 'metaphor.dev'
        Get-Game | Should -Not -BeNullOrEmpty
    }

    It 'deploys the real mod: its ModConfig.json names a DLL that is there, and the loader-supplied one is not' {
        $result = Invoke-Deploy -Project (Join-Path $repo 'src/Metaphor.Hooks')

        $result.ExitCode | Should -Be 0
        $folder = Join-Path $script:mods 'metaphor.accessibility'
        $config = Get-Content -Raw (Join-Path $folder 'ModConfig.json') | ConvertFrom-Json
        Join-Path $folder $config.ModDll | Should -Exist
        Join-Path $folder 'Reloaded.Mod.Interfaces.dll' | Should -Not -Exist
        Get-EnabledMods | Should -Contain 'metaphor.accessibility'
    }

    It 'keeps the mods the user already enabled, and a backup of their app config' {
        $before = Get-FileHash $script:appConfig

        Invoke-Deploy | Out-Null

        Get-EnabledMods | Should -Be @('user.mod', 'metaphor.dev')
        (Get-FileHash "$script:appConfig.deploy-backup").Hash | Should -Be $before.Hash
        Join-Path $script:mods 'user.mod/ModConfig.json' | Should -Exist
    }

    It 'refuses an unknown game build without building, deploying or launching' {
        $result = Invoke-Deploy -KnownBuild ('0' * 64)

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match "unknown game build $script:build"
        $script:deployed | Should -Not -Exist
        Get-EnabledMods | Should -Be @('user.mod')
        Get-Game | Should -BeNullOrEmpty
    }

    It 'replaces an earlier deploy with the new build' {
        Invoke-Deploy | Out-Null
        Get-Game | Stop-Process -Force
        Set-Content -LiteralPath (Join-Path $script:deployed 'stale.txt') -Value 'from an earlier build'
        Set-Content -LiteralPath (Join-Path $script:modProject 'Mod.cs') -Value 'namespace Metaphor.DevMod; public static class Changed { }'

        $result = Invoke-Deploy

        $result.ExitCode | Should -Be 0
        Join-Path $script:deployed 'stale.txt' | Should -Not -Exist
        # The type name lands in the assembly's metadata, so the deployed DLL is the rebuilt one.
        [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes((Join-Path $script:deployed 'Metaphor.DevMod.dll'))) |
            Should -Match 'Changed'
        Get-EnabledMods | Should -Be @('user.mod', 'metaphor.dev')
    }

    It 'refuses to start while the game is already running, and leaves it running' {
        $running = Start-Process $script:gameExe -PassThru

        $result = Invoke-Deploy

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'the game is already running'
        $running.HasExited | Should -BeFalse
        $script:deployed | Should -Not -Exist
    }

    It 'leaves a folder alone that holds another mod under the same name' {
        Write-Json (Join-Path $script:deployed 'ModConfig.json') @{ ModId = 'someone.else' }

        $result = Invoke-Deploy

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'holds something other than metaphor\.dev'
        (Get-Content -Raw (Join-Path $script:deployed 'ModConfig.json') | ConvertFrom-Json).ModId | Should -Be 'someone.else'
        Get-Game | Should -BeNullOrEmpty
    }

    It 'refuses a mod ID that is not a plain folder name, and deletes nothing' {
        Write-Json (Join-Path $script:modProject 'ModConfig.json') @{ ModId = '../user.mod' }

        $result = Invoke-Deploy

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'mod ID .* is not a plain folder name'
        Join-Path $script:mods 'user.mod/ModConfig.json' | Should -Exist
        Get-EnabledMods | Should -Be @('user.mod')
    }

    It 'says it could not start when the mod config is not JSON' {
        Set-Content -LiteralPath (Join-Path $script:modProject 'ModConfig.json') -Value '{ not json'

        $result = Invoke-Deploy

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match "deploy can't start"
        Get-Game | Should -BeNullOrEmpty
    }

    It 'changes nothing when the build fails' {
        Set-Content -LiteralPath (Join-Path $script:modProject 'Mod.cs') -Value 'this is not C#'

        $result = Invoke-Deploy

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'the build of .* failed'
        $script:deployed | Should -Not -Exist
        (Get-FileHash $script:config).Hash | Should -Be $script:configBefore.Hash
        Get-EnabledMods | Should -Be @('user.mod')
        Get-Game | Should -BeNullOrEmpty
    }

    It 'fails when the mod refuses' {
        $result = Invoke-Deploy -Behaviour refuse

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match ([regex]::Escape("metaphor.dev: refused on $script:build (unknown game build)"))
    }
}
