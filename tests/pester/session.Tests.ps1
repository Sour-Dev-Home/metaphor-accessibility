# Tests for scripts/dev/session.ps1's deploy, run and status verbs, run against the stand-in launcher and game
# (tests/Metaphor.StandInGame) and a stand-in mod project built in the test folder. Run: Invoke-Pester tests/pester
# (Pester 5 or later).

BeforeAll {
    $repo = (Resolve-Path "$PSScriptRoot/../..").Path
    $script = Join-Path $repo 'scripts/dev/session.ps1'
    $standIn = Join-Path $repo 'tests/Metaphor.StandInGame/bin/Release/net9.0'
    if (-not $env:METAPHOR_STANDIN_BUILT) { # scripts/check/run-pester.sh builds it once for every process
        dotnet build (Join-Path $repo 'tests/Metaphor.StandInGame') -c Release -v q --nologo | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'the stand-in game failed to build' }
    }
    Import-Module (Join-Path $PSScriptRoot 'StandIn.psm1') -Force

    # Runs a verb in its own process, so its exit code and cleanup are real. The timeout is a generous upper bound: the
    # script returns as soon as the mod reports, so a loaded machine only slows a test down (coding-standards judgement
    # rule 4).
    function Invoke-Session([string] $Verb, [string[]] $Extra = @(), [string] $Behaviour = 'ready', [switch] $NoPaths) {
        $env:METAPHOR_STANDIN_BEHAVIOUR = $Behaviour
        $more = @(if ($Verb -ne 'status') { '-TimeoutSeconds', '60' }
            if ($Extra -notcontains '-Project') { '-Project', $f.Project })
        $output = & pwsh -NoProfile -File $script $Verb @(Get-FixtureArgs $f -NoPaths:$NoPaths) @more @Extra 2>&1
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n"); Last = "$(@($output)[-1])" }
    }

    # By name first, as scripts/dev/ReloadedDev.psm1's Get-Game: reading every process's path is slow on a busy machine.
    function Get-Game { [Diagnostics.Process]::GetProcessesByName('METAPHOR') | Where-Object Path -EQ $f.GameExe }
    function Get-EnabledMods { @((Get-Content -Raw $f.AppConfig | ConvertFrom-Json).EnabledMods) }
    function Get-ConfigHash { (Get-FileHash $f.Config).Hash }
    function Get-OptInFiles { @(Get-ChildItem -LiteralPath $f.ModFolder -Filter 'opt-in-*.txt' -ErrorAction SilentlyContinue) }
}

