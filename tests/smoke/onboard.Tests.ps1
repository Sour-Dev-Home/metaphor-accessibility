# Tests for scripts/onboard.ps1, run in a stand-in clone: a real git repository holding the script and stand-ins for
# the scripts it calls (setup-wsl.ps1, claude.ps1, listen.ps1, ghidra.ps1, install-docs-tools.sh, check.sh), with
# stand-ins for dotnet, gh, winget, npm, claude, setx and notepad first on PATH, a stand-in Steam library and a stand-in
# Reloaded-II config. Real git and Git Bash run. Answers to the script's questions come through -Answers, in order.
# Run: Invoke-Pester tests/smoke (Pester 5 or later).

BeforeAll {
    $repo = (Resolve-Path "$PSScriptRoot/../..").Path
    $pwsh = Join-Path $PSHOME 'pwsh.exe'
    $gitDir = Split-Path (Get-Command git -CommandType Application | Select-Object -First 1).Source
    $sdk = (Get-Content -Raw (Join-Path $repo 'global.json') | ConvertFrom-Json).sdk.version

    # A stand-in Windows command: a .cmd file that logs its name and arguments to calls.log, then runs BODY.
    function New-StandIn([string] $Name, [string] $Body = '') {
        Set-Content -LiteralPath (Join-Path $script:bin "$Name.cmd") -Encoding ascii -Value @"
@echo off
>>"%~dp0calls.log" echo $Name %*
$Body
"@
    }

    function New-Clone {
        $script:root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $script:clone = (New-Item -ItemType Directory (Join-Path $root 'clone')).FullName
        $script:bin = (New-Item -ItemType Directory (Join-Path $root 'bin')).FullName
        git -C $clone init -q
        $scripts = New-Item -ItemType Directory (Join-Path $clone 'scripts')
        Copy-Item @('onboard.ps1', 'GameLocator.psm1', 'ReloadedDev.psm1', 'Interactive.psm1' |
                ForEach-Object { Join-Path $repo "scripts/$_" }) $scripts
        Copy-Item (Join-Path $repo 'global.json') $clone
        New-Item -ItemType Directory (Join-Path $clone '.claude') | Out-Null
        Copy-Item (Join-Path $repo '.claude/pii-patterns.example.txt') (Join-Path $clone '.claude')

        $log = '(Join-Path $PSScriptRoot ''../../bin/calls.log'')'
        Set-Content (Join-Path $scripts 'setup-wsl.ps1') @"
Add-Content $log "setup-wsl `$args"
if (`$env:STUB_WSL -eq 'create') { Write-Host 'setup-wsl: created metaphor-checks.' }
if (`$env:STUB_WSL -in 'ready', 'create') { Write-Host 'setup-wsl: metaphor-checks is ready.'; exit 0 }
Write-Host 'setup-wsl: WSL is not installed. The check suites keep running in Git Bash.'; exit 1
"@
        Set-Content (Join-Path $scripts 'claude.ps1') @"
Add-Content $log "claude.ps1 `$args"
`$state = Join-Path `$PSScriptRoot '../../claude-state.txt'
`$have = @(if (Test-Path `$state) { Get-Content `$state })
switch ("`$args") {
    'plugin marketplace list --json' { ConvertTo-Json -AsArray @(if (`$have -contains 'market') { @{ name = 'mattpocock' } }) }
    'plugin list --json' { ConvertTo-Json -AsArray @(if (`$have -contains 'plugin') { @{ id = 'mattpocock-skills@mattpocock'; enabled = `$true } }) }
    'plugin marketplace add mattpocock/skills' { Add-Content `$state 'market' }
    'plugin install mattpocock-skills@mattpocock' { Add-Content `$state 'plugin' }
}
"@
        Set-Content (Join-Path $scripts 'listen.ps1') @"
Add-Content $log "listen.ps1 `$args"
Write-Host 'metaphor.hooks: ready on abc, hooks 3/4'
exit 1
"@
        Set-Content (Join-Path $scripts 'ghidra.ps1') "Add-Content $log `"ghidra.ps1 `$args`""
        # Parameter expansion, not basename and dirname: each process Git Bash starts costs about 0.3 s, one at a time.
        $shLog = 'echo "${0##*/} $*" >> "${0%/*}/../../bin/calls.log"'
        [IO.File]::WriteAllText((Join-Path $scripts 'check.sh'), "#!/usr/bin/env bash`n$shLog`nexit `${STUB_CHECK:-0}`n")
        [IO.File]::WriteAllText((Join-Path $scripts 'install-docs-tools.sh'), (@(
                '#!/usr/bin/env bash', 'LYCHEE_VERSION=0.24.2', 'VALE_VERSION=3.25.1', $shLog
                'mkdir -p .tools/bin tools/docs/node_modules/.bin'
                'printf "#!/bin/sh\necho lychee %s\n" "$LYCHEE_VERSION" > .tools/bin/lychee'
                'printf "#!/bin/sh\necho vale version %s\n" "$VALE_VERSION" > .tools/bin/vale'
                'touch tools/docs/node_modules/.bin/markdownlint-cli2', ''
            ) -join "`n"))

        New-StandIn dotnet 'if "%1"=="--list-sdks" if not "%STUB_SDK%"=="" echo %STUB_SDK% [C:\stand-in\sdk]'
        New-StandIn gh "if `"%1`"==`"auth`" exit /b %STUB_GH_AUTH%`r`nif `"%1`"==`"api`" echo %STUB_GH_PUSH%"
        New-StandIn winget
        New-StandIn npm
        New-StandIn claude
        New-StandIn setx
        New-StandIn notepad 'if "%STUB_NOTEPAD%"=="edit" >"%~1" echo private-pattern-for-tests'

        # Steam with the game in its own library, and Reloaded-II's config naming its launcher.
        $script:steam = Join-Path $root 'Steam'
        $gameDir = New-Item -ItemType Directory -Force (Join-Path $steam 'steamapps/common/METAPHOR')
        $script:gameExe = Join-Path $gameDir 'METAPHOR.exe'
        Set-Content -LiteralPath $gameExe 'stand-in'
        $launcher = Join-Path $root 'Reloaded-II.exe'
        Set-Content -LiteralPath $launcher 'stand-in'
        $script:reloadedConfig = Join-Path $root 'ReloadedII.json'
        $apps = New-Item -ItemType Directory (Join-Path $root 'Apps')
        @{ LauncherPath = $launcher; ApplicationConfigDirectory = $apps.FullName } | ConvertTo-Json |
            Set-Content -LiteralPath $reloadedConfig

        $script:stub = @{
            STUB_SDK = $sdk; STUB_GH_AUTH = '0'; STUB_GH_PUSH = 'true'; STUB_WSL = 'ready'; STUB_NOTEPAD = 'edit'
            STUB_CHECK = '0'; METAPHOR_GAME_EXE = $null; METAPHOR_RELOADED_EXE = $null
        }
    }

    # Runs onboard.ps1 in its own process, PATH holding the stand-ins, git and Windows only. With ANSWERS it's a
    # scripted run (-Answers); with -NonInteractive, a run without a console that is given no answers.
    function Invoke-Onboard([string[]] $Answers = @(), [string[]] $Path = @($script:bin, $gitDir),
        [switch] $NonInteractive) {
        $saved = @{}
        $vars = $script:stub.Clone()
        $vars.PATH = (@($Path) + "$env:SystemRoot\System32") -join ';'
        foreach ($name in $vars.Keys) {
            $saved[$name] = [Environment]::GetEnvironmentVariable($name)
            [Environment]::SetEnvironmentVariable($name, $vars[$name])
        }
        try {
            $script = Join-Path $clone 'scripts/onboard.ps1'
            if ($NonInteractive) {
                $output = & $pwsh -NoProfile -NonInteractive -File $script -SteamRoot $steam `
                    -ReloadedConfig $reloadedConfig 2>&1
            }
            else {
                $list = ($Answers | ForEach-Object { "'" + ($_ -replace "'", "''") + "'" }) -join ','
                $output = & $pwsh -NoProfile -Command ("& '$script' -NoClaude -SteamRoot '$steam' " +
                    "-ReloadedConfig '$reloadedConfig' -Answers @($list); exit `$LASTEXITCODE") 2>&1
            }
            $code = $LASTEXITCODE
        }
        finally {
            foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
        }
        [pscustomobject]@{ ExitCode = $code; Output = ($output -join "`n") }
    }

    function Get-Calls {
        $file = Join-Path $script:bin 'calls.log'
        @(if (Test-Path $file) { Get-Content $file })
    }
    function Clear-Calls { Remove-Item (Join-Path $script:bin 'calls.log') -ErrorAction SilentlyContinue }
}

Describe 'onboard.ps1' {
    BeforeEach { New-Clone }

    It 'sets up a clean clone, says what it did, and a second run changes nothing' {
        # Answers: Enter after editing the patterns, use the game found, no test deploy, no Ghidra project.
        $first = Invoke-Onboard @('', 'y', 'y', 'n', 'n')

        $first.ExitCode | Should -Be 0 -Because $first.Output
        git -C $clone config --local --get core.hooksPath | Should -Be 'scripts/githooks'
        Get-Content (Join-Path $clone '.claude/pii-patterns.txt') | Should -Be 'private-pattern-for-tests'
        $calls = Get-Calls
        $calls | Should -Contain 'claude.ps1 plugin marketplace add mattpocock/skills'
        $calls | Should -Contain 'claude.ps1 plugin install mattpocock-skills@mattpocock'
        $calls | Should -Contain 'install-docs-tools.sh '
        $calls | Should -Contain 'setup-wsl '
        $calls -match "^setx METAPHOR_GAME_EXE `"?$([regex]::Escape($gameExe))`"?$" | Should -Not -BeNullOrEmpty
        $calls | Should -Contain 'check.sh fast'
        $calls | Should -Not -Contain 'winget'
        $calls | Where-Object { $_ -like 'listen.ps1*' -or $_ -like 'ghidra.ps1*' } | Should -BeNullOrEmpty
        foreach ($line in 'skills: installed', 'docs tools: installed', 'WSL: the metaphor-checks distro is ready',
            'push access: can push', 'game path: METAPHOR_GAME_EXE is set', 'Reloaded-II: found', 'check.sh fast: passed') {
            $first.Output | Should -Match ([regex]::Escape($line))
        }
        $first.Output | Should -Match 'Changed:'
        $first.Output | Should -Not -Match 'private-pattern-for-tests'
        $first.Output | Should -Match 'docs/agents/workflow.md'

        Clear-Calls
        $script:stub.METAPHOR_GAME_EXE = $gameExe # as setx left it for new processes
        $second = Invoke-Onboard @('n', 'n')

        $second.ExitCode | Should -Be 0 -Because $second.Output
        $second.Output | Should -Match 'Nothing changed'
        $calls = Get-Calls
        $calls | Where-Object { $_ -match '^(notepad|setx|install-docs-tools|winget)|marketplace add|plugin install' } |
            Should -BeNullOrEmpty
    }

    It 'lists every item of the ready summary' {
        $result = Invoke-Onboard @('', 'y', 'y', 'n', 'n')

        foreach ($item in '.NET SDK', 'Git', 'PowerShell 7', 'GitHub CLI', 'GitHub login', 'push access', 'git hooks',
            'private patterns', 'skills', 'docs tools', 'WSL', 'game') {
            $result.Output | Should -Match ([regex]::Escape($item))
        }
    }

    It 'stops with what to do when the SDK is missing or another version and the install is declined' {
        $script:stub.STUB_SDK = '9.0.100'
        $result = Invoke-Onboard @('n')

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match ([regex]::Escape($sdk))
        $result.Output | Should -Match 'dotnet.microsoft.com'
        Get-Calls | Where-Object { $_ -like 'winget*' } | Should -BeNullOrEmpty
        git -C $clone config --local --get core.hooksPath | Should -BeNullOrEmpty
    }

    It 'installs the pinned SDK through winget after a yes, then asks for a new window' {
        $script:stub.STUB_SDK = ''
        $result = Invoke-Onboard @('y')

        $result.ExitCode | Should -Be 1
        $major = $sdk.Split('.')[0]
        Get-Calls | Should -Contain ("winget install --id Microsoft.DotNet.SDK.$major --exact --version $sdk " +
            '--source winget --accept-package-agreements --accept-source-agreements')
        $result.Output | Should -Match 'admin'
        $result.Output | Should -Match 'accept its licence agreement\?'
        $result.Output | Should -Match ([regex]::Escape("winget show --id Microsoft.DotNet.SDK.$major --exact --version $sdk"))
        $result.Output | Should -Match 'new PowerShell window'
    }

    It 'stops with what to do when Git is missing and the install is declined' {
        $result = Invoke-Onboard -Answers @('n') -Path @($script:bin)

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'git-scm.com'
    }

    It 'stops while the private patterns file still holds the example lines, without printing it' {
        $script:stub.STUB_NOTEPAD = 'none'
        $result = Invoke-Onboard @('')

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'pii-patterns.txt'
        $result.Output | Should -Not -Match 'your-real-name'
        Get-Calls | Should -Not -Contain 'check.sh fast'
    }

    It 'asks before saving the game it found, and saves nothing after a no' {
        $result = Invoke-Onboard @('', 'y', 'n')

        $result.ExitCode | Should -Be 0 -Because $result.Output
        $result.Output | Should -Match ([regex]::Escape("Found the game: $gameExe"))
        $result.Output | Should -Match 'Use it, saving its path in .*METAPHOR_GAME_EXE environment variable\? \[Y/n\]'
        Get-Calls | Where-Object { $_ -match '^(setx|listen|ghidra)' } | Should -BeNullOrEmpty
    }

    It 'asks before installing the skills, and installs nothing after a no' {
        $result = Invoke-Onboard @('', 'n', 'y', 'n', 'n')

        $result.ExitCode | Should -Be 0 -Because $result.Output
        $result.Output | Should -Match 'Install Matt Pocock''s skills'
        $result.Output | Should -Match 'skills: not installed; run .*plugin install mattpocock-skills@mattpocock'
        Get-Calls | Where-Object { $_ -match 'marketplace add|plugin install' } | Should -BeNullOrEmpty
    }

    It 'offers the game Reloaded-II has registered' {
        Remove-Item $gameExe
        $registered = Join-Path $root 'Registered/METAPHOR.exe'
        New-Item -ItemType File -Force $registered | Out-Null
        $app = New-Item -ItemType Directory (Join-Path $root 'Apps/metaphor.exe')
        @{ AppId = 'metaphor.exe'; AppLocation = $registered } | ConvertTo-Json |
            Set-Content (Join-Path $app 'AppConfig.json')
        $result = Invoke-Onboard @('', 'y', 'y', 'n', 'n')

        $result.Output | Should -Match ([regex]::Escape("Found the game: $registered"))
        Get-Calls | Where-Object { $_ -like "setx METAPHOR_GAME_EXE*$registered*" } | Should -Not -BeNullOrEmpty
    }

    It 'records a newly created WSL distro among the changes' {
        $script:stub.STUB_WSL = 'create'
        $result = Invoke-Onboard @('', 'y', 'y', 'n', 'n')

        $result.Output | Should -Match 'Changed:.*created the metaphor-checks WSL distro'
    }

    It 'answers every question no without a console, opens neither Notepad nor Claude, and says so' {
        $result = Invoke-Onboard -NonInteractive

        $result.ExitCode | Should -Be 1 -Because $result.Output
        $result.Output | Should -Match 'no console'
        $result.Output | Should -Match 'private patterns: replace the example lines'
        $result.Output | Should -Not -Match 'NonInteractive mode|Read-Host'
        $calls = Get-Calls
        $calls | Where-Object { $_ -match '^(notepad|setx|listen|ghidra|winget)' -or $_ -eq 'claude.ps1 ' } |
            Should -BeNullOrEmpty
        $calls | Should -Contain 'check.sh fast'
    }

    It 'asks before the test deploy, then reports the hooks' {
        $result = Invoke-Onboard @('', 'y', 'y', 'y', 'n')

        Get-Calls | Should -Contain "listen.ps1 -GameExe $gameExe -Seconds 30 -Close"
        $result.Output | Should -Match 'hooks 3/4'
    }

    It 'offers no test deploy without Reloaded-II, and asks for the game when Steam has none' {
        Remove-Item $reloadedConfig
        Remove-Item $gameExe
        # Answers: Enter after the patterns, then Enter to skip the game path. Ghidra isn't offered without the game.
        $result = Invoke-Onboard @('', 'y', '')

        $result.ExitCode | Should -Be 0 -Because $result.Output
        $result.Output | Should -Match 'METAPHOR.exe'
        Get-Calls | Where-Object { $_ -match '^(listen|ghidra|setx)' } | Should -BeNullOrEmpty
    }

    It 'says why WSL matters when it can''t be set up, and carries on' {
        $script:stub.STUB_WSL = 'missing'
        $result = Invoke-Onboard @('', 'y', 'y', 'n', 'n')

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'WSL is not installed'
        $result.Output | Should -Match '4 minutes'
        Get-Calls | Should -Contain 'check.sh fast'
    }

    It 'says to ask the maintainer when the account can''t push, and carries on' {
        $script:stub.STUB_GH_PUSH = 'false'
        $result = Invoke-Onboard @('', 'y', 'y', 'n', 'n')

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'ask the maintainer for access'
        Get-Calls | Should -Contain 'check.sh fast'
    }

    It 'offers gh auth login when not logged in, and skips the push check after a no' {
        $script:stub.STUB_GH_AUTH = '1'
        $result = Invoke-Onboard @('', 'y', 'n', 'y', 'n', 'n')

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'gh auth login'
        Get-Calls | Where-Object { $_ -like 'gh api*' } | Should -BeNullOrEmpty
    }
}
