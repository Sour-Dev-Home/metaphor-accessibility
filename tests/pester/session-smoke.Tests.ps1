# Tests for scripts/dev/session.ps1's smoke and spike verbs, run against the stand-in launcher and game
# (tests/Metaphor.StandInGame). Run: Invoke-Pester tests/pester (Pester 5 or later).

BeforeAll {
    $repo = (Resolve-Path "$PSScriptRoot/../..").Path
    $script = Join-Path $repo 'scripts/dev/session.ps1'
    $standIn = Join-Path $repo 'tests/Metaphor.StandInGame/bin/Release/net9.0'
    if (-not $env:METAPHOR_STANDIN_BUILT) { # scripts/check/run-pester.sh builds it once for every process
        dotnet build (Join-Path $repo 'tests/Metaphor.StandInGame') -c Release -v q --nologo | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'the stand-in game failed to build' }
    }
    Import-Module (Join-Path $PSScriptRoot 'StandIn.psm1') -Force

    # Runs a verb in its own process, so its exit code and cleanup are real. The default timeout is a generous upper
    # bound: the script returns as soon as the state it waits for arrives, so a loaded machine only slows a test down
    # (coding-standards judgement rule 4). Only a test of the timeout itself sets a short one.
    function Invoke-Session([string] $Verb, [string[]] $Extra = @(), [string] $Behaviour = 'ready', [int] $TimeoutSeconds = 60) {
        $env:METAPHOR_STANDIN_BEHAVIOUR = $Behaviour
        $output = & pwsh -NoProfile -File $script $Verb @(Get-FixtureArgs $f) -TimeoutSeconds $TimeoutSeconds @Extra 2>&1
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n"); Last = "$(@($output)[-1])" }
    }

    # By name first, as scripts/dev/ReloadedDev.psm1's Get-Game: reading every process's path is slow on a busy machine.
    function Get-Game { [Diagnostics.Process]::GetProcessesByName('METAPHOR') | Where-Object Path -EQ $f.GameExe }
    function Get-ConfigHash { (Get-FileHash $f.Config).Hash }
}