Describe 'session.ps1' {
    AfterAll {
        # Every stand-in still running from TestDrive, so Pester can delete it.
        Stop-StandIn $TestDrive
    }

    BeforeEach {
        # TestDrive is shared by every test in this Describe, so each test gets its own folder.
        $f = New-SessionFixture (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName $standIn
        $configBefore = Get-ConfigHash

        # Reloaded-II's log of a run, as the stand-in writes it. Every SECRET stands for game text.
        $loaderLog = Join-Path $f.Root 'loader-log.txt'
        Set-Content -LiteralPath $loaderLog -Value @(
            '[12:00:00] [Reloaded] Loading mod metaphor.dev'
            '[12:00:01] [metaphor.dev] speech: SAPI'
            "[12:00:01] [metaphor.dev] hook DialogueWindowOpenHook skipped: opt-in, and opt-in-dialogue-window.txt isn't in the mod's folder"
            '[12:00:01] [metaphor.dev] hook UiTextPaneListener failed to install: InvalidOperationException'
            '[12:00:01] [metaphor.dev] build abc1234: Ready on game build 0123, hooks 1/1; load message: Metaphor accessibility ready'
            '[12:00:02] [other.mod] something failed'
        )
        # A message log as the Debug mod writes it (Metaphor.Core.MessageLog).
        $messageLog = Join-Path $f.Root 'message-log.txt'
        Set-Content -LiteralPath $messageLog -Value @(
            '120 ms register name=MsgRequest rva=0x90c420 direct'
            '130 ms compile id=7 path=script/SECRET.msg chars=30'
            '  | SECRET line one'
            '150 ms request via=MsgRequestByName kind=0 text="SECRET three"'
            '170 ms request via=MsgRequestById kind=0 id=1 label=2'
            'SECRET in a line that is no record'
        )
        $transcript = Join-Path $f.Root 'transcript.txt'
        Set-Content -LiteralPath $transcript -Value 'SECRET announcement'
        $env:METAPHOR_STANDIN_LOADER_LOG = $loaderLog
        $env:METAPHOR_STANDIN_MESSAGE_LOG = $messageLog
        $env:METAPHOR_STANDIN_TRANSCRIPT = $transcript
    }

    AfterEach {
        Get-Game | Stop-Process -Force -PassThru | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
        Remove-Item Env:METAPHOR_STANDIN_BEHAVIOUR, Env:METAPHOR_STANDIN_RELOADED_CONFIG, Env:METAPHOR_STANDIN_LOADER_LOG,
            Env:METAPHOR_STANDIN_MESSAGE_LOG, Env:METAPHOR_STANDIN_TRANSCRIPT -ErrorAction SilentlyContinue
    }

    Context 'deploy' {
        It 'builds, installs and enables the mod, keeping the user''s mods, and closes the game once it loaded' {
            $before = Get-FileHash $f.AppConfig

            $result = Invoke-Session deploy

            $result.ExitCode | Should -Be 0 -Because $result.Output
            $result.Output | Should -Match ([regex]::Escape("metaphor.dev: ready on $($f.Build), hooks 1/1"))
            $result.Last | Should -Be 'ok: deployed metaphor.dev and it loaded; closed the game'
            Join-Path $f.ModFolder 'Metaphor.DevMod.dll' | Should -Exist
            Get-EnabledMods | Should -Be @('user.mod', 'metaphor.dev')
            (Get-FileHash "$($f.AppConfig).deploy-backup").Hash | Should -Be $before.Hash
            Get-ConfigHash | Should -Be $configBefore
            Get-Game | Should -BeNullOrEmpty
        }

        It 'leaves the game running with -KeepGame' {
            $result = Invoke-Session deploy -Extra '-KeepGame'

            $result.ExitCode | Should -Be 0 -Because $result.Output
            $result.Last | Should -Be 'ok: deployed metaphor.dev; the game is left running'
            Get-Game | Should -Not -BeNullOrEmpty
        }

        It 'deploys the real mod: its ModConfig.json names a DLL that is there, and the loader-supplied one is not' {
            $result = Invoke-Session deploy -Extra '-Project', (Join-Path $repo 'src/Metaphor.Hooks')

            $result.ExitCode | Should -Be 0 -Because $result.Output
            $folder = Join-Path $f.Mods 'metaphor.accessibility'
            $config = Get-Content -Raw (Join-Path $folder 'ModConfig.json') | ConvertFrom-Json
            Join-Path $folder $config.ModDll | Should -Exist
            Join-Path $folder 'Reloaded.Mod.Interfaces.dll' | Should -Not -Exist
        }

        It 'finds the game and Reloaded-II through the locator when no path or variable is given' {
            # The locator takes only a launcher named Reloaded-II.exe.
            $reloadedExe = Join-Path (Split-Path $f.GameExe) 'Reloaded-II.exe'
            Copy-Item $f.Launcher $reloadedExe
            $loader = Get-Content -Raw $f.Config | ConvertFrom-Json
            $loader.LauncherPath = $reloadedExe
            $loader | ConvertTo-Json | Set-Content -LiteralPath $f.Config -Encoding utf8NoBOM
            $configBefore = Get-ConfigHash
            $saved = $env:METAPHOR_GAME_EXE, $env:METAPHOR_RELOADED_EXE
            Remove-Item Env:METAPHOR_GAME_EXE, Env:METAPHOR_RELOADED_EXE -ErrorAction SilentlyContinue
            try { $result = Invoke-Session deploy -NoPaths }
            finally { $env:METAPHOR_GAME_EXE, $env:METAPHOR_RELOADED_EXE = $saved }

            $result.ExitCode | Should -Be 0 -Because $result.Output
            $result.Output | Should -Match ([regex]::Escape("metaphor.dev: ready on $($f.Build)"))
            Get-ConfigHash | Should -Be $configBefore
        }

        It 'takes the game and Reloaded-II from the environment over the locator' {
            $loader = Get-Content -Raw $f.Config | ConvertFrom-Json
            $loader.LauncherPath = Join-Path $f.Root 'nowhere/Reloaded-II.exe'
            $loader | ConvertTo-Json | Set-Content -LiteralPath $f.Config -Encoding utf8NoBOM
            $reloadedExe = Join-Path (Split-Path $f.GameExe) 'Reloaded-II.exe'
            Copy-Item $f.Launcher $reloadedExe
            $saved = $env:METAPHOR_GAME_EXE, $env:METAPHOR_RELOADED_EXE
            $env:METAPHOR_GAME_EXE, $env:METAPHOR_RELOADED_EXE = $f.GameExe, $reloadedExe
            try { $result = Invoke-Session deploy -NoPaths }
            finally { $env:METAPHOR_GAME_EXE, $env:METAPHOR_RELOADED_EXE = $saved }

            $result.ExitCode | Should -Be 0 -Because $result.Output
        }

        # Windows paths ignore case, but Reloaded-II's --launch compares them exactly, as the stand-in does (#457).
        It 'finds the game registered under another casing of its path, and launches it by the registered one' {
            $result = Invoke-Session deploy -NoPaths -Extra '-GameExe', $f.GameExe.ToLowerInvariant(), '-LauncherExe', $f.Launcher

            $result.ExitCode | Should -Be 0 -Because $result.Output
            $result.Output | Should -Match ([regex]::Escape("metaphor.dev: ready on $($f.Build), hooks 1/1"))
            Get-Game | Should -BeNullOrEmpty
        }

        # Exit code 2 promises nothing changed (session.ps1's help).
        It 'exits 1, not 2, when it fails after installing the mod and before launching' {
            New-Item -ItemType Directory "$($f.AppConfig).deploy-new" | Out-Null

            $result = Invoke-Session deploy

            $result.ExitCode | Should -Be 1 -Because $result.Output
            $result.Output | Should -Match 'deployed metaphor\.dev to '
            $f.ModFolder | Should -Exist
            Get-ConfigHash | Should -Be $configBefore
            Get-Game | Should -BeNullOrEmpty
        }

        It 'refuses an unknown game build without building, installing or launching' {
            Set-Content -LiteralPath $f.Known -Value (ConvertTo-Json @(@{ sha256 = '0' * 64; steamBuildId = 1; evidence = 'test' }))

            $result = Invoke-Session deploy

            $result.ExitCode | Should -Be 2
            $result.Last | Should -Match "^failed: unknown game build $($f.Build) - add it"
            $f.ModFolder | Should -Not -Exist
            Get-EnabledMods | Should -Be @('user.mod')
            Get-Game | Should -BeNullOrEmpty
        }

        It 'refuses to start while the game is already running, and leaves it running' {
            $running = Start-Process $f.GameExe -PassThru

            $result = Invoke-Session deploy

            $result.ExitCode | Should -Be 2
            $result.Last | Should -Be 'failed: the game is already running - close it first'
            $running.HasExited | Should -BeFalse
            $f.ModFolder | Should -Not -Exist
        }

        It 'changes nothing when the build fails' {
            Set-Content -LiteralPath (Join-Path $f.Project 'Mod.cs') -Value 'this is not C#'

            $result = Invoke-Session deploy

            $result.ExitCode | Should -Be 2
            $result.Last | Should -Match '^failed: the build of .* failed - fix the errors above'
            $f.ModFolder | Should -Not -Exist
            Get-ConfigHash | Should -Be $configBefore
            Get-EnabledMods | Should -Be @('user.mod')
            Get-Game | Should -BeNullOrEmpty
        }

        It 'replaces an earlier deploy, and leaves a folder alone that holds another mod' {
            Invoke-Session deploy | Out-Null
            Set-Content -LiteralPath (Join-Path $f.ModFolder 'stale.txt') -Value 'from an earlier build'

            (Invoke-Session deploy).ExitCode | Should -Be 0
            Join-Path $f.ModFolder 'stale.txt' | Should -Not -Exist

            Set-Content -LiteralPath (Join-Path $f.ModFolder 'ModConfig.json') -Value '{"ModId":"someone.else"}'
            $result = Invoke-Session deploy
            $result.ExitCode | Should -Be 2
            $result.Last | Should -Match 'holds something other than metaphor\.dev - move it out of the way first'
        }

        It 'refuses a mod ID that is not a plain folder name, and deletes nothing' {
            Set-Content -LiteralPath (Join-Path $f.Project 'ModConfig.json') -Value '{"ModId":"../user.mod"}'

            $result = Invoke-Session deploy

            $result.ExitCode | Should -Be 2
            $result.Last | Should -Match 'mod ID .* is not a plain folder name'
            Join-Path $f.Mods 'user.mod/ModConfig.json' | Should -Exist
        }

        # Windows drops trailing dots and spaces, and reserves device names, with or without an extension.
        It 'refuses the mod ID <_>, which Windows doesn''t keep as a folder name' -ForEach 'metaphor.dev.', 'metaphor.dev ', 'CON', 'nul.txt', 'com1', 'LPT9.mod' {
            @{ ModId = $_ } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $f.Project 'ModConfig.json')

            $result = Invoke-Session deploy

            $result.ExitCode | Should -Be 2 -Because $result.Output
            $result.Last | Should -Match 'mod ID .* is not a plain folder name'
        }

        It 'fails when the mod refuses, then closes the game and restores the config' {
            $result = Invoke-Session deploy -Behaviour refuse

            $result.ExitCode | Should -Be 1
            $result.Output | Should -Match ([regex]::Escape("metaphor.dev: refused on $($f.Build) (unknown game build)"))
            $result.Last | Should -Match "^failed: metaphor.dev isn't ready with all its hooks - "
            Get-Game | Should -BeNullOrEmpty
            Get-ConfigHash | Should -Be $configBefore
        }

        It 'restores the config a killed session left behind before starting' {
            $original = Get-Content -Raw $f.Config
            Copy-Item $f.Config "$($f.Config).session-backup"
            Set-Content -LiteralPath $f.Config -Value '{"left":"by a killed run"}'

            $result = Invoke-Session deploy

            $result.Output | Should -Match 'restored .* from an interrupted session'
            Get-Content -Raw $f.Config | Should -Be $original
            "$($f.Config).session-backup" | Should -Not -Exist
        }

        It 'refuses an option the verb does not take, before anything' {
            $result = Invoke-Session deploy -Extra '-Seconds', '5'

            $result.ExitCode | Should -Be 2
            $result.Last | Should -Match "^failed: deploy doesn't take -Seconds - "
            $f.ModFolder | Should -Not -Exist
        }
    }

    Context 'run' {
        It 'deploys with the opt-ins, reports the hooks and the loader''s lines, summarises the log and removes the opt-ins' {
            # Older logs named for each of the next two minutes, so this run's log clashes with one and gets the
            # loader's " [n]" suffix (#339; reloaded-ii-launch-and-logs-2026-11-13.md).
            $logs = New-Item -ItemType Directory -Force (Join-Path (Split-Path $f.Config) 'Logs')
            $now = [DateTime]::UtcNow
            foreach ($second in 0..120) {
                New-Item -ItemType File (Join-Path $logs "$($now.AddSeconds($second).ToString('yyyy-MM-dd HH.mm.ss')) ~ METAPHOR.txt") | Out-Null
            }

            $result = Invoke-Session run -Extra '-Seconds', '2', '-OptIn', 'ui-text-panes,dialogue-window'

            $result.ExitCode | Should -Be 0 -Because $result.Output
            $result.Output | Should -Match 'opted in: ui-text-panes'
            $result.Output | Should -Match 'opted in: dialogue-window'
            $result.Output | Should -Match 'metaphor.dev: ready on [0-9a-f]{64}, hooks 1/1'
            $result.Output | Should -Match 'hook UiTextPaneListener failed to install'
            $result.Output | Should -Match 'Ready on game build 0123, hooks 1/1'
            $result.Output | Should -Not -Match 'speech: SAPI|other\.mod'
            $result.Output | Should -Match 'the game is still running after 2 s'
            $result.Output | Should -Match 'message log: 1 compile \(1 source lines\), 1 register, 2 request, 1 unrecognised line'
            $result.Output | Should -Match 'requests by function: MsgRequestById 1, MsgRequestByName 1'
            $result.Last | Should -Be 'ok: the game ran 2 s with metaphor.dev loaded; closed the game'
            Get-OptInFiles | Should -BeNullOrEmpty
            Get-Game | Should -BeNullOrEmpty
            Get-ConfigHash | Should -Be $configBefore
        }

        It 'prints no game text and no local path' {
            $result = Invoke-Session run -Extra '-Seconds', '1'

            $result.Output | Should -Not -Match 'SECRET'
            $result.Output | Should -Not -Match ([regex]::Escape($f.Root))
            $result.Output | Should -Not -Match ([regex]::Escape([Environment]::GetFolderPath('UserProfile')))
            $result.Output | Should -Match 'deployed metaphor.dev to <mods>'
        }

        It 'leaves the game running with -KeepGame, and still removes the opt-ins' {
            $result = Invoke-Session run -Extra '-Seconds', '1', '-OptIn', 'ui-text-panes', '-KeepGame'

            $result.ExitCode | Should -Be 0 -Because $result.Output
            $result.Last | Should -Match 'the game is left running$'
            Get-Game | Should -Not -BeNullOrEmpty
            Get-OptInFiles | Should -BeNullOrEmpty
        }

        It 'fails when the game exits while it runs, and still summarises the log' {
            $result = Invoke-Session run -Extra '-Seconds', '30' -Behaviour exitlater

            $result.ExitCode | Should -Be 1
            $result.Output | Should -Match 'message log: 1 compile'
            $result.Last | Should -Match '^failed: the game exited after \d+ s - '
        }

        It 'fails when the mod wrote no message log' {
            Remove-Item Env:METAPHOR_STANDIN_MESSAGE_LOG

            $result = Invoke-Session run -Extra '-Seconds', '1'

            $result.ExitCode | Should -Be 1
            $result.Last | Should -Match '^failed: no message-log.txt from metaphor.dev - '
        }

        It 'saves the message log and transcript once the game exits, with -SaveLog' {
            $save = Join-Path $TestDrive "captures-$(New-Guid)"

            # The game quitting ends a capture.
            $result = Invoke-Session run -Extra '-Seconds', '60', '-SaveLog', $save -Behaviour exitlater

            $result.ExitCode | Should -Be 0 -Because $result.Output
            $result.Last | Should -Match '^ok: .*; logs saved to <save>'
            $saved = @(Get-ChildItem -LiteralPath $save -Directory)
            $saved.Count | Should -Be 1
            Join-Path $saved[0].FullName 'message-log.txt' | Should -Exist
            Join-Path $saved[0].FullName 'transcript.txt' | Should -Exist
            $result.Output | Should -Not -Match 'SECRET'
        }

        It 'refuses to save the logs inside a git repository, before deploying' {
            $result = Invoke-Session run -Extra '-SaveLog', (Join-Path $repo 'captures')

            $result.ExitCode | Should -Be 2
            $result.Last | Should -Match '^failed: -SaveLog .* is inside the git repository'
            $f.ModFolder | Should -Not -Exist
            Join-Path $repo 'captures' | Should -Not -Exist
        }

        It 'refuses an unknown opt-in with the list of valid names, before deploying' -ForEach @(
            @{ Name = 'ui-text-pain' }
            @{ Name = '../evil' }
        ) {
            $result = Invoke-Session run -Extra '-OptIn', $Name

            $result.ExitCode | Should -Be 2
            $result.Last | Should -Be "failed: unknown opt-in $Name - use one of: dialogue-window, ui-text-panes"
            $f.ModFolder | Should -Not -Exist
            Get-Game | Should -BeNullOrEmpty
        }

        It 'stops without launching when the build is unknown' {
            Set-Content -LiteralPath $f.Known -Value (ConvertTo-Json @(@{ sha256 = '0' * 64; steamBuildId = 1; evidence = 'test' }))

            $result = Invoke-Session run -Extra '-Seconds', '1'

            $result.ExitCode | Should -Be 2
            $result.Output | Should -Not -Match 'message log'
            Get-Game | Should -BeNullOrEmpty
        }

        It 'restores the config, removes the opt-ins and closes the game when interrupted partway' {
            # Ctrl+C stops the running pipeline; stopping an in-process one does the same.
            $session = [powershell]::Create()
            $null = $session.AddCommand('Import-Module').AddArgument((Join-Path $repo 'scripts/dev/GameSession.psm1')).AddStatement()
            $null = $session.AddCommand('Invoke-GameSession').AddParameter('Verb', 'run').AddParameter('Options', @{
                    GameExe = $f.GameExe; LauncherExe = $f.Launcher; ReloadedConfig = $f.Config; KnownBuilds = $f.Known
                    Project = $f.Project; Seconds = 600; OptIn = @('ui-text-panes'); TimeoutSeconds = 60
                })
            $null = $session.BeginInvoke()
            try {
                # Partway: the game is running with the opt-in in place.
                $deadline = (Get-Date).AddSeconds(120)
                while (-not ((Get-OptInFiles) -and (Test-Path (Join-Path $f.ModFolder 'mod-status.json')) -and (Get-Game))) {
                    if ((Get-Date) -gt $deadline -or $session.InvocationStateInfo.State -ne 'Running') {
                        throw "the run never got going: $($session.Streams.Information -join "`n") $($session.Streams.Error)"
                    }
                    Start-Sleep -Milliseconds 100
                }
                # Reloaded-II rewrites its config on a launch; the session puts back what it found.
                Set-Content -LiteralPath $f.Config -Value '{"saved":"by the launcher"}'

                $session.Stop()
            }
            finally { $session.Dispose() }

            Get-ConfigHash | Should -Be $configBefore
            "$($f.Config).session-backup" | Should -Not -Exist
            Get-OptInFiles | Should -BeNullOrEmpty
            Get-Game | Should -BeNullOrEmpty
        }
    }

    Context 'status' {
        It 'reports an undeployed mod, the game build and the valid opt-ins, and changes nothing' {
            $result = Invoke-Session status

            $result.ExitCode | Should -Be 0 -Because $result.Output
            $result.Output | Should -Match "game: <game>\\METAPHOR.exe, build $($f.Build) \(Steam build 1\)"
            $result.Output | Should -Match 'deployed: metaphor.dev is not deployed'
            $result.Output | Should -Match 'inspector: not listening'
            $result.Output | Should -Match 'valid opt-ins: dialogue-window, ui-text-panes'
            $result.Last | Should -Be 'ok: status read'
            Get-ConfigHash | Should -Be $configBefore
        }

        It 'reports the deployed mod and its active opt-ins' {
            New-Item -ItemType Directory $f.ModFolder | Out-Null
            Set-Content -LiteralPath (Join-Path $f.ModFolder 'ModConfig.json') -Value '{"ModId":"metaphor.dev"}'
            New-Item -ItemType File (Join-Path $f.ModFolder 'opt-in-ui-text-panes.txt') | Out-Null
            $app = Get-Content -Raw $f.AppConfig | ConvertFrom-Json
            $app.EnabledMods = @('user.mod', 'metaphor.dev')
            $app | ConvertTo-Json | Set-Content -LiteralPath $f.AppConfig -Encoding utf8NoBOM

            $result = Invoke-Session status

            $result.ExitCode | Should -Be 0
            $result.Output | Should -Match 'deployed: metaphor.dev in <mods>\\metaphor.dev, enabled'
            $result.Output | Should -Match 'opt-ins active: ui-text-panes'
        }
    }
}
