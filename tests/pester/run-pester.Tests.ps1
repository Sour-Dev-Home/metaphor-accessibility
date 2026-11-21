# Tests for scripts/check/run-pester.sh with the real pwsh and Pester, for what a stand-in pwsh can't show
# (tests/bash/test-run-pester.sh has the rest).

BeforeAll {
    $script:repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
    # Git Bash beside git on Windows, as install.Tests finds it, so a plain pwsh never picks WSL's bash.exe from System32.
    $script:bash = 'bash'
    $git = Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($IsWindows -and $git) {
        $dir = Split-Path $git.Source
        $found = (Join-Path $dir 'bash.exe'), (Join-Path $dir '..\bin\bash.exe'), (Join-Path $dir '..\..\bin\bash.exe') |
            Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if ($found) { $script:bash = [IO.Path]::GetFullPath($found) }
    }
    Add-Type -Namespace RunPesterTests -Name Console -MemberDefinition @'
[DllImport("kernel32.dll")] public static extern uint GetConsoleOutputCP();
[DllImport("kernel32.dll")] public static extern bool SetConsoleOutputCP(uint codePage);
'@
}

Describe 'run-pester.sh' {
    # #629: the dotnet CLI switches the console's code page to UTF-8 while it runs, and every process in a window shares
    # one console, so a test running dotnet switched the code page under the Pester processes beside it. A Pester file
    # here switches its console's code page and leaves it switched; run through run-pester.sh, that must not reach the
    # console run-pester.sh runs in.
    It 'runs each Pester process in a console of its own' -Skip:(-not $IsWindows) {
        $before = [RunPesterTests.Console]::GetConsoleOutputCP()
        if ($before -eq 0) { Set-ItResult -Skipped -Because 'this process has no console' }
        $other = if ($before -eq 65001) { 437 } else { 65001 }
        $file = Join-Path $TestDrive 'switch.Tests.ps1'
        Set-Content -LiteralPath $file @"
Describe 'switch' {
    It 'switches the code page' {
        Add-Type -Namespace Switch -Name Console -MemberDefinition '[DllImport("kernel32.dll")] public static extern bool SetConsoleOutputCP(uint codePage);'
        [Switch.Console]::SetConsoleOutputCP($other) | Should -BeTrue
    }
}
"@
        # No dotnet on PATH, so the run builds no stand-in game, and no live log: this run's lines stay out of the
        # clone's.
        $saved = @{ PATH = $env:PATH; METAPHOR_LIVE_LOG = $env:METAPHOR_LIVE_LOG }
        try {
            $gitRoot = Split-Path (Split-Path $bash)
            $env:PATH = ((Join-Path $gitRoot 'usr\bin'), (Join-Path $gitRoot 'bin'), $PSHOME) -join ';'
            $env:METAPHOR_LIVE_LOG = $null
            $output = & $bash (Join-Path $repo 'scripts/check/run-pester.sh') $file 2>&1
            $code = $LASTEXITCODE
            $after = [RunPesterTests.Console]::GetConsoleOutputCP()
        }
        finally {
            foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
            [RunPesterTests.Console]::SetConsoleOutputCP($before) | Out-Null
        }

        $code | Should -Be 0 -Because ($output -join "`n")
        $after | Should -Be $before
    }
}
