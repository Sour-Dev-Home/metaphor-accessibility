# Tests for scripts/setup/onboard.ps1, run in a stand-in clone: a real git repository holding the script and stand-ins for
# the scripts it calls (setup-wsl.ps1, claude.ps1, listen.ps1, ghidra.ps1, install-docs-tools.sh, check.sh), with
# stand-ins for dotnet, gh, winget, npm, claude, setx and notepad first on PATH, a stand-in Steam library and a stand-in
# Reloaded-II config, and a stand-in System32 for the Visual C++ runtime. Real git and Git Bash run. Answers to the
# script's questions come through -Answers, in order.
# Git Bash starts one process at a time at about 0.3 s each (#348), so the clone starts with the docs tools' stand-ins
# installed, and only the test of their install pays for it.
# Run: Invoke-Pester tests/pester (Pester 5 or later).

BeforeAll {
    $repo = (Resolve-Path "$PSScriptRoot/../..").Path
    $pwsh = Join-Path $PSHOME 'pwsh.exe'
    $windowsPowerShell = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
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

    # A literal for a -Command string, escaped as PowerShell itself does (typographic single quotes included).
    function ConvertTo-Literal([string] $Text) {
        "'" + [Management.Automation.Language.CodeGeneration]::EscapeSingleQuotedStringContent($Text) + "'"
    }

    function New-Clone([string] $Name = (New-Guid)) {
        $script:root = (New-Item -ItemType Directory (Join-Path $TestDrive $Name)).FullName
        $script:clone = (New-Item -ItemType Directory (Join-Path $root 'clone')).FullName
        $script:bin = (New-Item -ItemType Directory (Join-Path $root 'bin')).FullName
        git -C $clone init -q
        $scripts = New-Item -ItemType Directory (Join-Path $clone 'scripts')
        $setup, $dev, $player = 'setup', 'dev', 'player' | ForEach-Object { New-Item -ItemType Directory (Join-Path $scripts $_) }
        foreach ($file in 'setup/onboard.ps1', 'player/GameLocator.psm1', 'dev/ReloadedDev.psm1', 'player/ReloadedRead.psm1',
            'player/Interactive.psm1') {
            Copy-Item (Join-Path $repo "scripts/$file") (Join-Path $scripts $file)
        }
        Copy-Item (Join-Path $repo 'global.json') $clone
        New-Item -ItemType Directory (Join-Path $clone '.claude') | Out-Null
        Copy-Item (Join-Path $repo '.claude/pii-patterns.example.txt') (Join-Path $clone '.claude')

        $log = '(Join-Path $PSScriptRoot ''../../../bin/calls.log'')'
        Set-Content (Join-Path $setup 'setup-wsl.ps1') @"
Add-Content $log "setup-wsl `$args"
if (`$env:STUB_WSL -eq 'create') { Write-Host 'setup-wsl: created metaphor-checks.' }
if (`$env:STUB_WSL -in 'ready', 'create') { Write-Host 'setup-wsl: metaphor-checks is ready.'; exit 0 }
if (`$env:STUB_WSL -eq 'network') {
    Write-Host 'setup-wsl: downloading Debian failed twice on a network error. Check the network and run .\scripts\setup\setup-wsl.ps1 again. The check suites keep running in Git Bash.'; exit 1
}
if (`$env:STUB_WSL -eq 'restart') {
    Write-Host 'setup-wsl: WSL is waiting for Windows to restart before it can create metaphor-checks. Restart Windows, then run .\scripts\setup\setup-wsl.ps1. The check suites keep running in Git Bash.'; exit 2
}
if (`$env:STUB_WSL -eq 'novirt') {
    Write-Host 'setup-wsl: WSL 2 can''t start because virtualization is off. Turn on virtualization in the firmware settings; on a Hyper-V VM, shut it down and run Set-VMProcessor -VMName <vm> -ExposeVirtualizationExtensions `$true on the host. Then run .\scripts\setup\setup-wsl.ps1. The check suites keep running in Git Bash.'; exit 2
}
Write-Host 'setup-wsl: WSL is not installed. The check suites keep running in Git Bash.'; exit 2
"@
        Set-Content (Join-Path $setup 'claude.ps1') @"
Add-Content $log "claude.ps1 `$args"
`$state = Join-Path `$PSScriptRoot '../../../claude-state.txt'
`$have = @(if (Test-Path `$state) { Get-Content `$state })
switch ("`$args") {
    'plugin marketplace list --json' { ConvertTo-Json -AsArray @(if (`$have -contains 'market') { @{ name = 'mattpocock' } }) }
    'plugin list --json' { ConvertTo-Json -AsArray @(if (`$have -contains 'plugin') { @{ id = 'mattpocock-skills@mattpocock'; enabled = `$true } }) }
    'plugin marketplace add mattpocock/skills' { Add-Content `$state 'market' }
    'plugin install mattpocock-skills@mattpocock' { Add-Content `$state 'plugin' }
}
"@
        Set-Content (Join-Path $dev 'listen.ps1') @"
Add-Content $log "listen.ps1 `$args"
Write-Host 'metaphor.hooks: ready on abc, hooks 3/4'
exit 1
"@
        Set-Content (Join-Path $dev 'ghidra.ps1') "Add-Content $log `"ghidra.ps1 `$args`""
        # Parameter expansion, not basename and dirname: each process Git Bash starts costs about 0.3 s, one at a time.
        $shLog = 'echo "${0##*/} $*" >> "${0%/*}/../../bin/calls.log"'
        [IO.File]::WriteAllText((Join-Path $scripts 'check.sh'), "#!/usr/bin/env bash`n$shLog`nexit `${STUB_CHECK:-0}`n")
        [IO.File]::WriteAllText((Join-Path $setup 'install-docs-tools.sh'), (@(
                '#!/usr/bin/env bash', 'LYCHEE_VERSION=0.24.2', 'VALE_VERSION=3.25.1'
                '[ "$1" != --path ] || { echo .tools/bin:.tools/bin:tools/docs/node_modules/.bin; exit 0; }'
                $shLog.Replace('/../../bin/', '/../../../bin/')
                # STUB_DOCS_INSTALL: silent, the installer before #385 (exit 0, nothing fixed); fail, after it.
                '[ "${STUB_DOCS_INSTALL:-}" != silent ] || exit 0'
                '[ "${STUB_DOCS_INSTALL:-}" != fail ] ||'
                '  { echo "install-docs-tools: lychee doesn''t run" >&2; exit 1; }'
                'mkdir -p .tools/bin tools/docs/node_modules/.bin'
                'printf "#!/bin/sh\necho lychee %s\n" "$LYCHEE_VERSION" > .tools/bin/lychee'
                'printf "#!/bin/sh\necho vale version %s\n" "$VALE_VERSION" > .tools/bin/vale'
                'touch tools/docs/node_modules/.bin/markdownlint-cli2', ''
            ) -join "`n"))
        # The docs tools as that installer leaves them.
        New-Item -ItemType Directory (Join-Path $clone '.tools/bin'), (Join-Path $clone 'tools/docs/node_modules/.bin') |
            Out-Null
        [IO.File]::WriteAllText((Join-Path $clone '.tools/bin/lychee'), "#!/bin/sh`necho lychee 0.24.2`n")
        [IO.File]::WriteAllText((Join-Path $clone '.tools/bin/vale'), "#!/bin/sh`necho vale version 3.25.1`n")
        New-Item -ItemType File (Join-Path $clone 'tools/docs/node_modules/.bin/markdownlint-cli2') | Out-Null
        # A System32 holding the Visual C++ runtime that lychee's Windows build loads (#385).
        $script:system32 = (New-Item -ItemType Directory (Join-Path $root 'System32')).FullName
        $script:vcRuntime = (New-Item -ItemType File (Join-Path $system32 'vcruntime140.dll')).FullName

        New-StandIn dotnet 'if "%1"=="--list-sdks" if not "%STUB_SDK%"=="" echo %STUB_SDK% [C:\stand-in\sdk]'
        New-StandIn gh "if `"%1`"==`"auth`" exit /b %STUB_GH_AUTH%`r`nif `"%1`"==`"api`" echo %STUB_GH_PUSH%"
        # STUB_WINGET_CREATES: a file the install puts in place.
        New-StandIn winget ("if not `"%STUB_WINGET_CREATES%`"==`"`" type nul >`"%STUB_WINGET_CREATES%`"`r`n" +
            'exit /b %STUB_WINGET%')
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

        # An empty Program Files, so no PowerShell 7 is at its default path unless a test puts one there.
        $script:programFiles = (New-Item -ItemType Directory (Join-Path $root 'ProgramFiles')).FullName
        $script:stub = @{
            STUB_SDK = $sdk; STUB_GH_AUTH = '0'; STUB_GH_PUSH = 'true'; STUB_WSL = 'ready'; STUB_NOTEPAD = 'edit'
            STUB_CHECK = '0'; STUB_WINGET = '0'; STUB_WINGET_CREATES = $null; STUB_DOCS_INSTALL = $null
            METAPHOR_GAME_EXE = $null; METAPHOR_RELOADED_EXE = $null
            STUB_CALLS = (Join-Path $bin 'calls.log'); STUB_REAL_PWSH = $pwsh
        }
    }

    # A stand-in pwsh.exe in FOLDER: it logs its arguments to calls.log, then hands them to the real pwsh and returns
    # its exit code. Compiled once with the .NET Framework's csc, which every Windows has.
    function Install-PwshStandIn([string] $Folder) {
        $exe = Join-Path $TestDrive 'pwsh-stand-in/pwsh.exe'
        if (-not (Test-Path $exe)) {
            $source = Join-Path (New-Item -ItemType Directory -Force (Split-Path $exe)) 'Program.cs'
            Set-Content -LiteralPath $source -Value @'
using System; using System.Diagnostics; using System.IO;
static class Program {
    static int Main() {
        string line = Environment.CommandLine;
        int start = line.StartsWith("\"") ? line.IndexOf('"', 1) + 1 : line.IndexOf(' ');
        string rest = start <= 0 || start >= line.Length ? "" : line.Substring(start).TrimStart();
        File.AppendAllText(Environment.GetEnvironmentVariable("STUB_CALLS"), "pwsh " + rest + Environment.NewLine);
        var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("STUB_REAL_PWSH"), rest) { UseShellExecute = false };
        using (var process = Process.Start(info)) { process.WaitForExit(); return process.ExitCode; }
    }
}
'@
            $csc = Join-Path $env:SystemRoot 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
            $built = & $csc -nologo "-out:$exe" $source 2>&1
            if ($LASTEXITCODE -ne 0) { throw "csc failed: $built" }
        }
        Copy-Item $exe (New-Item -ItemType Directory -Force $Folder)
    }

    # Runs onboard.ps1 in its own process, PATH holding the stand-ins, git and Windows only. With ANSWERS it's a
    # scripted run (-Answers); with -NonInteractive, a run without a console that is given no answers. SHELL runs it:
    # PowerShell 7 unless a test passes Windows PowerShell's.
    function Invoke-Onboard([string[]] $Answers = @(), [string[]] $Path = @($script:bin, $gitDir),
        [switch] $NonInteractive, [string] $Shell = $pwsh) {
        $saved = @{}
        $vars = $script:stub.Clone()
        $vars.PATH = (@($Path) + "$env:SystemRoot\System32") -join ';'
        foreach ($name in $vars.Keys) {
            $saved[$name] = [Environment]::GetEnvironmentVariable($name)
            [Environment]::SetEnvironmentVariable($name, $vars[$name])
        }
        try {
            $script = Join-Path $clone 'scripts/setup/onboard.ps1'
            if ($NonInteractive) {
                $output = & $Shell -NoProfile -NonInteractive -File $script -SteamRoot $steam `
                    -ReloadedConfig $reloadedConfig -ProgramFiles $programFiles -SystemDirectory $system32 2>&1
            }
            else {
                $list = ($Answers | ForEach-Object { ConvertTo-Literal $_ }) -join ','
                $output = & $Shell -NoProfile -Command ("& $(ConvertTo-Literal $script) -NoClaude " +
                    "-SteamRoot $(ConvertTo-Literal $steam) -ReloadedConfig $(ConvertTo-Literal $reloadedConfig) " +
                    "-ProgramFiles $(ConvertTo-Literal $programFiles) " +
                    "-SystemDirectory $(ConvertTo-Literal $system32) -Answers @($list); exit `$LASTEXITCODE") 2>&1
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
    # The command the one relaunch through the stand-in pwsh ran, decoded from its -EncodedCommand.
    function Get-RelaunchCommand {
        $launches = @(Get-Calls | Where-Object { $_ -like 'pwsh *' })
        $launches | Should -HaveCount 1
        $launches[0] -match '^pwsh -NoProfile -OutputFormat Text -EncodedCommand (\S+)$' |
            Should -BeTrue -Because $launches[0]
        [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String($Matches[1]))
    }
    function Clear-Calls { Remove-Item (Join-Path $script:bin 'calls.log') -ErrorAction SilentlyContinue }
}

Describe 'onboard.ps1' {
    BeforeEach { New-Clone }

    It 'sets up a clean clone, says what it did and lists every item, and a second run changes nothing' {
        Remove-Item -Recurse (Join-Path $clone '.tools')
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
        foreach ($item in '.NET SDK', 'Git', 'PowerShell 7', 'GitHub CLI', 'Node.js', 'Visual C++ runtime',
            'GitHub login', 'push access', 'git hooks', 'private patterns', 'skills', 'docs tools', 'WSL', 'game') {
            $first.Output | Should -Match ("(?m)^  \S $([regex]::Escape($item))")
        }

        Clear-Calls
        $script:stub.METAPHOR_GAME_EXE = $gameExe # as setx left it for new processes
        $second = Invoke-Onboard @('n', 'n')

        $second.ExitCode | Should -Be 0 -Because $second.Output
        $second.Output | Should -Match 'Nothing changed'
        $calls = Get-Calls
        $calls | Where-Object { $_ -match '^(notepad|setx|install-docs-tools|winget)|marketplace add|plugin install' } |
            Should -BeNullOrEmpty
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

    It 'finds Git Bash when the git first on PATH is the one in libexec\git-core, as inside a git hook' {
        $execPath = git --exec-path
        Test-Path -LiteralPath (Join-Path $execPath 'git.exe') | Should -BeTrue -Because "$execPath holds git.exe"
        $result = Invoke-Onboard @('', 'y', 'n') -Path @($script:bin, $execPath)

        $result.ExitCode | Should -Be 0 -Because $result.Output
        $result.Output | Should -Not -Match 'Git Bash isn''t installed'
    }

    It 'never takes a bash.exe from outside the Git install when Git\bin\bash.exe is missing' {
        $cmd = New-Item -ItemType Directory (Join-Path $root 'drive/outer/Git/cmd')
        New-Item -ItemType File (Join-Path $cmd 'git.cmd') | Out-Null
        foreach ($decoy in 'drive/outer/bin', 'drive/bin') { # one and two levels above the Git root
            New-Item -ItemType File (Join-Path (New-Item -ItemType Directory -Force (Join-Path $root $decoy)) 'bash.exe') |
                Out-Null
        }
        $result = Invoke-Onboard -Answers @('n') -Path @($script:bin, $cmd.FullName)

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'Git Bash isn''t installed'
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
        $result.Output | Should -Match 'run wsl --install in an admin PowerShell'
        Get-Calls | Should -Contain 'check.sh fast'
    }

    It 'gives network advice, not install advice, when the WSL download failed on the network' {
        $script:stub.STUB_WSL = 'network'
        $result = Invoke-Onboard @('', 'y', 'y', 'n', 'n')

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'WSL: downloading Debian failed twice on a network error\.'
        $result.Output | Should -Match 'Check the network and run \.\\scripts\\setup\\setup-wsl\.ps1 again'
        $result.Output | Should -Not -Match 'wsl --install'
    }

    It 'carries setup-wsl''s restart fix into the summary, without install advice' {
        $script:stub.STUB_WSL = 'restart'
        $result = Invoke-Onboard @('', 'y', 'y', 'n', 'n')

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match ('WSL: WSL is waiting for Windows to restart before it can create ' +
            'metaphor-checks\. Restart Windows, then run \.\\scripts\\setup\\setup-wsl\.ps1; until then')
        $result.Output | Should -Not -Match 'wsl --install|Fix that'
    }

    It 'carries setup-wsl''s virtualization fix into the summary, without install advice' {
        $script:stub.STUB_WSL = 'novirt'
        $result = Invoke-Onboard @('', 'y', 'y', 'n', 'n')

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match ('WSL: WSL 2 can''t start because virtualization is off\. Turn on ' +
            'virtualization in the firmware settings; on a Hyper-V VM, .*ExposeVirtualizationExtensions \$true ' +
            'on the host\. Then run \.\\scripts\\setup\\setup-wsl\.ps1; until then')
        $result.Output | Should -Not -Match 'wsl --install|Fix that'
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

    It 'treats winget''s already-installed result as installed, and says why it still doesn''t find the tool' {
        $script:stub.STUB_SDK = ''
        $script:stub.STUB_WINGET = '-1978335189' # 0x8A15002B, no applicable update
        $result = Invoke-Onboard @('y')

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'winget says \.NET SDK is already installed'
        $result.Output | Should -Match ([regex]::Escape("$sdk in dotnet --list-sdks"))
        $result.Output | Should -Not -Match 'Installed: '
    }

    It 'offers a missing Node.js as a pinned winget install after a yes that names the licence' {
        Remove-Item (Join-Path $bin 'npm.cmd')
        $result = Invoke-Onboard @('y')

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'Install Node\.js \S+ and accept its licence agreement\?'
        $result.Output | Should -Match 'winget show --id OpenJS\.NodeJS\.LTS --exact --version \d+\.\d+\.\d+'
        @(Get-Calls) -match ('^winget install --id OpenJS\.NodeJS\.LTS --exact --version \d+\.\d+\.\d+ ' +
            '--source winget --accept-package-agreements --accept-source-agreements$') | Should -HaveCount 1
        $result.Output | Should -Match 'new PowerShell window'
    }

    It 'carries on without Node.js after a no, and lists it as missing' {
        Remove-Item (Join-Path $bin 'npm.cmd')
        $result = Invoke-Onboard @('n', '', 'y', 'y', 'n', 'n')

        $result.ExitCode | Should -Be 0 -Because $result.Output
        Get-Calls | Where-Object { $_ -like 'winget*' } | Should -BeNullOrEmpty
        $result.Output | Should -Match 'Node\.js: not installed, and the docs tools need it: .*nodejs\.org'
        Get-Calls | Should -Contain 'check.sh fast'
    }

    Context 'the Visual C++ runtime lychee needs (#385)' {
        BeforeEach {
            # A stand-in lychee that runs only while the runtime is there, as the MSVC build does: without it, Git Bash
            # exits 127 and prints nothing.
            Remove-Item -LiteralPath $vcRuntime
            [IO.File]::WriteAllText((Join-Path $clone '.tools/bin/lychee'),
                "#!/bin/sh`n[ -f '$vcRuntime' ] || exit 127`necho lychee 0.24.2`n")
        }

        It 'offers the runtime as a pinned install with an admin prompt, and after a yes the docs tools run' {
            $script:stub.STUB_WINGET_CREATES = $vcRuntime
            # Answers: install the runtime, Enter after the patterns, skills, the game found, no deploy, no Ghidra.
            $result = Invoke-Onboard @('y', '', 'y', 'y', 'n', 'n')

            $result.ExitCode | Should -Be 0 -Because $result.Output
            $result.Output |
                Should -Match 'Install Visual C\+\+ runtime 14\.52\.37029\.0 and accept its licence agreement\?'
            $result.Output | Should -Match 'needs a Windows admin prompt'
            Get-Calls | Should -Contain ('winget install --id Microsoft.VCRedist.2015+.x64 --exact --version ' +
                '14.52.37029.0 --source winget --accept-package-agreements --accept-source-agreements')
            $result.Output | Should -Not -Match 'new PowerShell window'
            $result.Output | Should -Match '(?m)^  \S Visual C\+\+ runtime: installed'
            $result.Output | Should -Match '(?m)^  \S docs tools: installed'
            $result.Output | Should -Match 'Changed: .*installed the Visual C\+\+ runtime'
            Get-Calls | Should -Not -Contain 'install-docs-tools.sh '
        }

        It 'installs nothing after a no, and the summary names lychee and the runtime it needs' {
            $script:stub.STUB_DOCS_INSTALL = 'fail'
            $result = Invoke-Onboard @('n', '', 'y', 'y', 'n', 'n')

            $result.ExitCode | Should -Be 0 -Because $result.Output
            Get-Calls | Where-Object { $_ -like 'winget*' } | Should -BeNullOrEmpty
            Get-Calls | Should -Contain 'install-docs-tools.sh '
            $result.Output | Should -Match "install-docs-tools: lychee doesn't run"
            $result.Output | Should -Match ('(?m)^  \S docs tools: lychee did not run; it needs the Visual C\+\+ ' +
                'Redistributable \(x64\)')
            $result.Output | Should -Match '(?m)^  \S Visual C\+\+ runtime: not installed, and lychee'
        }
    }

    It 'names the docs tool that does not run, even when the installer says nothing' {
        [IO.File]::WriteAllText((Join-Path $clone '.tools/bin/lychee'), "#!/bin/sh`nexit 127`n")
        $script:stub.STUB_DOCS_INSTALL = 'silent'
        $result = Invoke-Onboard @('', 'y', 'y', 'n', 'n')

        $result.ExitCode | Should -Be 0 -Because $result.Output
        $result.Output | Should -Match "(?m)^  \S docs tools: lychee did not run; see install-docs-tools.sh's message"
        $result.Output | Should -Not -Match 'Vale did not run|Vale, '
    }

    It 'shows the skills as optional without Claude Code' {
        Remove-Item (Join-Path $bin 'claude.cmd')
        $result = Invoke-Onboard @('', 'y', 'n', 'n')

        $result.ExitCode | Should -Be 0 -Because $result.Output
        $result.Output | Should -Match '(?m)^  - skills: optional: install Claude Code'
    }

    Context 'run from Windows PowerShell' {
        It 'relaunches itself under the PowerShell 7 on PATH, with the same arguments, in the same window' {
            Install-PwshStandIn $bin
            $script:stub.STUB_SDK = ''
            $result = Invoke-Onboard @('n') -Shell $windowsPowerShell

            $result.ExitCode | Should -Be 1 -Because $result.Output
            $result.Output | Should -Match 'PowerShell 7 is installed'
            # The relaunched run: PowerShell 7 found, and the answer and paths passed on.
            $result.Output | Should -Match 'PowerShell 7: found'
            $result.Output | Should -Match 'dotnet\.microsoft\.com'
            $command = Get-RelaunchCommand
            $command | Should -Match ([regex]::Escape("-SteamRoot $(ConvertTo-Literal $steam)"))
            $command | Should -Match ([regex]::Escape("-Answers @('n')"))
            Get-Calls | Where-Object { $_ -like 'winget*' } | Should -BeNullOrEmpty
        }

        It 'passes on paths with typographic quotes and spaces as data, never as code' {
            $quote = [char]0x2019 # right single quotation mark, which PowerShell also ends a '...' string on
            New-Clone "O${quote}Brien x"
            Install-PwshStandIn $bin
            $script:stub.STUB_SDK = ''
            $script:steam = Join-Path $root "Steam O$quote; Write-Host INJECTED; #"
            $result = Invoke-Onboard @('n') -Shell $windowsPowerShell

            $result.ExitCode | Should -Be 1 -Because $result.Output
            $result.Output | Should -Match 'PowerShell 7: found'
            $result.Output | Should -Not -Match '(?m)^INJECTED'
            Get-RelaunchCommand | Should -Match ([regex]::Escape("-SteamRoot $(ConvertTo-Literal $steam)"))
        }

        It 'prefers PowerShell 7 at its default path, which PATH may not have yet, to one on PATH' {
            Install-PwshStandIn (Join-Path $programFiles 'PowerShell/7')
            Install-PwshStandIn $bin
            $script:stub.STUB_SDK = ''
            $result = Invoke-Onboard @('n') -Shell $windowsPowerShell

            $result.ExitCode | Should -Be 1 -Because $result.Output
            $result.Output | Should -Match ([regex]::Escape((Join-Path $programFiles 'PowerShell\7\pwsh.exe')))
            $result.Output | Should -Match 'PowerShell 7: found'
            Get-Calls | Where-Object { $_ -like 'winget*' } | Should -BeNullOrEmpty
        }

        It 'offers the install once when PowerShell 7 is missing' {
            $result = Invoke-Onboard @('n') -Shell $windowsPowerShell

            $result.ExitCode | Should -Be 1 -Because $result.Output
            @([regex]::Matches($result.Output, 'Install PowerShell 7 \S+ and accept its licence agreement\?')).Count |
                Should -Be 1
            $result.Output | Should -Match 'github\.com/PowerShell/PowerShell'
            Get-Calls | Where-Object { $_ -like 'pwsh*' -or $_ -like 'winget*' } | Should -BeNullOrEmpty
        }
    }
}
