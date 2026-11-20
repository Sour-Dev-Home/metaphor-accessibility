# Tests for scripts/dev/session.ps1's inspect verb, and status's view of the inspector, run against the stand-in game
# acting as the Debug mod's live inspector (tests/Metaphor.StandInGame/InspectorStandIn.cs): the same commands over a
# pipe, from a fake 0x400-byte module at 0x140000000 holding deadbeef at +0x10 and a pointer to +0x100 at +0x40.

BeforeAll {
    $repo = (Resolve-Path "$PSScriptRoot/../..").Path
    $script = Join-Path $repo 'scripts/dev/session.ps1'
    $standIn = Join-Path $repo 'tests/Metaphor.StandInGame/bin/Release/net9.0'
    if (-not $env:METAPHOR_STANDIN_BUILT) { # scripts/check/run-pester.sh builds it once for every process
        dotnet build (Join-Path $repo 'tests/Metaphor.StandInGame') -c Release -v q --nologo | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'the stand-in game failed to build' }
    }

    function Invoke-Inspect([string] $Pipe, [string[]] $Requests, [int] $Timeout = 10000) {
        $output = & pwsh -NoProfile -File $script inspect -PipeName $Pipe -TimeoutMilliseconds $Timeout @Requests
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = @($output) }
    }

    # Runs inspect -Batch on a file holding LINES, with KEYS as the console's input lines, logging under $TestDrive/save.
    function Invoke-Batch([string] $Pipe, [string[]] $Lines, [string[]] $Options = @(), [string[]] $Keys = @()) {
        $file = Join-Path $TestDrive 'states.txt'
        Set-Content -LiteralPath $file -Value $Lines
        $save = Join-Path $TestDrive 'save'
        $output = $Keys | & pwsh -NoProfile -File $script inspect -PipeName $Pipe -TimeoutMilliseconds 10000 -Batch $file -SaveLog $save @Options
        $log = Join-Path $save 'inspector-log.txt'
        [pscustomobject]@{
            ExitCode = $LASTEXITCODE; Output = @($output)
            # Each logged line without its time, which must lead it.
            Log = @(if (Test-Path -LiteralPath $log) {
                    Get-Content -LiteralPath $log | ForEach-Object {
                        $_ | Should -Match '^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}[+-]\d\d:\d\d\t'
                        $_ -replace '^[^\t]*\t'
                    }
                })
        }
    }
}