Describe 'session.ps1 smoke' {
    AfterAll {
        Stop-StandIn $TestDrive
    }

    BeforeEach {
        # TestDrive is shared by every test in this Describe, so each test gets its own folder.
        $f = New-SessionFixture (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName $standIn
        $configBefore = Get-ConfigHash
        $testMod = Join-Path $f.Root 'test-mods/metaphor.smoke'
        Set-Content -LiteralPath (New-Item -ItemType File -Force (Join-Path $testMod 'ModConfig.json')) -Value '{"ModId":"metaphor.smoke"}'
        $userMod = Join-Path $f.Mods 'user.mod'
    }

    AfterEach {
        Get-Game | Stop-Process -Force -PassThru | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
        Remove-Item Env:METAPHOR_STANDIN_BEHAVIOUR, Env:METAPHOR_STANDIN_RELOADED_CONFIG -ErrorAction SilentlyContinue
    }

    It 'passes when every test mod is ready, enables only the test mods, then restores the config and closes the game' {
        $result = Invoke-Session smoke -Extra '-ModFolders', $testMod

        $result.ExitCode | Should -Be 0 -Because $result.Output
        $result.Output | Should -Match ([regex]::Escape("metaphor.smoke: ready on $($f.Build), hooks 1/1"))
        $result.Output | Should -Not -Match 'user\.mod'
        $result.Last | Should -Be 'ok: every test mod is ready with all its hooks; closed the game'
        Join-Path $userMod 'mod-status.json' | Should -Not -Exist
        Get-ConfigHash | Should -Be $configBefore
        Get-Game | Should -BeNullOrEmpty
    }

    It 'refuses an unknown game build without launching or touching the config' {
        Set-Content -LiteralPath $f.Known -Value (ConvertTo-Json @(@{ sha256 = '0' * 64; steamBuildId = 1; evidence = 'test' }))

        $result = Invoke-Session smoke -Extra '-ModFolders', $testMod

        $result.ExitCode | Should -Be 2
        $result.Last | Should -Match "^failed: unknown game build $($f.Build) - "
        Get-ConfigHash | Should -Be $configBefore
        Get-Game | Should -BeNullOrEmpty
    }

    It 'refuses to start without test mods' {
        $result = Invoke-Session smoke

        $result.ExitCode | Should -Be 2
        $result.Last | Should -Match '^failed: no test mods - pass -ModFolders'
    }

    It 'refuses to start when Reloaded-II''s own hooks mod is missing, and restores the config' {
        Remove-Item -LiteralPath (Join-Path $f.Mods 'reloaded.sharedlib.hooks') -Recurse

        $result = Invoke-Session smoke -Extra '-ModFolders', $testMod

        $result.ExitCode | Should -Be 2
        $result.Last | Should -Match 'reloaded\.sharedlib\.hooks'
        Get-ConfigHash | Should -Be $configBefore
        Get-Game | Should -BeNullOrEmpty
    }

    # As deploy checks its mod (#464): the ID names the test mod's folder in the throwaway profile.
    It 'refuses a test mod ID that is not a plain folder name, and writes nothing outside the profile' {
        $escape = "metaphor-escape-$(New-Guid)"
        Set-Content -LiteralPath (Join-Path $testMod 'ModConfig.json') -Value "{`"ModId`":`"../../$escape`"}"

        $result = Invoke-Session smoke -Extra '-ModFolders', $testMod -TimeoutSeconds 5

        $result.ExitCode | Should -Be 2 -Because $result.Output
        $result.Last | Should -Match "^failed: the mod ID '\.\./\.\./$escape' in .* is not a plain folder name - "
        Join-Path ([IO.Path]::GetTempPath()) $escape | Should -Not -Exist
        Get-ConfigHash | Should -Be $configBefore
    }

    It 'fails when a mod refuses' {
        $result = Invoke-Session smoke -Extra '-ModFolders', $testMod -Behaviour refuse

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match ([regex]::Escape("metaphor.smoke: refused on $($f.Build) (unknown game build)"))
        $result.Last | Should -Match '^failed: a test mod is not ready'
    }

    It 'times out when a mod never reports, then restores the config and closes the game' {
        $result = Invoke-Session smoke -Extra '-ModFolders', $testMod -Behaviour silent -TimeoutSeconds 3

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'metaphor\.smoke: no status within 3 s'
        Get-ConfigHash | Should -Be $configBefore
        Get-Game | Should -BeNullOrEmpty
    }

    It 'restores the config when the game crashes' {
        $result = Invoke-Session smoke -Extra '-ModFolders', $testMod -Behaviour crash

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'game exited'
        Get-ConfigHash | Should -Be $configBefore
    }

    It 'fails when a mod loads fewer hooks than it has, or reports another build' -ForEach @(
        @{ Behaviour = 'partial'; Line = 'metaphor\.smoke: ready on .*, hooks 0/1' }
        @{ Behaviour = 'otherbuild'; Line = "metaphor\.smoke: ready on $('1' * 64)" }
    ) {
        $result = Invoke-Session smoke -Extra '-ModFolders', $testMod -Behaviour $Behaviour

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match $Line
    }

    It 'ignores a status file left in a test mod folder by an earlier run' {
        Set-Content -LiteralPath (Join-Path $testMod 'mod-status.json') -Value (@{
                modId = 'metaphor.smoke'; state = 'ready'; gameBuildSha256 = $f.Build; hooksLoaded = 1; hooksExpected = 1
            } | ConvertTo-Json)

        $result = Invoke-Session smoke -Extra '-ModFolders', $testMod -Behaviour silent -TimeoutSeconds 3

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'metaphor\.smoke: no status within 3 s'
    }

    It 'refuses to start while the game is already running, and leaves it running' {
        $running = Start-Process $f.GameExe -PassThru

        $result = Invoke-Session smoke -Extra '-ModFolders', $testMod

        $result.ExitCode | Should -Be 2
        $result.Last | Should -Be 'failed: the game is already running - close it first'
        $running.HasExited | Should -BeFalse
        Get-ConfigHash | Should -Be $configBefore
    }
}

