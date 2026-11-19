# Tests for scripts/smoke-test.ps1, run against the stand-in launcher and game (tests/Metaphor.StandInGame).
# Run: Invoke-Pester tests/smoke (Pester 5 or later).

BeforeAll {
    $repo = (Resolve-Path "$PSScriptRoot/../..").Path
    $script = Join-Path $repo 'scripts/smoke-test.ps1'
    $standIn = Join-Path $repo 'tests/Metaphor.StandInGame/bin/Release/net9.0'
    dotnet build (Join-Path $repo 'tests/Metaphor.StandInGame') -c Release -v q --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'the stand-in game failed to build' }

    function Write-Json([string] $Path, $Value) {
        New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
        $Value | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
    }

    # The default timeout is a generous upper bound: the script returns as soon as the state it waits for arrives, so a
    # loaded machine only slows a test down (coding-standards judgement rule 4). Only a test of the timeout itself sets
    # a short one.
    # Runs the smoke test in its own process, so its exit code and cleanup are real.
    function Invoke-SmokeTest([string] $Behaviour = 'ready', [string[]] $KnownBuild = @($script:build),
        [string[]] $Extra = @(), [int] $TimeoutSeconds = 60) {
        Write-Json $script:known @($KnownBuild | ForEach-Object { @{ sha256 = $_; steamBuildId = 1; evidence = 'test' } })
        $env:METAPHOR_STANDIN_BEHAVIOUR = $Behaviour
        $output = & pwsh -NoProfile -File $script -GameExe $script:gameExe -LauncherExe $script:launcher `
            -ModFolders $script:testMod -ReloadedConfig $script:config -KnownBuilds $script:known -TimeoutSeconds $TimeoutSeconds @Extra 2>&1
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
    }

    # By name first, as scripts/ReloadedDev.psm1's Get-Game: reading every process's path is slow on a busy machine.
    function Get-Game { [Diagnostics.Process]::GetProcessesByName('METAPHOR') | Where-Object Path -EQ $script:gameExe }
}

Describe 'smoke-test.ps1' {
    BeforeEach {
        # TestDrive is shared by every test in this Describe, so each test gets its own folder.
        $root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $game = Join-Path $root 'game'
        Copy-Item $standIn $game -Recurse
        $script:gameExe = Join-Path $game 'METAPHOR.exe'
        Copy-Item (Join-Path $game 'Metaphor.StandInGame.exe') $script:gameExe
        $script:launcher = Join-Path $game 'Metaphor.StandInGame.exe'
        $script:build = (Get-FileHash $script:gameExe -Algorithm SHA256).Hash.ToLowerInvariant()

        # The user's own Reloaded-II setup, which the smoke test must leave as it found it.
        $reloaded = Join-Path $root 'reloaded'
        $script:config = Join-Path $reloaded 'ReloadedII.json'
        Write-Json $script:config ([ordered]@{
                ApplicationConfigDirectory = Join-Path $reloaded 'Apps'
                ModConfigDirectory         = Join-Path $reloaded 'Mods'
                ModUserConfigDirectory     = Join-Path $reloaded 'User/Mods'
                MiscConfigDirectory        = Join-Path $reloaded 'User/Misc'
                PluginConfigDirectory      = Join-Path $reloaded 'Plugins'
                LauncherPath               = 'C:/Reloaded/Reloaded-II.exe'
            })
        Write-Json (Join-Path $reloaded 'Apps/metaphor.exe/AppConfig.json') ([ordered]@{
                AppId = 'metaphor.exe'; AppLocation = $script:gameExe; EnabledMods = @('user.mod')
            })
        $script:userMod = Join-Path $reloaded 'Mods/user.mod'
        Write-Json (Join-Path $script:userMod 'ModConfig.json') @{ ModId = 'user.mod' }
        # Reloaded-II's own mod, which every install has and the loader needs to start.
        $script:sharedHooks = Join-Path $reloaded 'Mods/reloaded.sharedlib.hooks'
        Write-Json (Join-Path $script:sharedHooks 'ModConfig.json') @{ ModId = 'reloaded.sharedlib.hooks' }
        $script:configBefore = Get-FileHash $script:config

        $script:testMod = Join-Path $root 'test-mods/metaphor.smoke'
        Write-Json (Join-Path $script:testMod 'ModConfig.json') @{ ModId = 'metaphor.smoke' }
        $script:known = Join-Path $root 'known-game-builds.json'
        $env:METAPHOR_STANDIN_RELOADED_CONFIG = $script:config
    }

    AfterEach {
        Get-Game | Stop-Process -Force
        Remove-Item Env:METAPHOR_STANDIN_BEHAVIOUR, Env:METAPHOR_STANDIN_RELOADED_CONFIG -ErrorAction SilentlyContinue
    }

    It 'passes when every test mod is ready, and enables only the test mods' {
        $result = Invoke-SmokeTest

        $result.Output | Should -Match ([regex]::Escape("metaphor.smoke: ready on $script:build, hooks 1/1"))
        $result.Output | Should -Not -Match 'user\.mod'
        $result.ExitCode | Should -Be 0
        Join-Path $script:userMod 'mod-status.json' | Should -Not -Exist
    }

    It 'restores the config and closes the game after a pass' {
        Invoke-SmokeTest | Out-Null

        (Get-FileHash $script:config).Hash | Should -Be $script:configBefore.Hash
        Get-Game | Should -BeNullOrEmpty
    }

    It 'refuses an unknown game build without launching or touching the config' {
        $result = Invoke-SmokeTest -KnownBuild ('0' * 64)

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match "unknown game build $script:build"
        (Get-FileHash $script:config).Hash | Should -Be $script:configBefore.Hash
        Get-Game | Should -BeNullOrEmpty
    }

    It 'refuses to start when Reloaded-II''s own hooks mod is missing, without launching or touching the config' {
        Remove-Item -LiteralPath $script:sharedHooks -Recurse

        $result = Invoke-SmokeTest

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'reloaded\.sharedlib\.hooks'
        (Get-FileHash $script:config).Hash | Should -Be $script:configBefore.Hash
        Get-Game | Should -BeNullOrEmpty
    }

    It 'collects the files a test mod writes after its status, before cleaning up' {
        # A probes file asks the (stand-in) mod for a signature scan, which it writes after its status file.
        Write-Json (Join-Path $script:testMod 'signature-probes.json') @(@{ name = 'p'; pattern = '4D 5A'; source = 's' })
        $collected = Join-Path $TestDrive "collected-$([guid]::NewGuid())"

        $result = Invoke-SmokeTest -Extra '-CollectFiles', 'signature-scan.json', '-CollectTo', $collected

        $result.ExitCode | Should -Be 0
        Join-Path $collected 'metaphor.smoke/signature-scan.json' | Should -Exist
        (Get-FileHash $script:config).Hash | Should -Be $script:configBefore.Hash
        Get-Game | Should -BeNullOrEmpty
    }

    It 'fails when a test mod never writes a file it should, and still cleans up' {
        $collected = Join-Path $TestDrive "collected-$([guid]::NewGuid())"

        # The game exits a second after the status, which ends the wait for the file at once. A game that keeps
        # running would hold the test to the whole timeout (#339); the test above checks the game is closed.
        $result = Invoke-SmokeTest -Behaviour exitlater -Extra '-CollectFiles', 'signature-scan.json', '-CollectTo', $collected

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'metaphor\.smoke: no signature-scan\.json'
        (Get-FileHash $script:config).Hash | Should -Be $script:configBefore.Hash
        Get-Game | Should -BeNullOrEmpty
    }

    It 'fails when a mod refuses' {
        $result = Invoke-SmokeTest -Behaviour refuse

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match ([regex]::Escape("metaphor.smoke: refused on $script:build (unknown game build)"))
    }

    It 'times out when a mod never reports, then restores the config and closes the game' {
        $result = Invoke-SmokeTest -Behaviour silent -TimeoutSeconds 3

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'metaphor\.smoke: no status within 3 s'
        (Get-FileHash $script:config).Hash | Should -Be $script:configBefore.Hash
        Get-Game | Should -BeNullOrEmpty
    }

    It 'restores the config when the game crashes' {
        $result = Invoke-SmokeTest -Behaviour crash

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'game exited'
        (Get-FileHash $script:config).Hash | Should -Be $script:configBefore.Hash
    }

    It 'fails when a mod loads fewer hooks than it has' {
        $result = Invoke-SmokeTest -Behaviour partial

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'metaphor\.smoke: ready on .*, hooks 0/1'
    }

    It 'fails when a mod reports a different game build' {
        $result = Invoke-SmokeTest -Behaviour otherbuild

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match "metaphor\.smoke: ready on $('1' * 64)"
    }

    It 'ignores a status file left in a test mod folder by an earlier run' {
        Set-Content -LiteralPath (Join-Path $script:testMod 'mod-status.json') -Value (@{
                modId = 'metaphor.smoke'; state = 'ready'; gameBuildSha256 = $script:build; hooksLoaded = 1; hooksExpected = 1
            } | ConvertTo-Json)

        $result = Invoke-SmokeTest -Behaviour silent -TimeoutSeconds 3

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'metaphor\.smoke: no status within 3 s'
    }

    It 'refuses to start while the game is already running, and leaves it running' {
        $running = Start-Process $script:gameExe -PassThru

        $result = Invoke-SmokeTest

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'the game is already running'
        $running.HasExited | Should -BeFalse
        (Get-FileHash $script:config).Hash | Should -Be $script:configBefore.Hash
    }

    It 'restores the config a killed run left behind before starting' {
        $original = Get-Content -Raw $script:config
        Copy-Item $script:config "$script:config.smoke-backup"
        Set-Content -LiteralPath $script:config -Value '{"left":"by a killed run"}'

        $result = Invoke-SmokeTest

        $result.Output | Should -Match 'restored .* from an interrupted run'
        Get-Content -Raw $script:config | Should -Be $original
        "$script:config.smoke-backup" | Should -Not -Exist
    }
}
