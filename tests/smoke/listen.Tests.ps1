# Tests for scripts/listen.ps1, run against the stand-in launcher and game (tests/Metaphor.StandInGame) and a stand-in
# mod project built in the test folder. Run: Invoke-Pester tests/smoke (Pester 5 or later).

BeforeAll {
    $repo = (Resolve-Path "$PSScriptRoot/../..").Path
    $script = Join-Path $repo 'scripts/listen.ps1'
    $standIn = Join-Path $repo 'tests/Metaphor.StandInGame/bin/Release/net9.0'
    dotnet build (Join-Path $repo 'tests/Metaphor.StandInGame') -c Release -v q --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'the stand-in game failed to build' }

    function Write-Json([string] $Path, $Value) {
        New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
        $Value | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
    }

    # Runs the listening run in its own process, so its exit code is real.
    function Invoke-Listen([string] $Behaviour = 'ready', [string[]] $KnownBuild = @($script:build), [switch] $Close) {
        Write-Json $script:known @($KnownBuild | ForEach-Object { @{ sha256 = $_; steamBuildId = 1; evidence = 'test' } })
        $env:METAPHOR_STANDIN_BEHAVIOUR = $Behaviour
        $closeArgs = @(if ($Close) { '-Close' })
        $output = & pwsh -NoProfile -File $script -GameExe $script:gameExe -LauncherExe $script:launcher `
            -Project $script:modProject -ReloadedConfig $script:config -KnownBuilds $script:known -TimeoutSeconds 5 `
            -Seconds 3 @closeArgs 2>&1
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
    }

    function Get-Game { Get-Process -ErrorAction SilentlyContinue | Where-Object Path -EQ $script:gameExe }
}

Describe 'listen.ps1' {
    BeforeEach {
        # TestDrive is shared by every test in this Describe, so each test gets its own folder.
        $script:root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $game = Join-Path $root 'game'
        Copy-Item $standIn $game -Recurse
        $script:gameExe = Join-Path $game 'METAPHOR.exe'
        Copy-Item (Join-Path $game 'Metaphor.StandInGame.exe') $script:gameExe
        $script:launcher = Join-Path $game 'Metaphor.StandInGame.exe'
        $script:build = (Get-FileHash $script:gameExe -Algorithm SHA256).Hash.ToLowerInvariant()

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
        $script:known = Join-Path $root 'known-game-builds.json'

        # A message log as the Debug mod writes it (Metaphor.Core.MessageLog). Every SECRET stands for game text.
        $script:messageLog = Join-Path $root 'message-log.txt'
        Set-Content -LiteralPath $script:messageLog -Value @(
            '120 ms register name=MsgRequest rva=0x90c420 direct'
            '130 ms compile id=7 path=script/SECRET.msg chars=30'
            '  | SECRET line one'
            '  | [msg x]SECRET line two'
            '140 ms lookup id=7 label=3'
            '150 ms request via=MsgRequestByName kind=0 text="SECRET three"'
            '160 ms request via=MsgRequestByName kind=2 text="SECRET four"'
            '170 ms request via=MsgRequestById kind=0 id=1 label=2'
            'SECRET in a line that is no record'
        )
        $env:METAPHOR_STANDIN_MESSAGE_LOG = $script:messageLog
        $env:METAPHOR_STANDIN_RELOADED_CONFIG = $script:config
    }

    AfterEach {
        Get-Game | Stop-Process -Force
        Remove-Item Env:METAPHOR_STANDIN_BEHAVIOUR, Env:METAPHOR_STANDIN_RELOADED_CONFIG, Env:METAPHOR_STANDIN_MESSAGE_LOG `
            -ErrorAction SilentlyContinue
    }

    It 'deploys the mod, says the game survived, and leaves it running' {
        $result = Invoke-Listen

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'the game is still running after 3 s'
        $result.Output | Should -Match 'listening run passed'
        Get-Game | Should -Not -BeNullOrEmpty
    }

    It 'summarises the message log by record type' {
        $result = Invoke-Listen

        $result.Output | Should -Match 'message log: 1 compile \(2 source lines\), 1 lookup, 1 register, 3 request, 1 unrecognised line'
        $result.Output | Should -Match 'requests by function: MsgRequestById 1, MsgRequestByName 2'
    }

    It 'prints no game text and no local path' {
        $result = Invoke-Listen

        $result.Output | Should -Not -Match 'SECRET'
        $result.Output | Should -Not -Match ([regex]::Escape($script:root))
        $result.Output | Should -Not -Match ([regex]::Escape([Environment]::GetFolderPath('UserProfile')))
        $result.Output | Should -Match 'deployed metaphor.dev to <mods>'
    }

    It 'closes the game when asked' {
        $result = Invoke-Listen -Close

        $result.ExitCode | Should -Be 0 -Because $result.Output
        $result.Output | Should -Match 'closed the game'
        Get-Game | Should -BeNullOrEmpty
    }

    It 'fails when the game exits while it listens, and still summarises the log' {
        $result = Invoke-Listen -Behaviour exitlater

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'the game exited after \d+ s'
        $result.Output | Should -Match 'message log: 1 compile'
        $result.Output | Should -Match 'listening run FAILED'
    }

    It 'fails when the mod wrote no message log' {
        Remove-Item Env:METAPHOR_STANDIN_MESSAGE_LOG

        $result = Invoke-Listen

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'no message-log.txt: is the mod a Debug build\?'
    }

    It 'stops without listening when the deploy cannot start' {
        $result = Invoke-Listen -KnownBuild ('0' * 64)

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'unknown game build'
        $result.Output | Should -Not -Match 'message log'
        Get-Game | Should -BeNullOrEmpty
    }
}