Describe 'session.ps1 spike' {
    BeforeAll {
        $f = New-SessionFixture (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName $standIn
        $configBefore = Get-ConfigHash
        Remove-Item Env:METAPHOR_STANDIN_BEHAVIOUR -ErrorAction SilentlyContinue

        # The committed probes, plus one that matches the stand-in exe: its DOS header, at the image's start both on
        # disk and in "memory".
        $probes = Join-Path $f.Root 'probes.json'
        $committed = Get-Content -Raw (Join-Path $repo 'tools/Metaphor.SignatureCheck/signature-probes.json') | ConvertFrom-Json
        (@($committed) + @{ name = 'dos-header'; pattern = '4D 5A 90 00'; source = 'test' }) | ConvertTo-Json -Depth 5 |
            Set-Content -LiteralPath $probes -Encoding utf8NoBOM

        # A run where the mod refuses, so the smoke run fails.
        $script:failedDraft = Join-Path $f.Root 'failed-draft.md'
        $script:failedRun = Invoke-Session spike -Extra '-Probes', $probes, '-OutFile', $script:failedDraft -Behaviour refuse
        Get-Game | Stop-Process -Force -PassThru | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue

        $script:draft = Join-Path $f.Root 'draft.md'
        $script:run = Invoke-Session spike -Extra '-Probes', $probes, '-OutFile', $script:draft
        $script:configRestored = (Get-ConfigHash) -eq $configBefore
        $script:running = Get-Game
    }

    AfterAll {
        Stop-StandIn $TestDrive
        Remove-Item Env:METAPHOR_STANDIN_RELOADED_CONFIG, Env:METAPHOR_STANDIN_BEHAVIOUR -ErrorAction SilentlyContinue
    }

    It 'writes a draft and exits 0' {
        $script:run.ExitCode | Should -Be 0 -Because $script:run.Output
        $script:run.Last | Should -Match '^ok: draft written: '
        $script:draft | Should -Exist
    }

    It 'names the game build and the command in the raw-source header' {
        $draft = Get-Content -Raw $script:draft
        $draft | Should -Match ([regex]::Escape("- **Game build:** ``$($f.Build)``"))
        $draft | Should -Match ([regex]::Escape('`scripts/dev/session.ps1 spike`'))
    }

    It 'names the probes file it scanned for and where its probes came from' {
        $draft = Get-Content -Raw $script:draft
        $draft | Should -Match ([regex]::Escape('Probes: `probes.json`'))
        $draft | Should -Match ([regex]::Escape('`docs/raw-sources/crifs-v2-hook-metaphor-patterns-2026-11-15.md`, `test`'))
    }

    It 'compares each probe on disk and in memory' {
        $draft = Get-Content -Raw $script:draft
        $draft | Should -Match '\| `CriFs_InitializeLibrary` \| 0 \|  \| 0 \|  \| absent from both \|'
        $draft | Should -Match '\| `dos-header` \| 1 \| 0x0 \| 1 \| 0x0 \| same place \|'
    }

    It 'closes the game and restores the Reloaded-II config' {
        $script:running | Should -BeNullOrEmpty
        $script:configRestored | Should -BeTrue
    }

    It 'fails and writes no draft when the smoke run fails' {
        $script:failedRun.ExitCode | Should -Be 1 -Because $script:failedRun.Output
        $script:failedRun.Last | Should -Match '^failed: the smoke run failed'
        $script:failedDraft | Should -Not -Exist
    }

    It 'keeps local paths out of the draft' {
        Get-Content -Raw $script:draft | Should -Not -Match ([regex]::Escape($f.Root))
    }
}
