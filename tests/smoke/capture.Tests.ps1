# Tests for scripts/capture.ps1, run against the stand-in launcher and game (tests/Metaphor.StandInGame) and a stand-in
# mod project built in the test folder. Run: Invoke-Pester tests/smoke (Pester 5 or later).

BeforeAll {
    $repo = (Resolve-Path "$PSScriptRoot/../..").Path
    $script = Join-Path $repo 'scripts/capture.ps1'
    $standIn = Join-Path $repo 'tests/Metaphor.StandInGame/bin/Release/net9.0'
    dotnet build (Join-Path $repo 'tests/Metaphor.StandInGame') -c Release -v q --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'the stand-in game failed to build' }

    function Write-Json([string] $Path, $Value) {
        New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
        $Value | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
    }

    # Runs the capture in its own process, so its exit code is real.
    function Invoke-Capture([string[]] $OptIns = @(), [string] $SaveLog, [string] $Behaviour = 'ready') {
        $env:METAPHOR_STANDIN_BEHAVIOUR = $Behaviour
        $extra = @()
        if ($OptIns) { $extra += '-OptIns', ($OptIns -join ',') }
        if ($SaveLog) { $extra += '-SaveLog', $SaveLog }
        $output = & pwsh -NoProfile -File $script -GameExe $script:gameExe -LauncherExe $script:launcher `
            -Project $script:modProject -ReloadedConfig $script:config -KnownBuilds $script:known -TimeoutSeconds 10 @extra 2>&1
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
    }

    function Get-Game { Get-Process -ErrorAction SilentlyContinue | Where-Object Path -EQ $script:gameExe }
}

Describe 'capture.ps1' {
    BeforeEach {
        # TestDrive is shared by every test in this Describe, so each test gets its own folder.
        $script:root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $game = Join-Path $root 'game'
        Copy-Item $standIn $game -Recurse
        $script:gameExe = Join-Path $game 'METAPHOR.exe'
        Copy-Item (Join-Path $game 'Metaphor.StandInGame.exe') $script:gameExe
        $script:launcher = Join-Path $game 'Metaphor.StandInGame.exe'
        $build = (Get-FileHash $script:gameExe -Algorithm SHA256).Hash.ToLowerInvariant()
        $script:known = Join-Path $root 'known-game-builds.json'
        Write-Json $script:known @(@{ sha256 = $build; steamBuildId = 1; evidence = 'test' })

        $reloaded = Join-Path $root 'reloaded'
        $script:config = Join-Path $reloaded 'ReloadedII.json'
        Write-Json $script:config ([ordered]@{
                ApplicationConfigDirectory = Join-Path $reloaded 'Apps'
                ModConfigDirectory         = Join-Path $reloaded 'Mods'
                ModUserConfigDirectory     = Join-Path $reloaded 'User/Mods'
                MiscConfigDirectory        = Join-Path $reloaded 'User/Misc'
                PluginConfigDirectory      = Join-Path $reloaded 'Plugins'
                LauncherPath               = $script:launcher
            })
        Write-Json (Join-Path $reloaded 'Apps/metaphor.exe/AppConfig.json') ([ordered]@{
                AppId = 'metaphor.exe'; AppLocation = $script:gameExe; EnabledMods = @()
            })
        Write-Json (Join-Path $reloaded 'Mods/reloaded.sharedlib.hooks/ModConfig.json') @{ ModId = 'reloaded.sharedlib.hooks' }
        $script:modFolder = Join-Path $reloaded 'Mods/metaphor.dev'

        # A stand-in for the mod's source, naming its opt-in files as the hooks' OptInFile does.
        $script:modProject = Join-Path $root 'src/Metaphor.DevMod'
        New-Item -ItemType Directory -Force $script:modProject | Out-Null
        Set-Content -LiteralPath (Join-Path $script:modProject 'Metaphor.DevMod.csproj') -Value @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>
  <ItemGroup><None Include="ModConfig.json" CopyToOutputDirectory="PreserveNewest" /></ItemGroup>
</Project>
'@
        Set-Content -LiteralPath (Join-Path $script:modProject 'Mod.cs') -Value @'
namespace Metaphor.DevMod;
public static class Mod { public const string A = "opt-in-ui-text-panes.txt"; public const string B = "opt-in-dialogue-window.txt"; }
'@
        Write-Json (Join-Path $script:modProject 'ModConfig.json') @{ ModId = 'metaphor.dev' }

        # Reloaded-II's log of a run, as the stand-in game writes it. Every SECRET stands for game text.
        $script:loaderLog = Join-Path $root 'loader-log.txt'
        Set-Content -LiteralPath $script:loaderLog -Value @(
            '[12:00:00] [Reloaded] Loading mod metaphor.dev'
            '[12:00:01] [metaphor.dev] speech: SAPI'
            "[12:00:01] [metaphor.dev] hook DialogueWindowOpenHook skipped: opt-in, and opt-in-dialogue-window.txt isn't in the mod's folder"
            '[12:00:01] [metaphor.dev] hook UiTextPaneListener failed to install: InvalidOperationException'
            '[12:00:01] [metaphor.dev] build abc1234: Ready on game build 0123, hooks 1/1; load message: Metaphor accessibility ready'
            '[12:00:02] [other.mod] something failed'
        )
        $script:messageLog = Join-Path $root 'message-log.txt'
        Set-Content -LiteralPath $script:messageLog -Value '120 ms request via=MsgRequestByName kind=0 text="SECRET"'
        $script:transcript = Join-Path $root 'transcript.txt'
        Set-Content -LiteralPath $script:transcript -Value 'SECRET announcement'
        $env:METAPHOR_STANDIN_RELOADED_CONFIG = $script:config
        $env:METAPHOR_STANDIN_LOADER_LOG = $script:loaderLog
        $env:METAPHOR_STANDIN_MESSAGE_LOG = $script:messageLog
        $env:METAPHOR_STANDIN_TRANSCRIPT = $script:transcript
    }

    AfterEach {
        Get-Game | Stop-Process -Force
        Remove-Item Env:METAPHOR_STANDIN_BEHAVIOUR, Env:METAPHOR_STANDIN_RELOADED_CONFIG, Env:METAPHOR_STANDIN_LOADER_LOG,
            Env:METAPHOR_STANDIN_MESSAGE_LOG, Env:METAPHOR_STANDIN_TRANSCRIPT -ErrorAction SilentlyContinue
    }

    It 'deploys, adds the opt-ins, relaunches and reports the hooks and any skipped or failed listener' {
        $result = Invoke-Capture -OptIns 'ui-text-panes', 'dialogue-window'

        $result.ExitCode | Should -Be 0 -Because $result.Output
        Join-Path $script:modFolder 'opt-in-ui-text-panes.txt' | Should -Exist
        Join-Path $script:modFolder 'opt-in-dialogue-window.txt' | Should -Exist
        $result.Output | Should -Match 'metaphor.dev: ready on [0-9a-f]{64}, hooks 1/1'
        $result.Output | Should -Match 'hook DialogueWindowOpenHook skipped: opt-in'
        $result.Output | Should -Match 'hook UiTextPaneListener failed to install'
        $result.Output | Should -Match 'Ready on game build 0123, hooks 1/1'
        $result.Output | Should -Not -Match 'speech: SAPI|other.mod'
        Get-Game | Should -Not -BeNullOrEmpty
    }

    It 'closes the game it launched and fails when the deploy fails after launching' {
        $result = Invoke-Capture -Behaviour silent

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'deploy FAILED'
        Get-Game | Should -BeNullOrEmpty
    }

    It 'saves the message log and transcript once the game exits, printing no game text and no local path' {
        $save = Join-Path $script:root 'captures'

        $result = Invoke-Capture -OptIns 'opt-in-ui-text-panes.txt' -SaveLog $save -Behaviour exitlater

        $result.ExitCode | Should -Be 0 -Because $result.Output
        $saved = @(Get-ChildItem -LiteralPath $save -Directory)
        $saved.Count | Should -Be 1
        Get-Content -LiteralPath (Join-Path $saved[0].FullName 'message-log.txt') | Should -Match 'SECRET'
        Get-Content -LiteralPath (Join-Path $saved[0].FullName 'transcript.txt') | Should -Be 'SECRET announcement'
        $result.Output | Should -Match 'saved message-log.txt and transcript.txt to <save>'
        $result.Output | Should -Match 'message log: 1 request'
        $result.Output | Should -Not -Match 'SECRET'
        $result.Output | Should -Not -Match ([regex]::Escape($script:root))
        $result.Output | Should -Not -Match ([regex]::Escape([Environment]::GetFolderPath('UserProfile')))
    }

    It 'refuses to save the logs inside a git repository, before deploying' {
        $result = Invoke-Capture -SaveLog (Join-Path $repo 'captures')

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match "capture can't start: -SaveLog .* is inside the git repository"
        $script:modFolder | Should -Not -Exist
        Join-Path $repo 'captures' | Should -Not -Exist
    }

    It 'refuses an opt-in no hook names, or one that is not a bare name, before deploying' -ForEach @(
        @{ Name = 'ui-text-pain'; Why = 'no hook in .* opts in with opt-in-ui-text-pain.txt' }
        @{ Name = '../evil'; Why = "'../evil' isn't an opt-in name" }
    ) {
        $result = Invoke-Capture -OptIns $Name

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match $Why
        $script:modFolder | Should -Not -Exist
    }

    It "stops without launching when the deploy can't start" {
        Write-Json $script:known @(@{ sha256 = '0' * 64; steamBuildId = 1; evidence = 'test' })

        $result = Invoke-Capture

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'unknown game build'
        $result.Output | Should -Match "capture can't start: the deploy failed"
        Get-Game | Should -BeNullOrEmpty
    }
}
