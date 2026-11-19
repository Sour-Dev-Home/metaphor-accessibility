# Tests for scripts/inspect.ps1, run against the stand-in game acting as the Debug mod's live inspector
# (tests/Metaphor.StandInGame/InspectorStandIn.cs): the same commands over a pipe, from a fake 0x400-byte module at
# 0x140000000 holding deadbeef at +0x10 and a pointer to +0x100 at +0x40.

BeforeAll {
    $repo = (Resolve-Path "$PSScriptRoot/../..").Path
    $script = Join-Path $repo 'scripts/inspect.ps1'
    $standIn = Join-Path $repo 'tests/Metaphor.StandInGame/bin/Release/net9.0'
    if (-not $env:METAPHOR_STANDIN_BUILT) { # scripts/run-pester.sh builds it once for every process
        dotnet build (Join-Path $repo 'tests/Metaphor.StandInGame') -c Release -v q --nologo | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'the stand-in game failed to build' }
    }

    function Invoke-Inspect([string] $Pipe, [string[]] $Requests) {
        $output = & pwsh -NoProfile -File $script -PipeName $Pipe -TimeoutMilliseconds 10000 @Requests
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = @($output) }
    }
}

Describe 'inspect.ps1' {
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

    It 'prints one response per request, in order' {
        $result = Invoke-Inspect $script:pipe @('base', 'read base+0x10 4', 'follow base+0x40 -0x10')

        $result.Output | Should -Be @('ok 0x140000000 0x400', 'ok deadbeef', 'ok 0x1400000f0')
        $result.ExitCode | Should -Be 0
    }

    It 'exits 1 when the inspector refuses a request, and still answers the rest' {
        $result = Invoke-Inspect $script:pipe @('write base+0x10 00', 'read base+0x10 1')

        $result.Output | Should -Be @('err unknown command; try help', 'ok de')
        $result.ExitCode | Should -Be 1
    }

    It 'keeps watches between runs' {
        (Invoke-Inspect $script:pipe @('watch base+0x10 4')).Output | Should -Be @('ok 1')

        $result = Invoke-Inspect $script:pipe @('watches', 'changes')

        $result.Output | Should -Be @('ok 1=0x140000010+4', 'ok')
        $result.ExitCode | Should -Be 0
    }

    It 'exits 2 when no inspector is listening' {
        $result = & pwsh -NoProfile -File $script -PipeName "metaphor-inspector-none-$(New-Guid)" -TimeoutMilliseconds 200 'base'

        $LASTEXITCODE | Should -Be 2
        $result | Should -Match '^err no live inspector'
    }
}