Describe 'session.ps1 inspect' {
    BeforeAll {
        # A pipe of its own, so test processes running at once never meet.
        $script:pipe = "metaphor-inspector-test-$(New-Guid)"
        # Run from a copy, so Stop-StandIn can find and stop it and the build output is never held open.
        $copy = Join-Path $TestDrive 'stand-in'
        Copy-Item $standIn $copy -Recurse
        Start-Process (Join-Path $copy 'Metaphor.StandInGame.exe') -ArgumentList '--inspector', $script:pipe | Out-Null
    }

    AfterAll {
        Import-Module (Join-Path $PSScriptRoot 'StandIn.psm1') -Force
        Stop-StandIn $TestDrive
    }

    It 'prints one response per request, in order, then the summary' {
        $result = Invoke-Inspect $script:pipe @('base', 'read base+0x10 4', 'follow base+0x40 -0x10')

        $result.Output | Should -Be @('ok 0x140000000 0x400', 'ok deadbeef', 'ok 0x1400000f0', 'ok: 3 requests answered')
        $result.ExitCode | Should -Be 0
    }

    It 'exits 1 when the inspector refuses a request, and still answers the rest' {
        $result = Invoke-Inspect $script:pipe @('write base+0x10 00', 'read base+0x10 1')

        $result.Output[0..1] | Should -Be @('err unknown command; try help', 'ok de')
        $result.Output[-1] | Should -Match '^failed: 1 of 2 requests answered err - '
        $result.ExitCode | Should -Be 1
    }

    It 'keeps watches between runs' {
        (Invoke-Inspect $script:pipe @('watch base+0x10 4')).Output[0] | Should -Be 'ok 1'

        $result = Invoke-Inspect $script:pipe @('watches', 'changes')

        $result.Output[0..1] | Should -Be @('ok 1=0x140000010+4', 'ok')
        $result.ExitCode | Should -Be 0
    }

    It 'exits 2 when no inspector is listening' {
        $result = Invoke-Inspect "metaphor-inspector-none-$(New-Guid)" @('base') -Timeout 200

        $result.ExitCode | Should -Be 2
        $result.Output[-1] | Should -Match '^failed: no live inspector on .* - start a Debug build with: session.ps1 run -OptIn inspector'
    }

    It 'exits 2 with no request' {
        $result = Invoke-Inspect $script:pipe @()

        $result.ExitCode | Should -Be 2
        $result.Output[-1] | Should -Match '^failed: no request - '
    }

    Context '-Batch' {
        BeforeEach { Remove-Item -LiteralPath (Join-Path $TestDrive 'save') -Recurse -Force -ErrorAction Ignore }

        It 'runs every request in order and logs each with its label' {
            $result = Invoke-Batch $script:pipe @('# the title screen', 'base', '', '  read base+0x10 4  ', 'follow base+0x40 -0x10') -Options '-Label', 'title'

            $result.Log | Should -Be @("title`tbase`tok 0x140000000 0x400", "title`tread base+0x10 4`tok deadbeef", "title`tfollow base+0x40 -0x10`tok 0x1400000f0")
            $result.Output[0..2] | Should -Be @('[title] base => ok 0x140000000 0x400', '[title] read base+0x10 4 => ok deadbeef', '[title] follow base+0x40 -0x10 => ok 0x1400000f0')
            $result.Output[-1] | Should -Match '^ok: 3 requests answered; 1 of 1 sections run, logged to <save>\\inspector-log\.txt$'
            $result.ExitCode | Should -Be 0
        }

        It 'resolves relative -Batch and -SaveLog against the caller''s location, not the process folder (#523)' {
            $where = Join-Path $TestDrive 'where'
            $elsewhere = Join-Path $TestDrive 'elsewhere'
            New-Item -ItemType Directory -Force $where, $elsewhere | Out-Null
            Set-Content -LiteralPath (Join-Path $where 'rel.txt') -Value 'base'
            # The process folder is $elsewhere; the PowerShell location is $where.
            $command = "[Environment]::CurrentDirectory = '$elsewhere'; Set-Location '$where'; " +
            "& '$script' inspect -PipeName '$script:pipe' -TimeoutMilliseconds 10000 -Batch rel.txt -SaveLog relsave"
            $output = @(& pwsh -NoProfile -Command $command)
            $exit = $LASTEXITCODE

            $output[-1] | Should -Match '^ok: 1 requests answered'
            $exit | Should -Be 0
            Join-Path $where 'relsave/inspector-log.txt' | Should -Exist
        }

        It 'resolves a relative -Batch against the start folder when a profile moved the location (#523)' {
            $start = Join-Path $TestDrive 'start'
            $moved = Join-Path $TestDrive 'moved'
            New-Item -ItemType Directory -Force $start, $moved | Out-Null
            Set-Content -LiteralPath (Join-Path $start 'rel.txt') -Value 'base'
            # What a profile does before -File runs the script: moves PowerShell's location, not the process's folder.
            $launcher = Join-Path $TestDrive 'launcher.ps1'
            Set-Content -LiteralPath $launcher -Value @(
                'param($Script, $Pipe, $Where)', 'Set-Location $Where',
                '& $Script inspect -PipeName $Pipe -TimeoutMilliseconds 10000 -Batch rel.txt -SaveLog relsave')
            Push-Location $start
            try { $output = @(& pwsh -NoProfile -File $launcher $script $script:pipe $moved); $exit = $LASTEXITCODE } finally { Pop-Location }

            $output[-1] | Should -Match '^ok: 1 requests answered'
            $exit | Should -Be 0
            Join-Path $start 'relsave/inspector-log.txt' | Should -Exist
        }

        It 'logs a refused request, goes on with the batch and exits 1' {
            $result = Invoke-Batch $script:pipe @('read base+0x10 0', 'read base+0x10 1')

            $result.Log | Should -Be @("states`tread base+0x10 0`terr count must be 1 to 4096", "states`tread base+0x10 1`tok de")
            $result.Output[-1] | Should -Match '^failed: 1 of 2 requests answered err; 1 of 1 sections run, .* - read the responses above'
            $result.ExitCode | Should -Be 1
        }

        It 'reports an unknown command by its line and sends nothing' {
            $result = Invoke-Batch $script:pipe @('base', '[battle]', 'raed base+0x10 4')

            $result.Log | Should -BeNullOrEmpty
            $result.Output[-1] | Should -Match "^failed: line 3: unknown command 'raed' - fix the batch file; the inspector knows help, base, read, "
            $result.ExitCode | Should -Be 2
        }

        It 'runs one section per Enter with -WaitKey, and q stops' {
            $lines = @('[title]', 'base', '[battle]', 'read base+0x10 1', '[menu]', 'read base+0x11 1')

            $result = Invoke-Batch $script:pipe $lines -Options '-WaitKey' -Keys '', '', 'q'

            $result.Log | Should -Be @("states/title`tbase`tok 0x140000000 0x400", "states/battle`tread base+0x10 1`tok de")
            $result.Output[0..4] | Should -Be @(
                'next: states/title, 1 requests - press Enter to run it, or q and Enter to stop'
                '[states/title] base => ok 0x140000000 0x400'
                'next: states/battle, 1 requests - press Enter to run it, or q and Enter to stop'
                '[states/battle] read base+0x10 1 => ok de'
                'next: states/menu, 1 requests - press Enter to run it, or q and Enter to stop')
            $result.Output[-1] | Should -Match '^ok: 2 requests answered; 2 of 3 sections run \(stopped by you\), '
            $result.ExitCode | Should -Be 0
        }

        It 'refuses a log folder inside a git repository before connecting' {
            $file = Join-Path $TestDrive 'states.txt'
            Set-Content -LiteralPath $file -Value 'base'

            $output = @(& pwsh -NoProfile -File $script inspect -PipeName $script:pipe -Batch $file -SaveLog (Join-Path $repo 'artifacts/inspector'))

            $LASTEXITCODE | Should -Be 2
            $output[-1] | Should -Match '^failed: -SaveLog .* is inside the git repository at '
        }
    }

    It 'shows in status as listening' {
        $none = Join-Path $TestDrive 'none'
        $output = & pwsh -NoProfile -File $script status -PipeName $script:pipe -GameExe "$none/METAPHOR.exe" -LauncherExe "$none/Reloaded-II.exe" `
            -ReloadedConfig "$none/ReloadedII.json" 2>&1

        $LASTEXITCODE | Should -Be 0
        $output | Should -Contain "inspector: listening on \\.\pipe\$script:pipe"
        $output | Should -Contain 'game: not found (register it in Reloaded-II, or set METAPHOR_GAME_EXE)'
    }
}
